namespace RemoteFileManager.Core.Entities;

public class FileMetadata
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Extension { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public int UploadedByUserId { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Navigation properties
    public User UploadedBy { get; set; } = null!;
    public ICollection<FileShare> Shares { get; set; } = new List<FileShare>();
}