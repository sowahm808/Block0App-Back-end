using MindUnlocking.Domain.Identity;

namespace MindUnlocking.Application.Security;

public static class AuthorizationPolicies
{
    public const string PermissionClaimType = "permission";
    public static readonly string[] All = [Permissions.ScholarAccess, Permissions.MentorAccess, Permissions.ReviewContent, Permissions.ManageUsers, Permissions.ManageCohorts, Permissions.ManageTeams, Permissions.ManageContent, Permissions.ApproveContent, Permissions.PublishContent, Permissions.ManageReadiness, Permissions.ManageRaffles, Permissions.GenerateReports, Permissions.ViewAuditLogs, Permissions.ManageSystemSettings];
}
