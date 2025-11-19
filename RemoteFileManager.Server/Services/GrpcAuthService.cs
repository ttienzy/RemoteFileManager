using Grpc.Core;
using RemoteFileManager.Contracts.Auth;
using RemoteFileManager.Contracts.Common;
using RemoteFileManager.Core.Interfaces.Services;

namespace RemoteFileManager.Server.Services;

public class GrpcAuthService : AuthService.AuthServiceBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<GrpcAuthService> _logger;

    public GrpcAuthService(
        IAuthenticationService authService,
        ILogger<GrpcAuthService> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Login request for username: {Username}", request.Username);

        var (success, token, user) = await _authService.LoginAsync(request.Username, request.Password);

        if (!success || user == null)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Invalid username or password"
            };
        }

        return new LoginResponse
        {
            Success = true,
            Token = token,
            Message = "Login successful",
            UserInfo = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            }
        };
    }

    public override async Task<OperationResult> Logout(LogoutRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Logout request");

        await _authService.LogoutAsync(request.Token);

        return new OperationResult
        {
            Success = true,
            Message = "Logout successful"
        };
    }

    public override async Task<ValidateTokenResponse> ValidateToken(ValidateTokenRequest request, ServerCallContext context)
    {
        var isValid = await _authService.ValidateTokenAsync(request.Token);

        if (!isValid)
        {
            return new ValidateTokenResponse
            {
                IsValid = false
            };
        }

        var user = await _authService.GetUserFromTokenAsync(request.Token);

        if (user == null)
        {
            return new ValidateTokenResponse
            {
                IsValid = false
            };
        }

        return new ValidateTokenResponse
        {
            IsValid = true,
            UserInfo = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            }
        };
    }
}