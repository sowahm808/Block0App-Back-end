using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using MindUnlocking.Application.Options;
using MindUnlocking.Infrastructure.Identity;

namespace MindUnlocking.Infrastructure.Auth;

public sealed class FirebaseUserStore(FirestoreDb firestore, IOptions<FirebaseOptions> options)
{
    private readonly FirebaseOptions _options = options.Value;

    public DocumentReference UserDocument(string userId) => firestore.Collection(_options.UsersCollection).Document(userId);
    public CollectionReference RefreshSessions => firestore.Collection(_options.RefreshSessionsCollection);

    public async Task SaveUserAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await UserDocument(user.Id).SetAsync(new Dictionary<string, object?>
        {
            ["uid"] = user.Id,
            ["email"] = user.Email,
            ["displayName"] = user.DisplayName,
            ["emailVerified"] = user.EmailVerified,
            ["mfaEnabled"] = user.MfaEnabled,
            ["administrativeMfaRequired"] = user.AdministrativeMfaRequired,
            ["permissions"] = user.Permissions.ToArray(),
            ["createdUtc"] = Timestamp.FromDateTimeOffset(user.CreatedUtc)
        }, cancellationToken: cancellationToken);
    }

    public async Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var snapshot = await UserDocument(userId).GetSnapshotAsync(cancellationToken);
        return snapshot.Exists ? FromSnapshot(snapshot) : null;
    }

    public static ApplicationUser FromSnapshot(DocumentSnapshot snapshot)
    {
        var data = snapshot.ToDictionary();
        return new ApplicationUser
        {
            Id = snapshot.Id,
            Email = data.GetValueOrDefault("email")?.ToString() ?? string.Empty,
            DisplayName = data.GetValueOrDefault("displayName")?.ToString() ?? string.Empty,
            EmailVerified = data.TryGetValue("emailVerified", out var ev) && ev is bool b && b,
            MfaEnabled = data.TryGetValue("mfaEnabled", out var mfa) && mfa is bool mb && mb,
            AdministrativeMfaRequired = data.TryGetValue("administrativeMfaRequired", out var amfa) && amfa is bool ab && ab,
            Permissions = data.TryGetValue("permissions", out var permissions) && permissions is IEnumerable<object> values ? values.Select(x => x.ToString() ?? string.Empty).Where(x => x.Length > 0).ToArray() : []
        };
    }
}
