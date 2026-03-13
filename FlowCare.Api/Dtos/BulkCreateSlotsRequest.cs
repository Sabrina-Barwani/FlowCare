namespace FlowCare.Api.DTOs
{
    public class BulkCreateSlotsRequest
    {
        public int ServiceTypeId { get; set; }
        public int? StaffProfileId { get; set; }

        public DateTime StartDateUtc { get; set; }
        public DateTime EndDateUtc { get; set; }

        public TimeSpan DailyStartTime { get; set; }
        public TimeSpan DailyEndTime { get; set; }

        public int SlotDurationMinutes { get; set; }
    }
}