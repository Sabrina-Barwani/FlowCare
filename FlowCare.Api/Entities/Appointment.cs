using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Entities
{
    public class Appointment
    {
        public int Id { get; set; }

        public int SlotId { get; set; }
        public int CustomerProfileId { get; set; }

        // assigned staff snapshot (from slot)
        public int? AssignedStaffProfileId { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CancelledAtUtc { get; set; }

        // attachment
        public string? AttachmentPath { get; set; }
        public string? AttachmentContentType { get; set; }
        public long? AttachmentSize { get; set; }

        public Slot Slot { get; set; } = default!;
        public CustomerProfile CustomerProfile { get; set; } = default!;
        public StaffProfile? AssignedStaffProfile { get; set; }

        public string? Notes { get; set; }
    }
}
