using RemoteFileManager.Client.Core;

namespace RemoteFileManager.Client.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // Biến này sẽ chứa DashboardViewModel
        private ViewModelBase _currentViewModel;

        public MainViewModel(DashboardViewModel dashboardViewModel)
        {
            // Khởi động lên là vào thẳng Dashboard (Giao diện chia đôi: Sidebar + Content)
            CurrentViewModel = dashboardViewModel;
        }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }
    }
}