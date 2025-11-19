using RemoteFileManager.Core.Entities;
using FileShare = RemoteFileManager.Core.Entities.FileShare;

namespace RemoteFileManager.Core.Interfaces.Repositories;

public interface IFileShareRepository : IRepository<FileShare>
{
    Task<IEnumerable<FileShare>> GetSharedWithUserAsync(int userId);
    Task<IEnumerable<FileShare>> GetSharesByFileIdAsync(int fileId);
    Task<bool> HasAccessAsync(int userId, int fileId);
    Task RevokeShareAsync(int fileId, int userId);
}