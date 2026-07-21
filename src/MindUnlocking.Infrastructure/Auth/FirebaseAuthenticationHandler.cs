using System.Security.Claims;
using System.Text.Encodings.Web;

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
    FirebaseAuth firebaseAuth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        logger,
        encoder)
{
    public const string AuthenticationScheme = "Firebase";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var idToken = authorization["Bearer ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            var token = await firebaseAuth.VerifyIdTokenAsync(
                idToken,
                Context.RequestAborted);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, token.Uid),
                new("firebase_uid", token.Uid)
            };

            if (token.Claims.TryGetValue("email", out var email) &&
                email is not null &&
                !string.IsNullOrWhiteSpace(email.ToString()))
            {
                claims.Add(
                    new Claim(
                        ClaimTypes.Email,
                        email.ToString()!));
            }

            if (token.Claims.TryGetValue("name", out var name) &&
                name is not null &&
                !string.IsNullOrWhiteSpace(name.ToString()))
            {
                claims.Add(
                    new Claim(
                        ClaimTypes.Name,
                        name.ToString()!));
            }
            else
            {
                claims.Add(
                    new Claim(
                        ClaimTypes.Name,
                        token.Uid));
            }

            if (token.Claims.TryGetValue(
                    "permissions",
                    out var permissions))
            {
                AddPermissionClaims(claims, permissions);
            }

            var identity = new ClaimsIdentity(
                claims,
                AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            var ticket = new AuthenticationTicket(
                principal,
                AuthenticationScheme);

            return AuthenticateResult.Success(ticket);
        }
        catch (FirebaseAuthException exception)
        {
            Logger.LogWarning(
                exception,
                "Firebase ID token validation failed.");

            return AuthenticateResult.Fail(
                "Firebase ID token is invalid or expired.");
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "Unexpected Firebase authentication failure.");

            return AuthenticateResult.Fail(
                "Authentication could not be completed.");
        }
    }

    private static void AddPermissionClaims(
        ICollection<Claim> claims,
        object permissions)
    {
        if (permissions is IEnumerable<object> values)
        {
            foreach (var value in values)
            {
                var permission = value?.ToString();

                if (!string.IsNullOrWhiteSpace(permission))
                {
                    claims.Add(
                        new Claim(
                            AuthorizationPolicies.PermissionClaimType,
                            permission));
                }
            }

            return;
        }

        var singleValue = permissions.ToString();

        if (string.IsNullOrWhiteSpace(singleValue))
        {
            return;
        }

        foreach (var permission in singleValue.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            claims.Add(
                new Claim(
                    AuthorizationPolicies.PermissionClaimType,
                    permission));
        }
    }
}