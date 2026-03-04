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
        [HttpGet]
        public async Task<IActionResult> Get() =>
            Ok(await _db.Branches.AsNoTracking().ToListAsync());

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Branch branch)
        {
            _db.Branches.Add(branch);
            await _db.SaveChangesAsync();   
            return Ok(branch);
        }
    }
}
