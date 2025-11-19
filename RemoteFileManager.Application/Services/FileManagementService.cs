using RemoteFileManager.Application.DTOs;
using RemoteFileManager.Core.Interfaces.Repositories;
using RemoteFileManager.Core.Interfaces.Services;
using RemoteFileManager.Core.Exceptions;
using Microsoft.Extensions.Logging;
using FileNotFoundException = RemoteFileManager.Core.Exceptions.FileNotFoundException;


namespace RemoteFileManager.Application.Services;

public class FileManagementService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FileManagementService> _logger;

    public FileManagementService(
        IFileSystemService fileSystemService,
        IUnitOfWork unitOfWork,
        ILogger<FileManagementService> logger)
    {
        _fileSystemService = fileSystemService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<FileInfoDto>> ListFilesAsync(string path, int userId)
    {
        _logger.LogInformation("Listing files in path: {Path} for user: {UserId}", path, userId);

        if (!await _fileSystemService.DirectoryExistsAsync(path))
        {
            throw new FileNotFoundException(path);
        }

        var files = await _fileSystemService.ListFilesAsync(path);

        var result = files.Select(f => new FileInfoDto
        {
            Name = f.Name,
            FullPath = f.FullName,
            Size = f is System.IO.FileInfo fileInfo ? fileInfo.Length : 0,
            CreatedDate = f.CreationTime,
            ModifiedDate = f.LastWriteTime,
            IsDirectory = f is DirectoryInfo,
            Extension = f.Extension
        });

        return result;
    }

    public async Task<OperationResultDto> CreateFolderAsync(string path, string folderName, int userId)
    {
        try
        {
            var safeFolderName = _fileSystemService.GetSafeFileName(folderName);
            var fullPath = _fileSystemService.CombinePaths(path, safeFolderName);

            if (await _fileSystemService.DirectoryExistsAsync(fullPath))
            {
                return OperationResultDto.Failed("Folder already exists", "FOLDER_EXISTS");
            }

            await _fileSystemService.CreateDirectoryAsync(fullPath);

            _logger.LogInformation("Folder created: {Path} by user: {UserId}", fullPath, userId);

            return OperationResultDto.Successful("Folder created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating folder");
            return OperationResultDto.Failed(ex.Message, "CREATE_FOLDER_ERROR");
        }
    }

    public async Task<OperationResultDto> DeleteFileAsync(string filePath, int userId)
    {
        try
        {
            if (!await _fileSystemService.FileExistsAsync(filePath))
            {
                throw new FileNotFoundException(filePath);
            }

            await _fileSystemService.DeleteFileAsync(filePath);

            // Update metadata
            var metadata = await _unitOfWork.FileMetadata.GetByFilePathAsync(filePath);
            if (metadata != null)
            {
                metadata.IsDeleted = true;
                metadata.ModifiedAt = DateTime.UtcNow;
                await _unitOfWork.FileMetadata.UpdateAsync(metadata);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("File deleted: {Path} by user: {UserId}", filePath, userId);

            return OperationResultDto.Successful("File deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file");
            return OperationResultDto.Failed(ex.Message, "DELETE_FILE_ERROR");
        }
    }

    public async Task<OperationResultDto> RenameFileAsync(string oldPath, string newName, int userId)
    {
        try
        {
            if (!await _fileSystemService.FileExistsAsync(oldPath))
            {
                throw new FileNotFoundException(oldPath);
            }

            var directory = Path.GetDirectoryName(oldPath) ?? string.Empty;
            var safeNewName = _fileSystemService.GetSafeFileName(newName);
            var newPath = _fileSystemService.CombinePaths(directory, safeNewName);

            await _fileSystemService.RenameFileAsync(oldPath, newPath);

            // Update metadata
            var metadata = await _unitOfWork.FileMetadata.GetByFilePathAsync(oldPath);
            if (metadata != null)
            {
                metadata.FileName = safeNewName;
                metadata.FilePath = newPath;
                metadata.ModifiedAt = DateTime.UtcNow;
                await _unitOfWork.FileMetadata.UpdateAsync(metadata);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("File renamed from {OldPath} to {NewPath} by user: {UserId}",
                oldPath, newPath, userId);

            return OperationResultDto.Successful("File renamed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming file");
            return OperationResultDto.Failed(ex.Message, "RENAME_FILE_ERROR");
        }
    }

    public async Task<OperationResultDto> MoveFileAsync(string sourcePath, string destinationPath, int userId)
    {
        try
        {
            if (!await _fileSystemService.FileExistsAsync(sourcePath))
            {
                throw new FileNotFoundException(sourcePath);
            }

            await _fileSystemService.MoveFileAsync(sourcePath, destinationPath);

            // Update metadata
            var metadata = await _unitOfWork.FileMetadata.GetByFilePathAsync(sourcePath);
            if (metadata != null)
            {
                metadata.FilePath = destinationPath;
                metadata.ModifiedAt = DateTime.UtcNow;
                await _unitOfWork.FileMetadata.UpdateAsync(metadata);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("File moved from {Source} to {Destination} by user: {UserId}",
                sourcePath, destinationPath, userId);

            return OperationResultDto.Successful("File moved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving file");
            return OperationResultDto.Failed(ex.Message, "MOVE_FILE_ERROR");
        }
    }

    public async Task<IEnumerable<FileInfoDto>> SearchFilesAsync(string searchTerm, string rootPath, int userId)
    {
        _logger.LogInformation("Searching for '{SearchTerm}' in {RootPath} by user: {UserId}",
            searchTerm, rootPath, userId);

        var files = await _fileSystemService.ListFilesAsync(rootPath);

        var filtered = files
            .Where(f => f.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .Select(f => new FileInfoDto
            {
                Name = f.Name,
                FullPath = f.FullName,
                Size = f is System.IO.FileInfo fileInfo ? fileInfo.Length : 0,
                CreatedDate = f.CreationTime,
                ModifiedDate = f.LastWriteTime,
                IsDirectory = f is DirectoryInfo,
                Extension = f.Extension
            });

        return filtered;
    }
}