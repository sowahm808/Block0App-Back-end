using System.Security.Cryptography;
using System.Text;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using MindUnlocking.Infrastructure.Identity;

namespace MindUnlocking.Infrastructure.Auth;

public sealed class AuthTokenService(FirebaseApp app)
{
    public async Task<AccessTokenResult> CreateAccessToken(ApplicationUser user, IReadOnlyCollection<string> permissions, DateTimeOffset nowUtc)
    {
        var claims = new Dictionary<string, object>
        {
            ["permissions"] = permissions.ToArray(),
            ["role"] = permissions.Contains(MindUnlocking.Domain.Identity.Permissions.AdminAccess) ? "admin" : "scholar",
            ["email_verified"] = user.EmailVerified
        };

        await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(user.Id, claims);
        var token = await FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(user.Id, claims);
        return new AccessTokenResult(token, nowUtc.AddHours(1));
    }

    public RefreshTokenResult CreateRefreshToken(DateTimeOffset nowUtc)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new RefreshTokenResult(token, HashRefreshToken(token), nowUtc.AddDays(14));
    }

    public string HashRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return string.Empty;
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(refreshToken)));
    }
}
