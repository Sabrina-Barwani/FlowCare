using FlowCare.Api.Entities;
using FlowCare.Api.Services;
using Microsoft.EntityFrameworkCore;
namespace FlowCare.Api.Data;

using static FlowCare.Api.Entities.Enums;
using FlowCare.Api.Services;
using Microsoft.Extensions.DependencyInjection;
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
        await SeedSlotsAsync(db);
        await SeedDefaultAdminAsync(db, hasher, config);
        await SeedAppSettingsAsync(db);
        await SeedTestUsersAsync(db, hasher);

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
    }

   
    private static async Task SeedAppSettingsAsync(AppDbContext db)
    {
        if (await db.AppSettings.AnyAsync())
            return;

        db.AppSettings.Add(new AppSetting { SlotRetentionDays = 30 });
    }

    //This is idempotent.
    
    private static async Task SeedTestUsersAsync(AppDbContext db, IPasswordHasher hasher)
    {
        // Need branches to exist to link manager/staff
        var branches = await db.Branches.OrderBy(b => b.Id).ToListAsync();
        if (branches.Count < 2) return;

        var branch1Id = branches[0].Id;
        var branch2Id = branches[1].Id;

        // Branch Manager for branch 1
        if (!await db.Users.AnyAsync(u => u.Username == "manager1"))
        {
            db.Users.Add(new User
            {
                Username = "manager1",
                PasswordHash = hasher.Hash("Manager@123"),
                Role = UserRole.BranchManager,
                BranchId = branch1Id,
                IsActive = true
            });
        }

        // Staff for branch 1
        if (!await db.Users.AnyAsync(u => u.Username == "staff1"))
        {
            // Create staff user
            var staffUser = new User
            {
                Username = "staff1",
                PasswordHash = hasher.Hash("Staff@123"),
                Role = UserRole.Staff,
                BranchId = branch1Id,
                IsActive = true
            };
            db.Users.Add(staffUser);

            // Create staff profile 
            db.StaffProfiles.Add(new StaffProfile
            {
                User = staffUser,
                FullName = "Test Staff 1"
            });
        }

        // Customer 1
        if (!await db.Users.AnyAsync(u => u.Username == "cust1"))
        {
            var custUser = new User
            {
                Username = "cust1",
                PasswordHash = hasher.Hash("Cust@123"),
                Role = UserRole.Customer,
                IsActive = true
            };
            db.Users.Add(custUser);

            db.CustomerProfiles.Add(new CustomerProfile
            {
                User = custUser,
                FullName = "Test Customer 1",
                Phone = "95327499"
            });
        }

        // Customer 2 (for testing "customer cannot access other customer")
        if (!await db.Users.AnyAsync(u => u.Username == "cust2"))
        {
            var custUser2 = new User
            {
                Username = "cust2",
                PasswordHash = hasher.Hash("Cust@123"),
                Role = UserRole.Customer,
                IsActive = true
            };
            db.Users.Add(custUser2);

            db.CustomerProfiles.Add(new CustomerProfile
            {
                User = custUser2,
                FullName = "Test Customer 2",
                Phone = "88888888"
            });
        }

        //  Staff in branch 2 to test cross branch behavior later
        if (!await db.Users.AnyAsync(u => u.Username == "staff2"))
        {
            var staffUser2 = new User
            {
                Username = "staff2",
                PasswordHash = hasher.Hash("Staff@123"),
                Role = UserRole.Staff,
                BranchId = branch2Id,
                IsActive = true
            };
            db.Users.Add(staffUser2);

            db.StaffProfiles.Add(new StaffProfile
            {
                User = staffUser2,
                FullName = "Test Staff 2"
            });
        }
    }

    private static async Task SeedServiceTypesAsync(AppDbContext db)
    {
        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        if (branches.Count == 0) return;

        foreach (var branch in branches)
        {
            // إذا عند هذا الفرع خدمات، لا نكرر
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

            //unique slots as per branch service type and time
            var hasFutureSlots = await db.Slots.AnyAsync(s =>
                s.BranchId == branch.Id &&
                s.StartTimeUtc > now &&
                s.DeletedAtUtc == null);

            if (hasFutureSlots) continue;

            var serviceTypeId = services[0].Id; 

            for (int day = 1; day <= 5; day++)
            {
                var date = now.Date.AddDays(day);

                db.Slots.Add(new Slot
                {
                    BranchId = branch.Id,
                    ServiceTypeId = serviceTypeId,
                    StaffProfileId = null,
                    StartTimeUtc = date.AddHours(9),
                    EndTimeUtc = date.AddHours(9).AddMinutes(30),
                    DeletedAtUtc = null
                });

                db.Slots.Add(new Slot
                {
                    BranchId = branch.Id,
                    ServiceTypeId = serviceTypeId,
                    StaffProfileId = null,
                    StartTimeUtc = date.AddHours(10),
                    EndTimeUtc = date.AddHours(10).AddMinutes(30),
                    DeletedAtUtc = null
                });
            }
        }

        await db.SaveChangesAsync();
    }

}