namespace RemoteFileManager.Client.Services.Helpers;

public static class ChunkHelper
{
    public const int DefaultChunkSize = 65536; // 64KB

    public static int CalculateTotalChunks(long fileSize, int chunkSize = DefaultChunkSize)
    {
        return (int)Math.Ceiling((double)fileSize / chunkSize);
    }

    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}