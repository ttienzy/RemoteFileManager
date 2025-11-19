using RemoteFileManager.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RemoteFileManager.Infrastructure.Services;

public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;

    public int ChunkSize { get; }

    public ChunkingService(IConfiguration configuration, ILogger<ChunkingService> logger)
    {
        _logger = logger;
        ChunkSize = configuration.GetValue<int>("FileStorage:ChunkSize", 65536); // 64KB default
    }

    public async IAsyncEnumerable<byte[]> SplitFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation("Starting to split file: {FilePath}, ChunkSize: {ChunkSize}", filePath, ChunkSize);

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[ChunkSize];
        int bytesRead;
        int chunkIndex = 0;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            var chunk = new byte[bytesRead];
            Array.Copy(buffer, chunk, bytesRead);

            _logger.LogDebug("Yielding chunk {ChunkIndex}, Size: {Size}", chunkIndex, bytesRead);

            yield return chunk;
            chunkIndex++;
        }

        _logger.LogInformation("Finished splitting file: {FilePath}, Total chunks: {TotalChunks}", filePath, chunkIndex);
    }

    public async Task<string> MergeChunksAsync(IAsyncEnumerable<byte[]> chunks, string destinationPath, string fileName)
    {
        var fullPath = Path.Combine(destinationPath, fileName);

        _logger.LogInformation("Starting to merge chunks to: {FullPath}", fullPath);

        // Ensure directory exists
        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(destinationPath);
        }

        // Delete existing file if it exists
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        int chunkIndex = 0;
        long totalBytesWritten = 0;

        using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await foreach (var chunk in chunks)
            {
                await stream.WriteAsync(chunk, 0, chunk.Length);
                totalBytesWritten += chunk.Length;
                chunkIndex++;

                _logger.LogDebug("Merged chunk {ChunkIndex}, Size: {Size}, Total: {Total}",
                    chunkIndex, chunk.Length, totalBytesWritten);
            }
        }

        _logger.LogInformation("Finished merging {ChunkCount} chunks, Total size: {TotalSize} bytes, Path: {FullPath}",
            chunkIndex, totalBytesWritten, fullPath);

        return fullPath;
    }

    public int CalculateTotalChunks(long fileSize)
    {
        return (int)Math.Ceiling((double)fileSize / ChunkSize);
    }
}