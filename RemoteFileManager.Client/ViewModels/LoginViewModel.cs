using RemoteFileManager.Client.Core;
using RemoteFileManager.Client.Services;
using RemoteFileManager.Shared.Constants;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace RemoteFileManager.Client.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly INetworkService _networkService;
        private string _ipAddress = "127.0.0.1";
        private int _port = AppConstants.MainPort;
        private bool _isConnecting;
        private string _errorMessage = string.Empty;

        // Sự kiện báo cho MainViewModel biết là đã đăng nhập thành công
        public event Action? LoginSuccess;

        public LoginViewModel(INetworkService networkService)
        {
            _networkService = networkService;
            ConnectCommand = new RelayCommand(async _ => await ConnectAsync(), _ => !IsConnecting);
        }

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                SetProperty(ref _isConnecting, value);
                // Cập nhật lại trạng thái nút bấm (Enable/Disable)
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand ConnectCommand { get; }

        private async Task ConnectAsync()
        {
            IsConnecting = true;
            ErrorMessage = string.Empty;

            bool success = await _networkService.ConnectAsync(IpAddress, Port);

            if (success)
            {
                // Gọi sự kiện để chuyển trang
                LoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = "Không thể kết nối tới Server. Vui lòng kiểm tra IP/Firewall.";
            }

            IsConnecting = false;
        }
    }
}
