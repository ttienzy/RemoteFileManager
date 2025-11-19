using Grpc.Net.Client;
using RemoteFileManager.Contracts.Auth;
using RemoteFileManager.Contracts.Common;

namespace RemoteFileManager.Client.Services.GrpcServices;

public class AuthGrpcClient
{
    private readonly AuthService.AuthServiceClient _client;

    public AuthGrpcClient(GrpcChannel channel)
    {
        _client = new AuthService.AuthServiceClient(channel);
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var request = new LoginRequest
        {
            Username = username,
            Password = password
        };

        return await _client.LoginAsync(request);
    }

    public async Task<OperationResult> LogoutAsync(string token)
    {
        var request = new LogoutRequest { Token = token };
        return await _client.LogoutAsync(request);
    }

    public async Task<ValidateTokenResponse> ValidateTokenAsync(string token)
    {
        var request = new ValidateTokenRequest { Token = token };
        return await _client.ValidateTokenAsync(request);
    }
}