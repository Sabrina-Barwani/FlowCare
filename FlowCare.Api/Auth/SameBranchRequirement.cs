using Microsoft.AspNetCore.Authorization;

namespace FlowCare.Api.Auth;

// Checks that the current user's branch_id claim matches the target branch id.
public class SameBranchRequirement : IAuthorizationRequirement 
{

}