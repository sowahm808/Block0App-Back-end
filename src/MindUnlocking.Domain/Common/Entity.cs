namespace MindUnlocking.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public DateTimeOffset CreatedUtc { get; protected init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; protected set; }
    public byte[] RowVersion { get; private set; } = [];
}
