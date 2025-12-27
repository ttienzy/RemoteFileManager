using Microsoft.Extensions.DependencyInjection;
using RemoteFileManager.Client.Services;
using RemoteFileManager.Client.ViewModels;
using System.Windows;

namespace RemoteFileManager.Client
{
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();

            // --- SERVICES ---
            // 1. SessionManager: Singleton (Quản lý danh sách các máy kết nối)
            services.AddSingleton<SessionManager>();

            // 2. NetworkService: Transient (Mỗi máy con tạo 1 cái riêng)
            services.AddTransient<INetworkService, NetworkService>();

            // --- VIEWMODELS ---
            // 3. MainViewModel: Chứa Dashboard
            services.AddSingleton<MainViewModel>();

            // 4. DashboardViewModel: Quản lý logic chính
            services.AddSingleton<DashboardViewModel>();

            // 5. FileExplorerViewModel: Transient (Vì nó nằm trong Dashboard)
            services.AddTransient<FileExplorerViewModel>();

            // 6. LoginViewModel: Transient (Dùng làm Dialog để thêm máy)
            services.AddTransient<LoginViewModel>();

            // --- WINDOW ---
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            // Gán DataContext tự động
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
            mainWindow.Show();
            base.OnStartup(e);
        }
    }
}