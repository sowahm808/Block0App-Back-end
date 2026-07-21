using FluentAssertions;
using MindUnlocking.Application.Security;
using MindUnlocking.Contracts.Auth;

namespace MindUnlocking.UnitTests;

public sealed class AuthRequestValidationTests
{
    [Fact]
    public void Register_validation_returns_field_errors_for_blank_required_values()
    {
        var result = AuthRequestValidation.ValidateRegister(new RegisterRequest(null!, null!, null!));

        result.Should().ContainKey("email");
        result.Should().ContainKey("password");
        result.Should().ContainKey("displayName");
    }

    [Fact]
    public void Register_validation_returns_field_errors_for_invalid_email_and_short_password()
    {
        var result = AuthRequestValidation.ValidateRegister(
            new RegisterRequest("not-an-email", "short", "Michael Sowah"));

        result.Should().ContainKey("email")
            .WhoseValue.Should().Contain("Enter a valid email address.");
        result.Should().ContainKey("password")
            .WhoseValue.Should().Contain("Password must be at least 12 characters.");
    }

    [Fact]
    public void Register_validation_accepts_valid_registration_details()
    {
        var result = AuthRequestValidation.ValidateRegister(
            new RegisterRequest("sowahm@gmail.com", "twelve-chars", "Michael Sowah"));

        result.Should().BeEmpty();
    }
}
