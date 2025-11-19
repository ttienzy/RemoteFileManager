using RemoteFileManager.Core.Entities;

namespace RemoteFileManager.Core.Interfaces.Services;

public interface IAuthenticationService
{
    Task<(bool Success, string Token, User? User)> LoginAsync(string username, string password);
    Task<bool> ValidateTokenAsync(string token);
    Task<User?> GetUserFromTokenAsync(string token);
    Task LogoutAsync(string token);
    string GenerateToken(User user);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}