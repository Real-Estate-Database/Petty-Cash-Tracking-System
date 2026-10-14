namespace PettyCashDatabase.Shared.Auth;

public static class LoginSecurityPolicy
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static TimeSpan GetProgressiveDelay(int failedAttempt)
    {
        var boundedAttempt = Math.Clamp(failedAttempt, 1, MaxFailedAttempts);
        var milliseconds = Math.Min(2000, 1000 + ((boundedAttempt - 1) * 250));
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
