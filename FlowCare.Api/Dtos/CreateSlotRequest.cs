namespace FlowCare.Api.DTOs
{
    public class CreateSlotRequest
    {
        public int ServiceTypeId { get; set; }
        public int? StaffProfileId { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
    }
}