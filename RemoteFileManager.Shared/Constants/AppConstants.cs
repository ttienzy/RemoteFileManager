using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Shared.Constants
{
    public static class AppConstants
    {
        public const int MainPort = 45000;           // Port cho kênh lệnh (Control Channel)
        public const int DataPort = 45001;           // Port cho kênh dữ liệu (Data Channel)
        public const int BufferSize = 8192;          // 8KB buffer cho socket
        public const int FileChunkSize = 64 * 1024;  // 64KB cho mỗi gói file khi upload/download
        public const string EndOfFile = "<EOF>";     // Đánh dấu kết thúc file (nếu cần)
        public const int DiscoveryPort = 45002; // Port riêng cho UDP Broadcast
        public const string DiscoveryMessage = "RFM_DISCOVERY_REQUEST"; // Mật khẩu nhận diện
    }
}
