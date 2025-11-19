namespace RemoteFileManager.Core.Interfaces.Services;

public interface IChunkingService
{
    int ChunkSize { get; }
    IAsyncEnumerable<byte[]> SplitFileAsync(string filePath);
    Task<string> MergeChunksAsync(IAsyncEnumerable<byte[]> chunks, string destinationPath, string fileName);
    int CalculateTotalChunks(long fileSize);
}