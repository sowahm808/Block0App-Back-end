using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MindUnlocking.Application.Options;
using MindUnlocking.Application.Security;
using AppAuthErrorCode = MindUnlocking.Application.Security.AuthErrorCode;
using MindUnlocking.Contracts.Auth;
using MindUnlocking.Domain.Identity;
using MindUnlocking.Infrastructure.Identity;

namespace MindUnlocking.Infrastructure.Auth;

public sealed class AuthUseCases(FirebaseApp app, FirebaseUserStore store, AuthTokenService tokens, IOptions<FirebaseOptions> options, ILogger<AuthUseCases> logger) : IAuthUseCases
{
    private readonly FirebaseAuth _firebaseAuth = FirebaseAuth.GetAuth(app);
    private readonly ActionCodeSettings _actionCodeSettings = new()
    {
        Url = options.Value.ActionCodeUrl
    };
    public async Task<AuthUseCaseResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var errors = AuthRequestValidation.ValidateRegister(request);
        if (errors.Count > 0) return AuthUseCaseResult<RegisterResponse>.Failure(AppAuthErrorCode.ValidationFailed, errors);

        var email = request.Email.Trim();
        try
        {
            var record = await _firebaseAuth.CreateUserAsync(new UserRecordArgs
            {
                Email = email,
                Password = request.Password,
                DisplayName = request.DisplayName.Trim(),
                EmailVerified = false
            }, cancellationToken);

            var user = new ApplicationUser { Id = record.Uid, Email = email, DisplayName = request.DisplayName.Trim(), Permissions = [Permissions.ScholarAccess] };
            await store.SaveUserAsync(user, cancellationToken);
            await _firebaseAuth.SetCustomUserClaimsAsync(record.Uid, new Dictionary<string, object> { ["permissions"] = user.Permissions.ToArray(), ["role"] = "scholar" }, cancellationToken);
            var link = await _firebaseAuth.GenerateEmailVerificationLinkAsync(email, _actionCodeSettings, cancellationToken);
            logger.LogInformation("Created Firebase user {UserId} and generated email verification link.", record.Uid);
            return AuthUseCaseResult<RegisterResponse>.Success(new RegisterResponse(record.Uid, email, link));
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == FirebaseAdmin.Auth.AuthErrorCode.EmailAlreadyExists)
        {
            return AuthUseCaseResult<RegisterResponse>.Failure(AppAuthErrorCode.DuplicateEmail, new Dictionary<string, string[]> { ["email"] = ["An account with this email address already exists."] });
        }
    }

    public async Task<AuthUseCaseResult<object>> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var decoded = await _firebaseAuth.VerifyIdTokenAsync(request.Token, cancellationToken);
        var user = await _firebaseAuth.GetUserAsync(decoded.Uid, cancellationToken);
        if (!string.Equals(user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase)) return AuthUseCaseResult<object>.Failure(AppAuthErrorCode.UserNotFound);
        await store.UserDocument(decoded.Uid).UpdateAsync("emailVerified", user.EmailVerified, cancellationToken: cancellationToken);
        return AuthUseCaseResult<object>.Success(new object());
    }

    public async Task<AuthUseCaseResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FirebaseIdToken)) return AuthUseCaseResult<TokenResponse>.Failure(AppAuthErrorCode.InvalidCredentials);
        var decoded = await _firebaseAuth.VerifyIdTokenAsync(request.FirebaseIdToken, cancellationToken);
        var firebaseUser = await _firebaseAuth.GetUserAsync(decoded.Uid, cancellationToken);
        if (!firebaseUser.EmailVerified) return AuthUseCaseResult<TokenResponse>.Failure(AppAuthErrorCode.EmailVerificationRequired);
        var user = await store.GetUserAsync(decoded.Uid, cancellationToken) ?? new ApplicationUser { Id = decoded.Uid, Email = firebaseUser.Email, DisplayName = firebaseUser.DisplayName, EmailVerified = firebaseUser.EmailVerified, Permissions = [Permissions.ScholarAccess] };
        user.EmailVerified = firebaseUser.EmailVerified;
        await store.SaveUserAsync(user, cancellationToken);
        return AuthUseCaseResult<TokenResponse>.Success(await IssueTokenResponse(user, cancellationToken));
    }

    public async Task<AuthUseCaseResult<TokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var query = await store.RefreshSessions.WhereEqualTo("tokenHash", hash).Limit(1).GetSnapshotAsync(cancellationToken);
        var doc = query.Documents.SingleOrDefault();
        if (doc is null) return AuthUseCaseResult<TokenResponse>.Failure(AppAuthErrorCode.InvalidRefreshToken);
        var session = ToSession(doc);
        if (!session.IsActive(DateTimeOffset.UtcNow)) return AuthUseCaseResult<TokenResponse>.Failure(AppAuthErrorCode.InvalidRefreshToken);
        var user = await store.GetUserAsync(session.UserId, cancellationToken);
        if (user is null) return AuthUseCaseResult<TokenResponse>.Failure(AppAuthErrorCode.InvalidRefreshToken);
        await doc.Reference.UpdateAsync(new Dictionary<string, object?> { ["revokedUtc"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow), ["revocationReason"] = "rotated" }, cancellationToken: cancellationToken);
        return AuthUseCaseResult<TokenResponse>.Success(await IssueTokenResponse(user, cancellationToken));
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, bool includeResetToken, CancellationToken cancellationToken = default)
    {
        var link = await _firebaseAuth.GeneratePasswordResetLinkAsync(request.Email.Trim(), _actionCodeSettings, cancellationToken);
        return new ForgotPasswordResponse(includeResetToken ? link : null);
    }

    public Task<AuthUseCaseResult<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthUseCaseResult<object>.Failure(AppAuthErrorCode.ValidationFailed, new Dictionary<string, string[]> { ["token"] = ["Password resets are completed with the Firebase password reset link."] }));

    public async Task RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var query = await store.RefreshSessions.WhereEqualTo("tokenHash", hash).Limit(1).GetSnapshotAsync(cancellationToken);
        foreach (var doc in query.Documents) await doc.Reference.UpdateAsync("revokedUtc", Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow), cancellationToken: cancellationToken);
    }

    public async Task LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        var query = await store.RefreshSessions.WhereEqualTo("userId", userId).WhereEqualTo("revokedUtc", null).GetSnapshotAsync(cancellationToken);
        foreach (var doc in query.Documents) await doc.Reference.UpdateAsync("revokedUtc", Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow), cancellationToken: cancellationToken);
    }

    public async Task<AuthUseCaseResult<CurrentUserResponse>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await store.GetUserAsync(userId, cancellationToken);
        return user is null ? AuthUseCaseResult<CurrentUserResponse>.Failure(AppAuthErrorCode.UserNotFound) : AuthUseCaseResult<CurrentUserResponse>.Success(new CurrentUserResponse(user.Id, user.Email, user.DisplayName, user.Permissions, user.EmailVerified, user.MfaEnabled));
    }

    private async Task<TokenResponse> IssueTokenResponse(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var access = await tokens.CreateAccessToken(user, user.Permissions, now);
        var refresh = tokens.CreateRefreshToken(now);
        await store.RefreshSessions.Document().SetAsync(new Dictionary<string, object?> { ["userId"] = user.Id, ["tokenHash"] = refresh.Hash, ["createdUtc"] = Timestamp.FromDateTimeOffset(now), ["expiresUtc"] = Timestamp.FromDateTimeOffset(refresh.ExpiresUtc), ["revokedUtc"] = null }, cancellationToken: cancellationToken);
        return new TokenResponse(access.Token, access.ExpiresUtc, refresh.Token, refresh.ExpiresUtc);
    }

    private static RefreshSession ToSession(DocumentSnapshot doc)
    {
        var data = doc.ToDictionary();
        return new RefreshSession { Id = doc.Id, UserId = data["userId"].ToString()!, TokenHash = data["tokenHash"].ToString()!, ExpiresUtc = new DateTimeOffset(((Timestamp)data["expiresUtc"]).ToDateTime(), TimeSpan.Zero), RevokedUtc = data.TryGetValue("revokedUtc", out var revoked) && revoked is Timestamp ts ? new DateTimeOffset(ts.ToDateTime(), TimeSpan.Zero) : null };
    }
}
