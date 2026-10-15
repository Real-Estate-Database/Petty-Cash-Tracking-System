using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Shared.Interfaces;
using PettyCashDatabase.Shared.Services;

namespace PettyCashDatabase.Shared.Authentication;

/// <summary>
/// Custom Blazor AuthenticationStateProvider managing client-side JWT token storage,
/// silent proactive refresh rotation, and 30-minute inactivity session lockouts.
/// </summary>
public sealed class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _tokenStorage;
    private readonly IAuthService _authService;
    private readonly ISessionLockState _sessionLock;
    private readonly JwtSecurityTokenHandler _handler = new();
    private CancellationTokenSource? _refreshCts;

    public CustomAuthenticationStateProvider(
        ITokenStorage tokenStorage,
        IAuthService authService,
        ISessionLockState sessionLock)
    {
        _tokenStorage = tokenStorage;
        _authService = authService;
        _sessionLock = sessionLock;
    }

    /// <summary>
    /// Resolves the current ClaimsPrincipal based on local storage, session locks, and token validity.
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // 1. Initialize session lock state and retrieve stored tokens
        await _sessionLock.InitializeAsync();
        var (access, refresh, expires) = await _tokenStorage.GetAsync();

        // 2. Check if circuit is currently locked due to inactivity
        if (_sessionLock.IsLocked)
        {
            if (!string.IsNullOrWhiteSpace(access) || !string.IsNullOrWhiteSpace(refresh))
                await _tokenStorage.ClearAsync();

            return LockedAuthenticationState();
        }

        // 3. Perform silent token refresh if access token is missing or expired
        if (string.IsNullOrWhiteSpace(access) || IsExpired(access))
        {
            if (!string.IsNullOrWhiteSpace(refresh))
            {
                try
                {
                    // 3a. Exchange refresh token for new credentials
                    var refreshed = await _authService.RefreshTokenAsync(refresh);
                    await _tokenStorage.SaveAsync(refreshed.AccessToken, refreshed.RefreshToken, refreshed.ExpiresAt);
                    access = refreshed.AccessToken;
                    expires = refreshed.ExpiresAt;

                    // 3b. Schedule proactive refresh before expiry
                    ScheduleProactiveRefresh(expires);
                    return new AuthenticationState(CreatePrincipal(access));
                }
                catch
                {
                    // 3c. Clear credentials if refresh fails
                    await _tokenStorage.ClearAsync();
                }
            }

            // Return anonymous principal if no valid credentials
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // 4. Token is valid; schedule refresh and return authenticated principal
        ScheduleProactiveRefresh(expires);
        return new AuthenticationState(CreatePrincipal(access));
    }

    /// <summary>
    /// Stores newly issued tokens, clears any active lock, and notifies Blazor of authentication change.
    /// </summary>
    public async Task SignInAsync(string accessToken, string refreshToken, DateTime expiresAt)
    {
        // 1. Clear session lock state
        await _sessionLock.ClearAsync();

        // 2. Persist tokens in storage
        await _tokenStorage.SaveAsync(accessToken, refreshToken, expiresAt);

        // 3. Schedule proactive token refresh timer
        ScheduleProactiveRefresh(expiresAt);

        // 4. Notify Blazor components of state change
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Immediately removes local credentials and turns the current circuit into a locked shell.
    /// </summary>
    public async Task LockForInactivityAsync(string returnUrl)
    {
        // 1. Cancel pending refresh timer
        _refreshCts?.Cancel();
        var (_, refresh, _) = await _tokenStorage.GetAsync();

        // 2. Clear credentials from local storage
        await _tokenStorage.ClearAsync();

        // 3. Mark session lock active with return URL
        await _sessionLock.BeginAsync(returnUrl);

        // 4. Notify components to render locked state
        NotifyAuthenticationStateChanged(Task.FromResult(LockedAuthenticationState()));

        // 5. Fire-and-forget server refresh token revocation
        if (!string.IsNullOrWhiteSpace(refresh))
            _ = RevokeRefreshTokenAsync(refresh);
    }

    /// <summary>
    /// Signs out the user by revoking the refresh token and clearing client storage.
    /// </summary>
    public async Task SignOutAsync()
    {
        // 1. Revoke refresh token on the server
        var (_, refresh, _) = await _tokenStorage.GetAsync();
        if (!string.IsNullOrWhiteSpace(refresh))
        {
            try { await _authService.LogoutAsync(refresh); }
            catch { /* best effort */ }
        }

        // 2. Cancel proactive refresh timer
        _refreshCts?.Cancel();

        // 3. Clear local storage
        await _tokenStorage.ClearAsync();

        // 4. Notify Blazor of logout state change
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Checks whether the currently authenticated user possesses a specific permission claim.
    /// </summary>
    public async Task<bool> HasPermission(string code)
    {
        var state = await GetAuthenticationStateAsync();
        return state.User.HasClaim(AuthClaimTypes.Permission, code);
    }

    /// <summary>
    /// Creates a ClaimsPrincipal from the decoded JWT claims.
    /// </summary>
    private ClaimsPrincipal CreatePrincipal(string accessToken)
    {
        var token = _handler.ReadJwtToken(accessToken);
        var identity = new ClaimsIdentity(token.Claims, authenticationType: "jwt");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a ClaimsPrincipal marked with session-lock identity.
    /// </summary>
    private static AuthenticationState LockedAuthenticationState() =>
        new(new ClaimsPrincipal(new ClaimsIdentity("session-lock")));

    /// <summary>
    /// Best-effort revocation of the refresh token on the server.
    /// </summary>
    private async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        try
        {
            await _authService.LogoutAsync(refreshToken);
        }
        catch
        {
            // Local credentials are already gone; server revocation is best effort.
        }
    }

    /// <summary>
    /// Checks if a JWT token has expired or is within 30 seconds of expiration.
    /// </summary>
    private static bool IsExpired(string accessToken)
    {
        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return token.ValidTo <= DateTime.UtcNow.AddSeconds(30);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Schedules a background timer to proactively refresh the access token 2 minutes before it expires.
    /// </summary>
    private void ScheduleProactiveRefresh(DateTime? expiresAt)
    {
        // 1. Cancel previous timer
        _refreshCts?.Cancel();
        if (expiresAt is null)
            return;

        // 2. Calculate delay (refresh 2 minutes before expiration)
        _refreshCts = new CancellationTokenSource();
        var delay = expiresAt.Value - DateTime.UtcNow - TimeSpan.FromMinutes(2);
        if (delay < TimeSpan.FromSeconds(5))
            delay = TimeSpan.FromSeconds(5);

        var cts = _refreshCts;
        _ = Task.Run(async () =>
        {
            try
            {
                // 3. Wait until refresh window opens
                await Task.Delay(delay, cts.Token);
                var (_, refresh, _) = await _tokenStorage.GetAsync(cts.Token);
                if (string.IsNullOrWhiteSpace(refresh))
                    return;

                // 4. Request new token pair from API
                var auth = await _authService.RefreshTokenAsync(refresh, cts.Token);
                await _tokenStorage.SaveAsync(auth.AccessToken, auth.RefreshToken, auth.ExpiresAt, cts.Token);

                // 5. Update state and schedule next cycle
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                ScheduleProactiveRefresh(auth.ExpiresAt);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                await _tokenStorage.ClearAsync();
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            }
        }, cts.Token);
    }
}
