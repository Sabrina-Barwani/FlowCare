using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;

        // Branch-scoped for Manager/Staff.
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public StaffProfile? StaffProfile { get; set; }
        public CustomerProfile? CustomerProfile { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
