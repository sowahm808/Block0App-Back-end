namespace MindUnlocking.Domain.Identity;

public static class Permissions
{
    public const string ScholarAccess = "scholar.access";
    public const string MentorAccess = "mentor.access";
    public const string ReviewContent = "content.review";
    public const string ManageUsers = "users.manage";
    public const string ManageCohorts = "cohorts.manage";
    public const string ManageTeams = "teams.manage";
    public const string ManageContent = "content.manage";
    public const string ApproveContent = "content.approve";
    public const string PublishContent = "content.publish";
    public const string ManageReadiness = "readiness.manage";
    public const string ManageRaffles = "raffles.manage";
    public const string GenerateReports = "reports.generate";
    public const string ViewAuditLogs = "audit.view";
    public const string ManageSystemSettings = "system.manage";
}

public static class Roles
{
    public const string Scholar = "Scholar";
    public const string Mentor = "Mentor";
    public const string ContentReviewer = "ContentReviewer";
    public const string Administrator = "Administrator";
    public const string SuperAdministrator = "SuperAdministrator";
}
