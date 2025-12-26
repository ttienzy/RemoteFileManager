using System.Net;
using System.Net.Sockets;
using System.Text;
using RemoteFileManager.Shared.Constants;

namespace RemoteFileManager.Server.Core
{
    public class DiscoveryService
    {
        public async Task StartListeningAsync(CancellationToken token)
        {
            // Lắng nghe trên tất cả các IP (0.0.0.0)
            using var udpClient = new UdpClient(AppConstants.DiscoveryPort);
            udpClient.EnableBroadcast = true;

            ServerLogger.LogInfo($"[UDP] Discovery Service đang lắng nghe port {AppConstants.DiscoveryPort}...");
            ServerLogger.LogInfo($"[UDP] Server IP hiện tại: {GetLocalIpAddress()}"); // Check xem IP đúng chưa

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Chờ nhận tin
                    var result = await udpClient.ReceiveAsync(token);
                    string message = Encoding.UTF8.GetString(result.Buffer);
                    string clientIp = result.RemoteEndPoint.Address.ToString();

                    // --- LOG CHI TIẾT ĐỂ DEBUG ---
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[UDP] Nhận được tín hiệu từ: {clientIp} | Nội dung: {message}");
                    Console.ResetColor();

                    if (message == AppConstants.DiscoveryMessage)
                    {
                        string myIp = GetLocalIpAddress();
                        byte[] responseData = Encoding.UTF8.GetBytes(myIp);

                        // Gửi trả lại Client
                        await udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);

                        Console.WriteLine($"[UDP] -> Đã gửi phản hồi ({myIp}) tới {clientIp}");
                    }
                    else
                    {
                        Console.WriteLine($"[UDP] -> Sai mật khẩu, bỏ qua.");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ServerLogger.LogError($"Discovery Error: {ex.Message}");
            }
        }

        private string GetLocalIpAddress()
        {
            try
            {
                // Mẹo: Tạo một kết nối UDP ảo ra ngoài Internet (Google DNS 8.8.8.8)
                // Hệ điều hành sẽ tự chọn IP của card mạng có Internet/LAN để định tuyến.
                // Cách này chính xác hơn việc duyệt danh sách IP AddressList.
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 65530);
                    if (socket.LocalEndPoint is IPEndPoint endPoint)
                    {
                        return endPoint.Address.ToString();
                    }
                }
            }
            catch
            {
                // Fallback: Nếu không có mạng, dùng cách cũ
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(ip =>
                    ip.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ip)); // Lấy cái IPv4 đầu tiên không phải localhost

                return ip?.ToString() ?? "127.0.0.1";
            }

            return "127.0.0.1";
        }
    }
}