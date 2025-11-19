using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RemoteFileManager.Core.Entities;

namespace RemoteFileManager.Infrastructure.Persistence.Configurations;

public class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadata>
{
    public void Configure(EntityTypeBuilder<FileMetadata> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(f => f.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(f => f.FilePath);

        builder.Property(f => f.FileSize)
            .IsRequired();

        builder.Property(f => f.Extension)
            .HasMaxLength(50);

        builder.Property(f => f.UploadedAt)
            .IsRequired();

        builder.Property(f => f.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(f => f.UploadedBy)
            .WithMany(u => u.UploadedFiles)
            .HasForeignKey(f => f.UploadedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.Shares)
            .WithOne(s => s.File)
            .HasForeignKey(s => s.FileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}