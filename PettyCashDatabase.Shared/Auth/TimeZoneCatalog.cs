namespace PettyCashDatabase.Shared.Auth;

public sealed record SystemTimeZoneEntry(
    string Id,
    short OffsetHours,
    string OffsetFormatted,
    string DisplayName,
    string RegionLabel)
{
    public string FullLabel => $"{OffsetFormatted} — {RegionLabel}";
}

public static class TimeZoneCatalog
{
    private static readonly Lazy<IReadOnlyList<SystemTimeZoneEntry>> _timeZones = new(LoadSystemTimeZones);

    public static IReadOnlyList<SystemTimeZoneEntry> All => _timeZones.Value;

    public static SystemTimeZoneEntry Default =>
        All.FirstOrDefault(tz =>
                tz.Id.Equals("E. Africa Standard Time", StringComparison.OrdinalIgnoreCase)
                || tz.Id.Equals("Africa/Nairobi", StringComparison.OrdinalIgnoreCase)
                || tz.Id.Equals("Africa/Kampala", StringComparison.OrdinalIgnoreCase)
                || tz.RegionLabel.Contains("Nairobi", StringComparison.OrdinalIgnoreCase)
                || tz.RegionLabel.Contains("Kampala", StringComparison.OrdinalIgnoreCase))
        ?? All.FirstOrDefault(tz => tz.OffsetHours == 3)
        ?? new("Africa/Kampala", 3, "UTC+03:00", "(UTC+03:00) East Africa Time", "Kampala, Nairobi, Addis Ababa");

    public static TimeZoneInfo Resolve(string? timeZoneId, short fallbackOffsetHours = 3)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // The ID may have been created on a different operating system.
            }
            catch (InvalidTimeZoneException)
            {
                // Fall back to the legacy fixed offset when the stored zone is invalid.
            }

            // The fallback catalog uses compact IDs such as UTC+5.5. They are
            // valid application IDs but are not registered with the OS.
            if (TryParseFixedOffsetId(timeZoneId, out var catalogOffset))
                return CreateFixedOffsetTimeZone(catalogOffset);
        }

        return CreateFixedOffsetTimeZone(TimeSpan.FromHours(fallbackOffsetHours));
    }

    public static short GetOffsetHours(string? timeZoneId, short fallbackOffsetHours = 3) =>
        (short)Math.Round(Resolve(timeZoneId, fallbackOffsetHours).GetUtcOffset(DateTime.UtcNow).TotalHours);

    private static TimeZoneInfo CreateFixedOffsetTimeZone(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var hours = Math.Abs(offset.TotalHours);
        var id = $"UTC{sign}{hours:0.##}";
        return TimeZoneInfo.CreateCustomTimeZone(id, offset, id, id);
    }

    private static bool TryParseFixedOffsetId(string timeZoneId, out TimeSpan offset)
    {
        offset = default;
        var value = timeZoneId.Trim();
        if (!value.StartsWith("UTC", StringComparison.OrdinalIgnoreCase))
            return false;

        var numericPart = value[3..];
        if (!double.TryParse(
                numericPart,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var hours)
            || hours is < -14 or > 14)
            return false;

        offset = TimeSpan.FromHours(hours);
        return true;
    }

    private static IReadOnlyList<SystemTimeZoneEntry> LoadSystemTimeZones()
    {
        try
        {
            var systemTzs = TimeZoneInfo.GetSystemTimeZones();
            var list = new List<SystemTimeZoneEntry>();

            foreach (var tz in systemTzs)
            {
                var offset = tz.BaseUtcOffset;
                var hours = (short)Math.Round(offset.TotalHours);
                var sign = offset < TimeSpan.Zero ? "-" : "+";
                var offsetFormatted = $"UTC{sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";

                // Clean up the Windows/IANA display names into user-friendly region label
                var displayName = tz.DisplayName;
                var regionLabel = displayName;
                var closingParenIndex = displayName.IndexOf(')');
                if (closingParenIndex >= 0 && closingParenIndex < displayName.Length - 1)
                {
                    regionLabel = displayName[(closingParenIndex + 1)..].Trim();
                }

                list.Add(new SystemTimeZoneEntry(
                    tz.Id,
                    hours,
                    offsetFormatted,
                    displayName,
                    regionLabel));
            }

            return list.OrderBy(tz => tz.OffsetHours).ThenBy(tz => tz.RegionLabel).ToList();
        }
        catch
        {
            // Fallback list if system timezones are inaccessible in constrained WASM sandbox
            return
            [
                new("UTC-8", -8, "UTC-08:00", "(UTC-08:00) Pacific Time", "Pacific Time (US & Canada)"),
                new("UTC-5", -5, "UTC-05:00", "(UTC-05:00) Eastern Time", "Eastern Time (US & Canada)"),
                new("UTC+0", 0, "UTC+00:00", "(UTC+00:00) GMT / London", "Greenwich Mean Time (London, Dublin)"),
                new("UTC+1", 1, "UTC+01:00", "(UTC+01:00) Central European Time", "Central European Time (Paris, Berlin)"),
                new("UTC+2", 2, "UTC+02:00", "(UTC+02:00) Central Africa / Cairo", "Cairo, Johannesburg, Harare"),
                new("UTC+3", 3, "UTC+03:00", "(UTC+03:00) East Africa Time", "East Africa Time (Kampala, Nairobi, Addis Ababa)"),
                new("UTC+4", 4, "UTC+04:00", "(UTC+04:00) Gulf Standard Time", "Dubai, Abu Dhabi, Muscat"),
                new("UTC+5", 5, "UTC+05:00", "(UTC+05:00) Pakistan / Karachi", "Islamabad, Karachi, Tashkent"),
                new("UTC+5.5", 5, "UTC+05:30", "(UTC+05:30) India Standard Time", "New Delhi, Mumbai, Kolkata"),
                new("UTC+8", 8, "UTC+08:00", "(UTC+08:00) China / Singapore", "Beijing, Singapore, Hong Kong"),
                new("UTC+9", 9, "UTC+09:00", "(UTC+09:00) Japan Standard Time", "Tokyo, Seoul")
            ];
        }
    }
}
