using Mapster;
using RemoteFileManager.Application.DTOs;
using RemoteFileManager.Core.Entities;

namespace RemoteFileManager.Application.Mappings;

public static class MappingConfig
{
    public static void RegisterMappings()
    {
        TypeAdapterConfig<User, UserDto>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Username, src => src.Username)
            .Map(dest => dest.Role, src => src.Role)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.LastLoginAt, src => src.LastLoginAt);

        TypeAdapterConfig<FileMetadata, FileInfoDto>.NewConfig()
            .Map(dest => dest.Name, src => src.FileName)
            .Map(dest => dest.FullPath, src => src.FilePath)
            .Map(dest => dest.Size, src => src.FileSize)
            .Map(dest => dest.CreatedDate, src => src.UploadedAt)
            .Map(dest => dest.ModifiedDate, src => src.ModifiedAt ?? src.UploadedAt)
            .Map(dest => dest.Extension, src => src.Extension)
            .Map(dest => dest.IsDirectory, src => false);
    }
}