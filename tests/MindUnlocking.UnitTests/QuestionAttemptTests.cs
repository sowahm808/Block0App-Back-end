using FluentAssertions;
using MindUnlocking.Domain.Attempts;

namespace MindUnlocking.UnitTests;

public sealed class QuestionAttemptTests
{
    [Fact]
    public void Submitted_answer_cannot_be_modified_by_different_idempotency_key()
    {
        var attempt = new QuestionAttempt { ScholarUserId = Guid.NewGuid(), QuestionId = Guid.NewGuid(), ContentVersionId = Guid.NewGuid() };
        attempt.PresentChallenge();
        attempt.Submit([Guid.NewGuid()], "first", DateTimeOffset.UtcNow).Should().BeTrue();
        attempt.Submit([Guid.NewGuid()], "second", DateTimeOffset.UtcNow).Should().BeFalse();
    }
}
