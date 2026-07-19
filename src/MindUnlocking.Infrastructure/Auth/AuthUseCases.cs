using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MindUnlocking.Application.Security;
using MindUnlocking.Contracts.Auth;
using MindUnlocking.Infrastructure.Identity;
using MindUnlocking.Infrastructure.Persistence;

namespace MindUnlocking.Infrastructure.Auth;

public sealed class AuthUseCases(
    UserManager<ApplicationUser> users,
    MindUnlockingDbContext db,
    AuthTokenService tokens,
    ILogger<AuthUseCases> logger) : IAuthUseCases
{
    public async Task<AuthUseCaseResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var requestValidationErrors = AuthRequestValidation.ValidateRegister(request);
        if (requestValidationErrors.Count > 0)
        {
            return AuthUseCaseResult<RegisterResponse>.Failure(AuthErrorCode.ValidationFailed, requestValidationErrors);
        }

        var email = request.Email.Trim();
        var existingUser = await users.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return AuthUseCaseResult<RegisterResponse>.Failure(AuthErrorCode.DuplicateEmail, FieldError("email", "An account with this email address already exists."));
        }

        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = request.DisplayName.Trim() };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return AuthUseCaseResult<RegisterResponse>.Failure(AuthErrorCode.ValidationFailed, ToValidationDictionary(result));
        }

        await users.AddClaimAsync(user, new Claim(AuthorizationPolicies.PermissionClaimType, MindUnlocking.Domain.Identity.Permissions.ScholarAccess));
        var confirmationToken = await users.GenerateEmailConfirmationTokenAsync(user);
        return AuthUseCaseResult<RegisterResponse>.Success(new RegisterResponse(user.Id, user.Email!, confirmationToken));
    }

    public async Task<AuthUseCaseResult<object>> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return AuthUseCaseResult<object>.Failure(AuthErrorCode.UserNotFound);
        }

        var result = await users.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded
            ? AuthUseCaseResult<object>.Success(new object())
            : AuthUseCaseResult<object>.Failure(AuthErrorCode.ValidationFailed, ToValidationDictionary(result));
    }

    public async Task<AuthUseCaseResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
        {
            return AuthUseCaseResult<TokenResponse>.Failure(AuthErrorCode.InvalidCredentials);
        }

        if (!await users.IsEmailConfirmedAsync(user))
        {
            return AuthUseCaseResult<TokenResponse>.Failure(AuthErrorCode.EmailVerificationRequired);
        }

        var requiresMfa = user.AdministrativeMfaRequired || await users.GetTwoFactorEnabledAsync(user);
        if (requiresMfa && string.IsNullOrWhiteSpace(request.MfaCode))
        {
            return AuthUseCaseResult<TokenResponse>.Failure(AuthErrorCode.MfaRequired);
        }

        if (!string.IsNullOrWhiteSpace(request.MfaCode) && !await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.MfaCode))
        {
            return AuthUseCaseResult<TokenResponse>.Failure(AuthErrorCode.InvalidMfaCode);
        }

        return AuthUseCaseResult<TokenResponse>.Success(await IssueTokenResponse(user, cancellationToken));
    }

    public async Task<AuthUseCaseResult<TokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = tokens.HashRefreshToken(request.RefreshToken);
        var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (session is null)
        {
            return AuthUseCaseResult<TokenResponse>.Failure(AuthErrorCode.InvalidRefreshToken);
        }

        if (!session.IsActive(now))
        {
            await RevokeAllUserSessions(session.UserId, now, "refresh-token-reuse-detected", cancellationToken);
            logger.LogWarning("Refresh token reuse detected for user {UserId} and session {SessionId}", session.UserId, session.Id);
            return AuthUseCaseResult<TokenResponse>.Failure(AuthErrorCode.InvalidRefreshToken);
        }

        var user = await users.FindByIdAsync(session.UserId.ToString());
        if (user is null || !user.EmailConfirmed)
        {
            return AuthUseCaseResult<TokenResponse>.Failure(AuthErrorCode.InvalidRefreshToken);
        }

        session.RevokedUtc = now;
        session.RevocationReason = "rotated";
        var refreshToken = tokens.CreateRefreshToken(now);
        session.ReplacedByTokenHash = refreshToken.Hash;
        db.RefreshSessions.Add(new RefreshSession { UserId = user.Id, TokenHash = refreshToken.Hash, ExpiresUtc = refreshToken.ExpiresUtc });
        await db.SaveChangesAsync(cancellationToken);

        var accessToken = tokens.CreateAccessToken(user, await GetPermissions(user), now);
        return AuthUseCaseResult<TokenResponse>.Success(new TokenResponse(accessToken.Token, accessToken.ExpiresUtc, refreshToken.Token, refreshToken.ExpiresUtc));
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, bool includeResetToken, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return new ForgotPasswordResponse(null);
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        return new ForgotPasswordResponse(includeResetToken ? token : null);
    }

    public async Task<AuthUseCaseResult<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return AuthUseCaseResult<object>.Success(new object());
        }

        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (result.Succeeded)
        {
            await RevokeAllUserSessions(user.Id, DateTimeOffset.UtcNow, "password-reset", cancellationToken);
            return AuthUseCaseResult<object>.Success(new object());
        }

        return AuthUseCaseResult<object>.Failure(AuthErrorCode.ValidationFailed, ToValidationDictionary(result));
    }

    public async Task RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == tokens.HashRefreshToken(request.RefreshToken), cancellationToken);
        if (session is not null && session.RevokedUtc is null)
        {
            session.RevokedUtc = DateTimeOffset.UtcNow;
            session.RevocationReason = string.IsNullOrWhiteSpace(request.Reason) ? "revoked" : request.Reason.Trim();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task LogoutAsync(Guid userId, CancellationToken cancellationToken = default) =>
        RevokeAllUserSessions(userId, DateTimeOffset.UtcNow, "logout", cancellationToken);

    public async Task<AuthUseCaseResult<CurrentUserResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null
            ? AuthUseCaseResult<CurrentUserResponse>.Failure(AuthErrorCode.UserNotFound)
            : AuthUseCaseResult<CurrentUserResponse>.Success(new CurrentUserResponse(user.Id, user.Email ?? string.Empty, user.DisplayName, await GetPermissions(user), user.EmailConfirmed, user.TwoFactorEnabled));
    }

    private async Task<TokenResponse> IssueTokenResponse(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var accessToken = tokens.CreateAccessToken(user, await GetPermissions(user), now);
        var refreshToken = tokens.CreateRefreshToken(now);
        db.RefreshSessions.Add(new RefreshSession { UserId = user.Id, TokenHash = refreshToken.Hash, ExpiresUtc = refreshToken.ExpiresUtc });
        await db.SaveChangesAsync(cancellationToken);
        return new TokenResponse(accessToken.Token, accessToken.ExpiresUtc, refreshToken.Token, refreshToken.ExpiresUtc);
    }

    private async Task<string[]> GetPermissions(ApplicationUser user) =>
        (await users.GetClaimsAsync(user))
        .Where(claim => claim.Type == AuthorizationPolicies.PermissionClaimType)
        .Select(claim => claim.Value)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private async Task RevokeAllUserSessions(Guid userId, DateTimeOffset now, string reason, CancellationToken cancellationToken) =>
        await db.RefreshSessions
            .Where(session => session.UserId == userId && session.RevokedUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedUtc, now).SetProperty(session => session.RevocationReason, reason), cancellationToken);

    private static Dictionary<string, string[]> ToValidationDictionary(IdentityResult result) =>
        result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());

    private static IReadOnlyDictionary<string, string[]> FieldError(string field, string error) =>
        new Dictionary<string, string[]> { [field] = [error] };
}
