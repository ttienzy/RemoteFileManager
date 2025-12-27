using K4os.Compression.LZ4; // Đảm bảo đã cài NuGet: K4os.Compression.LZ4
using RemoteFileManager.Shared.Constants;
using RemoteFileManager.Shared.Models.RemoteDesktop;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RemoteFileManager.Client.Views
{
    public partial class RemoteDesktopWindow : Window
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _isRunning = true;
        private readonly string _serverIp;
        private int _serverWidth;
        private int _serverHeight;


        // Bitmap hiệu năng cao để vẽ hình ảnh
        private WriteableBitmap? _writeableBitmap;

        public RemoteDesktopWindow(string ip)
        {
            InitializeComponent();
            _serverIp = ip;

            Title = $"Remote Desktop - Viewing {_serverIp}";

            // Đăng ký sự kiện
            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = $"Connecting to {_serverIp}:45003...";

                _client = new TcpClient();
                _client.NoDelay = true; // QUAN TRỌNG: Tắt Nagle để giảm độ trễ tối đa

                // Timeout kết nối 3 giây
                var connectTask = _client.ConnectAsync(_serverIp, AppConstants.RemoteDesktopPort);
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                {
                    throw new TimeoutException("Connection timed out.");
                }

                if (!_client.Connected) throw new Exception("Failed to connect.");

                _stream = _client.GetStream();

                StatusText.Text = "Connected. Waiting for video stream...";

                // Bắt đầu luồng nhận dữ liệu
                _ = Task.Run(ReceiveLoop);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Connection failed: {ex.Message}", "Remote Desktop", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ReceiveLoop()
        {
            // Buffer đọc header (đủ lớn cho gói tin lớn nhất là Frame Header 20 bytes)
            byte[] headerBuffer = new byte[100];

            while (_isRunning && _client != null && _client.Connected)
            {
                try
                {
                    if (_stream == null) break;

                    // 1. Đọc Loại gói tin (1 byte)
                    int typeRead = await _stream.ReadAsync(headerBuffer, 0, 1);
                    if (typeRead == 0) break; // Server ngắt kết nối

                    var packetType = (RdpPacketType)headerBuffer[0];

                    // --- XỬ LÝ GÓI TIN KHỞI TẠO (Screen Info) ---
                    if (packetType == RdpPacketType.ScreenInfo)
                    {
                        // Đọc W (4) + H (4)
                        await ReadExactly(_stream, headerBuffer, 8);
                        int w = BitConverter.ToInt32(headerBuffer, 0);
                        int h = BitConverter.ToInt32(headerBuffer, 4);
                        _serverWidth = w;
                        _serverHeight = h;

                        // Cập nhật UI (Chạy trên Main Thread)
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // === FIX LỖI MÀN HÌNH ĐEN TẠI ĐÂY ===
                            // Dùng PixelFormats.Bgr32 thay vì Bgra32 để bỏ qua kênh Alpha (Transparency)
                            _writeableBitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
                            ScreenDisplay.Source = _writeableBitmap;

                            // Chỉnh kích thước cửa sổ cho phù hợp (tối đa 90% màn hình hiện tại)
                            double screenWidth = SystemParameters.PrimaryScreenWidth;
                            double screenHeight = SystemParameters.PrimaryScreenHeight;

                            this.Width = Math.Min(w + 40, screenWidth * 0.9);
                            this.Height = Math.Min(h + 60, screenHeight * 0.9);

                            // Căn giữa màn hình
                            this.Left = (screenWidth - this.Width) / 2;
                            this.Top = (screenHeight - this.Height) / 2;

                            StatusText.Visibility = Visibility.Collapsed; // Ẩn chữ Loading
                        });
                    }
                    // --- XỬ LÝ GÓI TIN HÌNH ẢNH (Frame Update) ---
                    else if (packetType == RdpPacketType.FrameUpdate)
                    {
                        // Đọc Header: X, Y, W, H, DataLen (5 * 4 = 20 bytes)
                        await ReadExactly(_stream, headerBuffer, 20);

                        int x = BitConverter.ToInt32(headerBuffer, 0);
                        int y = BitConverter.ToInt32(headerBuffer, 4);
                        int w = BitConverter.ToInt32(headerBuffer, 8);
                        int h = BitConverter.ToInt32(headerBuffer, 12);
                        int dataLen = BitConverter.ToInt32(headerBuffer, 16);

                        // Đọc dữ liệu nén
                        byte[] compressedData = new byte[dataLen];
                        await ReadExactly(_stream, compressedData, dataLen);

                        try
                        {
                            // Giải nén LZ4
                            byte[] rawData = LZ4Pickler.Unpickle(compressedData);

                            // Vẽ lên màn hình
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (_writeableBitmap != null)
                                {
                                    // Kiểm tra biên giới hạn để tránh crash
                                    if (x + w <= _writeableBitmap.PixelWidth && y + h <= _writeableBitmap.PixelHeight)
                                    {
                                        // WritePixels là phương thức AN TOÀN của WPF, tự động xử lý bộ nhớ
                                        // Stride = Độ rộng 1 dòng tính bằng byte. Với Bgr32 hay Bgra32 đều là 4 byte/pixel.
                                        int stride = w * 4;

                                        Int32Rect rect = new Int32Rect(x, y, w, h);
                                        _writeableBitmap.WritePixels(rect, rawData, stride, 0);
                                    }
                                }
                            });
                        }
                        catch (Exception)
                        {
                            // Bỏ qua frame lỗi (do mạng lag hoặc giải nén sai)
                        }
                    }
                    // --- XỬ LÝ CON TRỎ CHUỘT (Tùy chọn) ---
                    else if (packetType == RdpPacketType.CursorUpdate)
                    {
                        // Đọc bỏ qua 9 byte để không bị lệch stream (X, Y, Visible)
                        await ReadExactly(_stream, headerBuffer, 9);
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }

            // Khi vòng lặp kết thúc (Mất kết nối)
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_isRunning)
                {
                    StatusText.Visibility = Visibility.Visible;
                    StatusText.Text = "Connection Closed";
                }
            });
        }

        // Hàm đọc đủ số byte yêu cầu (Tránh lỗi TCP phân mảnh)
        private async Task ReadExactly(NetworkStream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
        }
        private void SendInput(byte type, int val1, int val2)
        {
            if (_stream == null || !_client.Connected) return;

            try
            {
                byte[] packet = new byte[10];
                packet[0] = (byte)RdpPacketType.ClientInput; // Type = 4 (Bạn nhớ thêm vào Enum ở Shared nhé)
                packet[1] = type;
                BitConverter.TryWriteBytes(packet.AsSpan(2), val1);
                BitConverter.TryWriteBytes(packet.AsSpan(6), val2);

                // Gửi bất đồng bộ không cần chờ (Fire & Forget)
                _stream.WriteAsync(packet, 0, packet.Length);
            }
            catch { }
        }
        private void ScreenDisplay_MouseMove(object sender, MouseEventArgs e)
        {
            if (_serverWidth == 0) return;

            var pos = e.GetPosition(ScreenDisplay); // Lấy tọa độ trên ảnh

            // Vì Image đang để Stretch="None" và Bitmap đúng bằng size server gửi
            // Nên tọa độ pos.X chính là tọa độ trên Server (nếu Server ko resize)
            // Nếu Server có resize 60%, thì Server phải tự nhân lại. 
            // Client cứ gửi tọa độ mình nhìn thấy.

            SendInput(1, (int)pos.X, (int)pos.Y);
        }

        // 2. Mouse Down
        private void ScreenDisplay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ScreenDisplay.Focus(); // Focus để nhận phím
            int btn = (e.ChangedButton == MouseButton.Left) ? 0 : 1; // 0: Left, 1: Right
            SendInput(2, btn, 0);
        }

        // 3. Mouse Up
        private void ScreenDisplay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            int btn = (e.ChangedButton == MouseButton.Left) ? 0 : 1;
            SendInput(3, btn, 0);
        }

        // 4. Key Down
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Chuyển Key WPF sang Virtual Key Code của Windows
            int vKey = KeyInterop.VirtualKeyFromKey(e.Key);
            SendInput(4, vKey, 0);
        }

        // 5. Key Up
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            int vKey = KeyInterop.VirtualKeyFromKey(e.Key);
            SendInput(5, vKey, 0);
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isRunning = false;
            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
        }
    }
}