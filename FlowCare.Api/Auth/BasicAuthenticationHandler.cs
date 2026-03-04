using FlowCare.Api.Data;
using FlowCare.Api.Entities;
using FlowCare.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace FlowCare.Api.Auth;

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db,
        IPasswordHasher hasher)
        : base(options, logger, encoder)
    {
        _db = db;
        _hasher = hasher;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.Fail("Missing Authorization header");

        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]!);
            if (!"Basic".Equals(authHeader.Scheme, StringComparison.OrdinalIgnoreCase))
                return AuthenticateResult.Fail("Invalid auth scheme");

            var credentialBytes = Convert.FromBase64String(authHeader.Parameter ?? "");
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);

            if (credentials.Length != 2)
                return AuthenticateResult.Fail("Invalid Basic credentials format");

            var username = credentials[0];
            var password = credentials[1];

            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

            if (user is null)
                return AuthenticateResult.Fail("Invalid username or password");

            if (!_hasher.Verify(password, user.PasswordHash))
                return AuthenticateResult.Fail("Invalid username or password");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("sub", user.Id.ToString())
            };

            if (user.BranchId.HasValue)
                claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch
        {
            return AuthenticateResult.Fail("Invalid Authorization header");
        }
    }
}