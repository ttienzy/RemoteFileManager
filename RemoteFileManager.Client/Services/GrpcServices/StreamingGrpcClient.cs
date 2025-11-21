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
        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException("File not found", localFilePath);
        }

        var call = _client.UploadFile(headers: GetHeaders());

        var fileName = Path.GetFileName(localFilePath);
        var fileInfo = new System.IO.FileInfo(localFilePath);
        var totalSize = fileInfo.Length;

        const int chunkSize = 64 * 1024; // 64KB
        var totalChunks = (int)Math.Ceiling((double)totalSize / chunkSize);

        // Log for debugging
        System.Diagnostics.Debug.WriteLine($"Upload: FileName={fileName}, DestPath={destinationPath}, TotalChunks={totalChunks}");

        try
        {
            using var fileStream = File.OpenRead(localFilePath);
            var buffer = new byte[chunkSize];
            int bytesRead;
            int chunkIndex = 0;

            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var chunk = new FileChunk
                {
                    FileName = fileName,
                    DestinationPath = destinationPath ?? "/",  // Ensure not null
                    Data = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead),
                    ChunkIndex = chunkIndex,
                    TotalChunks = totalChunks,
                    Token = _token
                };

                // Debug first chunk
                if (chunkIndex == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"First chunk - FileName: '{chunk.FileName}', DestPath: '{chunk.DestinationPath}', DataLen: {chunk.Data.Length}");
                }

                await call.RequestStream.WriteAsync(chunk);

                chunkIndex++;

                // Report progress
                var percentComplete = Math.Min(100, (int)((double)chunkIndex / totalChunks * 100));
                progress?.Report(percentComplete);
            }

            // Complete the stream
            await call.RequestStream.CompleteAsync();

            // Wait for response
            var response = await call.ResponseAsync;

            // Ensure 100% reported
            progress?.Report(100);

            System.Diagnostics.Debug.WriteLine($"Upload response: Success={response.Success}, Message={response.Message}");

            return response;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Upload error: {ex.Message}");
            throw new Exception($"Upload failed: {ex.Message}", ex);
        }
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

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(localSavePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var call = _client.DownloadFile(request, headers: GetHeaders());
            using var fileStream = new FileStream(
                localSavePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920); // 80KB buffer

            int chunkCount = 0;
            int totalChunks = 0;
            int lastPercent = 0;

            await foreach (var chunk in call.ResponseStream.ReadAllAsync())
            {
                // Write chunk to file
                if (chunk.Data.Length > 0)
                {
                    await fileStream.WriteAsync(chunk.Data.Memory);
                }

                chunkCount++;

                // Update total chunks from first chunk
                if (totalChunks == 0 && chunk.TotalChunks > 0)
                {
                    totalChunks = chunk.TotalChunks;
                }

                // Calculate and report progress
                if (totalChunks > 0)
                {
                    var percent = Math.Min(100, (int)((double)chunkCount / totalChunks * 100));

                    if (percent > lastPercent)
                    {
                        lastPercent = percent;
                        progress?.Report(percent);
                    }
                }
            }

            // Ensure file is flushed
            await fileStream.FlushAsync();

            // Ensure 100% reported
            progress?.Report(100);
        }
        catch (Exception ex)
        {
            // Clean up partial file on error
            if (File.Exists(localSavePath))
            {
                try { File.Delete(localSavePath); } catch { }
            }

            throw new Exception($"Download failed: {ex.Message}", ex);
        }
    }
}