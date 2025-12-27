using RemoteFileManager.Server.Core;
using System.Threading.Tasks;
// 1. Cấu hình Console

Console.Title = "Remote File Manager - SERVER";
Console.BackgroundColor = ConsoleColor.Black;
Console.ForegroundColor = ConsoleColor.White;
Console.Clear();

// Header đẹp
Console.WriteLine("==============================================");
Console.WriteLine("    REMOTE FILE MANAGER SERVER - v1.0         ");
Console.WriteLine("    Powered by .NET 8 | TCP Sockets           ");
Console.WriteLine("==============================================");
Console.WriteLine();
// 2. Khởi tạo Server
var server = new RemoteServer();
var discovery = new DiscoveryService(); // Mới
var remoteDesktop = new RemoteDesktopService();
var cts = new CancellationTokenSource();

// 3. Xử lý sự kiện Ctrl+C (Graceful Shutdown)
// Khi người dùng bấm Ctrl+C hoặc tắt cửa sổ, đoạn code này sẽ chạy
Console.CancelKeyPress += async (sender, e) =>
{
    e.Cancel = true; // Ngăn tiến trình bị kill ngay lập tức
    ServerLogger.LogWarning("Shutdown signal received. Cleaning up...");
    await server.StopAsync();
    Environment.Exit(0);
};

// 4. Chạy Server
try
{
    var task1 = server.StartAsync();
    var task2 = discovery.StartListeningAsync(cts.Token);
    var task3 = remoteDesktop.StartAsync();

    Console.WriteLine("All services started. Press Ctrl+C to stop.");

    await Task.WhenAll(task1, task2, task3);
}
catch (Exception ex)
{
    ServerLogger.LogError($"Fatal Error: {ex.Message}");
}
finally
{
    // Đảm bảo dừng server nếu crash
    await server.StopAsync();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
