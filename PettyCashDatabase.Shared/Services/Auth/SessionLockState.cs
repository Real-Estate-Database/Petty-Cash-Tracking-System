using Microsoft.JSInterop;
using PettyCashDatabase.Shared.Auth;

namespace PettyCashDatabase.Shared.Services;

/// <summary>
/// Service contract managing inactivity lockout state and failed PIN/password re-auth attempts.
/// </summary>
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

    /// <summary>
    /// Reads stored session lock flags from browser storage via JS interop.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 1. Guard against duplicate initialization
        if (_initialized)
            return;

        try
        {
            // 2. Fetch lock state, target return URL, and failed attempt counts
            var locked = await _js.InvokeAsync<string?>("pcSessionStorage.get", cancellationToken, LockedKey);
            ReturnUrl = await _js.InvokeAsync<string?>("pcSessionStorage.get", cancellationToken, ReturnUrlKey);
            var attempts = await _js.InvokeAsync<string?>("pcSessionStorage.get", cancellationToken, AttemptsKey);

            // 3. Populate in-memory properties
            IsLocked = string.Equals(locked, "1", StringComparison.Ordinal);
            FailedAttempts = int.TryParse(attempts, out var parsed)
                ? Math.Clamp(parsed, 0, LoginSecurityPolicy.MaxFailedAttempts)
                : 0;

            _initialized = true;
        }
        catch
        {
            // Interactive rendering may not have JS access yet; retry on user action.
        }
    }

    /// <summary>
    /// Locks the circuit for inactivity and stores the return URL.
    /// </summary>
    public async Task BeginAsync(string returnUrl, CancellationToken cancellationToken = default)
    {
        // 1. Ensure initialization
        await InitializeAsync(cancellationToken);

        // 2. Update memory state
        IsLocked = true;
        ReturnUrl = string.IsNullOrWhiteSpace(ReturnUrl) ? returnUrl : ReturnUrl;
        FailedAttempts = Math.Clamp(FailedAttempts, 0, LoginSecurityPolicy.MaxFailedAttempts);

        // 3. Persist to browser storage
        await SetAsync(LockedKey, "1", cancellationToken);
        await SetAsync(ReturnUrlKey, ReturnUrl ?? "/", cancellationToken);
        await SetAsync(AttemptsKey, FailedAttempts.ToString(), cancellationToken);
    }

    /// <summary>
    /// Increments failed unlock attempts and stores the updated counter.
    /// </summary>
    public async Task<int> RecordFailedAttemptAsync(CancellationToken cancellationToken = default)
    {
        // 1. Ensure initialization
        await InitializeAsync(cancellationToken);

        // 2. Increment failed counter up to maximum threshold
        IsLocked = true;
        FailedAttempts = Math.Min(LoginSecurityPolicy.MaxFailedAttempts, FailedAttempts + 1);

        // 3. Persist updated attempt count
        await SetAsync(AttemptsKey, FailedAttempts.ToString(), cancellationToken);
        return FailedAttempts;
    }

    /// <summary>
    /// Clears the lock state and removes all lock keys from browser storage upon successful unlock.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // 1. Reset in-memory flags
        IsLocked = false;
        ReturnUrl = null;
        FailedAttempts = 0;

        // 2. Remove storage keys
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
