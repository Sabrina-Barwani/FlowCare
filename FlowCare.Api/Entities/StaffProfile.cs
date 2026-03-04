namespace FlowCare.Api.Entities
{
    public class StaffProfile
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = default!;

        public User User { get; set; } = default!;
        public ICollection<StaffServiceType> StaffServiceTypes { get; set; } = new List<StaffServiceType>();
        public ICollection<Slot> Slots { get; set; } = new List<Slot>();
        public ICollection<Appointment> AssignedAppointments { get; set; } = new List<Appointment>();
    }
}
