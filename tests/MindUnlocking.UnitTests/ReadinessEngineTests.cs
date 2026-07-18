using FluentAssertions;
using MindUnlocking.Domain.Readiness;

namespace MindUnlocking.UnitTests;

public sealed class ReadinessEngineTests
{
    [Fact]
    public void Calculate_keeps_academic_and_engagement_components_separate()
    {
        var result = new ReadinessEngine().Calculate(new ReadinessInputs(50, 50, 50, 100, 50, 100, 100), ReadinessWeights.Default);
        result.AcademicScore.Should().BeLessThan(result.EngagementScore);
        result.Level.Should().Be(ReadinessLevel.BuildingFoundation);
        result.Disclaimer.Should().Be(ReadinessEngine.Disclaimer);
    }
}
