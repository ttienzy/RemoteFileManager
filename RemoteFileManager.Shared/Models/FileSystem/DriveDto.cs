using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Shared.Models.FileSystem
{
    public class DriveDto
    {
        public string Name { get; set; } = string.Empty; // C:\
        public string Label { get; set; } = string.Empty; // Windows
        public long TotalSize { get; set; }
        public long AvailableFreeSpace { get; set; }
        public bool IsReady { get; set; }
    }
}
