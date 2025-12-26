namespace RemoteFileManager.Shared.Enums
{
    public enum CommandCode
    {
        // --- AUTHENTICATION ---
        LoginRequest,
        LoginSuccess,
        LoginFailed,

        // --- FILE SYSTEM BROWSING ---
        GetDrives,
        GetDirectoryContent,
        ExecuteFile,    // Mở file trên server
        RenameItem,
        DeleteItem,
        CreateFolder,

        // --- TRANSFER & SYNC (Advanced) ---
        TransferReady,      // Server đã sẵn sàng nhận/gửi
        TransferComplete,
        CancelTransfer,

        // --- REMOTE SYSTEM ADMIN (Advanced) ---
        GetSystemInfo,      // CPU, RAM usage
        GetProcesses,       // Task Manager
        KillProcess,        // Tắt ứng dụng
        CaptureScreen,      // Chụp màn hình

        // --- REALTIME EVENTS ---
        DirectoryChanged,   // Server push sự kiện khi có ai đó sửa file
        SystemAlert,        // Cảnh báo server quá tải

        // --- DOWNLOAD COMMANDS ---
        RequestDownload,    // Client xin tải
        DownloadResponse,   // Server trả lời (kèm file size)
        FileChunk,          // Gói dữ liệu
            // --- UPLOAD COMMANDS ---
        RequestUpload,      // Client xin gửi file (kèm tên file + kích thước)
        UploadReady,        // Server bảo: "OK, bắn qua đi"

        SendClipboard, // Gửi text để set vào clipboard máy kia
        GetClipboard,   // Yêu cầu lấy text từ clipboard máy kia

        GetFileContent,     // Yêu cầu đọc file
        SaveFileContent,    // Yêu cầu lưu file
    }
}