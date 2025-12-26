using RemoteFileManager.Server.Core;
using RemoteFileManager.Server.Services.Implements;
using RemoteFileManager.Server.Services.Interfaces;
using RemoteFileManager.Shared.Enums;
using RemoteFileManager.Shared.Models.FileSystem;
using RemoteFileManager.Shared.Models.Network;
using RemoteFileManager.Shared.Models.Transfer;
using System;
using System.Collections.Generic;
using System.IO;
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
                Command = requestPacket.Command,
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
                        return await HandleDownloadRequestAsync(requestPacket, sendCallback);

                    case CommandCode.RequestUpload:
                        return await HandleUploadRequestAsync(requestPacket, session, response);

                    case CommandCode.FileChunk:
                        return await HandleFileChunkAsync(requestPacket, session, response);

                    case CommandCode.SendClipboard:
                        HandleSendClipboard(requestPacket, response);
                        break;

                    case CommandCode.GetClipboard:
                        HandleGetClipboard(response);
                        break;

                    case CommandCode.GetFileContent:
                        return await HandleGetFileContentAsync(requestPacket, response);

                    case CommandCode.SaveFileContent:
                        return await HandleSaveFileContentAsync(requestPacket, response);

                    default:
                        response.Message = "Command not implemented yet.";
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Command = CommandCode.SystemAlert;
                response.Message = $"ERROR: {ex.Message}";
            }

            return response;
        }

        // ==================== PRIVATE HELPER METHODS ====================

        private async Task<Packet?> HandleDownloadRequestAsync(Packet requestPacket, Func<Packet, Task> sendCallback)
        {
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
                headerPacket.SetPayload(new FileChunkDto { TotalSize = totalSize, FileName = fileName });
                await sendCallback(headerPacket);

                // 2. Cắt file và gửi từng chunk
                byte[] buffer = new byte[64 * 1024]; // 64KB
                int bytesRead;
                long currentOffset = 0;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
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

                    var chunkPacket = new Packet { Command = CommandCode.FileChunk };
                    chunkPacket.SetPayload(chunkDto);
                    await sendCallback(chunkPacket);

                    currentOffset += bytesRead;
                    await Task.Delay(1);
                }
            }

            return null; // Không gửi thêm response
        }

        private async Task<Packet?> HandleUploadRequestAsync(Packet requestPacket, ClientSession session, Packet response)
        {
            var uploadInfo = JsonSerializer.Deserialize<FileChunkDto>(requestPacket.Payload);
            if (uploadInfo == null) throw new Exception("Invalid payload");

            string savePath = uploadInfo.FileName;

            try
            {
                string? dir = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                session.CurrentUploadStream = new FileStream(savePath, FileMode.Create, FileAccess.Write);

                response.Command = CommandCode.UploadReady;
                response.Message = "Server Ready to receive data";
            }
            catch (Exception ex)
            {
                response.Command = CommandCode.SystemAlert;
                response.Message = "Upload Init Failed: " + ex.Message;
            }

            return response;
        }

        private async Task<Packet?> HandleFileChunkAsync(Packet requestPacket, ClientSession session, Packet response)
        {
            if (session.CurrentUploadStream != null)
            {
                var chunk = JsonSerializer.Deserialize<FileChunkDto>(requestPacket.Payload);
                if (chunk != null)
                {
                    await session.CurrentUploadStream.WriteAsync(chunk.Data, 0, chunk.Data.Length);

                    if (chunk.IsLastChunk)
                    {
                        session.CurrentUploadStream.Close();
                        session.CurrentUploadStream.Dispose();
                        session.CurrentUploadStream = null;
                        Console.WriteLine($"Upload finished: {chunk.FileName}");
                    }
                }

                return null; // Không gửi response để tối ưu tốc độ
            }
            else
            {
                response.Command = CommandCode.SystemAlert;
                response.Message = "Upload session not found or expired.";
                return response;
            }
        }

        private void HandleSendClipboard(Packet requestPacket, Packet response)
        {
            string textContent = JsonSerializer.Deserialize<string>(requestPacket.Payload);

            Thread staThread = new Thread(() =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(textContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Clipboard error: " + ex.Message);
                }
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================");
            Console.WriteLine($"[CLIPBOARD RECEIVED from Client]");
            Console.WriteLine($"Content: \"{textContent}\"");
            Console.WriteLine("========================================");
            Console.ResetColor();

            response.Message = "Server received clipboard data.";
        }

        private void HandleGetClipboard(Packet response)
        {
            string serverClipboardText = "";
            Thread readThread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                        serverClipboardText = System.Windows.Clipboard.GetText();
                }
                catch { }
            });
            readThread.SetApartmentState(ApartmentState.STA);
            readThread.Start();
            readThread.Join();

            response.SetPayload(serverClipboardText);
        }

        private async Task<Packet> HandleGetFileContentAsync(Packet requestPacket, Packet response)
        {
            string pathRead = JsonSerializer.Deserialize<string>(requestPacket.Payload);
            var fileInfo = new FileInfo(pathRead);

            if (!fileInfo.Exists)
            {
                response.Command = CommandCode.SystemAlert;
                response.Message = "File không tồn tại!";
            }
            else if (fileInfo.Length > 1024 * 1024)
            {
                response.Command = CommandCode.SystemAlert;
                response.Message = "File quá lớn (>1MB). Vui lòng dùng chức năng Download.";
            }
            else
            {
                try
                {
                    string content = await File.ReadAllTextAsync(pathRead);

                    var dto = new FileContentDto
                    {
                        FullPath = pathRead,
                        Content = content
                    };

                    response.Command = CommandCode.GetFileContent;
                    response.SetPayload(dto);
                    response.Message = "OK";
                }
                catch (Exception ex)
                {
                    response.Command = CommandCode.SystemAlert;
                    response.Message = "Lỗi đọc file: " + ex.Message;
                }
            }

            return response;
        }

        private async Task<Packet> HandleSaveFileContentAsync(Packet requestPacket, Packet response)
        {
            var saveDto = JsonSerializer.Deserialize<FileContentDto>(requestPacket.Payload);
            try
            {
                await File.WriteAllTextAsync(saveDto.FullPath, saveDto.Content);

                // SỬA DÒNG NÀY:
                // Cũ: response.Command = CommandCode.SystemAlert; 
                // Mới: Trả về chính lệnh SaveFileContent để báo thành công
                response.Command = CommandCode.SaveFileContent;
                response.Message = "Đã lưu file thành công trên Server!";
            }
            catch (Exception ex)
            {
                // Chỉ khi lỗi thật sự mới dùng SystemAlert
                response.Command = CommandCode.SystemAlert;
                response.Message = "Lỗi khi lưu file: " + ex.Message;
            }

            return response;
        }
    }
}
