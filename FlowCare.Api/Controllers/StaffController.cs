
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
        // Added pagination + optional search by customer/service/branch/notes/status
        [HttpGet("appointments")]
        public async Task<IActionResult> GetAppointments(
            [FromQuery] DateOnly? date,
            [FromQuery] string? status,
            [FromQuery] string? term,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            CancellationToken ct = default)
        {
            if (_current.UserId is null) return Unauthorized();
            if (_current.Role?.ToString() != "Staff")
                return Forbid();
            if (page <= 0) page = 1;
            if (size <= 0) size = 10;
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
                .Where(a => a.Slot != null && a.Slot.StaffProfileId == staffProfileId);
            // Optional date filter
            if (date.HasValue)
            {
                var dayStart = date.Value.ToDateTime(TimeOnly.MinValue);
                var dayEnd = date.Value.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(a =>
                    a.Slot != null &&
                    a.Slot.StartTimeUtc >= dayStart &&
                    a.Slot.StartTimeUtc <= dayEnd);
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
            // Search before pagination
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim().ToLower();
                query = query.Where(a =>
                    a.CustomerProfile.FullName.ToLower().Contains(term) ||
                    a.Slot!.Branch.Name.ToLower().Contains(term) ||
                    a.Slot.ServiceType.Name.ToLower().Contains(term) ||
                    a.Status.ToString().ToLower().Contains(term) ||
                    (a.Notes != null && a.Notes.ToLower().Contains(term)));
            }
            var total = await query.CountAsync(ct);
            var results = await query
                .OrderBy(a => a.Slot!.StartTimeUtc)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(a => new
                {
                    a.Id,
                    Status = a.Status.ToString(),
                    a.CustomerProfileId,
                    CustomerName = a.CustomerProfile.FullName,
                    a.SlotId,
                    StartTimeUtc = a.Slot != null ? a.Slot.StartTimeUtc : (DateTime?)null,
                    EndTimeUtc = a.Slot != null ? a.Slot.EndTimeUtc : (DateTime?)null,
                    BranchId = a.Slot != null ? a.Slot.BranchId : (int?)null,
                    BranchName = a.Slot != null ? a.Slot.Branch.Name : null,
                    ServiceTypeId = a.Slot != null ? a.Slot.ServiceTypeId : (int?)null,
                    ServiceName = a.Slot != null ? a.Slot.ServiceType.Name : null,
                    a.Notes
                })
                .ToListAsync(ct);
            return Ok(new
            {
                results,
                total
            });
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
                .FirstOrDefaultAsync(a => a.Id == id && a.Slot != null && a.Slot.StaffProfileId == staffProfileId, ct);
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
                BranchId = appointment.Slot?.BranchId,
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
                .FirstOrDefaultAsync(a => a.Id == id && a.Slot != null && a.Slot.StaffProfileId == staffProfileId, ct);
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
        // Admin: all staff
        // BranchManager: branch staff only
        // Added pagination + search
        [Authorize]
        [HttpGet("/api/staff")]
        public async Task<IActionResult> GetStaff(
            [FromQuery] string? term,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            CancellationToken ct = default)
        {
            if (_current.UserId is null) return Unauthorized();
            if (page <= 0) page = 1;
            if (size <= 0) size = 10;
            IQueryable<StaffProfile> query = _db.StaffProfiles.AsNoTracking()
                .Include(s => s.User);
            if (_current.Role == UserRole.Admin)
            {
                // all staff
            }
            else if (_current.Role == UserRole.BranchManager)
            {
                query = query.Where(s => s.User.BranchId == _current.BranchId);
            }
            else
            {
                return Forbid();
            }
            // Search before pagination
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim().ToLower();
                query = query.Where(s =>
                    s.FullName.ToLower().Contains(term) ||
                    s.User.Username.ToLower().Contains(term) ||
                    (s.User.BranchId != null && s.User.BranchId.ToString()!.Contains(term)));
            }
            var total = await query.CountAsync(ct);
            var results = await query
                .OrderBy(s => s.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(s => new
                {
                    s.Id,
                    s.UserId,
                    Username = s.User.Username,
                    s.FullName,
                    BranchId = s.User.BranchId
                })
                .ToListAsync(ct);
            return Ok(new
            {
                results,
                total
            });
        }
        [Authorize]
        [HttpPost("/api/staff/{staffId:int}/assign-services")]
        public async Task<IActionResult> AssignServices(
            int staffId,
            [FromBody] AssignStaffServicesRequest request,
            CancellationToken ct)
        {
            if (_current.UserId is null) return Unauthorized();
            var staff = await _db.StaffProfiles
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == staffId, ct);
            if (staff is null)
                return NotFound("Staff not found.");
            // Authorization
            if (_current.Role == UserRole.Admin)
            {
                // allowed
            }
            else if (_current.Role == UserRole.BranchManager)
            {
                if (staff.User.BranchId != _current.BranchId)
                    return Forbid();
            }
            else
            {
                return Forbid();
            }
            var validServiceIds = await _db.ServiceTypes
                .Where(s => request.ServiceTypeIds.Contains(s.Id) && s.BranchId == staff.User.BranchId)
                .Select(s => s.Id)
                .ToListAsync(ct);
            if (validServiceIds.Count != request.ServiceTypeIds.Count)
                return BadRequest("One or more service types are invalid for this staff branch.");
            // Remove old assignments
            var oldAssignments = await _db.StaffServiceTypes
                .Where(x => x.StaffProfileId == staffId)
                .ToListAsync(ct);
            _db.StaffServiceTypes.RemoveRange(oldAssignments);
            // Add new assignments
            var newAssignments = validServiceIds.Select(serviceId => new StaffServiceType
            {
                StaffProfileId = staffId,
                ServiceTypeId = serviceId
            }).ToList();
            _db.StaffServiceTypes.AddRange(newAssignments);
            _db.AuditLogs.Add(new AuditLog
            {
                ActionType = AuditActionType.StaffServiceAssignmentChanged,
                ActorUserId = _current.UserId.Value,
                ActorRole = _current.Role!.Value,
                BranchId = staff.User.BranchId,
                TargetEntityType = "StaffProfile",
                TargetEntityId = staffId.ToString(),
                MetadataJson = JsonSerializer.Serialize(new
                {
                    staffId,
                    serviceTypeIds = validServiceIds
                })
            });
            await _db.SaveChangesAsync(ct);
            return Ok(new
            {
                message = "Staff services assigned successfully.",
                staffId,
                serviceTypeIds = validServiceIds
            });
        }
    }
}
