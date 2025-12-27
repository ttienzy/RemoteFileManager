using Microsoft.Win32;
using RemoteFileManager.Client.Core;
using RemoteFileManager.Client.Services;
using RemoteFileManager.Client.Views;
using RemoteFileManager.Shared.Constants; // Để lấy Port 45000
using RemoteFileManager.Shared.Enums;
using RemoteFileManager.Shared.Models.Network;
using RemoteFileManager.Shared.Models.Transfer;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;

namespace RemoteFileManager.Client.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly SessionManager _sessionManager;
        private ServerSession? _selectedSession;
        private bool _isScanning; // Biến để hiện loading nếu cần
        public ICommand BroadcastUploadCommand { get; }
        public ICommand RemoteDesktopCommand { get; }

        public FileExplorerViewModel FileExplorerVM { get; }

        public DashboardViewModel(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
            FileExplorerVM = new FileExplorerViewModel();

            // Command này giờ sẽ kích hoạt Scan & Auto Connect
            ScanAndAddCommand = new RelayCommand(async _ => await ScanAndAutoConnect(), _ => !IsScanning);
            BroadcastUploadCommand = new RelayCommand(async _ => await ExecuteBroadcastUpload());
            RemoteDesktopCommand = new RelayCommand(ExecuteRemoteDesktop, CanExecuteRemoteDesktop);
        }
        private void ExecuteRemoteDesktop(object? obj)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] ExecuteRemoteDesktop called with obj type: {obj?.GetType().Name ?? "null"}");

            if (obj is ServerSession session)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Session IP: {session.IpAddress}, Connected: {session.IsConnected}");

                    // Kiểm tra kết nối
                    if (!session.IsConnected)
                    {
                        MessageBox.Show($"Máy {session.IpAddress} chưa kết nối!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Thông báo đang kết nối
                    MessageBox.Show($"Đang mở Remote Desktop tới {session.IpAddress}:45003...", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Mở cửa sổ Remote Desktop
                    var rdpWindow = new RemoteDesktopWindow(session.IpAddress);
                    rdpWindow.Show();

                    System.Diagnostics.Debug.WriteLine($"[DEBUG] RemoteDesktopWindow opened successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] {ex.Message}\n{ex.StackTrace}");
                    MessageBox.Show($"Lỗi khởi tạo Remote Desktop:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] obj is not ServerSession. Type: {obj?.GetType().Name ?? "null"}");
                MessageBox.Show($"Lỗi: Không lấy được thông tin máy chủ.\nType: {obj?.GetType().Name ?? "null"}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ✅ FIX: Hàm CanExecute để kiểm tra điều kiện
        private bool CanExecuteRemoteDesktop(object? obj)
        {
            // Chấp nhận cả khi obj = null vì ContextMenu có thể truyền늦
            return obj is ServerSession session && session.IsConnected;
        }
        // --- LOGIC GỬI HÀNG LOẠT ---
        private async Task ExecuteBroadcastUpload()
        {
            // 1. Lọc ra danh sách các máy được tick chọn
            var targetSessions = _sessionManager.Sessions.Where(s => s.IsSelected && s.IsConnected).ToList();

            if (targetSessions.Count == 0)
            {
                MessageBox.Show("Vui lòng tick chọn ít nhất 1 máy online để gửi!", "Thông báo");
                return;
            }

            // 2. Chọn file cần gửi
            var openDialog = new OpenFileDialog();
            if (openDialog.ShowDialog() != true) return;

            string sourceFilePath = openDialog.FileName;
            string fileName = Path.GetFileName(sourceFilePath);
            long fileSize = new FileInfo(sourceFilePath).Length;

            // 3. Quy định đường dẫn đích (Ví dụ: D:\RemoteShare\...)
            // Bạn có thể sửa thành hộp thoại cho người dùng nhập nếu muốn
            string targetFolder = @"D:\RemoteShare";
            string targetFullPath = Path.Combine(targetFolder, fileName);

            // 4. Bắt đầu gửi (Chạy song song cho nhanh)
            var uploadTasks = new List<Task>();

            foreach (var session in targetSessions)
            {
                // Tạo Task riêng cho từng máy
                uploadTasks.Add(Task.Run(async () =>
                {
                    await UploadToSingleServer(session, sourceFilePath, targetFullPath, fileSize);
                }));
            }

            MessageBox.Show($"Đang bắt đầu gửi file đến {targetSessions.Count} máy...", "Broadcast");

            // Chờ tất cả hoàn thành
            await Task.WhenAll(uploadTasks);

            MessageBox.Show("Đã hoàn tất gửi file hàng loạt!", "Thành công");
        }

        // Hàm gửi file cho 1 máy cụ thể (Logic giống hệt FileExplorerViewModel nhưng dùng session riêng)
        private async Task UploadToSingleServer(ServerSession session, string sourcePath, string destPath, long totalSize)
        {
            try
            {
                // A. Gửi gói tin yêu cầu Upload
                var requestPayload = new FileChunkDto
                {
                    FileName = destPath, // Đường dẫn đích (D:\RemoteShare\file.txt)
                    TotalSize = totalSize
                };

                var requestPacket = new Packet
                {
                    Command = CommandCode.RequestUpload
                };
                requestPacket.SetPayload(requestPayload);

                await session.NetworkService.SendPacketAsync(requestPacket);

                // B. Đợi xíu cho Server tạo file (hoặc chờ phản hồi UploadReady nếu muốn code phức tạp hơn)
                // Ở chế độ Broadcast, ta dùng "Fire & Forget" (Gửi luôn) cho nhanh, giả định Server luôn OK.
                await Task.Delay(200);

                // C. Cắt file và bắn dữ liệu
                using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[64 * 1024]; // 64KB chunk
                    int bytesRead;
                    long currentOffset = 0;

                    while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        // Copy dữ liệu thực
                        byte[] dataToSend = new byte[bytesRead];
                        Array.Copy(buffer, dataToSend, bytesRead);

                        var chunk = new FileChunkDto
                        {
                            CurrentOffset = currentOffset,
                            TotalSize = totalSize,
                            Data = dataToSend,
                            IsLastChunk = (currentOffset + bytesRead >= totalSize)
                        };

                        var chunkPacket = new Packet { Command = CommandCode.FileChunk };
                        chunkPacket.SetPayload(chunk);

                        // Gửi qua NetworkService của RIÊNG máy này
                        await session.NetworkService.SendPacketAsync(chunkPacket);

                        currentOffset += bytesRead;

                        // Delay nhỏ để tránh nghẽn mạng nếu gửi cho 50 máy cùng lúc
                        await Task.Delay(5);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần (Vì chạy background nên không hiện MessageBox liên tục gây phiền)
                System.Diagnostics.Debug.WriteLine($"Lỗi gửi tới {session.IpAddress}: {ex.Message}");
            }
        }

        public ObservableCollection<ServerSession> Sessions => _sessionManager.Sessions;

        public ServerSession? SelectedSession
        {
            get => _selectedSession;
            set
            {
                if (SetProperty(ref _selectedSession, value))
                {
                    if (value != null)
                    {
                        FileExplorerVM.SetTargetServer(value);
                    }
                }
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                SetProperty(ref _isScanning, value);
                CommandManager.InvalidateRequerySuggested(); // Cập nhật trạng thái nút bấm
            }
        }

        public ICommand ScanAndAddCommand { get; }

        // --- LOGIC QUÉT & KẾT NỐI TỰ ĐỘNG ---

        private async Task ScanAndAutoConnect()
        {
            IsScanning = true;
            var tasks = new List<Task>();
            int timeoutMs = 300; // Timeout ngắn

            try
            {
                // 1. Lấy giải mạng
                string baseIpPrefix = GetLocalSubnetPrefix();
                if (string.IsNullOrEmpty(baseIpPrefix)) baseIpPrefix = "192.168.1.";

                // 2. Tạo 254 Task quét song song
                for (int i = 1; i < 255; i++)
                {
                    string targetIp = $"{baseIpPrefix}{i}";

                    // Bỏ qua IP của chính máy mình (Localhost) để tránh tự kết nối chính mình
                    // (Tùy chọn, nếu muốn test 1 máy thì bỏ dòng if này đi)
                    // if (targetIp == GetLocalIpAddress()) continue; 

                    tasks.Add(CheckAndConnectAsync(targetIp, AppConstants.MainPort, timeoutMs));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi Scan: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
            }
        }

        // Hàm này vừa kiểm tra Port, vừa Kết nối luôn nếu thấy mở
        private async Task CheckAndConnectAsync(string ip, int port, int timeout)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(ip, port);
                    var delayTask = Task.Delay(timeout);

                    var completedTask = await Task.WhenAny(connectTask, delayTask);

                    if (completedTask == connectTask)
                    {
                        if (tcpClient.Connected)
                        {
                            // === TÌM THẤY SERVER! ===
                            // Thay vì chỉ thêm vào List string, ta gọi SessionManager để kết nối thật luôn

                            // Lưu ý: connectTask ở trên chỉ là check port, giờ ta đóng lại để SessionManager kết nối chính thức
                            // Hoặc tối ưu hơn: Pass cái tcpClient này vào SessionManager (nhưng để code đơn giản, ta cứ connect lại)
                            tcpClient.Close();

                            // Gọi về UI Thread để thực hiện kết nối chính thức
                            Application.Current.Dispatcher.Invoke(async () =>
                            {
                                var session = await _sessionManager.ConnectToServerAsync(ip, port);
                                if (session != null)
                                {
                                    // Tự động chọn máy vừa tìm thấy nếu chưa chọn ai
                                    if (SelectedSession == null) SelectedSession = session;
                                }
                            });
                        }
                    }
                }
            }
            catch { /* Bỏ qua nếu lỗi */ }
        }

        // Hàm lấy Subnet (Giữ nguyên logic của bạn)
        private string GetLocalSubnetPrefix()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 65530);
                    if (socket.LocalEndPoint is IPEndPoint endPoint)
                    {
                        string ip = endPoint.Address.ToString();
                        return ip.Substring(0, ip.LastIndexOf('.') + 1);
                    }
                }
            }
            catch { }
            return "192.168.1.";
        }
    }
}