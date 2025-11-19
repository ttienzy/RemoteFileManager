using RemoteFileManager.Core.Entities;

namespace RemoteFileManager.Core.Interfaces.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> UsernameExistsAsync(string username);
    Task UpdateLastLoginAsync(int userId);
}