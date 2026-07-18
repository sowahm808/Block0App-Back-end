namespace MindUnlocking.Domain.Attempts;

public enum QuestionAttemptState
{
    NotStarted, ChallengePresented, Submitted, CorrectAnswerPresented, MemoryPearlPresented, Completed, Expired, Invalidated
}

public sealed class QuestionAttempt
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ScholarUserId { get; init; }
    public Guid QuestionId { get; init; }
    public Guid ContentVersionId { get; init; }
    public QuestionAttemptState State { get; private set; } = QuestionAttemptState.NotStarted;
    public DateTimeOffset? SubmittedUtc { get; private set; }
    public DateTimeOffset? CompletedUtc { get; private set; }
    public string? SubmissionIdempotencyKey { get; private set; }
    public IReadOnlyCollection<Guid> SelectedChoiceIds => _selectedChoiceIds.AsReadOnly();
    private readonly List<Guid> _selectedChoiceIds = [];

    public void PresentChallenge() => State = State == QuestionAttemptState.NotStarted ? QuestionAttemptState.ChallengePresented : State;

    public bool Submit(IReadOnlyCollection<Guid> selectedChoiceIds, string idempotencyKey, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (State is QuestionAttemptState.Submitted or QuestionAttemptState.CorrectAnswerPresented or QuestionAttemptState.MemoryPearlPresented or QuestionAttemptState.Completed)
        {
            return SubmissionIdempotencyKey == idempotencyKey;
        }
        if (State != QuestionAttemptState.ChallengePresented) throw new InvalidOperationException("Question must be presented before submission.");
        _selectedChoiceIds.Clear();
        _selectedChoiceIds.AddRange(selectedChoiceIds.Distinct());
        SubmissionIdempotencyKey = idempotencyKey;
        SubmittedUtc = nowUtc;
        State = QuestionAttemptState.Submitted;
        return true;
    }

    public void PresentCorrectAnswer()
    {
        if (State != QuestionAttemptState.Submitted) throw new InvalidOperationException("Submit before showing correct-answer rationale.");
        State = QuestionAttemptState.CorrectAnswerPresented;
    }

    public void AcknowledgeMemoryPearl(DateTimeOffset nowUtc)
    {
        if (State != QuestionAttemptState.CorrectAnswerPresented && State != QuestionAttemptState.MemoryPearlPresented) throw new InvalidOperationException("W2 must be presented before W3 acknowledgement.");
        State = QuestionAttemptState.Completed;
        CompletedUtc = nowUtc;
    }
}
