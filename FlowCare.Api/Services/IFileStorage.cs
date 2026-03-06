namespace FlowCare.Api.Services;

public interface IFileStorage
{
    Task<(string relativePath, string contentType, long size)> SaveCustomerIdAsync(
        int customerProfileId,
        IFormFile file,
        CancellationToken ct = default);

    Task<(Stream stream, string contentType)> OpenReadAsync(
        string relativePath,
        CancellationToken ct = default);

    Task<(string relativePath, string contentType, long size)> SaveAppointmentAttachmentAsync(
    int appointmentId,
    IFormFile file,
    CancellationToken ct = default);
}
