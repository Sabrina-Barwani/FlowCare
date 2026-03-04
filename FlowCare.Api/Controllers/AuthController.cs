using FlowCare.Api.Data;
using FlowCare.Api.Dtos;
using FlowCare.Api.Entities;
using FlowCare.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const long MaxIdImageBytes = 5 * 1024 * 1024; // 5MB

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IFileStorage _storage;

    public AuthController(AppDbContext db, IPasswordHasher hasher, IFileStorage storage)
    {
        _db = db;
        _hasher = hasher;
        _storage = storage;
    }

   
    [AllowAnonymous]
    [HttpPost("register")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Register([FromForm] RegisterCustomerRequest req, CancellationToken ct)
    {
        // Basic validation
        if (req.IdImage == null || req.IdImage.Length == 0)
            return BadRequest("idImage is required.");

        if (req.IdImage.Length > MaxIdImageBytes)
            return BadRequest("idImage is too large. Max 5MB.");

        // Validate content-type starts with image/
        if (string.IsNullOrWhiteSpace(req.IdImage.ContentType) || !req.IdImage.ContentType.StartsWith("image/"))
            return BadRequest("idImage must be an image file (content-type must start with image/).");

        // Username unique
        var usernameTaken = await _db.Users.AnyAsync(u => u.Username == req.Username, ct);
        if (usernameTaken)
            return BadRequest("Username already exists.");

        // Create user + customer profile
        var user = new User
        {
            Username = req.Username,
            PasswordHash = _hasher.Hash(req.Password),
            Role = UserRole.Customer,
            IsActive = true,
            BranchId = null
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct); // to get user.Id

        var customer = new CustomerProfile
        {
            UserId = user.Id,
            FullName = req.FullName,
            Phone = req.Phone
        };

        _db.CustomerProfiles.Add(customer);
        await _db.SaveChangesAsync(ct); // to get customer.Id

        // Save file to local storage
        var (path, contentType, size) = await _storage.SaveCustomerIdAsync(customer.Id, req.IdImage, ct);

        customer.IdImagePath = path;
        customer.IdImageContentType = contentType;
        customer.IdImageSize = size;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            customerId = customer.Id,
            userId = user.Id,
            customer.FullName,
            customer.Phone
        });
    }
}
