namespace FlowCare.Api.Entities
{
    public class CustomerProfile
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string FullName { get; set; } = default!;
        public string? Phone { get; set; }

        // ID Image (Required later)
        public string? IdImagePath { get; set; }
        public string? IdImageContentType { get; set; }
        public long? IdImageSize { get; set; }

        public User User { get; set; } = default!;
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
