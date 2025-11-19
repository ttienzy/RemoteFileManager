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

            var safeFileName = _fileSystemService.GetSafeFileName(fileName);
            var fullPath = _fileSystemService.CombinePaths(destinationPath, safeFileName);

            // Merge chunks and save
            var savedPath = await _chunkingService.MergeChunksAsync(chunks, destinationPath, safeFileName);
            var fileSize = await _fileSystemService.GetFileSizeAsync(savedPath);

            // Validate file size
            if (fileSize > _maxFileSize)
            {
                await _fileSystemService.DeleteFileAsync(savedPath);
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
            _logger.LogError(ex, "Error during file upload");
            return (false, $"Upload failed: {ex.Message}", null, 0);
        }
    }

    public async IAsyncEnumerable<byte[]> HandleDownloadAsync(string filePath, int userId)
    {
        _logger.LogInformation("Starting download: {Path} by user: {UserId}", filePath, userId);

        if (!await _fileSystemService.FileExistsAsync(filePath))
        {
            throw new Core.Exceptions.FileNotFoundException(filePath);
        }

        await foreach (var chunk in _chunkingService.SplitFileAsync(filePath))
        {
            yield return chunk;
        }

        _logger.LogInformation("Download completed: {Path}", filePath);
    }
}