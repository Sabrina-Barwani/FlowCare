namespace FlowCare.Api.Entities
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string City { get; set; } = default!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ServiceType> ServiceTypes { get; set; } = new List<ServiceType>();
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Slot> Slots { get; set; } = new List<Slot>();
    }
}
