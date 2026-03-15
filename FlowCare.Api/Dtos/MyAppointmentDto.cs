namespace FlowCare.Api.Dtos;

public class MyAppointmentDto
{
    public int Id { get; set; }
    public string Status { get; set; } = default!;

    public int? SlotId { get; set; }
    public DateTime? StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }

    public int BranchId { get; set; }
    public string BranchName { get; set; } = default!;

    public int ServiceTypeId { get; set; }
    public string ServiceTypeName { get; set; } = default!;

    // attachment metadata only 
    public string? AttachmentPath { get; set; }
    public string? AttachmentContentType { get; set; }
    public long? AttachmentSize { get; set; }
}