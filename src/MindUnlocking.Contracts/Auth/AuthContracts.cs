namespace MindUnlocking.Contracts.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password, string? MfaCode);
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresUtc, string TokenType = "Bearer");
public sealed record CurrentUserResponse(Guid UserId, string Email, string DisplayName, IReadOnlyCollection<string> Permissions);
