using RemoteFileManager.Client.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RemoteFileManager.Client.Core
{
    // Class này đại diện cho 1 máy đang kết nối
    public class ServerSession : INotifyPropertyChanged
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Name { get; set; } = "Unknown PC"; // Tên máy (sẽ lấy sau)

        // Mỗi máy có 1 Service mạng riêng
        public INetworkService NetworkService { get; set; }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}