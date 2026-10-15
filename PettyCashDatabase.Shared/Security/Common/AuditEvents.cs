namespace PettyCashDatabase.Shared.Auth;

public static class AuditEvents
{
    public const string EventTokenMarker = "EVENT";

    public const string Login = "Login";
    public const string TokenRefresh = "Token refresh";
    public const string LoginFailed = "LoginFailed";
    public const string Locked = "Locked";
    public const string Unlocked = "Unlocked";
    public const string TamperDetected = "TamperDetected";
    public const string RefreshReuseDetected = "RefreshReuseDetected";
    public const string PasswordResetRequested = "PasswordResetRequested";
    public const string PasswordReset = "PasswordReset";
    public const string TenantRegistered = "TenantRegistered";
    public const string Logout = "Logout";
}
