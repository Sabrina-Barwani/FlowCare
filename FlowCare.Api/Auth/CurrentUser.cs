using System.Security.Claims;
using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Auth
{
    public class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _http;

        public CurrentUser(IHttpContextAccessor http) => _http = http;

        public int? UserId =>
            int.TryParse(_http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id : null;

        public UserRole? Role
        {
            get
            {
                var roleStr = _http.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);
                return Enum.TryParse<UserRole>(roleStr, out var r) ? r : null;
            }
        }

        public int? BranchId =>
            int.TryParse(_http.HttpContext?.User?.FindFirstValue("branch_id"), out var bid)
                ? bid : null;
    }
    public interface ICurrentUser
    {
        int? UserId { get; }
        UserRole? Role { get; }
        int? BranchId { get; }
    }
}
