using FlowCare.Api.Data;
using FlowCare.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static FlowCare.Api.Entities.Enums;
using System.Text.Json;
using FlowCare.Api.Auth;
using FlowCare.Api.DTOs;
using FlowCare.Api.Entities;

namespace FlowCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SlotsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    public SlotsController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }


    [AllowAnonymous]
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] int branchId,
        [FromQuery] int serviceTypeId,
        [FromQuery] DateOnly? date)
    {
        // Validate branch exists
        var branchExists = await _db.Branches.AnyAsync(b => b.Id == branchId);
        if (!branchExists) return NotFound("Branch not found.");

        // Validate service type belongs to the branch
        var serviceExists = await _db.ServiceTypes.AnyAsync(s => s.Id == serviceTypeId && s.BranchId == branchId);
        if (!serviceExists) return NotFound("Service type not found for this branch.");

        var now = DateTime.UtcNow;

        var query = _db.Slots.AsNoTracking()
            .Where(s =>
                s.BranchId == branchId &&
                s.ServiceTypeId == serviceTypeId &&
                s.DeletedAtUtc == null &&
                (s.Appointment == null || s.Appointment.Status == AppointmentStatus.Cancelled) &&
                s.StartTimeUtc > now
            );

        //  date filter 
        if (date.HasValue)
        {
            var dayStart = date.Value.ToDateTime(TimeOnly.MinValue);
            var dayEnd = date.Value.ToDateTime(TimeOnly.MaxValue);

            query = query.Where(s => s.StartTimeUtc >= dayStart && s.StartTimeUtc <= dayEnd);
        }

        var result = await query
            .OrderBy(s => s.StartTimeUtc)
            .Select(s => new AvailableSlotDto(
                s.Id,
                s.BranchId,
                s.ServiceTypeId,
                s.StaffProfileId,
                s.StartTimeUtc,
                s.EndTimeUtc
            ))
            .ToListAsync();

        return Ok(result);
    }
    private Task<bool> CanManageBranchAsync(int branchId)
    {
        if (_current.UserId is null) return Task.FromResult(false);

        if (_current.Role == UserRole.Admin)
            return Task.FromResult(true);

        if (_current.Role == UserRole.BranchManager && _current.BranchId == branchId)
            return Task.FromResult(true);

        return Task.FromResult(false);
    }
    [Authorize]
    [HttpPost("/api/branches/{branchId:int}/slots")]
    public async Task<IActionResult> CreateSlot(
    int branchId,
    [FromBody] CreateSlotRequest request,
    CancellationToken ct)
    {
        if (!await CanManageBranchAsync(branchId))
            return Forbid();

        var branchExists = await _db.Branches.AnyAsync(b => b.Id == branchId, ct);
        if (!branchExists) return NotFound("Branch not found.");

        var serviceExists = await _db.ServiceTypes
            .AnyAsync(s => s.Id == request.ServiceTypeId && s.BranchId == branchId, ct);

        if (!serviceExists)
            return BadRequest("Service type does not belong to this branch.");

        var slot = new Slot
        {
            BranchId = branchId,
            ServiceTypeId = request.ServiceTypeId,
            StaffProfileId = request.StaffProfileId,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            DeletedAtUtc = null
        };

        _db.Slots.Add(slot);
        await _db.SaveChangesAsync(ct);

        _db.AuditLogs.Add(new AuditLog
        {
            ActionType = AuditActionType.SlotCreated,
            ActorUserId = _current.UserId!.Value,
            ActorRole = _current.Role!.Value,
            BranchId = branchId,
            TargetEntityType = "Slot",
            TargetEntityId = slot.Id.ToString(),
            MetadataJson = JsonSerializer.Serialize(new
            {
                slot.Id,
                slot.ServiceTypeId,
                slot.StaffProfileId,
                slot.StartTimeUtc,
                slot.EndTimeUtc
            })
        });

        await _db.SaveChangesAsync(ct);

        return Ok(slot);
    }
    // create slot
    [Authorize]
    [HttpPost("/api/branches/{branchId:int}/slots/bulk")]
    public async Task<IActionResult> BulkCreateSlots(
    int branchId,
    [FromBody] BulkCreateSlotsRequest request,
    CancellationToken ct)
    {
        if (!await CanManageBranchAsync(branchId))
            return Forbid();

        var branchExists = await _db.Branches.AnyAsync(b => b.Id == branchId, ct);
        if (!branchExists) return NotFound("Branch not found.");

        var serviceExists = await _db.ServiceTypes
            .AnyAsync(s => s.Id == request.ServiceTypeId && s.BranchId == branchId, ct);

        if (!serviceExists)
            return BadRequest("Service type does not belong to this branch.");

        if (request.SlotDurationMinutes <= 0)
            return BadRequest("SlotDurationMinutes must be greater than zero.");

        var createdSlots = new List<Slot>();

        for (var day = request.StartDateUtc.Date; day <= request.EndDateUtc.Date; day = day.AddDays(1))
        {
            var current = day.Add(request.DailyStartTime);
            var dayEnd = day.Add(request.DailyEndTime);

            while (current.AddMinutes(request.SlotDurationMinutes) <= dayEnd)
            {
                createdSlots.Add(new Slot
                {
                    BranchId = branchId,
                    ServiceTypeId = request.ServiceTypeId,
                    StaffProfileId = request.StaffProfileId,
                    StartTimeUtc = current,
                    EndTimeUtc = current.AddMinutes(request.SlotDurationMinutes),
                    DeletedAtUtc = null
                });

                current = current.AddMinutes(request.SlotDurationMinutes);
            }
        }

        _db.Slots.AddRange(createdSlots);
        await _db.SaveChangesAsync(ct);

        foreach (var slot in createdSlots)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActionType = AuditActionType.SlotCreated,
                ActorUserId = _current.UserId!.Value,
                ActorRole = _current.Role!.Value,
                BranchId = branchId,
                TargetEntityType = "Slot",
                TargetEntityId = slot.Id.ToString(),
                MetadataJson = JsonSerializer.Serialize(new
                {
                    slot.Id,
                    slot.ServiceTypeId,
                    slot.StartTimeUtc,
                    slot.EndTimeUtc
                })
            });
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Slots created successfully.",
            count = createdSlots.Count
        });
    }
    //update slot 
    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateSlot(
    int id,
    [FromBody] UpdateSlotRequest request,
    CancellationToken ct)
    {
        var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (slot is null) return NotFound("Slot not found.");

        if (!await CanManageBranchAsync(slot.BranchId))
            return Forbid();

        var serviceExists = await _db.ServiceTypes
            .AnyAsync(s => s.Id == request.ServiceTypeId && s.BranchId == slot.BranchId, ct);

        if (!serviceExists)
            return BadRequest("Service type does not belong to this branch.");

        slot.ServiceTypeId = request.ServiceTypeId;
        slot.StaffProfileId = request.StaffProfileId;
        slot.StartTimeUtc = request.StartTimeUtc;
        slot.EndTimeUtc = request.EndTimeUtc;

        await _db.SaveChangesAsync(ct);

        _db.AuditLogs.Add(new AuditLog
        {
            ActionType = AuditActionType.SlotUpdated,
            ActorUserId = _current.UserId!.Value,
            ActorRole = _current.Role!.Value,
            BranchId = slot.BranchId,
            TargetEntityType = "Slot",
            TargetEntityId = slot.Id.ToString(),
            MetadataJson = JsonSerializer.Serialize(new
            {
                slot.Id,
                slot.ServiceTypeId,
                slot.StaffProfileId,
                slot.StartTimeUtc,
                slot.EndTimeUtc
            })
        });

        await _db.SaveChangesAsync(ct);

        return Ok(slot);
    }
    //delete slot 

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteSlot(int id, CancellationToken ct)
    {
        var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (slot is null) return NotFound("Slot not found.");

        if (!await CanManageBranchAsync(slot.BranchId))
            return Forbid();

        if (slot.DeletedAtUtc != null)
            return BadRequest("Slot already deleted.");

        slot.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _db.AuditLogs.Add(new AuditLog
        {
            ActionType = AuditActionType.SlotSoftDeleted,
            ActorUserId = _current.UserId!.Value,
            ActorRole = _current.Role!.Value,
            BranchId = slot.BranchId,
            TargetEntityType = "Slot",
            TargetEntityId = slot.Id.ToString(),
            MetadataJson = JsonSerializer.Serialize(new
            {
                slot.Id,
                slot.ServiceTypeId,
                slot.StartTimeUtc,
                slot.EndTimeUtc,
                deletedAtUtc = slot.DeletedAtUtc
            })
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Slot soft deleted successfully.",
            slot.Id,
            slot.DeletedAtUtc
        });
    }
    //get all slots 
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAllSlots([FromQuery] bool includeDeleted = false, CancellationToken ct = default)
    {
        if (_current.UserId is null) return Unauthorized();

        IQueryable<Slot> query = _db.Slots.AsNoTracking();

        if (_current.Role == UserRole.Admin)
        {
            if (!includeDeleted)
                query = query.Where(s => s.DeletedAtUtc == null);
        }
        else if (_current.Role == UserRole.BranchManager)
        {
            query = query.Where(s => s.BranchId == _current.BranchId);

            if (!includeDeleted)
                query = query.Where(s => s.DeletedAtUtc == null);
        }
        else
        {
            return Forbid();
        }

        var result = await query
            .OrderBy(s => s.StartTimeUtc)
            .Select(s => new
            {
                s.Id,
                s.BranchId,
                s.ServiceTypeId,
                s.StaffProfileId,
                s.StartTimeUtc,
                s.EndTimeUtc,
                s.DeletedAtUtc
            })
            .ToListAsync(ct);

        return Ok(result);
    }
}