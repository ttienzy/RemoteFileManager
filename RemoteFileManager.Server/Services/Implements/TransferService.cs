using RemoteFileManager.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Server.Services.Implements
{
    public class TransferService : ITransferService
    {
        public long GetFileSize(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException();
            return new FileInfo(path).Length;
        }

        public Stream GetFileStream(string path)
        {
            // Mở file ở chế độ Read, cho phép chia sẻ Read (để nhiều người cùng tải 1 file được)
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
