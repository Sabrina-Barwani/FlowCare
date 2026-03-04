using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace FlowCare.Api.Auth;

// Supports checking branch scope using a resource (branchId as int).
public class SameBranchHandler : AuthorizationHandler<SameBranchRequirement, int>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameBranchRequirement requirement,
        int resourceBranchId)
    {
        var branchClaim = context.User.FindFirst("branch_id")?.Value;

        if (int.TryParse(branchClaim, out var userBranchId) && userBranchId == resourceBranchId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}