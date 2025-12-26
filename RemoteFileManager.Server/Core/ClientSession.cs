using RemoteFileManager.Server.Handlers;
using RemoteFileManager.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RemoteFileManager.Server.Core
{
    public class ClientSession : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly RequestHandler _requestHandler;
        private readonly CancellationTokenSource _cts;

        public Guid Id { get; } = Guid.NewGuid();
        public string ClientIp { get; private set; } = "Unknown";
        public bool IsConnected => _client != null && _client.Connected;
        public FileStream? CurrentUploadStream { get; set; }

        // Sự kiện để báo cho Server biết session này đã kết thúc
        public event Action<Guid>? OnDisconnected;

        public ClientSession(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            _requestHandler = new RequestHandler(); // Khởi tạo bộ xử lý logic
            _cts = new CancellationTokenSource();

            // Lấy IP của Client để log
            if (client.Client.RemoteEndPoint is IPEndPoint endPoint)
            {
                ClientIp = endPoint.Address.ToString();
            }
        }

        public async Task StartSessionAsync()
        {
            ServerLogger.LogInfo($"Session started for client: {ClientIp} (ID: {Id})");

            try
            {
                while (IsConnected && !_cts.Token.IsCancellationRequested)
                {
                    // 1. Đọc Packet (Có check timeout hoặc cancel)
                    var packet = await SocketHelper.ReceivePacketAsync(_stream);

                    if (packet == null)
                    {
                        ServerLogger.LogWarning($"Client {ClientIp} disconnected cleanly.");
                        break;
                    }

                    // Log lệnh nhận được (trừ các lệnh heartbeat spam log)
                    ServerLogger.LogInfo($"[{ClientIp}] Request: {packet.Command}");

                    // 2. Xử lý logic
                    var response = await _requestHandler.HandleAsync(packet, this, async (p) =>
                    {
                        await SocketHelper.SendPacketAsync(_stream, p);
                    });

                    // 3. Gửi phản hồi
                    if (response != null)
                    {
                        await SocketHelper.SendPacketAsync(_stream, response);
                    }
                }
            }
            catch (IOException)
            {
                ServerLogger.LogWarning($"Client {ClientIp} connection lost (Socket Error).");
            }
            catch (Exception ex)
            {
                ServerLogger.LogError($"Error in session {Id}: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts.Cancel();
                _client.Close();
                OnDisconnected?.Invoke(Id); // Báo cho Server xóa session này khỏi list
            }
            catch { /* Ignored */ }
        }

        public void Dispose()
        {
            CurrentUploadStream?.Dispose();
            Disconnect();
            _stream.Dispose();
            _cts.Dispose();
        }
    }
}
