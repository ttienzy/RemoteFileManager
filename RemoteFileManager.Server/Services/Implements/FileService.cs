using RemoteFileManager.Server.Services.Interfaces;
using RemoteFileManager.Shared.Models.FileSystem;
using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Server.Services.Implements
{
    public class FileService : IFileService
    {
        public List<DriveDto> GetDrives()
        {
            var drives = DriveInfo.GetDrives();
            var result = new List<DriveDto>();

            foreach (var drive in drives)
            {
                // Chỉ lấy ổ đĩa đã sẵn sàng (tránh lỗi ổ CD/USB chưa cắm)
                if (drive.IsReady)
                {
                    result.Add(new DriveDto
                    {
                        Name = drive.Name,       // C:\
                        Label = drive.VolumeLabel, // Windows
                        TotalSize = drive.TotalSize,
                        AvailableFreeSpace = drive.AvailableFreeSpace,
                        IsReady = true
                    });
                }
            }
            return result;
        }

        public List<FileDto> GetDirectoryContent(string path)
        {
            var result = new List<FileDto>();
            var directoryInfo = new DirectoryInfo(path);

            if (!directoryInfo.Exists)
                throw new DirectoryNotFoundException($"Path not found: {path}");

            // 1. Lấy danh sách Folder
            try
            {
                foreach (var dir in directoryInfo.GetDirectories())
                {
                    result.Add(new FileDto
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsFolder = true,
                        DateModified = dir.LastWriteTime,
                        Size = 0 // Folder thường không tính size ngay để tránh chậm
                    });
                }
            }
            catch (UnauthorizedAccessException) { /* Bỏ qua folder không có quyền truy cập */ }

            // 2. Lấy danh sách File
            try
            {
                foreach (var file in directoryInfo.GetFiles())
                {
                    result.Add(new FileDto
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsFolder = false,
                        Size = file.Length,
                        DateModified = file.LastWriteTime,
                        Extension = file.Extension
                    });
                }
            }
            catch (UnauthorizedAccessException) { /* Bỏ qua file hệ thống */ }

            return result;
        }
        public void DeleteItem(string path)
        {
            // Kiểm tra xem là File hay Folder để dùng lệnh xóa phù hợp
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                // recursive: true nghĩa là xóa cả các file con bên trong
                Directory.Delete(path, recursive: true);
            }
            else
            {
                throw new FileNotFoundException("Path not found to delete.");
            }
        }

        public void CreateFolder(string path)
        {
            if (Directory.Exists(path))
                throw new Exception("Folder already exists.");

            Directory.CreateDirectory(path);
        }
    }
}
