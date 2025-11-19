namespace RemoteFileManager.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // User, Admin
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<FileMetadata> UploadedFiles { get; set; } = new List<FileMetadata>();
    public ICollection<FileShare> SharedFiles { get; set; } = new List<FileShare>();
}