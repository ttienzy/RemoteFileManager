using RemoteFileManager.Shared.Constants;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RemoteFileManager.Server.Core
{
    public class RemoteServer
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts; // Dùng để hủy lắng nghe khi tắt server

        // Dùng Dictionary an toàn luồng để quản lý danh sách Client
        private readonly ConcurrentDictionary<Guid, ClientSession> _sessions;

        public bool IsRunning { get; private set; }

        public RemoteServer()
        {
            _listener = new TcpListener(IPAddress.Any, AppConstants.MainPort);
            _sessions = new ConcurrentDictionary<Guid, ClientSession>();
            _cts = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            try
            {
                _listener.Start();
                IsRunning = true;
                ServerLogger.LogInfo($"Server STARTED on port {AppConstants.MainPort}");
                ServerLogger.LogInfo("Waiting for connections...");

                while (!_cts.Token.IsCancellationRequested)
                {
                    // Chấp nhận kết nối bất đồng bộ, hỗ trợ hủy (cancellation)
                    // Lưu ý: AcceptTcpClientAsync không hỗ trợ CancellationToken trực tiếp trong .NET cũ,
                    // nhưng logic dưới đây hoạt động tốt cho .NET 6/8.
                    var tcpClient = await _listener.AcceptTcpClientAsync(_cts.Token);

                    var session = new ClientSession(tcpClient);

                    // Đăng ký sự kiện: Khi session ngắt, tự xóa khỏi danh sách
                    session.OnDisconnected += RemoveSession;

                    if (_sessions.TryAdd(session.Id, session))
                    {
                        // Chạy session trên thread pool (Fire and Forget)
                        _ = Task.Run(() => session.StartSessionAsync(), _cts.Token);

                        UpdateServerTitle();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ServerLogger.LogInfo("Server stopping listener...");
            }
            catch (Exception ex)
            {
                ServerLogger.LogError($"Critical Server Error: {ex.Message}");
            }
            finally
            {
                await StopAsync();
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;

            ServerLogger.LogWarning("Stopping server...");
            _cts.Cancel(); // Dừng vòng lặp Accept
            _listener.Stop();

            // Ngắt kết nối toàn bộ Client
            foreach (var session in _sessions.Values)
            {
                session.Disconnect();
            }

            _sessions.Clear();
            IsRunning = false;
            ServerLogger.LogInfo("Server STOPPED.");
        }

        private void RemoveSession(Guid sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                ServerLogger.LogInfo($"Removed session {sessionId}. Active connections: {_sessions.Count}");
                session.OnDisconnected -= RemoveSession; // Gỡ event handler tránh leak
                session.Dispose();
                UpdateServerTitle();
            }
        }

        private void UpdateServerTitle()
        {
            Console.Title = $"Remote Server | Port: {AppConstants.MainPort} | Online: {_sessions.Count}";
        }
    }
}
