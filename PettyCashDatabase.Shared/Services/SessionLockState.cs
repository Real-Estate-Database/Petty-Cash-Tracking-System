using Microsoft.JSInterop;
using PettyCashDatabase.Shared.Auth;

namespace PettyCashDatabase.Shared.Services;

public interface ISessionLockState
{
    bool IsLocked { get; }
    string? ReturnUrl { get; }
    int FailedAttempts { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task BeginAsync(string returnUrl, CancellationToken cancellationToken = default);
    Task<int> RecordFailedAttemptAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists only the fact that the current browser session needs re-authentication.
/// Tokens and identity claims are deliberately not stored here.
/// </summary>
public sealed class SessionLockState : ISessionLockState
{
    private const string LockedKey = "pc.session.locked";
    private const string ReturnUrlKey = "pc.session.return-url";
    private const string AttemptsKey = "pc.session.attempts";

    private readonly IJSRuntime _js;
    private bool _initialized;

    public SessionLockState(IJSRuntime js) => _js = js;

    public bool IsLocked { get; private set; }
    public string? ReturnUrl { get; private set; }
    public int FailedAttempts { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        try
        {
            var locked = await _js.InvokeAsync<string?>("pcSessionStorage.get", cancellationToken, LockedKey);
            ReturnUrl = await _js.InvokeAsync<string?>("pcSessionStorage.get", cancellationToken, ReturnUrlKey);
            var attempts = await _js.InvokeAsync<string?>("pcSessionStorage.get", cancellationToken, AttemptsKey);

            IsLocked = string.Equals(locked, "1", StringComparison.Ordinal);
            FailedAttempts = int.TryParse(attempts, out var parsed)
                ? Math.Clamp(parsed, 0, LoginSecurityPolicy.MaxFailedAttempts)
                : 0;

            // Mark initialized only after JavaScript storage was read successfully.
            // This allows a prerendered circuit to retry after the browser becomes interactive.
            _initialized = true;
        }
        catch
        {
            // Interactive rendering may not have JS access yet. The state will
            // be established when the inactivity callback runs.
        }
    }

    public async Task BeginAsync(string returnUrl, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        IsLocked = true;
        ReturnUrl = string.IsNullOrWhiteSpace(ReturnUrl) ? returnUrl : ReturnUrl;
        FailedAttempts = Math.Clamp(FailedAttempts, 0, LoginSecurityPolicy.MaxFailedAttempts);

        await SetAsync(LockedKey, "1", cancellationToken);
        await SetAsync(ReturnUrlKey, ReturnUrl ?? "/", cancellationToken);
        await SetAsync(AttemptsKey, FailedAttempts.ToString(), cancellationToken);
    }

    public async Task<int> RecordFailedAttemptAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        IsLocked = true;
        FailedAttempts = Math.Min(LoginSecurityPolicy.MaxFailedAttempts, FailedAttempts + 1);
        await SetAsync(AttemptsKey, FailedAttempts.ToString(), cancellationToken);
        return FailedAttempts;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        IsLocked = false;
        ReturnUrl = null;
        FailedAttempts = 0;

        await RemoveAsync(LockedKey, cancellationToken);
        await RemoveAsync(ReturnUrlKey, cancellationToken);
        await RemoveAsync(AttemptsKey, cancellationToken);
    }

    private async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        try
        {
            await _js.InvokeVoidAsync("pcSessionStorage.set", cancellationToken, key, value);
        }
        catch
        {
            // The in-memory state still protects the current circuit.
        }
    }

    private async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _js.InvokeVoidAsync("pcSessionStorage.remove", cancellationToken, key);
        }
        catch
        {
        }
    }
}
