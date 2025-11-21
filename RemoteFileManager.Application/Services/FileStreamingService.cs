using RemoteFileManager.Core.Interfaces.Repositories;
using RemoteFileManager.Core.Interfaces.Services;
using RemoteFileManager.Core.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace RemoteFileManager.Application.Services;

public class FileStreamingService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IChunkingService _chunkingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FileStreamingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly long _maxFileSize;

    public FileStreamingService(
        IFileSystemService fileSystemService,
        IChunkingService chunkingService,
        IUnitOfWork unitOfWork,
        ILogger<FileStreamingService> logger,
        IConfiguration configuration)
    {
        _fileSystemService = fileSystemService;
        _chunkingService = chunkingService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _configuration = configuration;
        _maxFileSize = configuration.GetValue<long>("FileStorage:MaxFileSize", 524288000); // 500MB default
    }

    public async Task<(bool Success, string Message, string? SavedPath, long FileSize)> HandleUploadAsync(
        IAsyncEnumerable<byte[]> chunks,
        string fileName,
        string destinationPath,
        int userId)
    {
        try
        {
            _logger.LogInformation("Starting upload: {FileName} to {Path} by user: {UserId}",
                fileName, destinationPath, userId);

            // Validate inputs
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return (false, "File name cannot be empty", null, 0);
            }

            // Get safe filename
            var safeFileName = _fileSystemService.GetSafeFileName(fileName);

            // Resolve to user-specific path
            var absoluteDestinationPath = await ResolveUserPathAsync(destinationPath, userId);

            _logger.LogInformation("Resolved destination path: {AbsolutePath}", absoluteDestinationPath);

            // Ensure directory exists
            if (!Directory.Exists(absoluteDestinationPath))
            {
                Directory.CreateDirectory(absoluteDestinationPath);
                _logger.LogInformation("Created directory: {Path}", absoluteDestinationPath);
            }

            // Merge chunks and save
            var savedPath = await _chunkingService.MergeChunksAsync(
                chunks,
                absoluteDestinationPath,
                safeFileName);

            // Get file size
            var fileInfo = new System.IO.FileInfo(savedPath);
            var fileSize = fileInfo.Length;

            // Validate file size
            if (fileSize > _maxFileSize)
            {
                File.Delete(savedPath);
                return (false, $"File size exceeds maximum allowed size of {_maxFileSize / 1024 / 1024}MB", null, 0);
            }

            // Save metadata
            var metadata = new FileMetadata
            {
                FileName = safeFileName,
                FilePath = savedPath,
                FileSize = fileSize,
                Extension = Path.GetExtension(safeFileName),
                UploadedAt = DateTime.UtcNow,
                UploadedByUserId = userId
            };

            await _unitOfWork.FileMetadata.AddAsync(metadata);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Upload completed: {Path}, Size: {Size} bytes", savedPath, fileSize);

            return (true, "File uploaded successfully", savedPath, fileSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file upload: {FileName} to {Path}", fileName, destinationPath);
            return (false, $"Upload failed: {ex.Message}", null, 0);
        }
    }

    public async IAsyncEnumerable<byte[]> HandleDownloadAsync(string filePath, int userId)
    {
        _logger.LogInformation("Starting download: {Path} by user: {UserId}", filePath, userId);

        // Validate user access
        if (!await ValidateUserAccessAsync(filePath, userId))
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        if (!File.Exists(filePath))
        {
            throw new Core.Exceptions.FileNotFoundException(filePath);
        }

        await foreach (var chunk in _chunkingService.SplitFileAsync(filePath))
        {
            yield return chunk;
        }

        _logger.LogInformation("Download completed: {Path}", filePath);
    }

    #region Helper Methods

    /// <summary>
    /// Resolve path to user-specific absolute path
    /// </summary>
    private async Task<string> ResolveUserPathAsync(string path, int userId)
    {
        var storageRoot = _configuration["FileStorage:RootPath"] ?? "C:\\FileManagerStorage";

        // Get username from database
        string userFolder;
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            userFolder = user?.Username ?? $"User_{userId}";
        }
        catch
        {
            userFolder = $"User_{userId}";
        }

        var userRoot = Path.Combine(storageRoot, userFolder);

        // Handle root path
        if (string.IsNullOrWhiteSpace(path) || path == "/" || path == "\\")
        {
            return userRoot;
        }

        // If already absolute and within user root
        if (Path.IsPathRooted(path))
        {
            var normalized = Path.GetFullPath(path);
            if (normalized.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }
            // If absolute but outside user root, use root
            return userRoot;
        }

        // Handle relative paths
        path = path.TrimStart('/', '\\');
        return Path.Combine(userRoot, path);
    }

    /// <summary>
    /// Validate user access to path
    /// </summary>
    private async Task<bool> ValidateUserAccessAsync(string filePath, int userId)
    {
        try
        {
            var storageRoot = _configuration["FileStorage:RootPath"] ?? "C:\\FileManagerStorage";

            string userFolder;
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                userFolder = user?.Username ?? $"User_{userId}";
            }
            catch
            {
                userFolder = $"User_{userId}";
            }

            var userRoot = Path.Combine(storageRoot, userFolder);
            var normalizedFilePath = Path.GetFullPath(filePath);
            var normalizedUserRoot = Path.GetFullPath(userRoot);

            return normalizedFilePath.StartsWith(normalizedUserRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating access");
            return false;
        }
    }

    #endregion
}