using PettyCashDatabase.Shared.Auth;

namespace PettyCashDatabase.Web.Services;

public interface IUserTimeZoneService
{
    /// <summary>
    /// Returns the current date and time with hours added or removed based on the given timeZoneId (or fallback offset hours).
    /// </summary>
    DateTime GetCurrentTime(string? timeZoneId, short fallbackOffsetHours = 3);

    /// <summary>
    /// Converts a UTC timestamp into the local time for the given timeZoneId (or fallback offset hours).
    /// </summary>
    DateTime ConvertFromUtc(DateTime utc, string? timeZoneId, short fallbackOffsetHours = 3);

    /// <summary>
    /// Converts a local timestamp into UTC for the given timeZoneId (or fallback offset hours).
    /// </summary>
    DateTime ConvertToUtc(DateTime local, string? timeZoneId, short fallbackOffsetHours = 3);

    /// <summary>
    /// Gets the UTC offset for the given timeZoneId (or fallback offset hours).
    /// </summary>
    TimeSpan GetOffset(string? timeZoneId, short fallbackOffsetHours = 3);
}

/// <summary>
/// Handles timezone conversions so the system knows when to add or remove hours based on the selected timezone.
/// </summary>
public sealed class UserTimeZoneService : IUserTimeZoneService
{
    public DateTime GetCurrentTime(string? timeZoneId, short fallbackOffsetHours = 3)
    {
        var tz = TimeZoneCatalog.Resolve(timeZoneId, fallbackOffsetHours);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }

    public DateTime ConvertFromUtc(DateTime utc, string? timeZoneId, short fallbackOffsetHours = 3)
    {
        var utcValue = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var tz = TimeZoneCatalog.Resolve(timeZoneId, fallbackOffsetHours);
        return TimeZoneInfo.ConvertTimeFromUtc(utcValue, tz);
    }

    public DateTime ConvertToUtc(DateTime local, string? timeZoneId, short fallbackOffsetHours = 3)
    {
        var tz = TimeZoneCatalog.Resolve(timeZoneId, fallbackOffsetHours);
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

    public TimeSpan GetOffset(string? timeZoneId, short fallbackOffsetHours = 3)
    {
        var tz = TimeZoneCatalog.Resolve(timeZoneId, fallbackOffsetHours);
        return tz.GetUtcOffset(DateTime.UtcNow);
    }
}
