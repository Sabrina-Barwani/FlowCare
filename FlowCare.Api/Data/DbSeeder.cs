using FlowCare.Api.Entities;
using FlowCare.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await db.Database.MigrateAsync();

        await SeedBranchesAsync(db);
        await SeedServiceTypesAsync(db);
        await SeedDefaultAdminAsync(db, hasher, config);
        await SeedAppSettingsAsync(db);
        await SeedTestUsersAsync(db, hasher);
        await SeedSlotsAsync(db);

        await db.SaveChangesAsync();
    }

    // at least 2 branches so we can test branch-scoped rules.

    private static async Task SeedBranchesAsync(AppDbContext db)
    {
        if (await db.Branches.AnyAsync())
            return;

        db.Branches.AddRange(
            new Branch { Name = "FlowCare Muscat", City = "Muscat" },
            new Branch { Name = "FlowCare Sohar", City = "Sohar" }
        );
        await db.SaveChangesAsync();
    }

    
    /// Creates default Admin if no Admin exists (idempotent).
   
   
    private static async Task SeedDefaultAdminAsync(AppDbContext db, IPasswordHasher hasher, IConfiguration config)
    {
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
        await db.SaveChangesAsync();
    }

   
    private static async Task SeedAppSettingsAsync(AppDbContext db)
    {
        if (await db.AppSettings.AnyAsync())
            return;

        db.AppSettings.Add(new AppSetting { SlotRetentionDays = 30 });
        await db.SaveChangesAsync();
    }

    //This is idempotent.
    private static async Task SeedTestUsersAsync(AppDbContext db, IPasswordHasher hasher)
    {
        var branches = await db.Branches.OrderBy(b => b.Id).ToListAsync();
        if (branches.Count < 2) return;

        var branch1Id = branches[0].Id;
        var branch2Id = branches[1].Id;

        //Branch 1
        await CreateUserIfMissingAsync(db, hasher, "manager1", "Manager@123", UserRole.BranchManager, branch1Id);
        await CreateUserIfMissingAsync(db, hasher, "staff1", "Staff@123", UserRole.Staff, branch1Id, "Test Staff 1");
        await CreateUserIfMissingAsync(db, hasher, "staff2", "Staff@123", UserRole.Staff, branch1Id, "Test Staff 2");

        //Branch 2 
        await CreateUserIfMissingAsync(db, hasher, "manager2", "Manager@123", UserRole.BranchManager, branch2Id);
        await CreateUserIfMissingAsync(db, hasher, "staff3", "Staff@123", UserRole.Staff, branch2Id, "Test Staff 3");
        await CreateUserIfMissingAsync(db, hasher, "staff4", "Staff@123", UserRole.Staff, branch2Id, "Test Staff 4");

        //Customers
        await CreateCustomerIfMissingAsync(db, hasher, "cust1", "Cust@123", "Test Customer 1", "95327499");
        await CreateCustomerIfMissingAsync(db, hasher, "cust2", "Cust@123", "Test Customer 2", "88888888");
    }

    private static async Task CreateUserIfMissingAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        string username,
        string password,
        UserRole role,
        int? branchId,
        string? staffFullName = null)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
        {
            user = new User
            {
                Username = username,
                PasswordHash = hasher.Hash(password),
                Role = role,
                BranchId = branchId,
                IsActive = true
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        // If this user is staff ensure StaffProfile exists
        if (role == UserRole.Staff)
        {
            var profileExists = await db.StaffProfiles.AnyAsync(s => s.UserId == user.Id);
            if (!profileExists)
            {
                db.StaffProfiles.Add(new StaffProfile
                {
                    UserId = user.Id,
                    FullName = staffFullName ?? username
                });

                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task CreateCustomerIfMissingAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        string username,
        string password,
        string fullName,
        string phone)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
        {
            user = new User
            {
                Username = username,
                PasswordHash = hasher.Hash(password),
                Role = UserRole.Customer,
                IsActive = true
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var customerExists = await db.CustomerProfiles.AnyAsync(c => c.UserId == user.Id);
        if (!customerExists)
        {
            db.CustomerProfiles.Add(new CustomerProfile
            {
                UserId = user.Id,
                FullName = fullName,
                Phone = phone
            });

            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedServiceTypesAsync(AppDbContext db)
    {
        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        if (branches.Count == 0) return;

        foreach (var branch in branches)
        {
            var hasAny = await db.ServiceTypes.AnyAsync(s => s.BranchId == branch.Id);
            if (hasAny) continue;

            db.ServiceTypes.AddRange(
                new ServiceType { BranchId = branch.Id, Name = "General Inquiry" },
                new ServiceType { BranchId = branch.Id, Name = "Appointment Booking" },
                new ServiceType { BranchId = branch.Id, Name = "Customer Support" }
            );
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSlotsAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        if (branches.Count == 0) return;

        foreach (var branch in branches)
        {
            var services = await db.ServiceTypes.AsNoTracking()
                .Where(s => s.BranchId == branch.Id)
                .OrderBy(s => s.Id)
                .ToListAsync();

            if (services.Count == 0) continue;

            var hasFutureSlots = await db.Slots.AnyAsync(s =>
                s.BranchId == branch.Id &&
                s.StartTimeUtc > now &&
                s.DeletedAtUtc == null);

            if (hasFutureSlots) continue;

            var staffIds = await db.StaffProfiles
                .Where(sp => sp.User.BranchId == branch.Id)
                .OrderBy(sp => sp.Id)
                .Select(sp => sp.Id)
                .ToListAsync();

            var serviceTypeId = services[0].Id;

            for (int day = 1; day <= 5; day++)
            {
                var date = now.Date.AddDays(day);

                db.Slots.Add(new Slot
                {
                    BranchId = branch.Id,
                    ServiceTypeId = serviceTypeId,
                    StaffProfileId = staffIds.Count > 0 ? staffIds[0] : null,
                    StartTimeUtc = date.AddHours(9),
                    EndTimeUtc = date.AddHours(9).AddMinutes(30),
                    DeletedAtUtc = null
                });

                db.Slots.Add(new Slot
                {
                    BranchId = branch.Id,
                    ServiceTypeId = serviceTypeId,
                    StaffProfileId = staffIds.Count > 1 ? staffIds[1] : staffIds.FirstOrDefault(),
                    StartTimeUtc = date.AddHours(10),
                    EndTimeUtc = date.AddHours(10).AddMinutes(30),
                    DeletedAtUtc = null
                });
            }
        }

        await db.SaveChangesAsync();
    }
}