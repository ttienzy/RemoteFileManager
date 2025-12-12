using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Shared.Models.System
{
    public class ProcessDto
    {
        public int Id { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public long MemoryUsage { get; set; } // Bytes
        public string WindowTitle { get; set; } = string.Empty;
    }
}
