using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RemoteFileManager.Client.Services;
using RemoteFileManager.Client.ViewModels;

namespace RemoteFileManager.Client
{
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();

            // 1. Đăng ký Service (Singleton: Chỉ tạo 1 lần duy nhất)
            services.AddSingleton<INetworkService, NetworkService>();

            // 2. Đăng ký ViewModel
            services.AddSingleton<MainViewModel>();       // Main giữ trạng thái chuyển trang nên cần Singleton
            services.AddTransient<LoginViewModel>();      // Login mỗi lần hiện là mới
            services.AddTransient<FileExplorerViewModel>();

            // 3. Đăng ký MainWindow
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Lấy MainWindow từ DI container và hiển thị
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            base.OnStartup(e);
        }
    }
}