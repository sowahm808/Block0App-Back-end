using FluentAssertions;
using MindUnlocking.Domain.Identity;
using MindUnlocking.Infrastructure.Auth;
using MindUnlocking.Infrastructure.Identity;

namespace MindUnlocking.UnitTests;

public sealed class FirebaseUserStoreTests
{
    [Fact]
    public void Firestore_user_document_round_trips_application_user_fields()
    {
        var createdUtc = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var updatedUtc = createdUtc.AddMinutes(5);
        var user = new ApplicationUser
        {
            Id = "firebase-uid-123",
            Email = "scholar@example.com",
            DisplayName = "Example Scholar",
            EmailVerified = true,
            MfaEnabled = true,
            AdministrativeMfaRequired = false,
            CreatedUtc = createdUtc,
            Permissions = [Permissions.ScholarAccess, Permissions.ManageUsers]
        };

        var document = FirestoreApplicationUserDocument.FromApplicationUser(user, updatedUtc);
        var roundTripped = document.ToApplicationUser("fallback-id");

        document.Uid.Should().Be(user.Id);
        document.UpdatedUtc.ToDateTimeOffset().Should().Be(updatedUtc);
        roundTripped.Should().BeEquivalentTo(user);
    }

    [Fact]
    public void Firestore_user_document_uses_document_id_when_uid_is_missing()
    {
        var document = new FirestoreApplicationUserDocument
        {
            Uid = string.Empty,
            Email = "legacy@example.com",
            DisplayName = "Legacy Scholar",
            Permissions = [Permissions.ScholarAccess]
        };

        var user = document.ToApplicationUser("legacy-doc-id");

        user.Id.Should().Be("legacy-doc-id");
        user.Permissions.Should().Equal(Permissions.ScholarAccess);
    }
}
