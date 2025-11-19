using Microsoft.EntityFrameworkCore;
using RemoteFileManager.Core.Entities;
using RemoteFileManager.Core.Interfaces.Repositories;
using RemoteFileManager.Infrastructure.Persistence;
using FileShare = RemoteFileManager.Core.Entities.FileShare;

namespace RemoteFileManager.Infrastructure.Repositories;

public class FileShareRepository : Repository<FileShare>, IFileShareRepository
{
    public FileShareRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<FileShare>> GetSharedWithUserAsync(int userId)
    {
        return await _dbSet
            .Include(s => s.File)
            .Include(s => s.SharedWith)
            .Where(s => s.SharedWithUserId == userId)
            .OrderByDescending(s => s.SharedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<FileShare>> GetSharesByFileIdAsync(int fileId)
    {
        return await _dbSet
            .Include(s => s.SharedWith)
            .Where(s => s.FileId == fileId)
            .ToListAsync();
    }

    public async Task<bool> HasAccessAsync(int userId, int fileId)
    {
        return await _dbSet
            .AnyAsync(s => s.FileId == fileId && s.SharedWithUserId == userId);
    }

    public async Task RevokeShareAsync(int fileId, int userId)
    {
        var share = await _dbSet
            .FirstOrDefaultAsync(s => s.FileId == fileId && s.SharedWithUserId == userId);

        if (share != null)
        {
            await DeleteAsync(share);
        }
    }
}