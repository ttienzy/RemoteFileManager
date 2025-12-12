using RemoteFileManager.Shared.Models.FileSystem;
using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Server.Services.Interfaces
{
    public interface IFileService
    {
        // Lấy danh sách ổ đĩa (C:\, D:\)
        List<DriveDto> GetDrives();

        // Lấy danh sách File và Folder trong một đường dẫn cụ thể
        List<FileDto> GetDirectoryContent(string path);
        void DeleteItem(string path);
        void CreateFolder(string path);
    }
}
