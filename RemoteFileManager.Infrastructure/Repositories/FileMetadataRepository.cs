using Microsoft.EntityFrameworkCore;
using RemoteFileManager.Core.Entities;
using RemoteFileManager.Core.Interfaces.Repositories;
using RemoteFileManager.Infrastructure.Persistence;

namespace RemoteFileManager.Infrastructure.Repositories;

public class FileMetadataRepository : Repository<FileMetadata>, IFileMetadataRepository
{
    public FileMetadataRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<FileMetadata>> GetByUserIdAsync(int userId)
    {
        return await _dbSet
            .Where(f => f.UploadedByUserId == userId && !f.IsDeleted)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<FileMetadata>> GetByPathAsync(string path)
    {
        return await _dbSet
            .Where(f => f.FilePath.StartsWith(path) && !f.IsDeleted)
            .OrderBy(f => f.FileName)
            .ToListAsync();
    }

    public async Task<FileMetadata?> GetByFilePathAsync(string filePath)
    {
        return await _dbSet
            .FirstOrDefaultAsync(f => f.FilePath == filePath && !f.IsDeleted);
    }

    public async Task<IEnumerable<FileMetadata>> SearchFilesAsync(string searchTerm, int userId)
    {
        return await _dbSet
            .Where(f => f.UploadedByUserId == userId
                     && f.FileName.Contains(searchTerm)
                     && !f.IsDeleted)
            .OrderBy(f => f.FileName)
            .ToListAsync();
    }

    public async Task<long> GetTotalFileSizeByUserAsync(int userId)
    {
        return await _dbSet
            .Where(f => f.UploadedByUserId == userId && !f.IsDeleted)
            .SumAsync(f => f.FileSize);
    }
}