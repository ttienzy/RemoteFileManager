using Grpc.Net.Client;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using RemoteFileManager.Client.Services.GrpcServices;

namespace RemoteFileManager.Client.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGrpcClients(this IServiceCollection services)
    {
        // 1. Đăng ký GrpcChannel (Singleton - Dùng chung cho toàn App)
        services.AddSingleton(sp =>
        {
            var options = new GrpcChannelOptions
            {
                // Cấu hình dung lượng file tối đa (100MB)
                MaxReceiveMessageSize = 100 * 1024 * 1024,
                MaxSendMessageSize = 100 * 1024 * 1024,

                // Http thường (không SSL)
                Credentials = ChannelCredentials.Insecure
            };

            // Trả về channel đã cấu hình
            return GrpcChannel.ForAddress("http://localhost:5000", options);
        });

        services.AddSingleton<AuthGrpcClient>();
        services.AddSingleton<FileManagerGrpcClient>();
        services.AddSingleton<StreamingGrpcClient>();

        return services;
    }
}