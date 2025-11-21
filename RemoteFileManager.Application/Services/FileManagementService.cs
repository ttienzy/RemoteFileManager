using RemoteFileManager.Application.DTOs;
using RemoteFileManager.Core.Interfaces.Repositories;
using RemoteFileManager.Core.Interfaces.Services;
using RemoteFileManager.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FileNotFoundException = RemoteFileManager.Core.Exceptions.FileNotFoundException;

namespace RemoteFileManager.Application.Services;

public class FileManagementService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FileManagementService> _logger;
    private readonly IConfiguration _configuration;

    public FileManagementService(
        IFileSystemService fileSystemService,
        IUnitOfWork unitOfWork,
        ILogger<FileManagementService> logger,
        IConfiguration configuration)
    {
        _fileSystemService = fileSystemService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<IEnumerable<FileInfoDto>> ListFilesAsync(string path, int userId)
    {
        _logger.LogInformation("Listing files in path: {Path} for user: {UserId}", path, userId);

        // Resolve path to user-specific path
        var userPath = await ResolveUserPathAsync(path, userId);

        _logger.LogDebug("Resolved path: {UserPath}", userPath);

        if (!Directory.Exists(userPath))
        {
            // Create user directory if it doesn't exist
            Directory.CreateDirectory(userPath);
            _logger.LogInformation("Created user directory: {Path}", userPath);
        }

        var directory = new DirectoryInfo(userPath);
        var items = directory.GetFileSystemInfos();

        var result = items.Select(f => new FileInfoDto
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

            // Resolve user path
            var userPath = await ResolveUserPathAsync(path, userId);
            var fullPath = Path.Combine(userPath, safeFolderName);

            if (Directory.Exists(fullPath))
            {
                return OperationResultDto.Failed("Folder already exists", "FOLDER_EXISTS");
            }

            Directory.CreateDirectory(fullPath);

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
            _logger.LogInformation("Delete request: {Path} by user: {UserId}", filePath, userId);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return OperationResultDto.Failed("File path cannot be empty", "INVALID_PATH");
            }

            // Validate user access first
            if (!await ValidateUserAccessAsync(filePath, userId))
            {
                return OperationResultDto.Failed("Access denied", "ACCESS_DENIED");
            }

            // Check if it's a directory first
            if (Directory.Exists(filePath))
            {
                _logger.LogInformation("Deleting directory (recursive): {Path}", filePath);

                // Get all files in directory for metadata cleanup
                var allFiles = Directory.GetFiles(filePath, "*", SearchOption.AllDirectories);

                // Delete directory and all contents
                Directory.Delete(filePath, recursive: true);

                // Clean up metadata for all files in deleted directory
                try
                {
                    foreach (var file in allFiles)
                    {
                        var metadata = await _unitOfWork.FileMetadata.GetByFilePathAsync(file);
                        if (metadata != null)
                        {
                            metadata.IsDeleted = true;
                            metadata.ModifiedAt = DateTime.UtcNow;
                            await _unitOfWork.FileMetadata.UpdateAsync(metadata);
                        }
                    }
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not update metadata for deleted directory");
                }

                _logger.LogInformation("Successfully deleted directory: {Path}", filePath);
                return OperationResultDto.Successful("Folder deleted successfully");
            }
            // Then check if it's a file
            else if (File.Exists(filePath))
            {
                _logger.LogInformation("Deleting file: {Path}", filePath);

                File.Delete(filePath);

                // Update metadata
                try
                {
                    var metadata = await _unitOfWork.FileMetadata.GetByFilePathAsync(filePath);
                    if (metadata != null)
                    {
                        metadata.IsDeleted = true;
                        metadata.ModifiedAt = DateTime.UtcNow;
                        await _unitOfWork.FileMetadata.UpdateAsync(metadata);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not update metadata for deleted file: {Path}", filePath);
                }

                _logger.LogInformation("Successfully deleted file: {Path}", filePath);
                return OperationResultDto.Successful("File deleted successfully");
            }
            else
            {
                _logger.LogWarning("Path not found: {Path}", filePath);
                throw new FileNotFoundException($"File or folder not found: {filePath}");
            }
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "File not found: {Path}", filePath);
            return OperationResultDto.Failed(ex.Message, "FILE_NOT_FOUND");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied when deleting: {Path}", filePath);
            return OperationResultDto.Failed("Access denied: " + ex.Message, "ACCESS_DENIED");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error when deleting: {Path}", filePath);
            return OperationResultDto.Failed("Delete failed (file may be in use): " + ex.Message, "IO_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file/folder: {Path}", filePath);
            return OperationResultDto.Failed(ex.Message, "DELETE_FILE_ERROR");
        }
    }

    public async Task<OperationResultDto> RenameFileAsync(string oldPath, string newName, int userId)
    {
        try
        {
            // Validate access
            if (!await ValidateUserAccessAsync(oldPath, userId))
            {
                return OperationResultDto.Failed("Access denied", "ACCESS_DENIED");
            }

            // Check both file and directory
            bool isFile = File.Exists(oldPath);
            bool isDirectory = Directory.Exists(oldPath);

            if (!isFile && !isDirectory)
            {
                throw new FileNotFoundException(oldPath);
            }

            var directory = Path.GetDirectoryName(oldPath) ?? string.Empty;
            var safeNewName = _fileSystemService.GetSafeFileName(newName);
            var newPath = Path.Combine(directory, safeNewName);

            // Check if new path already exists
            if (File.Exists(newPath) || Directory.Exists(newPath))
            {
                return OperationResultDto.Failed("A file or folder with that name already exists", "NAME_EXISTS");
            }

            if (isDirectory)
            {
                Directory.Move(oldPath, newPath);
            }
            else
            {
                File.Move(oldPath, newPath);
            }

            // Update metadata for files
            if (isFile)
            {
                try
                {
                    var metadata = await _unitOfWork.FileMetadata.GetByFilePathAsync(oldPath);
                    if (metadata != null)
                    {
                        metadata.FileName = safeNewName;
                        metadata.FilePath = newPath;
                        metadata.ModifiedAt = DateTime.UtcNow;
                        await _unitOfWork.FileMetadata.UpdateAsync(metadata);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not update metadata for renamed file");
                }
            }

            _logger.LogInformation("Renamed from {OldPath} to {NewPath} by user: {UserId}",
                oldPath, newPath, userId);

            return OperationResultDto.Successful(isDirectory ? "Folder renamed successfully" : "File renamed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming file/folder");
            return OperationResultDto.Failed(ex.Message, "RENAME_FILE_ERROR");
        }
    }

    public async Task<OperationResultDto> MoveFileAsync(string sourcePath, string destinationPath, int userId)
    {
        try
        {
            // Validate access for both paths
            if (!await ValidateUserAccessAsync(sourcePath, userId) ||
                !await ValidateUserAccessAsync(destinationPath, userId))
            {
                return OperationResultDto.Failed("Access denied", "ACCESS_DENIED");
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(sourcePath);
            }

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Move(sourcePath, destinationPath);

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

        // Resolve user path
        var userPath = await ResolveUserPathAsync(rootPath, userId);

        if (!Directory.Exists(userPath))
        {
            return Enumerable.Empty<FileInfoDto>();
        }

        var directory = new DirectoryInfo(userPath);
        var allItems = directory.GetFileSystemInfos("*", SearchOption.AllDirectories);

        var filtered = allItems
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

    #region Helper Methods

    /// <summary>
    /// Resolve relative path to absolute user-specific path
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

        // Handle root path requests
        if (string.IsNullOrWhiteSpace(path) || path == "/" || path == "\\")
        {
            return userRoot;
        }

        // If path is already absolute
        if (Path.IsPathRooted(path))
        {
            var normalized = Path.GetFullPath(path);

            // Validate it's within user's root
            if (normalized.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            // If absolute but not in user root, treat as invalid
            _logger.LogWarning("Invalid absolute path access attempt: {Path}", path);
            return userRoot;
        }

        // Handle relative paths
        path = path.TrimStart('/', '\\');
        return Path.Combine(userRoot, path);
    }

    /// <summary>
    /// Validate if user has access to the specified path
    /// </summary>
    private async Task<bool> ValidateUserAccessAsync(string filePath, int userId)
    {
        try
        {
            var storageRoot = _configuration["FileStorage:RootPath"] ?? "C:\\FileManagerStorage";

            // Get username
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

            // Normalize paths
            var normalizedFilePath = Path.GetFullPath(filePath);
            var normalizedUserRoot = Path.GetFullPath(userRoot);

            // Check if path is within user's root
            bool hasAccess = normalizedFilePath.StartsWith(
                normalizedUserRoot,
                StringComparison.OrdinalIgnoreCase);

            if (!hasAccess)
            {
                _logger.LogWarning(
                    "Access denied: User {UserId} ({UserFolder}) attempted to access {FilePath} outside root {UserRoot}",
                    userId, userFolder, normalizedFilePath, normalizedUserRoot);
            }

            return hasAccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating user access");
            return false;
        }
    }

    #endregion
}