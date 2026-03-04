namespace FlowCare.Api.Entities
{
    public class AppSetting
    {
        public int Id { get; set; }
        public int SlotRetentionDays { get; set; } = 30;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
