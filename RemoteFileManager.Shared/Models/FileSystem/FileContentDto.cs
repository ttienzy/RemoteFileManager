namespace RemoteFileManager.Shared.Models.FileSystem
{
    public class FileContentDto
    {
        public string FullPath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = true; // Dùng để báo lỗi nếu cần
        public string ErrorMessage { get; set; } = string.Empty;
    }
}