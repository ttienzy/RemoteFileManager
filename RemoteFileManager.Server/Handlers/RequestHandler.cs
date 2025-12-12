using RemoteFileManager.Server.Core;
using RemoteFileManager.Server.Services.Implements;
using RemoteFileManager.Server.Services.Interfaces;
using RemoteFileManager.Shared.Enums;
using RemoteFileManager.Shared.Models.Network;
using RemoteFileManager.Shared.Models.Transfer;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace RemoteFileManager.Server.Handlers
{
    public class RequestHandler
    {
        // Khai báo Interface, không khai báo Class cụ thể (Dependency Injection thủ công)
        private readonly IFileService _fileService;
        private readonly ISystemService _systemService;
        private readonly ITransferService _transferService;

        public RequestHandler()
        {
            // Ở dự án thực tế ta dùng DI Container, ở đây ta new trực tiếp cho đơn giản
            _fileService = new FileService();
            _systemService = new SystemService();
            _transferService = new TransferService();
        }

        public async Task<Packet?> HandleAsync(Packet requestPacket, ClientSession session, Func<Packet, Task> sendCallback)
        {
            var response = new Packet
            {
                Command = requestPacket.Command, // Trả về cùng loại lệnh để Client biết đang rep cái gì
                Timestamp = DateTime.Now
            };

            try
            {
                switch (requestPacket.Command)
                {
                    // --- NHÓM FILE SYSTEM ---
                    case CommandCode.GetDrives:
                        var drives = _fileService.GetDrives();
                        response.SetPayload(drives);
                        response.Message = "OK";
                        break;

                    case CommandCode.GetDirectoryContent:
                        // Client gửi đường dẫn trong Payload (dạng string thuần hoặc JSON string)
                        string path = JsonSerializer.Deserialize<string>(requestPacket.Payload) ?? string.Empty;
                        var content = _fileService.GetDirectoryContent(path);
                        response.SetPayload(content);
                        response.Message = "OK";
                        break;

                    // --- NHÓM SYSTEM ---
                    case CommandCode.GetProcesses:
                        var processes = _systemService.GetProcesses();
                        response.SetPayload(processes);
                        break;

                    case CommandCode.KillProcess:
                        // Client gửi ID process cần kill
                        int pid = JsonSerializer.Deserialize<int>(requestPacket.Payload);
                        _systemService.KillProcess(pid);
                        response.Message = $"Process {pid} killed successfully.";
                        break;

                    case CommandCode.DeleteItem:
                        string pathToDelete = JsonSerializer.Deserialize<string>(requestPacket.Payload);
                        _fileService.DeleteItem(pathToDelete);
                        response.Message = "Deleted successfully."; 
                        break;

                    case CommandCode.CreateFolder:
                        string pathToCreate = JsonSerializer.Deserialize<string>(requestPacket.Payload);
                        _fileService.CreateFolder(pathToCreate);
                        response.Message = "Folder created.";
                        break;

                    case CommandCode.RequestDownload:
                        string filePath = JsonSerializer.Deserialize<string>(requestPacket.Payload);

                        using (var fileStream = _transferService.GetFileStream(filePath))
                        {
                            long totalSize = fileStream.Length;
                            string fileName = Path.GetFileName(filePath);

                            // 1. Gửi gói tin báo bắt đầu (Header)
                            var headerPacket = new Packet
                            {
                                Command = CommandCode.DownloadResponse,
                                Message = "Starting download..."
                            };
                            // Payload chứa FileChunkDto rỗng nhưng có TotalSize để Client biết
                            headerPacket.SetPayload(new FileChunkDto { TotalSize = totalSize, FileName = fileName });

                            // Gửi gói Header đi trước
                            await sendCallback(headerPacket);

                            // 2. Cắt file và gửi từng chunk
                            byte[] buffer = new byte[64 * 1024]; // 64KB mỗi gói
                            int bytesRead;
                            long currentOffset = 0;

                            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                // Copy đúng số byte vừa đọc (tránh gói cuối bị dư số 0)
                                byte[] dataToSend = new byte[bytesRead];
                                Array.Copy(buffer, dataToSend, bytesRead);

                                var chunkDto = new FileChunkDto
                                {
                                    FileName = fileName,
                                    TotalSize = totalSize,
                                    CurrentOffset = currentOffset,
                                    Data = dataToSend,
                                    IsLastChunk = (currentOffset + bytesRead >= totalSize)
                                };

                                var chunkPacket = new Packet
                                {
                                    Command = CommandCode.FileChunk
                                };
                                chunkPacket.SetPayload(chunkDto);

                                // Gửi gói chunk đi
                                await sendCallback(chunkPacket);

                                currentOffset += bytesRead;

                                // Delay cực nhỏ để không làm ngộp mạng (tùy chọn)
                                await Task.Delay(1);
                            }
                        }

                        // Vì đã gửi hết qua callback, ta trả về null để ClientSession không gửi thêm gì nữa
                        return null;

                    case CommandCode.RequestUpload:
                        // 1. Client gửi thông tin file muốn upload
                        var uploadInfo = JsonSerializer.Deserialize<FileChunkDto>(requestPacket.Payload);
                        if (uploadInfo == null) throw new Exception("Invalid payload");

                        // uploadInfo.FileName chứa đường dẫn đầy đủ đích đến (Do Client gửi lên)
                        string savePath = uploadInfo.FileName;

                        try
                        {
                            // Đảm bảo thư mục cha tồn tại
                            string? dir = Path.GetDirectoryName(savePath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }

                            // 2. Mở FileStream chế độ Create và LƯU VÀO SESSION
                            // Lưu vào session để các gói tin FileChunk sau đó có thể lấy ra dùng tiếp
                            session.CurrentUploadStream = new FileStream(savePath, FileMode.Create, FileAccess.Write);

                            // 3. Báo cho Client biết Server đã sẵn sàng hứng
                            response.Command = CommandCode.UploadReady;
                            response.Message = "Server Ready to receive data";
                        }
                        catch (Exception ex)
                        {
                            response.Command = CommandCode.SystemAlert;
                            response.Message = "Upload Init Failed: " + ex.Message;
                        }
                        break;
                    case CommandCode.FileChunk:
                        // 1. Kiểm tra xem Session có đang giữ Stream nào không
                        if (session.CurrentUploadStream != null)
                        {
                            var chunk = JsonSerializer.Deserialize<FileChunkDto>(requestPacket.Payload);
                            if (chunk != null)
                            {
                                // 2. Ghi dữ liệu vào file
                                await session.CurrentUploadStream.WriteAsync(chunk.Data, 0, chunk.Data.Length);

                                // 3. Nếu là gói cuối cùng
                                if (chunk.IsLastChunk)
                                {
                                    // Đóng file, lưu xuống đĩa cứng
                                    session.CurrentUploadStream.Close();
                                    session.CurrentUploadStream.Dispose();
                                    session.CurrentUploadStream = null; // Xóa khỏi session

                                    Console.WriteLine($"Upload finished: {chunk.FileName}");
                                }
                            }

                            // QUAN TRỌNG: Trả về null để KHÔNG gửi phản hồi cho Client
                            // Vì gửi phản hồi mỗi gói sẽ làm chậm tốc độ upload
                            return null;
                        }
                        else
                        {
                            // Nếu Client gửi Data mà Server không có Stream (lỗi logic hoặc timeout)
                            response.Command = CommandCode.SystemAlert;
                            response.Message = "Upload session not found or expired.";
                        }
                        break;
                    default:
                        response.Message = "Command not implemented yet.";
                        break;
                }
            }
            catch (Exception ex)
            {
                // Nếu có lỗi (ví dụ: truy cập thư mục bị cấm, kill process không được)
                // Server không được crash, mà phải báo lỗi về Client
                response.Command = CommandCode.SystemAlert; // Hoặc giữ nguyên command cũ nhưng thêm thông báo lỗi
                response.Message = $"ERROR: {ex.Message}";
            }

            return response;



        }
    }
}
