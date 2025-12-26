using MaterialDesignThemes.Wpf;
using RemoteFileManager.Client.Core;
using RemoteFileManager.Client.Services;
using RemoteFileManager.Client.Views;
using RemoteFileManager.Shared.Constants;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Input;

namespace RemoteFileManager.Client.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly INetworkService _networkService;
        private string _ipAddress = "127.0.0.1";
        private int _port = AppConstants.MainPort;
        private bool _isConnecting;
        private string _errorMessage = string.Empty;

        private ObservableCollection<string> _foundServers = new();
        public ObservableCollection<string> FoundServers => _foundServers;

        // Sự kiện báo cho MainViewModel biết là đã đăng nhập thành công
        public event Action? LoginSuccess;

        public LoginViewModel(INetworkService networkService)
        {
            _networkService = networkService;
            ConnectCommand = new RelayCommand(async _ => await ConnectAsync(), _ => !IsConnecting);
            ScanServerCommand = new RelayCommand(async _ => await OpenScanDialog());
        }

        public ICommand ScanServerCommand { get; }

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                SetProperty(ref _isConnecting, value);
                // Cập nhật lại trạng thái nút bấm (Enable/Disable)
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand ConnectCommand { get; }
        private async Task OpenScanDialog()
        {
            // 1. Reset danh sách và bắt đầu quét ngầm
            FoundServers.Clear();

            // Chạy hàm quét IP nhưng KHÔNG await nó ngay (Fire and forget)
            // Để nó tự chạy song song việc hiển thị Dialog
            _ = ScanServersLogic();

            // 2. Tạo giao diện Dialog
            var scanView = new ServerScanDialog();

            // QUAN TRỌNG: Gán DataContext của Dialog là chính ViewModel này
            // Để Dialog có thể Binding vào biến FoundServers
            scanView.DataContext = this;

            // 3. Hiển thị Dialog và chờ kết quả
            var result = await DialogHost.Show(scanView, "LoginDialog");

            // 4. Xử lý kết quả trả về (IP user chọn)
            if (result is string selectedIp)
            {
                IpAddress = selectedIp; // Điền vào ô nhập
            }
        }

        private async Task ScanServersLogic()
        {
            var tasks = new List<Task>();

            // Timeout cho mỗi lần thử kết nối (tương đương 0.3s trong code Python của bạn)
            // Mạng LAN thường rất nhanh (<10ms), nên 200-300ms là đủ.
            int timeoutMs = 300;

            try
            {
                System.Diagnostics.Debug.WriteLine("=== BẮT ĐẦU TCP SCAN ===");

                // 1. Lấy giải mạng (VD: 192.168.1.)
                string baseIpPrefix = GetLocalSubnetPrefix();
                if (string.IsNullOrEmpty(baseIpPrefix)) baseIpPrefix = "192.168.1.";

                // 2. Tạo 254 Task chạy song song (Multithreading)
                for (int i = 1; i < 255; i++)
                {
                    string targetIp = $"{baseIpPrefix}{i}";

                    // Tạo Task riêng cho mỗi IP
                    tasks.Add(Task.Run(async () =>
                    {
                        await CheckTcpPortAsync(targetIp, AppConstants.MainPort, timeoutMs);
                    }));
                }

                // 3. Chờ tất cả các luồng chạy xong
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Scan Error: " + ex.Message);
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine("=== KẾT THÚC TCP SCAN ===");
            }
        }

        // Hàm kiểm tra cổng TCP (Giống logic Python socket.connect)
        private async Task CheckTcpPortAsync(string ip, int port, int timeout)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    // Tạo Task kết nối
                    var connectTask = tcpClient.ConnectAsync(ip, port);

                    // Tạo Task đếm giờ (Timeout)
                    var delayTask = Task.Delay(timeout);

                    // Đua: Xem cái nào xong trước
                    var completedTask = await Task.WhenAny(connectTask, delayTask);

                    if (completedTask == connectTask)
                    {
                        // Nếu kết nối xong trước -> Kiểm tra xem có thành công không
                        if (tcpClient.Connected)
                        {
                            System.Diagnostics.Debug.WriteLine($"[OPEN] Tìm thấy Server tại: {ip}");

                            // Cập nhật UI (Vì đang ở luồng phụ nên cần Dispatcher)
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (!FoundServers.Contains(ip))
                                {
                                    FoundServers.Add(ip);
                                }
                            });
                        }
                    }
                    else
                    {
                        // Hết giờ (Timeout) -> Bỏ qua (Server không có ở đó hoặc bị chặn)
                    }
                }
            }
            catch
            {
                // Lỗi kết nối (Connection Refused, Host Unreachable...) -> Bỏ qua
            }
        }

        // Hàm lấy giải mạng (Giữ nguyên như cũ)
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


        private async Task ConnectAsync()
        {
            IsConnecting = true;
            ErrorMessage = string.Empty;

            bool success = await _networkService.ConnectAsync(IpAddress, Port);

            if (success)
            {
                // Gọi sự kiện để chuyển trang
                LoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = "Không thể kết nối tới Server. Vui lòng kiểm tra IP/Firewall.";
            }

            IsConnecting = false;
        }
    }
}
