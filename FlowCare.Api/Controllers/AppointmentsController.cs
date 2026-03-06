using System.Text.Json;
using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using FlowCare.Api.DTOs;
using FlowCare.Api.Entities;
using FlowCare.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentsController : ControllerBase
    {
        private const long MaxAttachmentBytes = 5 * 1024 * 1024; // 5MB

        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IFileStorage _storage;

        public AppointmentsController(AppDbContext db, ICurrentUser current, IFileStorage storage)
        {
            _db = db;
            _current = current;
            _storage = storage;
        }

        // Customer books one available slot only.
        [HttpPost("book")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Book([FromForm] BookAppointmentRequest request, CancellationToken ct)
        {
            if (_current.UserId is null) return Unauthorized();

            // Only customers can book
            if (_current.Role?.ToString() != "Customer")
                return Forbid();

            // Find current customer's profile
            var customerProfile = await _db.CustomerProfiles
                .FirstOrDefaultAsync(c => c.UserId == _current.UserId.Value, ct);

            if (customerProfile is null)
                return BadRequest("Customer profile not found.");

            // Validate optional attachment
            if (request.Attachment is not null)
            {
                if (request.Attachment.Length > MaxAttachmentBytes)
                    return BadRequest("Attachment is too large. Max 5MB.");

                var contentType = request.Attachment.ContentType ?? "";
                var allowed = contentType.StartsWith("image/") || contentType == "application/pdf";

                if (!allowed)
                    return BadRequest("Attachment must be image/* or application/pdf.");
            }

            // Start transaction
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            try
            {
                var now = DateTime.UtcNow;

                // Slot must exist, not deleted, and in future
                var slot = await _db.Slots
                    .Include(s => s.Branch)
                    .Include(s => s.ServiceType)
                    .FirstOrDefaultAsync(s => s.Id == request.SlotId, ct);

                if (slot is null)
                    return NotFound("Slot not found.");

                if (slot.DeletedAtUtc != null)
                    return BadRequest("Slot is deleted.");

                if (slot.StartTimeUtc <= now)
                    return BadRequest("Slot is not in the future.");

                // Check not already booked
                var alreadyBooked = await _db.Appointments
                    .AnyAsync(a => a.SlotId == request.SlotId, ct);

                if (alreadyBooked)
                    return Conflict("Slot is already booked.");

                // Create appointment
                var appointment = new Appointment
                {
                    SlotId = slot.Id,
                    CustomerProfileId = customerProfile.Id,
                    Status = AppointmentStatus.Booked,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.Appointments.Add(appointment);
                await _db.SaveChangesAsync(ct);

                // Optional attachment save
                if (request.Attachment is not null)
                {
                    var (path, contentType, size) =
                        await _storage.SaveAppointmentAttachmentAsync(appointment.Id, request.Attachment, ct);

                    appointment.AttachmentPath = path;
                    appointment.AttachmentContentType = contentType;
                    appointment.AttachmentSize = size;

                    await _db.SaveChangesAsync(ct);
                }

                // Audit log
                var metadata = JsonSerializer.Serialize(new
                {
                    slotId = slot.Id,
                    serviceTypeId = slot.ServiceTypeId
                });

                _db.AuditLogs.Add(new AuditLog
                {
                    ActionType = AuditActionType.AppointmentCreated,
                    ActorUserId = _current.UserId.Value,
                    ActorRole = _current.Role!.Value,
                    BranchId = slot.BranchId,
                    TargetEntityType = "Appointment",
                    TargetEntityId = appointment.Id.ToString(),
                    MetadataJson = metadata
                });

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return Ok(new
                {
                    appointment.Id,
                    appointment.SlotId,
                    appointment.CustomerProfileId,
                    appointment.Status,
                    appointment.CreatedAtUtc,
                    appointment.AttachmentPath,
                    appointment.AttachmentContentType,
                    appointment.AttachmentSize
                });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct);

                // Handles race condition from unique index on SlotId
                return Conflict("Slot is already booked.");
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }
}