namespace MindUnlocking.Contracts.Readiness;

public sealed record ReadinessResponse(decimal AcademicScore, decimal EngagementScore, decimal CombinedScore, string Level, string FormulaVersion, string Disclaimer, DateTimeOffset CalculatedUtc);
