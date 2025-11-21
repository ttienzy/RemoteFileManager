using RemoteFileManager.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RemoteFileManager.Infrastructure.Services;

public class FileSystemService : IFileSystemService
{
    private readonly string _rootPath;
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(IConfiguration configuration, ILogger<FileSystemService> logger)
    {
        _rootPath = configuration["FileStorage:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        _logger = logger;

        // Ensure root directory exists
        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
            _logger.LogInformation("Created root storage directory: {RootPath}", _rootPath);
        }
    }

    public async Task<IEnumerable<FileSystemInfo>> ListFilesAsync(string path)
    {
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        var directory = new DirectoryInfo(fullPath);
        var items = directory.GetFileSystemInfos();

        return await Task.FromResult(items);
    }

    public async Task<FileInfo> GetFileInfoAsync(string filePath)
    {
        var fullPath = GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        return await Task.FromResult(new FileInfo(fullPath));
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        var fullPath = GetFullPath(filePath);
        return await Task.FromResult(File.Exists(fullPath));
    }

    public async Task<bool> DirectoryExistsAsync(string path)
    {
        var fullPath = GetFullPath(path);
        return await Task.FromResult(Directory.Exists(fullPath));
    }

    public async Task CreateDirectoryAsync(string path)
    {
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            _logger.LogInformation("Created directory: {Path}", fullPath);
        }

        await Task.CompletedTask;
    }

    public async Task DeleteFileAsync(string filePath)
    {
        var fullPath = GetFullPath(filePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted file: {Path}", fullPath);
        }

        await Task.CompletedTask;
    }

    public async Task DeleteDirectoryAsync(string path, bool recursive = false)
    {
        var fullPath = GetFullPath(path);

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive);
            _logger.LogInformation("Deleted directory: {Path}", fullPath);
        }

        await Task.CompletedTask;
    }

    public async Task RenameFileAsync(string oldPath, string newPath)
    {
        var fullOldPath = GetFullPath(oldPath);
        var fullNewPath = GetFullPath(newPath);

        if (!File.Exists(fullOldPath))
        {
            throw new FileNotFoundException($"File not found: {oldPath}");
        }

        File.Move(fullOldPath, fullNewPath);
        _logger.LogInformation("Renamed file from {OldPath} to {NewPath}", fullOldPath, fullNewPath);

        await Task.CompletedTask;
    }

    public async Task MoveFileAsync(string sourcePath, string destinationPath)
    {
        var fullSourcePath = GetFullPath(sourcePath);
        var fullDestPath = GetFullPath(destinationPath);

        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException($"File not found: {sourcePath}");
        }

        // Ensure destination directory exists
        var destDirectory = Path.GetDirectoryName(fullDestPath);
        if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
        {
            Directory.CreateDirectory(destDirectory);
        }

        File.Move(fullSourcePath, fullDestPath);
        _logger.LogInformation("Moved file from {Source} to {Destination}", fullSourcePath, fullDestPath);

        await Task.CompletedTask;
    }

    public async Task<byte[]> ReadFileBytesAsync(string filePath, int offset, int length)
    {
        var fullPath = GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(offset, SeekOrigin.Begin);

        var buffer = new byte[length];
        var bytesRead = await stream.ReadAsync(buffer, 0, length);

        if (bytesRead < length)
        {
            Array.Resize(ref buffer, bytesRead);
        }

        return buffer;
    }

    public async Task WriteFileBytesAsync(string filePath, byte[] data, bool append = false)
    {
        var fullPath = GetFullPath(filePath);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var mode = append ? FileMode.Append : FileMode.Create;

        using var stream = new FileStream(fullPath, mode, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(data, 0, data.Length);
    }

    public async Task<long> GetFileSizeAsync(string filePath)
    {
        var fullPath = GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var fileInfo = new FileInfo(fullPath);
        return await Task.FromResult(fileInfo.Length);
    }

    public string GetSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return safeName;
    }

    public string CombinePaths(params string[] paths)
    {
        var combined = Path.Combine(paths);
        return GetFullPath(combined);
    }

    private string GetFullPath(string path)
    {
        // Handle null or empty path
        if (string.IsNullOrWhiteSpace(path))
        {
            return _rootPath;
        }

        // If path is already absolute and starts with root, return as-is
        if (Path.IsPathRooted(path))
        {
            var normalizedPath = Path.GetFullPath(path);

            // Security check: ensure path is within root directory
            if (!normalizedPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access to path outside root directory is not allowed: {path}");
            }

            return normalizedPath;
        }

        // Handle relative paths
        // Remove leading slash or backslash for relative paths only
        path = path.TrimStart('/', '\\');

        // If path is now empty after trimming (was just "/"), return root
        if (string.IsNullOrWhiteSpace(path))
        {
            return _rootPath;
        }

        // Combine with root path
        var fullPath = Path.Combine(_rootPath, path);

        // Normalize path
        fullPath = Path.GetFullPath(fullPath);

        // Security check: ensure path is within root directory
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Access to path outside root directory is not allowed: {path}");
        }

        return fullPath;
    }
}