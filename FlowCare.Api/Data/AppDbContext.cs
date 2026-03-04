using Microsoft.EntityFrameworkCore;
using FlowCare.Api.Entities;
namespace FlowCare.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Branch> Branches => Set<Branch>();
    }
}
