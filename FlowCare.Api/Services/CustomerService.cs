using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using FlowCare.Api.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using static FlowCare.Api.Entities.Enums;
namespace FlowCare.Api.Services;

public class CustomerService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public CustomerService(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CustomerProfile> GetCustomerAsync(int customerProfileId)
    {
        // Admin can view any customer
        if (_current.Role == UserRole.Admin)
        {
            var anyCustomer = await _db.CustomerProfiles.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerProfileId);

            return anyCustomer ?? throw new KeyNotFoundException("Customer not found");
        }

        // Customer can view ONLY his own customer profile
        if (_current.Role == UserRole.Customer)
        {
            var myCustomer = await _db.CustomerProfiles.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerProfileId && c.UserId == _current.UserId);

            if (myCustomer is null)
                throw new UnauthorizedAccessException("You cannot access another customer profile.");

            return myCustomer;
        }

        // Manager/Staff: later we can decide if they can list customers; for now forbid
        throw new UnauthorizedAccessException("Not allowed.");
    }
}