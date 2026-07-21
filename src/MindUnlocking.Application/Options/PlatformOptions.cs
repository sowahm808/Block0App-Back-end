using System.ComponentModel.DataAnnotations;

namespace MindUnlocking.Application.Options;

public sealed class FirebaseOptions
{
    [Required]
    public string ProjectId { get; init; } = string.Empty;

    public string? ServiceAccountPath { get; init; }
    public string UsersCollection { get; init; } = "users";
    public string RefreshSessionsCollection { get; init; } = "refreshSessions";
}

public sealed class RedisOptions { public string ConnectionString { get; init; } = string.Empty; public bool RequiredForReadiness { get; init; } }
public sealed class BlobStorageOptions { public string AccountName { get; init; } = string.Empty; public string Endpoint { get; init; } = string.Empty; public string CertificateContainer { get; init; } = "certificates"; }
public sealed class ServiceBusOptions { public string Namespace { get; init; } = string.Empty; public string OutboxQueue { get; init; } = "outbox"; }
public sealed class AzureOpenAIOptions { public string Endpoint { get; init; } = string.Empty; public string DeploymentName { get; init; } = string.Empty; public string PromptTemplateVersion { get; init; } = "v1"; }
public sealed class AzureAISearchOptions { public string Endpoint { get; init; } = string.Empty; public string IndexName { get; init; } = "approved-content"; }
public sealed class NotificationOptions { public int QuietHoursStart { get; init; } = 21; public int QuietHoursEnd { get; init; } = 7; }
public sealed class CertificateOptions { public int RequiredKnowledgeQuestions { get; init; } = 800; public int RequiredScenarios { get; init; } = 130; }
public sealed class RaffleOptions { public bool RedrawRequiresElevatedPermission { get; init; } = true; }
public sealed class ChallengeOptions { public int DefaultTeamMinSize { get; init; } = 3; public int DefaultTeamMaxSize { get; init; } = 4; public int DurationDays { get; init; } = 21; }
