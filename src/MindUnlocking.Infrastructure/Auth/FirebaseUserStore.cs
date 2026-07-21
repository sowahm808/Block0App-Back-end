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
        var now = DateTimeOffset.UtcNow;
        var document = FirestoreApplicationUserDocument.FromApplicationUser(user, now);

        await UserDocument(user.Id).SetAsync(document, SetOptions.MergeAll, cancellationToken);
    }

    public async Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var snapshot = await UserDocument(userId).GetSnapshotAsync(cancellationToken);
        return snapshot.Exists ? FromSnapshot(snapshot) : null;
    }

    public static ApplicationUser FromSnapshot(DocumentSnapshot snapshot)
    {
        var document = snapshot.ConvertTo<FirestoreApplicationUserDocument>();
        return document.ToApplicationUser(snapshot.Id);
    }
}

[FirestoreData]
public sealed class FirestoreApplicationUserDocument
{
    [FirestoreProperty("uid")]
    public string Uid { get; set; } = string.Empty;

    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [FirestoreProperty("emailVerified")]
    public bool EmailVerified { get; set; }

    [FirestoreProperty("mfaEnabled")]
    public bool MfaEnabled { get; set; }

    [FirestoreProperty("administrativeMfaRequired")]
    public bool AdministrativeMfaRequired { get; set; }

    [FirestoreProperty("permissions")]
    public List<string> Permissions { get; set; } = [];

    [FirestoreProperty("createdUtc")]
    public Timestamp CreatedUtc { get; set; }

    [FirestoreProperty("updatedUtc")]
    public Timestamp UpdatedUtc { get; set; }

    public static FirestoreApplicationUserDocument FromApplicationUser(ApplicationUser user, DateTimeOffset updatedUtc) =>
        new()
        {
            Uid = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            EmailVerified = user.EmailVerified,
            MfaEnabled = user.MfaEnabled,
            AdministrativeMfaRequired = user.AdministrativeMfaRequired,
            Permissions = user.Permissions.ToList(),
            CreatedUtc = Timestamp.FromDateTimeOffset(user.CreatedUtc),
            UpdatedUtc = Timestamp.FromDateTimeOffset(updatedUtc)
        };

    public ApplicationUser ToApplicationUser(string documentId) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(Uid) ? documentId : Uid,
            Email = Email,
            DisplayName = DisplayName,
            EmailVerified = EmailVerified,
            MfaEnabled = MfaEnabled,
            AdministrativeMfaRequired = AdministrativeMfaRequired,
            CreatedUtc = new DateTimeOffset(CreatedUtc.ToDateTime(), TimeSpan.Zero),
            Permissions = Permissions.Where(permission => !string.IsNullOrWhiteSpace(permission)).ToArray()
        };
}
