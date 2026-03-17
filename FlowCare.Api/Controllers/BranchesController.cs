
using FlowCare.Api.Data;
using FlowCare.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FlowCare.Api.Controllers
{
    // Protected by authentication depending on role
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BranchesController : Controller
    {
        private readonly AppDbContext _db;
        public BranchesController(AppDbContext db) => _db = db;
        // Public: Returns all branches with pagination + search
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string? term,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            if (page <= 0) page = 1;
            if (size <= 0) size = 10;
            var query = _db.Branches.AsNoTracking().AsQueryable();
            // Search before pagination
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim().ToLower();
                query = query.Where(b =>
                    b.Name.ToLower().Contains(term) ||
                    b.City.ToLower().Contains(term));
            }
            var total = await query.CountAsync();
            var results = await query
                .OrderBy(b => b.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();
            return Ok(new
            {
                results,
                total
            });
        }
        // Public: Returns services by branch with pagination + search
        // GET /api/branches/{branchId}/services
        [AllowAnonymous]
        [HttpGet("{branchId:int}/services")]
        public async Task<IActionResult> GetServicesByBranch(
            int branchId,
            [FromQuery] string? term,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            if (page <= 0) page = 1;
            if (size <= 0) size = 10;
            var branchExists = await _db.Branches.AnyAsync(b => b.Id == branchId);
            if (!branchExists) return NotFound("Branch not found.");
            var query = _db.ServiceTypes.AsNoTracking()
                .Where(s => s.BranchId == branchId);
            // Search before pagination
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim().ToLower();
                query = query.Where(s =>
                    s.Name.ToLower().Contains(term));
            }
            var total = await query.CountAsync();
            var results = await query
                .OrderBy(s => s.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();
            return Ok(new
            {
                results,
                total
            });
        }
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Branch branch)
        {
            _db.Branches.Add(branch);
            await _db.SaveChangesAsync();
            return Ok(branch);
        }
    }
}