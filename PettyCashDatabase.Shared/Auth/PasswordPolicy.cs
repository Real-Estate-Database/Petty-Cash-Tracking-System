using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PettyCashDatabase.Shared.Auth;

public static partial class PasswordPolicy
{
    public const int MinLength = 12;
    public const int StrongBonusLength = 16;

    public static bool HasMinLength(string? password) =>
        !string.IsNullOrEmpty(password) && password.Length >= MinLength;

    public static bool HasUpper(string? password) =>
        !string.IsNullOrEmpty(password) && password.Any(char.IsUpper);

    public static bool HasLower(string? password) =>
        !string.IsNullOrEmpty(password) && password.Any(char.IsLower);

    public static bool HasDigit(string? password) =>
        !string.IsNullOrEmpty(password) && password.Any(char.IsDigit);

    public static bool HasSpecial(string? password) =>
        !string.IsNullOrEmpty(password) && SpecialCharRegex().IsMatch(password);

    public static bool ContainsEmailLocalPart(string? password, string? email)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            return false;

        var at = email.IndexOf('@');
        if (at <= 0)
            return false;

        var local = email[..at].Trim();
        return local.Length > 0
            && password.Contains(local, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> GetUnmetRules(string? password, string? email = null)
    {
        var unmet = new List<string>();
        if (!HasMinLength(password)) unmet.Add($"At least {MinLength} characters");
        if (!HasUpper(password)) unmet.Add("One uppercase letter");
        if (!HasLower(password)) unmet.Add("One lowercase letter");
        if (!HasDigit(password)) unmet.Add("One digit");
        if (!HasSpecial(password)) unmet.Add("One special character");
        if (ContainsEmailLocalPart(password, email)) unmet.Add("Must not contain your email name");
        return unmet;
    }

    public static bool IsValid(string? password, string? email = null) =>
        GetUnmetRules(password, email).Count == 0;

    /// <summary>0=Weak, 1=Fair, 2=Good, 3=Strong.</summary>
    public static int Score(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return 0;

        var points = 0;
        if (HasMinLength(password)) points++;
        if (HasUpper(password)) points++;
        if (HasLower(password)) points++;
        if (HasDigit(password)) points++;
        if (HasSpecial(password)) points++;
        if (password.Length >= StrongBonusLength) points++;

        return points switch
        {
            <= 2 => 0,
            3 => 1,
            4 or 5 => 2,
            _ => 3
        };
    }

    public static string LevelLabel(int score) => score switch
    {
        0 => "Weak",
        1 => "Fair",
        2 => "Good",
        _ => "Strong"
    };

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex SpecialCharRegex();
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PasswordPolicyAttribute : ValidationAttribute
{
    public string? EmailProperty { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var password = value as string;
        string? email = null;

        if (!string.IsNullOrWhiteSpace(EmailProperty))
        {
            var prop = validationContext.ObjectType.GetProperty(EmailProperty);
            email = prop?.GetValue(validationContext.ObjectInstance) as string;
        }

        var unmet = PasswordPolicy.GetUnmetRules(password, email);
        if (unmet.Count == 0)
            return ValidationResult.Success;

        return new ValidationResult($"Password requirements not met: {string.Join("; ", unmet)}");
    }
}
