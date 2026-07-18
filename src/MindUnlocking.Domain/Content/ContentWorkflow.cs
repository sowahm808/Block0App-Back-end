namespace MindUnlocking.Domain.Content;

public enum ContentStatus { Draft, InReview, ChangesRequested, Approved, Published, Archived }

public sealed class ContentWorkflowException(string message) : InvalidOperationException(message);

public sealed class ContentWorkflowService
{
    public ContentStatus SubmitForReview(ContentStatus status) => status is ContentStatus.Draft or ContentStatus.ChangesRequested
        ? ContentStatus.InReview
        : throw new ContentWorkflowException("Only draft or changes-requested content can be submitted for review.");

    public ContentStatus Approve(ContentStatus status) => status == ContentStatus.InReview
        ? ContentStatus.Approved
        : throw new ContentWorkflowException("Only in-review content can be approved.");

    public ContentStatus Publish(ContentStatus status) => status == ContentStatus.Approved
        ? ContentStatus.Published
        : throw new ContentWorkflowException("Only approved content can be published.");

    public void EnsureEditable(ContentStatus status)
    {
        if (status is ContentStatus.InReview or ContentStatus.Published or ContentStatus.Archived)
        {
            throw new ContentWorkflowException("Content in this state is locked from ordinary editing; create a new version for corrections.");
        }
    }
}
