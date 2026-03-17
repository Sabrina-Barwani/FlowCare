using FlowCare.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QueueController : ControllerBase
{
    private readonly AppDbContext _db;

    public QueueController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("position")]
    public async Task<IActionResult> GetPosition(int appointmentId)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment is null || appointment.Slot is null)
            return NotFound("Appointment not found.");

        var branchId = appointment.Slot.BranchId;
        var startTime = appointment.Slot.StartTimeUtc.Date;

        var validStatuses = new[]
        {
            AppointmentStatus.Booked,
            AppointmentStatus.CheckedIn
        };

        var position = await _db.Appointments
            .Include(a => a.Slot)
            .Where(a =>
               a.Slot != null &&
                a.Slot.BranchId == branchId &&
                a.Slot.StartTimeUtc.Date == startTime &&
                validStatuses.Contains(a.Status) &&
                a.Slot.StartTimeUtc <= appointment.Slot.StartTimeUtc
            )
            .CountAsync();

        return Ok(new
        {
            appointmentId,
            position
        });
    }
}