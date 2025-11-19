using Grpc.Core;
using Grpc.Net.Client;
using RemoteFileManager.Contracts.Common;
using RemoteFileManager.Contracts.FileManager;

namespace RemoteFileManager.Client.Services.GrpcServices;

public class FileManagerGrpcClient
{
    private readonly FileManagerService.FileManagerServiceClient _client;
    private string _token = string.Empty;

    public FileManagerGrpcClient(GrpcChannel channel)
    {
        _client = new FileManagerService.FileManagerServiceClient(channel);
    }

    public void SetAuthToken(string token)
    {
        _token = token;
    }

    private Metadata GetHeaders()
    {
        var headers = new Metadata();
        if (!string.IsNullOrEmpty(_token))
        {
            headers.Add("Authorization", $"Bearer {_token}");
        }
        return headers;
    }

    public async Task<ListFilesResponse> ListFilesAsync(string path)
    {
        var request = new ListFilesRequest { Path = path, Token = _token };
        return await _client.ListFilesAsync(request, headers: GetHeaders());
    }

    public async Task<OperationResult> CreateFolderAsync(string path, string folderName)
    {
        var request = new CreateFolderRequest
        {
            Path = path,
            FolderName = folderName,
            Token = _token
        };
        return await _client.CreateFolderAsync(request, headers: GetHeaders());
    }

    public async Task<OperationResult> DeleteFileAsync(string filePath)
    {
        var request = new DeleteFileRequest { FilePath = filePath, Token = _token };
        return await _client.DeleteFileAsync(request, headers: GetHeaders());
    }

    public async Task<OperationResult> RenameFileAsync(string oldPath, string newName)
    {
        var request = new RenameFileRequest
        {
            OldPath = oldPath,
            NewName = newName,
            Token = _token
        };
        return await _client.RenameFileAsync(request, headers: GetHeaders());
    }

    public async Task<OperationResult> MoveFileAsync(string sourcePath, string destinationPath)
    {
        var request = new MoveFileRequest
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            Token = _token
        };
        return await _client.MoveFileAsync(request, headers: GetHeaders());
    }

    public async Task<SearchFilesResponse> SearchFilesAsync(string searchTerm, string rootPath)
    {
        var request = new SearchFilesRequest
        {
            SearchTerm = searchTerm,
            RootPath = rootPath,
            Token = _token
        };
        return await _client.SearchFilesAsync(request, headers: GetHeaders());
    }

    public async Task<FileInfo> GetFileInfoAsync(string filePath)
    {
        var request = new GetFileInfoRequest { FilePath = filePath, Token = _token };
        return await _client.GetFileInfoAsync(request, headers: GetHeaders());
    }
}