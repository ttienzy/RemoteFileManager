using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RemoteFileManager.Server.Services.Interfaces
{
    public interface ITransferService
    {
        // Hàm này trả về Stream của file để Handler tự đọc và gửi
        // Hoặc trả về FileInfo để lấy độ lớn
        long GetFileSize(string path);

        // Thực ra việc gửi chunk nên nằm ở Handler hoặc một luồng riêng
        // Service chỉ nên lo việc IO (Đọc file)
        Stream GetFileStream(string path);
    }
}
