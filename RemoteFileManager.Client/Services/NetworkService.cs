using RemoteFileManager.Shared.Helpers;
using RemoteFileManager.Shared.Models.Network;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace RemoteFileManager.Client.Services
{
    public class NetworkService : INetworkService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource _cts = new();

        public event Action<Packet>? PacketReceived;
        public event Action? OnDisconnected;

        public bool IsConnected => _client?.Connected ?? false;

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ip, port);
                _stream = _client.GetStream();

                // Sau khi kết nối thành công, bắt đầu lắng nghe ngay lập tức
                _cts = new CancellationTokenSource();
                _ = Task.Run(ReceiveLoop);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task SendPacketAsync(Packet packet)
        {
            if (_stream == null || !IsConnected) return;
            await SocketHelper.SendPacketAsync(_stream, packet);
        }

        private async Task ReceiveLoop()
        {
            try
            {
                while (IsConnected && !_cts.Token.IsCancellationRequested)
                {
                    if (_stream == null) break;

                    var packet = await SocketHelper.ReceivePacketAsync(_stream);
                    if (packet == null)
                    {
                        // Server đóng kết nối
                        Disconnect();
                        break;
                    }

                    // Bắn sự kiện ra ngoài cho ViewModel xử lý
                    // Lưu ý: Sự kiện này đang ở Thread Background, ViewModel cần đưa về MainThread
                    PacketReceived?.Invoke(packet);
                }
            }
            catch
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            _cts.Cancel();
            _client?.Close();
            _stream?.Dispose();
            OnDisconnected?.Invoke();
        }
    }
}
