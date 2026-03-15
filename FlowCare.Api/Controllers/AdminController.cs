using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using FlowCare.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static FlowCare.Api.Entities.Enums;
using System.Text.Json;
using FlowCare.Api.Entities;

namespace FlowCare.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public AdminController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        if (_current.Role != UserRole.Admin)
            return Forbid();

        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null)
            return NotFound("Settings not found.");

        return Ok(new
        {
            settings.SlotRetentionDays
        });
    }

    [HttpPut("settings/slot-retention-days")]
    public async Task<IActionResult> UpdateSlotRetentionDays(
        [FromBody] UpdateSlotRetentionDaysRequest request,
        CancellationToken ct)
    {
        if (_current.Role != UserRole.Admin)
            return Forbid();

        if (request.SlotRetentionDays <= 0)
            return BadRequest("SlotRetentionDays must be greater than zero.");

        var settings = await _db.AppSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
            return NotFound("Settings not found.");

        settings.SlotRetentionDays = request.SlotRetentionDays;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Slot retention days updated successfully.",
            settings.SlotRetentionDays
        });
    }

    [HttpPost("slots/cleanup")]
    public async Task<IActionResult> CleanupSoftDeletedSlots(CancellationToken ct)
    {
        if (_current.Role != UserRole.Admin)
            return Forbid();

        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null)
            return NotFound("Settings not found.");

        var cutoff = DateTime.UtcNow.AddDays(-settings.SlotRetentionDays);

        var slotsToDelete = await _db.Slots
            .Where(s => s.DeletedAtUtc != null && s.DeletedAtUtc <= cutoff)
            .ToListAsync(ct);

        if (slotsToDelete.Count == 0)
        {
            return Ok(new
            {
                message = "No slots eligible for cleanup.",
                deletedCount = 0
            });
        }

        var slotIds = slotsToDelete.Select(s => s.Id).ToList();

        // Load related appointments that still reference these slots
        var relatedAppointments = await _db.Appointments
            .Where(a => a.SlotId != null && slotIds.Contains(a.SlotId.Value))
            .ToListAsync(ct);

        foreach (var appointment in relatedAppointments)
        {
            var oldSlotId = appointment.SlotId;

            // Nullify SlotId safely so slot can be hard-deleted
            appointment.SlotId = null;

            _db.AuditLogs.Add(new AuditLog
            {
                ActionType = AuditActionType.SlotHardDeleted,
                ActorUserId = _current.UserId!.Value,
                ActorRole = _current.Role!.Value,
                BranchId = null,
                TargetEntityType = "Appointment",
                TargetEntityId = appointment.Id.ToString(),
                MetadataJson = JsonSerializer.Serialize(new
                {
                    oldSlotId,
                    reason = "Slot hard deleted during cleanup"
                })
            });
        }

        foreach (var slot in slotsToDelete)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActionType = AuditActionType.SlotHardDeleted,
                ActorUserId = _current.UserId!.Value,
                ActorRole = _current.Role!.Value,
                BranchId = slot.BranchId,
                TargetEntityType = "Slot",
                TargetEntityId = slot.Id.ToString(),
                MetadataJson = JsonSerializer.Serialize(new
                {
                    slot.Id,
                    slot.BranchId,
                    slot.ServiceTypeId,
                    slot.StaffProfileId,
                    slot.StartTimeUtc,
                    slot.EndTimeUtc,
                    slot.DeletedAtUtc
                })
            });
        }

        _db.Slots.RemoveRange(slotsToDelete);

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Cleanup completed successfully.",
            deletedCount = slotsToDelete.Count
        });
    }
}