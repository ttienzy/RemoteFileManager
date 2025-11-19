namespace RemoteFileManager.Core.Entities;

public class FileShare
{
    public int Id { get; set; }
    public int FileId { get; set; }
    public int SharedWithUserId { get; set; }
    public string Permission { get; set; } = "Read"; // Read, Write
    public DateTime SharedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    // Navigation properties
    public FileMetadata File { get; set; } = null!;
    public User SharedWith { get; set; } = null!;
}