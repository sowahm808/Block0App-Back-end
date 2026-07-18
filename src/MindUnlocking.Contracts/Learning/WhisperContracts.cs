namespace MindUnlocking.Contracts.Learning;

public sealed record QuestionChoiceView(Guid ChoiceId, string Text, int DisplayOrder);
public sealed record W1ChallengeResponse(Guid QuestionAttemptId, string Stem, IReadOnlyCollection<QuestionChoiceView> AnswerChoices, IReadOnlyCollection<string> FiguresOrTables, int SequenceNumber, string AllowedResponseType, DateTimeOffset PresentedUtc, TimeSpan? TimeLimit, bool MarkedForReview);
public sealed record SubmitAnswerRequest(IReadOnlyCollection<Guid> SelectedChoiceIds, int ResponseDurationMs, Guid ContentVersionId, string IdempotencyKey);
public sealed record W2AnswerResponse(Guid QuestionAttemptId, IReadOnlyCollection<Guid> SelectedChoiceIds, IReadOnlyCollection<Guid> CorrectChoiceIds, string CorrectAnswerRationale, IReadOnlyDictionary<Guid, string> IncorrectChoiceRationales, bool IsCorrect, string? ApprovedReference);
public sealed record W3MemoryResponse(Guid QuestionAttemptId, string HighYieldFact, string MemoryPearl, string ClinicalRelevance, string ExamTrap, string? Mnemonic);
