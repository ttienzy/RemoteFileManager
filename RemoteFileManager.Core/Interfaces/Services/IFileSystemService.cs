namespace RemoteFileManager.Core.Interfaces.Services;

public interface IFileSystemService
{
    Task<IEnumerable<FileSystemInfo>> ListFilesAsync(string path);
    Task<FileInfo> GetFileInfoAsync(string filePath);
    Task<bool> FileExistsAsync(string filePath);
    Task<bool> DirectoryExistsAsync(string path);
    Task CreateDirectoryAsync(string path);
    Task DeleteFileAsync(string filePath);
    Task DeleteDirectoryAsync(string path, bool recursive = false);
    Task RenameFileAsync(string oldPath, string newPath);
    Task MoveFileAsync(string sourcePath, string destinationPath);
    Task<byte[]> ReadFileBytesAsync(string filePath, int offset, int length);
    Task WriteFileBytesAsync(string filePath, byte[] data, bool append = false);
    Task<long> GetFileSizeAsync(string filePath);
    string GetSafeFileName(string fileName);
    string CombinePaths(params string[] paths);
}