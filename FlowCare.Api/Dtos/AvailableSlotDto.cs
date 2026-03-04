namespace FlowCare.Api.Dtos;

public record AvailableSlotDto(
    int Id,
    int BranchId,
    int ServiceTypeId,
    int? StaffProfileId,
    DateTime StartTime,
    DateTime EndTime
    
);
