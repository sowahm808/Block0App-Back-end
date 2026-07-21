using System.Security.Claims;
using System.Text.Encodings.Web;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MindUnlocking.Application.Security;

namespace MindUnlocking.Infrastructure.Auth;

public sealed class FirebaseAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FirebaseApp app) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "Firebase";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var idToken = authorization["Bearer ".Length..].Trim();
        if (idToken.Length == 0) return AuthenticateResult.NoResult();

        try
        {
            var token = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, token.Uid),
                new(ClaimTypes.Email, token.Claims.GetValueOrDefault("email")?.ToString() ?? string.Empty),
                new(ClaimTypes.Name, token.Claims.GetValueOrDefault("name")?.ToString() ?? token.Uid)
            };

            if (token.Claims.TryGetValue("permissions", out var permissions) && permissions is IEnumerable<object> values)
            {
                claims.AddRange(values.Select(value => new Claim(AuthorizationPolicies.PermissionClaimType, value.ToString() ?? string.Empty)));
            }

            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)), Scheme));
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail(ex);
        }
    }
}
