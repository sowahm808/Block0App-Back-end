namespace MindUnlocking.Contracts.Common;

public sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, long TotalCount)
{
    public long TotalPages => PageSize <= 0 ? 0 : (long)Math.Ceiling(TotalCount / (double)PageSize);
}
