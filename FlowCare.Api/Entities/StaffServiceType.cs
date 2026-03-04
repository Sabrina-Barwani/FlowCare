namespace FlowCare.Api.Entities
{
    public class StaffServiceType
    {
        public int StaffProfileId { get; set; }
        public int ServiceTypeId { get; set; }

        public StaffProfile StaffProfile { get; set; } = default!;
        public ServiceType ServiceType { get; set; } = default!;
    }
}
