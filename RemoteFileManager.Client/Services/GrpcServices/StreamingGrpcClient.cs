using Grpc.Core;
using Grpc.Net.Client;
using RemoteFileManager.Contracts.FileManager;
using System.IO;

namespace RemoteFileManager.Client.Services.GrpcServices;

public class StreamingGrpcClient
{
    private readonly FileManagerService.FileManagerServiceClient _client;
    private string _token = string.Empty;

    public StreamingGrpcClient(GrpcChannel channel)
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

    public async Task<UploadFileResponse> UploadFileAsync(
        string localFilePath,
        string destinationPath,
        IProgress<int>? progress = null)
    {
        var call = _client.UploadFile(headers: GetHeaders());

        var fileName = Path.GetFileName(localFilePath);
        var fileInfo = new System.IO.FileInfo(localFilePath);
        var totalSize = fileInfo.Length;

        const int chunkSize = 64 * 1024; // 64KB
        var totalChunks = (int)Math.Ceiling((double)totalSize / chunkSize);

        using var fileStream = File.OpenRead(localFilePath);
        var buffer = new byte[chunkSize];
        int bytesRead;
        int chunkIndex = 0;

        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            var chunk = new FileChunk
            {
                FileName = fileName,
                DestinationPath = destinationPath,
                Data = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead),
                ChunkIndex = chunkIndex,
                TotalChunks = totalChunks,
                Token = _token
            };

            await call.RequestStream.WriteAsync(chunk);

            chunkIndex++;
            // Tính phần trăm dựa trên số chunk đã gửi
            var percentComplete = (int)((double)chunkIndex / totalChunks * 100);
            progress?.Report(percentComplete);
        }

        await call.RequestStream.CompleteAsync();
        return await call.ResponseAsync;
    }

    public async Task DownloadFileAsync(
        string remoteFilePath,
        string localSavePath,
        IProgress<int>? progress = null)
    {
        var request = new DownloadFileRequest
        {
            FilePath = remoteFilePath,
            Token = _token
        };

        // Gọi Server
        using var call = _client.DownloadFile(request, headers: GetHeaders());

        // Tạo FileStream để ghi ngay lập tức (Tránh tràn RAM)
        // FileMode.Create: Tạo mới hoặc ghi đè
        using var fileStream = new FileStream(localSavePath, FileMode.Create, FileAccess.Write, FileShare.None);

        int lastPercent = 0;

        // Đọc từng chunk server gửi về
        await foreach (var chunk in call.ResponseStream.ReadAllAsync())
        {
            // 1. Ghi thẳng xuống ổ cứng
            if (chunk.Data.Length > 0)
            {
                await fileStream.WriteAsync(chunk.Data.Memory);
            }

            // 2. Tính toán tiến độ
            // Server phải gửi kèm TotalChunks trong message FileChunk
            if (chunk.TotalChunks > 0)
            {
                // ChunkIndex là index hiện tại (0-based), cần +1 để tính số lượng
                var currentChunkCount = chunk.ChunkIndex + 1;
                var percent = (int)((double)currentChunkCount / chunk.TotalChunks * 100);

                // Chỉ report khi phần trăm thay đổi để đỡ lag UI
                if (percent > lastPercent)
                {
                    lastPercent = percent;
                    progress?.Report(percent);
                }
            }
        }

        // Đảm bảo báo 100% khi xong
        progress?.Report(100);
    }
}