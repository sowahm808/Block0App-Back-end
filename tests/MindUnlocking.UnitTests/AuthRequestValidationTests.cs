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
}
