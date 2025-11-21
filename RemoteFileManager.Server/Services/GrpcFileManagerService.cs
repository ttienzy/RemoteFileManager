using Grpc.Core;
using RemoteFileManager.Contracts.FileManager;
using RemoteFileManager.Application.Services;
using RemoteFileManager.Core.Interfaces.Services;
using RemoteFileManager.Contracts.Common;
using FileInfo = RemoteFileManager.Contracts.FileManager.FileInfo;

namespace RemoteFileManager.Server.Services;

public class GrpcFileManagerService : FileManagerService.FileManagerServiceBase
{
    private readonly FileManagementService _fileManagementService;
    private readonly FileStreamingService _streamingService;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<GrpcFileManagerService> _logger;

    public GrpcFileManagerService(
        FileManagementService fileManagementService,
        FileStreamingService streamingService,
        IAuthenticationService authService,
        ILogger<GrpcFileManagerService> logger)
    {
        _fileManagementService = fileManagementService;
        _streamingService = streamingService;
        _authService = authService;
        _logger = logger;
    }

    public override async Task<ListFilesResponse> ListFiles(ListFilesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("ListFiles request for path: {Path}", request.Path);

        var user = await GetUserFromContext(context);
        var files = await _fileManagementService.ListFilesAsync(request.Path, user.Id);

        var response = new ListFilesResponse
        {
            CurrentPath = request.Path
        };

        foreach (var file in files)
        {
            response.Files.Add(new FileInfo
            {
                Name = file.Name,
                FullPath = file.FullPath,
                Size = file.Size,
                CreatedDate = file.CreatedDate.ToString("O"),
                ModifiedDate = file.ModifiedDate.ToString("O"),
                IsDirectory = file.IsDirectory,
                Extension = file.Extension
            });
        }

        return response;
    }

    public override async Task<OperationResult> CreateFolder(CreateFolderRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateFolder request: {Path}/{FolderName}", request.Path, request.FolderName);

        var user = await GetUserFromContext(context);
        var result = await _fileManagementService.CreateFolderAsync(request.Path, request.FolderName, user.Id);

        return new OperationResult
        {
            Success = result.Success,
            Message = result.Message,
            ErrorCode = result.ErrorCode ?? string.Empty
        };
    }

