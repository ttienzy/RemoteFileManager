using Microsoft.Extensions.DependencyInjection;
using RemoteFileManager.Client.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Client.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;
        private ViewModelBase _currentViewModel;

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            // Lấy LoginViewModel từ DI
            var loginVm = _serviceProvider.GetRequiredService<LoginViewModel>();

            // Đăng ký sự kiện chuyển trang
            loginVm.LoginSuccess += OnLoginSuccess;

            // QUAN TRỌNG: Dòng này quyết định màn hình đầu tiên hiện lên
            // Nếu dòng này thiếu hoặc null -> Màn hình sẽ trắng
            CurrentViewModel = loginVm;
        }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        private void OnLoginSuccess()
        {
            // Chuyển sang màn hình FileExplorer
            CurrentViewModel = _serviceProvider.GetRequiredService<FileExplorerViewModel>();
        }
    }
}
