using System.Text.RegularExpressions;

namespace SchoolManagement.Application.Common.Security;

public static class PasswordPolicyValidator
{
    private static readonly Regex HasUpperChar = new(@"[A-Z]");
    private static readonly Regex HasLowerChar = new(@"[a-z]");
    private static readonly Regex HasDigit = new(@"[0-9]");
    private static readonly Regex HasSpecialChar = new(@"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]");

    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    public static (bool IsValid, string? ErrorMessage) Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Password cannot be empty.");

        if (password.Length < MinimumLength)
            return (false, $"Password must be at least {MinimumLength} characters long.");

        if (password.Length > MaximumLength)
            return (false, $"Password cannot exceed {MaximumLength} characters.");

        if (!HasUpperChar.IsMatch(password))
            return (false, "Password must contain at least one uppercase letter (A-Z).");

        if (!HasLowerChar.IsMatch(password))
            return (false, "Password must contain at least one lowercase letter (a-z).");

        if (!HasDigit.IsMatch(password))
            return (false, "Password must contain at least one digit (0-9).");

        if (!HasSpecialChar.IsMatch(password))
            return (false, "Password must contain at least one special character (!@#$%^&*...).");

        return (true, null);
    }
}
