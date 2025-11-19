using Microsoft.EntityFrameworkCore;
using RemoteFileManager.Core.Entities;
using RemoteFileManager.Infrastructure.Persistence.Configurations;
using System.Collections.Generic;
using System.Reflection.Emit;
using FileShare = RemoteFileManager.Core.Entities.FileShare;

namespace RemoteFileManager.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<FileMetadata> FileMetadata { get; set; }
    public DbSet<FileShare> FileShares { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new FileMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new FileShareConfiguration());

        // Seed data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        var seedTime = new DateTime(2025, 11, 19, 0, 0, 0, DateTimeKind.Utc);

        // Password: "admin123" - hashed with BCrypt
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = "$2a$11$6EDUvpASVOlVOaNs5sv7v.hJ9ZM.p0r2kyTQgdh6uG1YdmW/jPnWu",
                Role = "Admin",
                CreatedAt = seedTime, 
                IsActive = true
            },
            new User
            {
                Id = 2,
                Username = "user",
                PasswordHash = "$2a$11$kh6v8SmsndypyKP/a9m2pONT9TXHe512YYXOn7VOHwf8TUumT.sMe",
                Role = "User",
                CreatedAt = seedTime, 
                IsActive = true
            }
        );
    }

}