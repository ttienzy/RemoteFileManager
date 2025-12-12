using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Shared.Models.Transfer
{
    public class FileChunkDto
    {
        public string FileName { get; set; } = string.Empty;
        public long CurrentOffset { get; set; } // Vị trí bắt đầu của gói này
        public long TotalSize { get; set; }     // Tổng dung lượng file (để tính %)
        public byte[] Data { get; set; } = Array.Empty<byte>(); // Dữ liệu nhị phân
        public bool IsLastChunk { get; set; }   // Cờ báo hiệu gói cuối cùng
    }
}
