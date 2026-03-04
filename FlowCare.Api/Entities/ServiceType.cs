namespace FlowCare.Api.Entities
{
    public class ServiceType
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = default!;
        public int DurationMinutes { get; set; } = 15;

        public Branch Branch { get; set; } = default!;
        public ICollection<Slot> Slots { get; set; } = new List<Slot>();
        public ICollection<StaffServiceType> StaffServiceTypes { get; set; } = new List<StaffServiceType>();
    }
}
