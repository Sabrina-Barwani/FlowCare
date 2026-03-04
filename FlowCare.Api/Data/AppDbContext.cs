using Microsoft.EntityFrameworkCore;
using FlowCare.Api.Entities;
namespace FlowCare.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
        public DbSet<User> Users => Set<User>();
        public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
        public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
        public DbSet<StaffServiceType> StaffServiceTypes => Set<StaffServiceType>();
        public DbSet<Slot> Slots => Set<Slot>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<AppSetting> AppSettings => Set<AppSetting>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User unique username
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Branch-ServiceType (1..*)
            modelBuilder.Entity<ServiceType>()
                .HasOne(st => st.Branch)
                .WithMany(b => b.ServiceTypes)
                .HasForeignKey(st => st.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            // Branch-User (1..*)
            modelBuilder.Entity<User>()
                .HasOne(u => u.Branch)
                .WithMany(b => b.Users)
                .HasForeignKey(u => u.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // User - StaffProfile (1..1)
            modelBuilder.Entity<StaffProfile>()
                .HasOne(sp => sp.User)
                .WithOne(u => u.StaffProfile)
                .HasForeignKey<StaffProfile>(sp => sp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // User - CustomerProfile (1..1)
            modelBuilder.Entity<CustomerProfile>()
                .HasOne(cp => cp.User)
                .WithOne(u => u.CustomerProfile)
                .HasForeignKey<CustomerProfile>(cp => cp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Many-to-Many via StaffServiceType
            modelBuilder.Entity<StaffServiceType>()
                .HasKey(x => new { x.StaffProfileId, x.ServiceTypeId });

            modelBuilder.Entity<StaffServiceType>()
                .HasOne(x => x.StaffProfile)
                .WithMany(s => s.StaffServiceTypes)
                .HasForeignKey(x => x.StaffProfileId);

            modelBuilder.Entity<StaffServiceType>()
                .HasOne(x => x.ServiceType)
                .WithMany(st => st.StaffServiceTypes)
                .HasForeignKey(x => x.ServiceTypeId);

            // Slot relations
            modelBuilder.Entity<Slot>()
                .HasOne(s => s.Branch)
                .WithMany(b => b.Slots)
                .HasForeignKey(s => s.BranchId);

            modelBuilder.Entity<Slot>()
                .HasOne(s => s.ServiceType)
                .WithMany(st => st.Slots)
                .HasForeignKey(s => s.ServiceTypeId);

            modelBuilder.Entity<Slot>()
                .HasOne(s => s.StaffProfile)
                .WithMany(sp => sp.Slots)
                .HasForeignKey(s => s.StaffProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            // Slot indexes (required)
            modelBuilder.Entity<Slot>().HasIndex(s => s.BranchId);
            modelBuilder.Entity<Slot>().HasIndex(s => s.ServiceTypeId);
            modelBuilder.Entity<Slot>().HasIndex(s => s.StartTimeUtc);
            modelBuilder.Entity<Slot>().HasIndex(s => s.DeletedAtUtc);

            // Appointment relations + Unique SlotId
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Slot)
                .WithOne(s => s.Appointment)
                .HasForeignKey<Appointment>(a => a.SlotId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Appointment>()
                .HasIndex(a => a.SlotId)
                .IsUnique();

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.CustomerProfile)
                .WithMany(c => c.Appointments)
                .HasForeignKey(a => a.CustomerProfileId);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.AssignedStaffProfile)
                .WithMany(s => s.AssignedAppointments)
                .HasForeignKey(a => a.AssignedStaffProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            // AppSetting: keep single row (unique index)
            modelBuilder.Entity<AppSetting>()
                .HasIndex(x => x.Id)
                .IsUnique();
        }
    }
}