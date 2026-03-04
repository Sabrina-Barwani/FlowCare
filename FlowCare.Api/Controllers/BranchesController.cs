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


        // Returns all branches
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Get() =>
            Ok(await _db.Branches.AsNoTracking().ToListAsync());

        // GET /api/branches/branchId/services
        [AllowAnonymous]
        [HttpGet("{branchId:int}/services")]
        public async Task<IActionResult> GetServicesByBranch(int branchId)
        {
            var branchExists = await _db.Branches.AnyAsync(b => b.Id == branchId);
            if (!branchExists) return NotFound("Branch not found.");

            var services = await _db.ServiceTypes.AsNoTracking()
                .Where(s => s.BranchId == branchId)
                .ToListAsync();

            return Ok(services);
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
