using System.Text.Json;
using FluentAssertions;
using MindUnlocking.Contracts.Learning;

namespace MindUnlocking.UnitTests;

public sealed class W1ContractLeakageTests
{
    [Fact]
    public void W1_contract_does_not_serialize_correct_answer_fields()
    {
        var json = JsonSerializer.Serialize(new W1ChallengeResponse(Guid.NewGuid(), "Stem", [new QuestionChoiceView(Guid.NewGuid(), "A", 1)], [], 1, "SingleChoice", DateTimeOffset.UtcNow, null, false));
        json.Should().NotContain("correctAnswer").And.NotContain("isCorrect").And.NotContain("rationale").And.NotContain("correctChoiceId").And.NotContain("answerKey").And.NotContain("memoryPearl");
    }
}
