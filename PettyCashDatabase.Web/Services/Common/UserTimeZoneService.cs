using PettyCashDatabase.Shared.Auth;

namespace PettyCashDatabase.Web.Services;

/// <summary>
/// Service contract for converting timestamps and managing timezone offsets across the application.
/// </summary>
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
    /// <summary>
    /// Calculates the current local time for the specified timezone ID.
    /// </summary>
    public DateTime GetCurrentTime(string? timeZoneId, short fallbackOffsetHours = 3)
    {
        // 1. Resolve TimeZoneInfo via catalog or fallback offset hours
        var tz = TimeZoneCatalog.Resolve(timeZoneId, fallbackOffsetHours);

        // 2. Convert current UTC timestamp to resolved local timezone
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }

    /// <summary>
    /// Converts an existing UTC timestamp into local timezone time.
    /// </summary>
    public DateTime ConvertFromUtc(DateTime utc, string? timeZoneId, short fallbackOffsetHours = 3)
    {
        // 1. Ensure timestamp has explicit Utc DateTimeKind
        var utcValue = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        // 2. Resolve target timezone
        var tz = TimeZoneCatalog.Resolve(timeZoneId, fallbackOffsetHours);

        // 3. Convert UTC timestamp to local target timezone
        return TimeZoneInfo.ConvertTimeFromUtc(utcValue, tz);
    }

    /// <summary>
    /// Converts an existing local timestamp into UTC.
    /// </summary>
    public DateTime ConvertToUtc(DateTime local, string? timeZoneId, short fallbackOffsetHours = 3)
    {
        // 1. Resolve source timezone
        var tz = TimeZoneCatalog.Resolve(timeZoneId, fallbackOffsetHours);

        // 2. Treat input as unspecified kind before UTC conversion
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // 3. Convert local timestamp to UTC
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

    /// <summary>
    /// Gets the UTC offset TimeSpan for the specified timezone ID.
    /// </summary>
    public TimeSpan GetOffset(string? timeZoneId, short fallbackOffsetHours = 3)
    {
        // 1. Resolve target timezone
        var tz = TimeZoneCatalog.Resolve(timeZoneId, fallbackOffsetHours);

        // 2. Compute current UTC offset
        return tz.GetUtcOffset(DateTime.UtcNow);
    }
}
