using System.Security.Cryptography;
using System.Text;

using FirebaseAdmin.Auth;

using MindUnlocking.Infrastructure.Identity;

namespace MindUnlocking.Infrastructure.Auth;

public sealed class AuthTokenService(FirebaseAuth firebaseAuth)
{
    public async Task<AccessTokenResult> CreateAccessToken(
        ApplicationUser user,
        IReadOnlyCollection<string> permissions,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(permissions);

        var permissionValues = permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var isAdmin = permissionValues.Contains(
            "admin:access",
            StringComparer.Ordinal);

        var claims = new Dictionary<string, object>
        {
            ["permissions"] = permissionValues,
            ["role"] = isAdmin ? "admin" : "scholar",
            ["email_verified"] = user.EmailVerified
        };

        await firebaseAuth.SetCustomUserClaimsAsync(
            user.Id,
            claims);

        var customToken = await firebaseAuth.CreateCustomTokenAsync(
            user.Id,
            claims);

        return new AccessTokenResult(
            customToken,
            nowUtc.AddHours(1));
    }

    public RefreshTokenResult CreateRefreshToken(
        DateTimeOffset nowUtc)
    {
        var token = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));

        return new RefreshTokenResult(
            token,
            HashRefreshToken(token),
            nowUtc.AddDays(14));
    }

    public string HashRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return string.Empty;
        }

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(refreshToken));

        return Convert.ToHexString(hash);
    }
}