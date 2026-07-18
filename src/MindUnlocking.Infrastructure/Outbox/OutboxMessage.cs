namespace MindUnlocking.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
