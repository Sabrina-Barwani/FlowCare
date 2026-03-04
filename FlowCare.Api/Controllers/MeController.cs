using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using FlowCare.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                StartTimeUtc = a.Slot.StartTimeUtc,
                EndTimeUtc = a.Slot.EndTimeUtc,

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
                StartTimeUtc = a.Slot.StartTimeUtc,
                EndTimeUtc = a.Slot.EndTimeUtc,

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
}
