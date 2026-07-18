namespace MindUnlocking.Domain.Readiness;

public enum ReadinessLevel { BuildingFoundation, Developing, Advancing, AlmostReady, BlockZeroReady }

public sealed record ReadinessWeights(decimal KnowledgeAccuracy, decimal ClinicalScenarios, decimal Completion, decimal Consistency, decimal RehearsalPerformance, decimal ConfidenceTrend, decimal Participation)
{
    public static ReadinessWeights Default => new(.35m, .25m, .15m, .10m, .05m, .05m, .05m);
}

public sealed record ReadinessInputs(decimal KnowledgeAccuracy, decimal ClinicalScenarioPerformance, decimal Completion, decimal Consistency, decimal RehearsalPerformance, decimal ConfidenceTrend, decimal Participation);

public sealed record ReadinessResult(decimal AcademicScore, decimal EngagementScore, decimal CombinedScore, ReadinessLevel Level, string Disclaimer);

public sealed class ReadinessEngine
{
    public const string Disclaimer = "This is a preparation indicator only. It does not guarantee exam success.";

    public ReadinessResult Calculate(ReadinessInputs inputs, ReadinessWeights weights)
    {
        var academic = WeightedAverage([inputs.KnowledgeAccuracy, inputs.ClinicalScenarioPerformance, inputs.Completion, inputs.RehearsalPerformance], [weights.KnowledgeAccuracy, weights.ClinicalScenarios, weights.Completion, weights.RehearsalPerformance]);
        var engagement = WeightedAverage([inputs.Consistency, inputs.ConfidenceTrend, inputs.Participation], [weights.Consistency, weights.ConfidenceTrend, weights.Participation]);
        var combined = Clamp(
            inputs.KnowledgeAccuracy * weights.KnowledgeAccuracy +
            inputs.ClinicalScenarioPerformance * weights.ClinicalScenarios +
            inputs.Completion * weights.Completion +
            inputs.Consistency * weights.Consistency +
            inputs.RehearsalPerformance * weights.RehearsalPerformance +
            inputs.ConfidenceTrend * weights.ConfidenceTrend +
            inputs.Participation * weights.Participation);
        return new ReadinessResult(academic, engagement, combined, ToLevel(academic, combined), Disclaimer);
    }

    private static decimal WeightedAverage(decimal[] values, decimal[] weights) => Clamp(values.Zip(weights, (value, weight) => value * weight).Sum() / weights.Sum());
    private static decimal Clamp(decimal value) => Math.Clamp(decimal.Round(value, 2), 0m, 100m);
    private static ReadinessLevel ToLevel(decimal academic, decimal combined) => academic < 60m ? ReadinessLevel.BuildingFoundation : combined switch
    {
        >= 90m => ReadinessLevel.BlockZeroReady,
        >= 80m => ReadinessLevel.AlmostReady,
        >= 70m => ReadinessLevel.Advancing,
        >= 60m => ReadinessLevel.Developing,
        _ => ReadinessLevel.BuildingFoundation
    };
}
