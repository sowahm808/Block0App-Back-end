using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MindUnlocking.Application.Options;
using MindUnlocking.Application.Security;
using MindUnlocking.Infrastructure.Identity;

namespace MindUnlocking.Infrastructure.Auth;

public sealed class AuthTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public AccessTokenResult CreateAccessToken(ApplicationUser user, IReadOnlyCollection<string> permissions, DateTimeOffset nowUtc)
    {
        var expiresUtc = nowUtc.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, nowUtc.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.DisplayName),
            new("security_stamp", user.SecurityStamp ?? string.Empty)
        };

        claims.AddRange(permissions.Select(permission => new Claim(AuthorizationPolicies.PermissionClaimType, permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            nowUtc.UtcDateTime,
            expiresUtc.UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresUtc);
    }

    public RefreshTokenResult CreateRefreshToken(DateTimeOffset nowUtc)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new RefreshTokenResult(token, HashRefreshToken(token), nowUtc.AddDays(_options.RefreshTokenDays));
    }

    public string HashRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return string.Empty;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(refreshToken)));
    }
}
