using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using FlowCare.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static FlowCare.Api.Entities.Enums;
namespace FlowCare.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CustomerService _service;
    private readonly ICurrentUser _current;
    private readonly IFileStorage _storage;
    public CustomersController(
        AppDbContext db,
        CustomerService service,
        ICurrentUser current,
        IFileStorage storage)
    {
        _db = db;
        _service = service;
        _current = current;
        _storage = storage;
    }
    // Admin: all customers
    // Manager: only customers who have appointments in the manager's branch
    // Added pagination + search
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        CancellationToken ct = default)
    {
        if (_current.UserId is null) return Unauthorized();
        if (page <= 0) page = 1;
        if (size <= 0) size = 10;
        IQueryable<FlowCare.Api.Entities.CustomerProfile> query = _db.CustomerProfiles.AsNoTracking();
        if (_current.Role == UserRole.Admin)
        {
            // all customers
        }
        else if (_current.Role == UserRole.BranchManager)
        {
            var branchId = _current.BranchId;
            query = query.Where(c =>
                _db.Appointments.Any(a =>
                    a.CustomerProfileId == c.Id &&
                    a.Slot != null &&
                    a.Slot.BranchId == branchId));
        }
        else
        {
            return Forbid();
        }
        // Search before pagination
        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim().ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                (c.IdImageContentType != null && c.IdImageContentType.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var results = await query
            .OrderBy(c => c.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(c => new
            {
                c.Id,
                c.UserId,
                c.FullName,
                c.Phone,
                c.IdImagePath,
                c.IdImageContentType,
                c.IdImageSize
            })
            .ToListAsync(ct);
        return Ok(new
        {
            results,
            total
        });
    }
    // Existing endpoint: role checks handled inside CustomerService
    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        try
        {
            var customer = await _service.GetCustomerAsync(id);
            return Ok(customer);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
    // Admin only: return customer ID image file
    [Authorize]
    [HttpGet("{id:int}/id-image")]
    public async Task<IActionResult> GetIdImage(int id, CancellationToken ct)
    {
        if (_current.UserId is null) return Unauthorized();
        if (_current.Role != UserRole.Admin)
            return Forbid();
        var customer = await _db.CustomerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
            return NotFound("Customer not found.");
        if (string.IsNullOrWhiteSpace(customer.IdImagePath))
            return NotFound("Customer ID image not found.");
        try
        {
            var (stream, contentType) = await _storage.OpenReadAsync(customer.IdImagePath, ct);
            return File(stream, customer.IdImageContentType ?? contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound("Customer ID image file is missing from storage.");
        }
    }
}
