using MindUnlocking.Contracts.Auth;

namespace MindUnlocking.Application.Security;

public enum AuthErrorCode
{
    None,
    DuplicateEmail,
    InvalidCredentials,
    EmailVerificationRequired,
    MfaRequired,
    InvalidMfaCode,
    InvalidRefreshToken,
    UserNotFound,
    ValidationFailed
}

public sealed record AuthUseCaseResult<T>(
    bool Succeeded,
    T? Value,
    AuthErrorCode ErrorCode = AuthErrorCode.None,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public static AuthUseCaseResult<T> Success(T value) => new(true, value);

    public static AuthUseCaseResult<T> Failure(
        AuthErrorCode errorCode,
        IReadOnlyDictionary<string, string[]>? validationErrors = null) =>
        new(false, default, errorCode, validationErrors);
}

public interface IAuthUseCases
{
    Task<AuthUseCaseResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthUseCaseResult<object>> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default);
    Task<AuthUseCaseResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthUseCaseResult<TokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, bool includeResetToken, CancellationToken cancellationToken = default);
    Task<AuthUseCaseResult<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AuthUseCaseResult<CurrentUserResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
