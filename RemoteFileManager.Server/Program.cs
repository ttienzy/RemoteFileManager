using RemoteFileManager.Server.Core;
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
    await server.StartAsync();
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
