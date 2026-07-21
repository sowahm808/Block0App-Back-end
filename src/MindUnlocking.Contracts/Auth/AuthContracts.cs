namespace MindUnlocking.Contracts.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record RegisterResponse(string UserId, string Email, string EmailVerificationLink);
public sealed record VerifyEmailRequest(string Email, string Token);
public sealed record LoginRequest(string Email, string Password, string? MfaCode, string? FirebaseIdToken = null);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ForgotPasswordResponse(string? ResetLink);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record RevokeRefreshTokenRequest(string RefreshToken, string Reason);
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresUtc, string RefreshToken, DateTimeOffset RefreshExpiresUtc, string TokenType = "Bearer");
public sealed record CurrentUserResponse(string UserId, string Email, string DisplayName, IReadOnlyCollection<string> Permissions, bool EmailVerified, bool MfaEnabled);
