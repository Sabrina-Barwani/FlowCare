using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Entities
{
    public class AuditLog
    {
        public long Id { get; set; }

        public AuditActionType ActionType { get; set; }

        public int ActorUserId { get; set; }
        public UserRole ActorRole { get; set; }

        public int? BranchId { get; set; }

        public string TargetEntityType { get; set; } = default!;
        public string TargetEntityId { get; set; } = default!; 

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        //  metadata json
        public string? MetadataJson { get; set; }
    }
}
