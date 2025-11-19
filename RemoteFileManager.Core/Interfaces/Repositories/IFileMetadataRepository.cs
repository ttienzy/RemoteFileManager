using RemoteFileManager.Core.Entities;

namespace RemoteFileManager.Core.Interfaces.Repositories;

public interface IFileMetadataRepository : IRepository<FileMetadata>
{
    Task<IEnumerable<FileMetadata>> GetByUserIdAsync(int userId);
    Task<IEnumerable<FileMetadata>> GetByPathAsync(string path);
    Task<FileMetadata?> GetByFilePathAsync(string filePath);
    Task<IEnumerable<FileMetadata>> SearchFilesAsync(string searchTerm, int userId);
    Task<long> GetTotalFileSizeByUserAsync(int userId);
}