    public override async Task<OperationResult> DeleteFile(DeleteFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("DeleteFile request: {FilePath}", request.FilePath);

        var user = await GetUserFromContext(context);

        try
        {
            var result = await _fileManagementService.DeleteFileAsync(request.FilePath, user.Id);

            return new OperationResult
            {
                Success = result.Success,
                Message = result.Message,
                ErrorCode = result.ErrorCode ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file/folder: {FilePath}", request.FilePath);

            return new OperationResult
            {
                Success = false,
                Message = $"Delete failed: {ex.Message}",
                ErrorCode = "DELETE_ERROR"
            };
        }
    }

    public override async Task<OperationResult> RenameFile(RenameFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("RenameFile request: {OldPath} -> {NewName}", request.OldPath, request.NewName);

        var user = await GetUserFromContext(context);
        var result = await _fileManagementService.RenameFileAsync(request.OldPath, request.NewName, user.Id);

        return new OperationResult
        {
            Success = result.Success,
            Message = result.Message,
            ErrorCode = result.ErrorCode ?? string.Empty
        };
    }

    public override async Task<OperationResult> MoveFile(MoveFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("MoveFile request: {SourcePath} -> {DestinationPath}",
            request.SourcePath, request.DestinationPath);

        var user = await GetUserFromContext(context);
        var result = await _fileManagementService.MoveFileAsync(request.SourcePath, request.DestinationPath, user.Id);

        return new OperationResult
        {
            Success = result.Success,
            Message = result.Message,
            ErrorCode = result.ErrorCode ?? string.Empty
        };
    }

    public override async Task<UploadFileResponse> UploadFile(
        IAsyncStreamReader<FileChunk> requestStream,
        ServerCallContext context)
    {
        _logger.LogInformation("UploadFile request started");

        var user = await GetUserFromContext(context);

        try
        {
            // Read first chunk to get metadata
            if (!await requestStream.MoveNext(context.CancellationToken))
            {
                _logger.LogError("No chunks received in upload request");
                return new UploadFileResponse
                {
                    Success = false,
                    Message = "No data received",
                    SavedPath = string.Empty,
                    FileSize = 0
                };
            }

            var firstChunk = requestStream.Current;
            var fileName = firstChunk.FileName;
            var destinationPath = firstChunk.DestinationPath;
            var totalChunks = firstChunk.TotalChunks;

            _logger.LogInformation(
                "Receiving file: {FileName} to {Path} (Total chunks: {TotalChunks})",
                fileName,
                destinationPath,
                totalChunks);

            // Validation
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogError("FileName is empty in upload request");
                return new UploadFileResponse
                {
                    Success = false,
                    Message = "File name cannot be empty",
                    SavedPath = string.Empty,
                    FileSize = 0
                };
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                _logger.LogWarning("DestinationPath is empty, using root path");
                destinationPath = "/";
            }

            // Create async enumerable that includes first chunk
            async IAsyncEnumerable<byte[]> ReadChunks()
            {
                // Yield first chunk data
                yield return firstChunk.Data.ToByteArray();

                // Yield remaining chunks
                while (await requestStream.MoveNext(context.CancellationToken))
                {
                    yield return requestStream.Current.Data.ToByteArray();
                }
            }

            var (success, message, savedPath, fileSize) = await _streamingService.HandleUploadAsync(
                ReadChunks(),
                fileName,
                destinationPath,
                user.Id);

            _logger.LogInformation("Upload completed: Success={Success}, Path={Path}, Size={Size}",
                success, savedPath, fileSize);

            return new UploadFileResponse
            {
                Success = success,
                Message = message,
                SavedPath = savedPath ?? string.Empty,
                FileSize = fileSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed: {Message}", ex.Message);

            return new UploadFileResponse
            {
                Success = false,
                Message = $"Upload failed: {ex.Message}",
                SavedPath = string.Empty,
                FileSize = 0
            };
        }
    }

    public override async Task DownloadFile(
        DownloadFileRequest request,
        IServerStreamWriter<FileChunk> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("DownloadFile request: {FilePath}", request.FilePath);

        var user = await GetUserFromContext(context);

        int chunkIndex = 0;
        var fileName = Path.GetFileName(request.FilePath);

        // Get file size to calculate total chunks
        var fileInfo = new System.IO.FileInfo(request.FilePath);
        const int chunkSize = 64 * 1024; // 64KB
        var totalChunks = (int)Math.Ceiling((double)fileInfo.Length / chunkSize);

        _logger.LogInformation("Starting download: {FileName}, Size: {Size}, Total chunks: {TotalChunks}",
            fileName, fileInfo.Length, totalChunks);

        await foreach (var chunkData in _streamingService.HandleDownloadAsync(request.FilePath, user.Id))
        {
            var chunk = new FileChunk
            {
                FileName = fileName,
                Data = Google.Protobuf.ByteString.CopyFrom(chunkData),
                ChunkIndex = chunkIndex,
                TotalChunks = totalChunks  // ← FIX: Gửi TotalChunks để client tính progress
            };

            await responseStream.WriteAsync(chunk);
            chunkIndex++;
        }

        _logger.LogInformation("Download completed: {FilePath}, Total chunks: {ChunkCount}",
            request.FilePath, chunkIndex);
    }

    public override async Task<SearchFilesResponse> SearchFiles(SearchFilesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("SearchFiles request: {SearchTerm} in {RootPath}",
            request.SearchTerm, request.RootPath);

        var user = await GetUserFromContext(context);
        var files = await _fileManagementService.SearchFilesAsync(request.SearchTerm, request.RootPath, user.Id);

        var response = new SearchFilesResponse();

        foreach (var file in files)
        {
            response.Results.Add(new FileInfo
            {
                Name = file.Name,
                FullPath = file.FullPath,
                Size = file.Size,
                CreatedDate = file.CreatedDate.ToString("O"),
                ModifiedDate = file.ModifiedDate.ToString("O"),
                IsDirectory = file.IsDirectory,
                Extension = file.Extension
            });
        }

        return response;
    }

    public override async Task<FileInfo> GetFileInfo(GetFileInfoRequest request, ServerCallContext context)
    {
        _logger.LogInformation("GetFileInfo request: {FilePath}", request.FilePath);

        var user = await GetUserFromContext(context);
        var files = await _fileManagementService.ListFilesAsync(Path.GetDirectoryName(request.FilePath) ?? "", user.Id);
        var file = files.FirstOrDefault(f => f.FullPath == request.FilePath);

        if (file == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "File not found"));
        }

        return new FileInfo
        {
            Name = file.Name,
            FullPath = file.FullPath,
            Size = file.Size,
            CreatedDate = file.CreatedDate.ToString("O"),
            ModifiedDate = file.ModifiedDate.ToString("O"),
            IsDirectory = file.IsDirectory,
            Extension = file.Extension
        };
    }

    private async Task<Core.Entities.User> GetUserFromContext(ServerCallContext context)
    {
        var token = context.RequestHeaders.GetValue("authorization")?.Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing authorization token"));
        }

        var user = await _authService.GetUserFromTokenAsync(token);

        if (user == null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));
        }

        return user;
    }
}