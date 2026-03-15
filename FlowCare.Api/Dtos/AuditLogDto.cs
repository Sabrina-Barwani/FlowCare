namespace FlowCare.Api.DTOs
{
    public class AuditLogDto
    {
        public long Id { get; set; }
        public string ActionType { get; set; } = default!;
        public int ActorUserId { get; set; }
        public string ActorRole { get; set; } = default!;
        public int? BranchId { get; set; }
        public string TargetEntityType { get; set; } = default!;
        public string? TargetEntityId { get; set; }
        public DateTime Timestamp { get; set; }
        public string? MetadataJson { get; set; }
    }
}