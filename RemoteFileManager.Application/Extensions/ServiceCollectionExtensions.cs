using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RemoteFileManager.Application.Mappings;
using RemoteFileManager.Application.Services;
using RemoteFileManager.Application.Validators;

namespace RemoteFileManager.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register services
        services.AddScoped<FileManagementService>();
        services.AddScoped<FileStreamingService>();
        services.AddScoped<UserService>();

        // Register validators
        services.AddValidatorsFromAssemblyContaining<LoginValidator>();

        // Configure Mapster
        MappingConfig.RegisterMappings();

        return services;
    }
}