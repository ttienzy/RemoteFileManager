using System.Collections.ObjectModel;
using RemoteFileManager.Client.Core;
using System.Windows;

namespace RemoteFileManager.Client.Services
{
    public class SessionManager
    {
        // Danh sách các máy đang quản lý (Bind lên Sidebar)
        public ObservableCollection<ServerSession> Sessions { get; } = new();

        public async Task<ServerSession?> ConnectToServerAsync(string ip, int port)
        {
            // 1. Kiểm tra xem đã kết nối IP này chưa
            var existing = Sessions.FirstOrDefault(s => s.IpAddress == ip);
            if (existing != null && existing.IsConnected) return existing;

            // 2. Tạo NetworkService MỚI (Không dùng Singleton nữa)
            INetworkService netService = new NetworkService();

            // 3. Thử kết nối
            bool success = await netService.ConnectAsync(ip, port);

            if (success)
            {
                var session = new ServerSession
                {
                    IpAddress = ip,
                    Port = port,
                    NetworkService = netService,
                    IsConnected = true,
                    Name = $"PC-{ip}" // Tạm thời đặt tên theo IP
                };

                // Thêm vào list (Dùng Dispatcher vì ObservableCollection gắn UI)
                Application.Current.Dispatcher.Invoke(() => Sessions.Add(session));

                // Lắng nghe sự kiện ngắt kết nối để cập nhật trạng thái
                netService.OnDisconnected += () =>
                {
                    session.IsConnected = false;
                };

                return session;
            }

            return null;
        }

        public void DisconnectSession(ServerSession session)
        {
            session.NetworkService.Disconnect();
            Application.Current.Dispatcher.Invoke(() => Sessions.Remove(session));
        }
    }
}