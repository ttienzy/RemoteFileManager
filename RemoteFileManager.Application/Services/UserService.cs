using RemoteFileManager.Application.DTOs;
using RemoteFileManager.Core.Entities;
using RemoteFileManager.Core.Interfaces.Repositories;
using RemoteFileManager.Core.Interfaces.Services;
using RemoteFileManager.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace RemoteFileManager.Application.Services;

public class UserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUnitOfWork unitOfWork,
        IAuthenticationService authService,
        ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _logger = logger;
    }

    public async Task<OperationResultDto> CreateUserAsync(string username, string password, string role = "User")
    {
        try
        {
            if (await _unitOfWork.Users.UsernameExistsAsync(username))
            {
                return OperationResultDto.Failed("Username already exists", "USERNAME_EXISTS");
            }

            var user = new User
            {
                Username = username,
                PasswordHash = _authService.HashPassword(password),
                Role = role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User created: {Username}", username);

            return OperationResultDto.Successful("User created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", username);
            return OperationResultDto.Failed(ex.Message, "CREATE_USER_ERROR");
        }
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);

        if (user == null)
            return null;

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _unitOfWork.Users.GetAllAsync();

        return users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Role = u.Role,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        });
    }

    public async Task<OperationResultDto> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            if (user == null)
            {
                return OperationResultDto.Failed("User not found", "USER_NOT_FOUND");
            }

            if (!_authService.VerifyPassword(currentPassword, user.PasswordHash))
            {
                return OperationResultDto.Failed("Current password is incorrect", "INVALID_PASSWORD");
            }

            user.PasswordHash = _authService.HashPassword(newPassword);
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Password changed for user: {UserId}", userId);

            return OperationResultDto.Successful("Password changed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
            return OperationResultDto.Failed(ex.Message, "CHANGE_PASSWORD_ERROR");
        }
    }

    public async Task<OperationResultDto> DeactivateUserAsync(int userId)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            if (user == null)
            {
                return OperationResultDto.Failed("User not found", "USER_NOT_FOUND");
            }

            user.IsActive = false;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User deactivated: {UserId}", userId);

            return OperationResultDto.Successful("User deactivated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user: {UserId}", userId);
            return OperationResultDto.Failed(ex.Message, "DEACTIVATE_USER_ERROR");
        }
    }
}