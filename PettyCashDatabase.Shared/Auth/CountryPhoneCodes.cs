using System.Globalization;
using PhoneNumbers;

namespace PettyCashDatabase.Shared.Auth;

public sealed record CountryDialCode(
    string IsoCode,
    string CountryName,
    string DialCode,
    string FlagEmoji,
    string? Iso3Code = null)
{
    public string DisplayText => $"{FlagEmoji} {CountryName} ({DialCode})";
}

public static class CountryPhoneCatalog
{
    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    private static readonly Lazy<IReadOnlyList<CountryDialCode>> _all = new(LoadCatalog);

    public static IReadOnlyList<CountryDialCode> All => _all.Value;

    public static CountryDialCode Default =>
        All.FirstOrDefault(c => c.IsoCode.Equals("UG", StringComparison.OrdinalIgnoreCase)) ?? All[0];

    public static CountryDialCode? FindByDialCode(string dialCode) =>
        All.FirstOrDefault(c => c.DialCode.Equals(dialCode, StringComparison.OrdinalIgnoreCase));

    public static CountryDialCode? FindByIsoCode(string isoCode)
    {
        if (string.IsNullOrWhiteSpace(isoCode))
            return null;

        var trimmed = isoCode.Trim();
        return All.FirstOrDefault(c =>
            c.IsoCode.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
            (c.Iso3Code != null && c.Iso3Code.Equals(trimmed, StringComparison.OrdinalIgnoreCase)));
    }

    public static CountryDialCode? Find(string? isoCode, string? countryName)
    {
        if (!string.IsNullOrWhiteSpace(isoCode))
        {
            var byCode = FindByIsoCode(isoCode);
            if (byCode != null)
                return byCode;
        }

        if (!string.IsNullOrWhiteSpace(countryName))
        {
            var trimmedName = countryName.Trim();
            return All.FirstOrDefault(c => c.CountryName.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    public static bool IsValidNumber(string rawPhone, string defaultRegion = "UG")
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            return false;

        try
        {
            var parsed = PhoneUtil.Parse(rawPhone, defaultRegion);
            return PhoneUtil.IsValidNumber(parsed);
        }
        catch
        {
            return false;
        }
    }

    public static string FormatE164(string rawPhone, string defaultRegion = "UG")
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            return string.Empty;

        try
        {
            var parsed = PhoneUtil.Parse(rawPhone, defaultRegion);
            return PhoneUtil.Format(parsed, PhoneNumberFormat.E164);
        }
        catch
        {
            return rawPhone.Trim();
        }
    }

    private static IReadOnlyList<CountryDialCode> LoadCatalog()
    {
        var supportedRegions = PhoneUtil.GetSupportedRegions();
        var list = new List<CountryDialCode>(supportedRegions.Count);

        foreach (var regionCode in supportedRegions)
        {
            var countryCode = PhoneUtil.GetCountryCodeForRegion(regionCode);
            if (countryCode <= 0)
                continue;

            string countryName;
            string? iso3 = null;

            try
            {
                var regionInfo = new RegionInfo(regionCode);
                countryName = regionInfo.EnglishName;
                iso3 = regionInfo.ThreeLetterISORegionName;
            }
            catch
            {
                countryName = regionCode;
            }

            var flagEmoji = Iso2ToFlagEmoji(regionCode);
            var dialCode = $"+{countryCode}";

            list.Add(new CountryDialCode(
                IsoCode: regionCode,
                CountryName: countryName,
                DialCode: dialCode,
                FlagEmoji: flagEmoji,
                Iso3Code: iso3));
        }

        // Put Uganda first as preferred default for the current deployment, then order rest alphabetically by country name
        return list
            .OrderByDescending(c => c.IsoCode.Equals("UG", StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.CountryName)
            .ToList();
    }

    private static string Iso2ToFlagEmoji(string iso2)
    {
        if (string.IsNullOrWhiteSpace(iso2) || iso2.Length != 2)
            return string.Empty;

        var upper = iso2.ToUpperInvariant();
        if (upper[0] < 'A' || upper[0] > 'Z' || upper[1] < 'A' || upper[1] > 'Z')
            return string.Empty;

        var first = 0x1F1E6 + (upper[0] - 'A');
        var second = 0x1F1E6 + (upper[1] - 'A');
        return char.ConvertFromUtf32(first) + char.ConvertFromUtf32(second);
    }
}
