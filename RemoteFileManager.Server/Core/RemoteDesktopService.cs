using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RemoteFileManager.Shared.Constants;
using RemoteFileManager.Shared.Models.RemoteDesktop;

namespace RemoteFileManager.Server.Core
{
    public class RemoteDesktopService
    {
        private TcpListener? _listener;
        private bool _isRunning;
        private readonly object _streamLock = new object();

        public async Task StartAsync()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, AppConstants.RemoteDesktopPort);
                _listener.Start();
                _isRunning = true;

                Console.WriteLine($"[RDP] Remote Desktop Service started on port {AppConstants.RemoteDesktopPort}");
                ServerLogger.LogInfo($"[RDP] Remote Desktop Service started on port {AppConstants.RemoteDesktopPort}");

                while (_isRunning)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    // Log IP kết nối để dễ debug
                    string ip = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                    Console.WriteLine($"[RDP] New connection request from {ip}");

                    _ = Task.Run(() => HandleStreamingSession(client));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RDP] Listener Error: {ex.Message}");
                ServerLogger.LogError($"[RDP] Listener Error: {ex.Message}");
            }
            finally
            {
                _listener?.Stop();
            }
        }

        private void HandleStreamingSession(TcpClient client)
        {
            string clientIp = "Unknown";
            int framesSent = 0;

            try
            {
                client.NoDelay = true;
                client.SendBufferSize = 64 * 1024;
                clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                Console.WriteLine($"[RDP] Client {clientIp} session initialization...");
                ServerLogger.LogInfo($"[RDP] Client {clientIp} started viewing screen.");

                using var stream = client.GetStream();
                using var cts = new CancellationTokenSource();

                // --- KHỞI TẠO DXGI (QUAN TRỌNG: Phải nằm trong Try-Catch) ---
                DxgiScreenCapturer capturer;
                try
                {
                    capturer = new DxgiScreenCapturer();
                }
                catch (Exception dxgiEx)
                {
                    // Đây là nơi bắt lỗi "Connection Closed" do không khởi tạo được màn hình
                    ServerLogger.LogError($"[RDP] CRITICAL: Failed to init DXGI for {clientIp}. Error: {dxgiEx.Message}");
                    Console.WriteLine($"[RDP] DXGI Init Failed: {dxgiEx.Message}");
                    throw; // Ném tiếp để nhảy xuống finally đóng kết nối
                }

                using (capturer)
                {
                    // 1. Gửi thông tin màn hình
                    SendScreenInfo(stream, capturer.SendWidth, capturer.SendHeight);
                    Console.WriteLine($"[RDP] Sent screen info: {capturer.SendWidth}x{capturer.SendHeight}");

                    // Chạy song song (Fire & Forget), không await để code chạy tiếp xuống dưới
                    _ = Task.Run(() => HandleInputLoop(client));


                    // 2. Đăng ký sự kiện Frame Update
                    capturer.OnScreenUpdate += (x, y, w, h, data) =>
                    {
                        try
                        {
                            byte[] packet = new byte[21 + data.Length];
                            packet[0] = (byte)RdpPacketType.FrameUpdate;

                            BitConverter.TryWriteBytes(packet.AsSpan(1), x);
                            BitConverter.TryWriteBytes(packet.AsSpan(5), y);
                            BitConverter.TryWriteBytes(packet.AsSpan(9), w);
                            BitConverter.TryWriteBytes(packet.AsSpan(13), h);
                            BitConverter.TryWriteBytes(packet.AsSpan(17), data.Length);

                            Buffer.BlockCopy(data, 0, packet, 21, data.Length);

                            lock (_streamLock)
                            {
                                stream.Write(packet, 0, packet.Length);
                            }

                            framesSent++;
                            // Log ít lại để đỡ spam console, chỉ log mỗi 100 frame
                            if (framesSent % 100 == 0)
                            {
                                Console.WriteLine($"[RDP] Streamed {framesSent} frames to {clientIp}");
                            }
                        }
                        catch
                        {
                            cts.Cancel(); // Client ngắt kết nối thì dừng capture
                        }
                    };

                    // 3. Đăng ký sự kiện Cursor Update
                    capturer.OnCursorUpdate += (x, y, isVisible) =>
                    {
                        try
                        {
                            byte[] packet = new byte[10];
                            packet[0] = (byte)RdpPacketType.CursorUpdate;
                            BitConverter.TryWriteBytes(packet.AsSpan(1), x);
                            BitConverter.TryWriteBytes(packet.AsSpan(5), y);
                            packet[9] = (byte)(isVisible ? 1 : 0);

                            lock (_streamLock)
                            {
                                stream.Write(packet, 0, packet.Length);
                            }
                        }
                        catch { cts.Cancel(); }
                    };

                    // 4. Bắt đầu vòng lặp Capture
                    Console.WriteLine($"[RDP] Starting capture loop for {clientIp}...");
                    capturer.CaptureLoop(cts.Token);
                }
            }
            catch (Exception ex)
            {
                // Log lỗi chung (Ngắt kết nối mạng hoặc lỗi DXGI)
                Console.WriteLine($"[RDP] Session ended for {clientIp}: {ex.Message}");
                ServerLogger.LogWarning($"[RDP] Session ended: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"[RDP] Client {clientIp} disconnected. Total frames sent: {framesSent}");
                ServerLogger.LogInfo($"[RDP] Client {clientIp} disconnected.");

                try { client.Close(); } catch { }
            }
        }
        private async Task HandleInputLoop(TcpClient client)
        {
            try
            {
                // Lưu ý: Không dùng 'using stream' ở đây vì stream đang được dùng bởi luồng gửi ảnh
                // Chỉ lấy reference thôi
                var stream = client.GetStream();
                byte[] header = new byte[10];

                while (client.Connected)
                {
                    // 1. Đọc 1 byte Type
                    int read = await stream.ReadAsync(header, 0, 1);
                    if (read == 0) break;

                    // Kiểm tra xem có phải gói tin Input không (Type = 4)
                    if (header[0] == (byte)RdpPacketType.ClientInput)
                    {
                        // 2. Đọc tiếp 9 byte dữ liệu (TypeInput:1 + Val1:4 + Val2:4)
                        await ReadExactly(stream, header, 9);

                        byte inputType = header[0];
                        int val1 = BitConverter.ToInt32(header, 1);
                        int val2 = BitConverter.ToInt32(header, 5);

                        // 3. Thực thi lệnh lên Windows thật
                        ExecuteInput(inputType, val1, val2);
                    }
                }
            }
            catch { /* Client ngắt kết nối hoặc lỗi đọc -> Kệ nó, luồng chính sẽ xử lý close */ }
        }

        private void ExecuteInput(byte type, int val1, int val2)
        {
            // LƯU Ý VỀ TỶ LỆ (SCALE):
            // Nếu bạn dùng tính năng Resize ảnh ở Server (ví dụ 60%), 
            // thì tọa độ val1, val2 nhận được cần phải nhân ngược lại (chia 0.6) để ra tọa độ thật.
            // Nếu dùng Full HD (không resize) thì dùng trực tiếp.

            // Giả sử đang dùng Full Size hoặc Client đã tính toán Scale rồi:
            try
            {
                switch (type)
                {
                    case 1: InputSimulator.MoveMouse(val1, val2); break;       // MouseMove
                    case 2: InputSimulator.MouseEvent(true, val1 == 0); break; // MouseDown
                    case 3: InputSimulator.MouseEvent(false, val1 == 0); break;// MouseUp
                    case 4: InputSimulator.KeyEvent((byte)val1, true); break;  // KeyDown
                    case 5: InputSimulator.KeyEvent((byte)val1, false); break; // KeyUp
                }
            }
            catch { }
        }

        private async Task ReadExactly(NetworkStream s, byte[] buff, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int r = await s.ReadAsync(buff, offset, count - offset);
                if (r == 0) throw new Exception();
                offset += r;
            }
        }

        private void SendScreenInfo(NetworkStream stream, int width, int height)
        {
            byte[] packet = new byte[9];
            packet[0] = (byte)RdpPacketType.ScreenInfo;
            BitConverter.TryWriteBytes(packet.AsSpan(1), width);
            BitConverter.TryWriteBytes(packet.AsSpan(5), height);

            lock (_streamLock)
            {
                stream.Write(packet, 0, packet.Length);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            Console.WriteLine("[RDP] Remote Desktop Service stopped");
        }
    }
}