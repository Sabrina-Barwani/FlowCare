using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using FlowCare.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using FlowCare.Api.DTOs;
using static FlowCare.Api.Entities.Enums;
using FlowCare.Api.Entities;
namespace FlowCare.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public MeController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> MyAppointments()
    {
        if (_current.UserId is null) return Unauthorized();

        // Customer only 
        if (_current.Role?.ToString() != "Customer")
            return Forbid();

        var userId = _current.UserId.Value;

        var items = await _db.Appointments.AsNoTracking()
            .Where(a => a.CustomerProfileId == userId)
            .OrderByDescending(a => a.Id)
            .Select(a => new MyAppointmentDto
            {
                Id = a.Id,
                Status = a.Status.ToString(),

                SlotId = a.SlotId,
           
                StartTimeUtc = a.Slot != null ? a.Slot.StartTimeUtc : null,
                EndTimeUtc = a.Slot != null ? a.Slot.EndTimeUtc : null,

                BranchId = a.Slot.BranchId,
                BranchName = a.Slot.Branch.Name,

                ServiceTypeId = a.Slot.ServiceTypeId,
                ServiceTypeName = a.Slot.ServiceType.Name,

                AttachmentPath = a.AttachmentPath,
                AttachmentContentType = a.AttachmentContentType,
                AttachmentSize = a.AttachmentSize
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("appointments/{id:int}")]
    public async Task<IActionResult> MyAppointmentById(int id)
    {
        if (_current.UserId is null) return Unauthorized();

        if (_current.Role?.ToString() != "Customer")
            return Forbid();

        var userId = _current.UserId.Value;

        var item = await _db.Appointments.AsNoTracking()
            .Where(a => a.Id == id && a.CustomerProfileId == userId)
            .Select(a => new MyAppointmentDto
            {
                Id = a.Id,
                Status = a.Status.ToString(),

                SlotId = a.SlotId,
  
                StartTimeUtc = a.Slot != null ? a.Slot.StartTimeUtc : null,
                EndTimeUtc = a.Slot != null ? a.Slot.EndTimeUtc : null,

                BranchId = a.Slot.BranchId,
                BranchName = a.Slot.Branch.Name,

                ServiceTypeId = a.Slot.ServiceTypeId,
                ServiceTypeName = a.Slot.ServiceType.Name,

                AttachmentPath = a.AttachmentPath,
                AttachmentContentType = a.AttachmentContentType,
                AttachmentSize = a.AttachmentSize
            })
            .FirstOrDefaultAsync();

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("appointments/{id:int}/cancel")]
    public async Task<IActionResult> CancelAppointment(int id, CancellationToken ct)
    {
        if (_current.UserId is null) return Unauthorized();

        if (_current.Role?.ToString() != "Customer")
            return Forbid();

        var myCustomerId = await _db.CustomerProfiles.AsNoTracking()
            .Where(c => c.UserId == _current.UserId.Value)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (myCustomerId == 0) return NotFound();

        var appointment = await _db.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerProfileId == myCustomerId, ct);

        if (appointment is null)
            return NotFound("Appointment not found.");

        if (appointment.Status == AppointmentStatus.Cancelled)
            return BadRequest("Appointment is already cancelled.");

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAtUtc = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            ActionType = AuditActionType.AppointmentCancelled,
            ActorUserId = _current.UserId.Value,
            ActorRole = _current.Role!.Value,
            BranchId = appointment.Slot.BranchId,
            TargetEntityType = "Appointment",
            TargetEntityId = appointment.Id.ToString(),
            MetadataJson = JsonSerializer.Serialize(new
            {
                slotId = appointment.SlotId,
                serviceTypeId = appointment.Slot.ServiceTypeId
            })
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Appointment cancelled successfully.",
            appointment.Id,
            appointment.Status,
            appointment.CancelledAtUtc
        });
    }

    [HttpPost("appointments/{id:int}/reschedule")]
    public async Task<IActionResult> RescheduleAppointment(
    int id,
    [FromBody] RescheduleAppointmentRequest request,
    CancellationToken ct)
    {
        if (_current.UserId is null) return Unauthorized();

        if (_current.Role?.ToString() != "Customer")
            return Forbid();

        var myCustomerId = await _db.CustomerProfiles.AsNoTracking()
            .Where(c => c.UserId == _current.UserId.Value)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (myCustomerId == 0) return NotFound();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var appointment = await _db.Appointments
                .Include(a => a.Slot)
                .FirstOrDefaultAsync(a => a.Id == id && a.CustomerProfileId == myCustomerId, ct);

            if (appointment is null)
                return NotFound("Appointment not found.");

            if (appointment.Status == AppointmentStatus.Cancelled)
                return BadRequest("Cancelled appointment cannot be rescheduled.");

            var newSlot = await _db.Slots
                .FirstOrDefaultAsync(s => s.Id == request.NewSlotId, ct);

            if (newSlot is null)
                return NotFound("New slot not found.");

            if (newSlot.DeletedAtUtc != null)
                return BadRequest("New slot is deleted.");

            if (newSlot.StartTimeUtc <= DateTime.UtcNow)
                return BadRequest("New slot is not in the future.");

            // Check new slot not booked by another appointment
            var alreadyBooked = await _db.Appointments
                .AnyAsync(a => a.SlotId == request.NewSlotId && a.Id != appointment.Id, ct);

            if (alreadyBooked)
                return Conflict("New slot is already booked.");

            var oldSlotId = appointment.SlotId;

            appointment.SlotId = request.NewSlotId;

            _db.AuditLogs.Add(new AuditLog
            {
                ActionType = AuditActionType.AppointmentRescheduled,
                ActorUserId = _current.UserId.Value,
                ActorRole = _current.Role!.Value,
                BranchId = newSlot.BranchId,
                TargetEntityType = "Appointment",
                TargetEntityId = appointment.Id.ToString(),
                MetadataJson = JsonSerializer.Serialize(new
                {
                    oldSlotId = oldSlotId,
                    newSlotId = request.NewSlotId
                })
            });

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Ok(new
            {
                message = "Appointment rescheduled successfully.",
                appointment.Id,
                oldSlotId,
                newSlotId = request.NewSlotId
            });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            return Conflict("New slot is already booked.");
        }
    }
}
