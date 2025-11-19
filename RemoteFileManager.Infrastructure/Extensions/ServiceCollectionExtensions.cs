using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RemoteFileManager.Core.Interfaces.Repositories;
using RemoteFileManager.Core.Interfaces.Services;
using RemoteFileManager.Infrastructure.Persistence;
using RemoteFileManager.Infrastructure.Repositories;
using RemoteFileManager.Infrastructure.Services;
using RemoteFileManager.Application.Services;

namespace RemoteFileManager.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFileMetadataRepository, FileMetadataRepository>();
        services.AddScoped<IFileShareRepository, FileShareRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Infrastructure Services
        services.AddScoped<IFileSystemService, FileSystemService>();
        services.AddScoped<IChunkingService, ChunkingService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        return services;
    }

    public static void EnsureDatabaseCreated(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
    }
}