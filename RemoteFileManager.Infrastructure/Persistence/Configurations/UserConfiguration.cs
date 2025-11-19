using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RemoteFileManager.Core.Entities;

namespace RemoteFileManager.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("User");

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Relationships
        builder.HasMany(u => u.UploadedFiles)
            .WithOne(f => f.UploadedBy)
            .HasForeignKey(f => f.UploadedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.SharedFiles)
            .WithOne(s => s.SharedWith)
            .HasForeignKey(s => s.SharedWithUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}