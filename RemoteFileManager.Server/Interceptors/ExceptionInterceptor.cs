using Grpc.Core;
using Grpc.Core.Interceptors;
using RemoteFileManager.Core.Exceptions;
using FileNotFoundException = RemoteFileManager.Core.Exceptions.FileNotFoundException;

namespace RemoteFileManager.Server.Interceptors;

public class ExceptionInterceptor : Interceptor
{
    private readonly ILogger<ExceptionInterceptor> _logger;

    public ExceptionInterceptor(ILogger<ExceptionInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in gRPC call: {Method}", context.Method);
            throw HandleException(ex);
        }
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(requestStream, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in client streaming gRPC call: {Method}", context.Method);
            throw HandleException(ex);
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in server streaming gRPC call: {Method}", context.Method);
            throw HandleException(ex);
        }
    }

    private RpcException HandleException(Exception exception)
    {
        return exception switch
        {
            UnauthorizedException => new RpcException(new Status(StatusCode.Unauthenticated, exception.Message)),
            FileNotFoundException => new RpcException(new Status(StatusCode.NotFound, exception.Message)),
            ValidationException => new RpcException(new Status(StatusCode.InvalidArgument, exception.Message)),
            InsufficientStorageException => new RpcException(new Status(StatusCode.ResourceExhausted, exception.Message)),
            _ => new RpcException(new Status(StatusCode.Internal, "An internal error occurred"))
        };
    }
}