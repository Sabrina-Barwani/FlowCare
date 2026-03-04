using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using FlowCare.Api.Entities;
using Microsoft.EntityFrameworkCore;
using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Services;

// Business logic for slots, including RBAC + branch scoping.
public class SlotService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public SlotService(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<Slot>> GetSlotsForBranchAsync(int branchId)
    {
        // Admin can access any branch.
        if (_current.Role == UserRole.Admin)
            return await _db.Slots.AsNoTracking()
                .Where(s => s.BranchId == branchId)
                .ToListAsync();

        // Manager/Staff: must match their own branch.
        if (_current.Role is UserRole.BranchManager or UserRole.Staff)
        {
            if (_current.BranchId != branchId)
                throw new UnauthorizedAccessException("You cannot access slots of another branch.");

            return await _db.Slots.AsNoTracking()
                .Where(s => s.BranchId == branchId)
                .ToListAsync();
        }

        // Customer: not allowed in this endpoint.
        throw new UnauthorizedAccessException("Not allowed.");
    }
}