using System.Net.Mail;
using MindUnlocking.Contracts.Auth;

namespace MindUnlocking.Application.Security;

public static class AuthRequestValidation
{
    public const int MinimumPasswordLength = 12;

    public static IReadOnlyDictionary<string, string[]> ValidateRegister(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }
        else if (!IsValidEmail(request.Email.Trim()))
        {
            errors["email"] = ["Enter a valid email address."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = ["Password is required."];
        }
        else if (request.Password.Length < MinimumPasswordLength)
        {
            errors["password"] = [$"Password must be at least {MinimumPasswordLength} characters."];
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["displayName"] = ["Display name is required."];
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
