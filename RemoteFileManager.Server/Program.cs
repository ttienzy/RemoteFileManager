using Microsoft.AspNetCore.Server.Kestrel.Core;
using RemoteFileManager.Application.Extensions;
using RemoteFileManager.Infrastructure.Extensions;
using RemoteFileManager.Infrastructure.Persistence;
using RemoteFileManager.Server.Interceptors;
using RemoteFileManager.Server.Services;
using Serilog;
using System.Net;


var builder = WebApplication.CreateBuilder(args);


Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/server-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting Remote File Manager Server...");

// =====================================================
// 3. ADD SERVICES TO CONTAINER
// =====================================================

// Add gRPC with interceptors
builder.Services.AddGrpc(options =>
{
    // Thêm interceptors để xử lý exceptions và authentication
    options.Interceptors.Add<ExceptionInterceptor>();
    options.Interceptors.Add<AuthenticationInterceptor>();

    // Tăng kích thước message tối đa (cho upload files lớn)
    options.MaxReceiveMessageSize = 100 * 1024 * 1024; // 100MB
    options.MaxSendMessageSize = 100 * 1024 * 1024;    // 100MB
});

// Add gRPC Reflection (để debug với tools như grpcurl)
builder.Services.AddGrpcReflection();


builder.Services.AddApplication();


builder.Services.AddInfrastructure(builder.Configuration);

//builder.WebHost.ConfigureKestrel(options =>
//{
//    options.Listen(IPAddress.Any, 5000, listenOptions =>
//    {
//        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
//    });
//});

//builder.WebHost.UseUrls("http://0.0.0.0:5000");

// =====================================================
// 5. BUILD APPLICATION
// =====================================================
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();

        // Tạo database nếu chưa tồn tại
        context.Database.EnsureCreated();

        Log.Information("Database initialized successfully");

        // Seed dữ liệu ban đầu (admin, user...)
        await DbSeeder.SeedAsync(context);

        Log.Information("Database seeded successfully");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "An error occurred while initializing the database");
        throw;
    }
}


app.MapGrpcService<GrpcAuthService>();
app.MapGrpcService<GrpcFileManagerService>();

// Enable gRPC Reflection (cho development/debugging)
if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

// Default endpoint (khi truy cập http://localhost:5000/ bằng browser)
//app.MapGet("/", () =>
//    "Remote File Manager gRPC Server is running!\n\n" +
//    "Connect using gRPC client at: http://localhost:5000\n" +
//    "Default users:\n" +
//    "  - admin / admin123\n" +
//    "  - user / user123");

// Health check endpoint
//app.MapGet("/health", () => Results.Ok(new
//{
//    status = "healthy",
//    timestamp = DateTime.UtcNow,
//    version = "1.0.0"
//}));


Log.Information("Server is ready and listening on http://0.0.0.0:5000");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
