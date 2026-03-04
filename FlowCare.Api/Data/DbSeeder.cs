using FlowCare.Api.Entities;
using FlowCare.Api.Services;
using Microsoft.EntityFrameworkCore;
using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Data;

public static class DbSeeder
{
    public static async Task SeedDefaultAdminAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await db.Database.MigrateAsync();

        var adminExists = await db.Users.AnyAsync(u => u.Role == UserRole.Admin);
        if (adminExists) return;

        var username = config["DefaultAdmin:Username"] ?? "admin";
        var password = config["DefaultAdmin:Password"] ?? "Admin@12345";

        db.Users.Add(new User
        {
            Username = username,
            PasswordHash = hasher.Hash(password),
            Role = UserRole.Admin,
            IsActive = true,
            BranchId = null
        });

        // also ensure AppSetting single row exists
        if (!await db.AppSettings.AnyAsync())
        {
            db.AppSettings.Add(new AppSetting { SlotRetentionDays = 30 });
        }

        await db.SaveChangesAsync();
    }
}