using RemoteFileManager.Core.Entities;

namespace RemoteFileManager.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        var seedTime = new DateTime(2025, 11, 19, 0, 0, 0, DateTimeKind.Utc);
        // Check if already seeded
        if (context.Users.Any())
        {
            return;
        }

        // Seed users
        var users = new[]
        {
            new User
            {
                Username = "admin",
                PasswordHash = "$2a$11$6EDUvpASVOlVOaNs5sv7v.hJ9ZM.p0r2kyTQgdh6uG1YdmW/jPnWu",
                Role = "Admin",
                CreatedAt = seedTime,
                IsActive = true
            },
            new User
            {
                Username = "user",
                PasswordHash = "$2a$11$kh6v8SmsndypyKP/a9m2pONT9TXHe512YYXOn7VOHwf8TUumT.sMe",
                Role = "User",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new User
            {
                Username = "testuser",
                PasswordHash = "$2a$11$9cGNOUhzjStd2A/0NB8LLezFOshiqr5IJQqGtI94vBUVBwC0RxZ/m",
                Role = "User",
                CreatedAt = seedTime,
                IsActive = true
            }
        };

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
    }
}