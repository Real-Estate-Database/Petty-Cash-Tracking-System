using System.Text.RegularExpressions;

namespace PettyCashDatabase.Shared.Auth;

public static partial class ContactNormalizer
{
    private const string DefaultCountryCode = "+256";

    public static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    public static string NormalizeTelephone(string telephone, string? defaultCountryCode = null)
    {
        var cleaned = SpacesAndDashesRegex().Replace(telephone.Trim(), string.Empty);

        if (cleaned.StartsWith("00", StringComparison.Ordinal))
            cleaned = "+" + cleaned[2..];

        if (cleaned.StartsWith('+'))
            return cleaned;

        if (cleaned.StartsWith('0'))
            cleaned = cleaned[1..];

        var countryCode = string.IsNullOrWhiteSpace(defaultCountryCode)
            ? DefaultCountryCode
            : defaultCountryCode.Trim();

        if (!countryCode.StartsWith('+'))
            countryCode = "+" + countryCode;

        return countryCode + cleaned;
    }

    public static bool LooksLikeEmail(string identifier) =>
        identifier.Contains('@', StringComparison.Ordinal);

    [GeneratedRegex(@"[\s\-]+")]
    private static partial Regex SpacesAndDashesRegex();
}
