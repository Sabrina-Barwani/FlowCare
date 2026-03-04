namespace FlowCare.Api.Entities
{
    public class Slot
    {
        public int Id { get; set; }

        public int BranchId { get; set; }
        public int ServiceTypeId { get; set; }

        // optional staff-specific slot
        public int? StaffProfileId { get; set; }

        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }

        // Soft delete
        public DateTime? DeletedAtUtc { get; set; }

        public Branch Branch { get; set; } = default!;
        public ServiceType ServiceType { get; set; } = default!;
        public StaffProfile? StaffProfile { get; set; }

        public Appointment? Appointment { get; set; }
    }
}
