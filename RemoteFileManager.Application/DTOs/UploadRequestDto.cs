namespace RemoteFileManager.Application.DTOs;

public class UploadRequestDto
{
    public string FileName { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public int UserId { get; set; }
}