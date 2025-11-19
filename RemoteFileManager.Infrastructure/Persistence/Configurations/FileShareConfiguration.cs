using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RemoteFileManager.Core.Entities;
using FileShare = RemoteFileManager.Core.Entities.FileShare;

namespace RemoteFileManager.Infrastructure.Persistence.Configurations;

public class FileShareConfiguration : IEntityTypeConfiguration<FileShare>
{
    public void Configure(EntityTypeBuilder<FileShare> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Permission)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("Read");

        builder.Property(s => s.SharedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(s => s.File)
            .WithMany(f => f.Shares)
            .HasForeignKey(s => s.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.SharedWith)
            .WithMany(u => u.SharedFiles)
            .HasForeignKey(s => s.SharedWithUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint
        builder.HasIndex(s => new { s.FileId, s.SharedWithUserId })
            .IsUnique();
    }
}