using Microsoft.Extensions.DependencyInjection;
using RemoteFileManager.Client.Services.Extensions; 
using RemoteFileManager.Client.ViewModels;
using RemoteFileManager.Client.Views;
using System.Windows;

namespace RemoteFileManager.Client;

public partial class App : Application
{
    // Chúng ta dùng ServiceProvider thay vì giữ biến _channel thủ công
    private IServiceProvider _serviceProvider;

    public App()
    {
        //InitializeComponent();
        // 1. Setup Dependency Injection Container
        var services = new ServiceCollection();

        // 2. Gọi hàm Extension bạn đã viết (Đăng ký Channel + Clients)
        // Hàm này nằm trong file ServiceCollectionExtensions.cs
        services.AddGrpcClients();


        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<MainViewModel>();

        // Register Windows (Transient: mỗi lần gọi là tạo mới, hoặc Singleton tùy nhu cầu)
        services.AddTransient<LoginWindow>();

        // 4. Build Provider (Tạo "cái máy" cung cấp dịch vụ)
        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // CRITICAL: Try-Catch block to prevent silent crashes
        try
        {
            base.OnStartup(e);

            // 5. Lấy LoginWindow từ DI Container
            // Container sẽ tự động:
            // - Tạo GrpcChannel
            // - Tạo AuthGrpcClient (bơm Channel vào)
            // - Tạo LoginViewModel (bơm AuthClient vào)
            // - Tạo LoginWindow
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();

            // 6. Gán DataContext (nếu chưa gán trong View)
            // Lấy ViewModel từ DI để đảm bảo nó có đủ các GrpcClient
            var viewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
            loginWindow.DataContext = viewModel;

            this.MainWindow = loginWindow;

            // 7. Hiển thị
            loginWindow.Show();
        }
        catch (Exception ex)
        {
            // Display error in English as requested
            string errorMessage = $"CRITICAL STARTUP ERROR:\n\n" +
                                  $"Message: {ex.Message}\n\n" +
                                  $"Stack Trace:\n{ex.StackTrace}";

            MessageBox.Show(errorMessage, "Application Crash", MessageBoxButton.OK, MessageBoxImage.Error);

            // Force shutdown
            Current.Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Dispose resources properly
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnExit(e);
    }
}