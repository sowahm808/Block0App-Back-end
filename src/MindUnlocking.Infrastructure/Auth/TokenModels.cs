namespace MindUnlocking.Infrastructure.Auth;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresUtc);
public sealed record RefreshTokenResult(string Token, string Hash, DateTimeOffset ExpiresUtc);
