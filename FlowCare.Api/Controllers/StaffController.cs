using System.Text.Json;
using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using FlowCare.Api.DTOs;
using FlowCare.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Controllers
{
    [ApiController]
    [Route("api/staff")]
    [Authorize]
    public class StaffController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;

        public StaffController(AppDbContext db, ICurrentUser current)
        {
            _db = db;
            _current = current;
        }

        // Staff can only view appointments assigned to their own slots.
        [HttpGet("appointments")]
        public async Task<IActionResult> GetAppointments(
            [FromQuery] DateOnly? date,
            [FromQuery] string? status,
            CancellationToken ct)
        {
            if (_current.UserId is null) return Unauthorized();

            if (_current.Role?.ToString() != "Staff")
                return Forbid();

            // Find current staff profile from current user
            var staffProfileId = await _db.StaffProfiles.AsNoTracking()
                .Where(s => s.UserId == _current.UserId.Value)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(ct);

            if (staffProfileId == 0)
                return NotFound("Staff profile not found.");

            var query = _db.Appointments.AsNoTracking()
                .Include(a => a.CustomerProfile)
                .Include(a => a.Slot)
                    .ThenInclude(s => s.Branch)
                .Include(a => a.Slot)
                    .ThenInclude(s => s.ServiceType)
                .Where(a => a.Slot.StaffProfileId == staffProfileId);

            // Optional date filter
            if (date.HasValue)
            {
                var dayStart = date.Value.ToDateTime(TimeOnly.MinValue);
                var dayEnd = date.Value.ToDateTime(TimeOnly.MaxValue);

                query = query.Where(a => a.Slot.StartTimeUtc >= dayStart && a.Slot.StartTimeUtc <= dayEnd);
            }

            // Optional status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
                {
                    query = query.Where(a => a.Status == parsedStatus);
                }
                else
                {
                    return BadRequest("Invalid status.");
                }
            }

            var result = await query
                .OrderBy(a => a.Slot.StartTimeUtc)
                .Select(a => new
                {
                    a.Id,
                    Status = a.Status.ToString(),
                    a.CustomerProfileId,
                    CustomerName = a.CustomerProfile.FullName,
                    a.SlotId,
                    a.Slot.StartTimeUtc,
                    a.Slot.EndTimeUtc,
                    a.Slot.BranchId,
                    BranchName = a.Slot.Branch.Name,
                    a.Slot.ServiceTypeId,
                    ServiceName = a.Slot.ServiceType.Name,
                    a.Notes
                })
                .ToListAsync(ct);

            return Ok(result);
        }

        // Staff updates status only for appointments assigned to them
        [HttpPatch("appointments/{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            [FromBody] UpdateAppointmentStatusRequest request,
            CancellationToken ct)
        {
            if (_current.UserId is null) return Unauthorized();

            if (_current.Role?.ToString() != "Staff")
                return Forbid();

            var staffProfileId = await _db.StaffProfiles.AsNoTracking()
                .Where(s => s.UserId == _current.UserId.Value)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(ct);

            if (staffProfileId == 0)
                return NotFound("Staff profile not found.");

            var appointment = await _db.Appointments
                .Include(a => a.Slot)
                .FirstOrDefaultAsync(a => a.Id == id && a.Slot.StaffProfileId == staffProfileId, ct);

            if (appointment is null)
                return NotFound("Appointment not found.");

            if (!Enum.TryParse<AppointmentStatus>(request.Status, true, out var parsedStatus))
                return BadRequest("Invalid status.");

            // Allowed staff status updates only
            if (parsedStatus != AppointmentStatus.CheckedIn &&
                parsedStatus != AppointmentStatus.NoShow &&
                parsedStatus != AppointmentStatus.Completed)
            {
                return BadRequest("Staff can only set CheckedIn, NoShow, or Completed.");
            }

            appointment.Status = parsedStatus;

            _db.AuditLogs.Add(new AuditLog
            {
                ActionType = AuditActionType.AppointmentStatusUpdated,
                ActorUserId = _current.UserId.Value,
                ActorRole = _current.Role!.Value,
                BranchId = appointment.Slot.BranchId,
                TargetEntityType = "Appointment",
                TargetEntityId = appointment.Id.ToString(),
                MetadataJson = JsonSerializer.Serialize(new
                {
                    newStatus = parsedStatus.ToString(),
                    slotId = appointment.SlotId
                })
            });

            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                message = "Appointment status updated successfully.",
                appointment.Id,
                Status = appointment.Status.ToString()
            });
        }

        // Optional: staff notes
        [HttpPatch("appointments/{id:int}/notes")]
        public async Task<IActionResult> UpdateNotes(
            int id,
            [FromBody] UpdateAppointmentNotesRequest request,
            CancellationToken ct)
        {
            if (_current.UserId is null) return Unauthorized();

            if (_current.Role?.ToString() != "Staff")
                return Forbid();

            var staffProfileId = await _db.StaffProfiles.AsNoTracking()
                .Where(s => s.UserId == _current.UserId.Value)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(ct);

            if (staffProfileId == 0)
                return NotFound("Staff profile not found.");

            var appointment = await _db.Appointments
                .Include(a => a.Slot)
                .FirstOrDefaultAsync(a => a.Id == id && a.Slot.StaffProfileId == staffProfileId, ct);

            if (appointment is null)
                return NotFound("Appointment not found.");

            appointment.Notes = request.Notes;

            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                message = "Appointment notes updated successfully.",
                appointment.Id,
                appointment.Notes
            });
        }
    }
}