using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Shared.Models.FileSystem
{
    public class FileDto
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsFolder { get; set; }
        public string Extension { get; set; } = string.Empty;
        public DateTime DateModified { get; set; }

        // Nâng cao: Dùng để check xem file đã thay đổi chưa (cho tính năng Sync)
        public string? FileHash { get; set; }
    }
}
