using FlowCare.Api.Data;
using FlowCare.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SlotsController : ControllerBase
{
    private readonly AppDbContext _db;
    public SlotsController(AppDbContext db) => _db = db;

    
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
                s.DeletedAtUtc == null &&     // not deleted
                s.Appointment == null &&      // not booked
                s.StartTimeUtc > now          // future only
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
}