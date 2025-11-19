using Grpc.Core;
using Grpc.Core.Interceptors;
using RemoteFileManager.Core.Interfaces.Services;

namespace RemoteFileManager.Server.Interceptors;

public class AuthenticationInterceptor : Interceptor
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthenticationInterceptor> _logger;

    // Methods that don't require authentication
    private static readonly HashSet<string> _publicMethods = new()
    {
        "/AuthService/Login"
    };

    public AuthenticationInterceptor(
        IAuthenticationService authService,
        ILogger<AuthenticationInterceptor> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        if (!_publicMethods.Contains(context.Method))
        {
            await ValidateTokenAsync(context);
        }

        return await continuation(request, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        if (!_publicMethods.Contains(context.Method))
        {
            await ValidateTokenAsync(context);
        }

        return await continuation(requestStream, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        if (!_publicMethods.Contains(context.Method))
        {
            await ValidateTokenAsync(context);
        }

        await continuation(request, responseStream, context);
    }

    private async Task ValidateTokenAsync(ServerCallContext context)
    {
        // 1. Lấy header an toàn hơn
        var authHeader = context.RequestHeaders.GetValue("authorization");

        // LOG RA ĐỂ KIỂM TRA (Xem client gửi lên cái gì)
        _logger.LogInformation($"[AuthInterceptor] Header received: '{authHeader}'");

        if (string.IsNullOrEmpty(authHeader))
        {
            _logger.LogWarning("Missing authorization token for method: {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing authorization token"));
        }

        // 2. Cắt chữ "Bearer " (Không phân biệt hoa thường)
        var token = authHeader;
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader.Substring("Bearer ".Length).Trim();
        }

        // LOG TOKEN SAU KHI CẮT
        _logger.LogInformation($"[AuthInterceptor] Token sent to validate: '{token}'");

        // 3. Validate
        var isValid = await _authService.ValidateTokenAsync(token);

        if (!isValid)
        {
            _logger.LogError($"[AuthInterceptor] Token validation failed for method: {context.Method}");
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired token"));
        }
    }
}