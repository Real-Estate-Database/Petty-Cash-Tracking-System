using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Shared.Interfaces;

namespace PettyCashDatabase.Shared.Authentication;

public sealed class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _tokenStorage;
    private readonly IAuthService _authService;
    private readonly JwtSecurityTokenHandler _handler = new();
    private CancellationTokenSource? _refreshCts;

    public CustomAuthenticationStateProvider(ITokenStorage tokenStorage, IAuthService authService)
    {
        _tokenStorage = tokenStorage;
        _authService = authService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var (access, refresh, expires) = await _tokenStorage.GetAsync();

        if (string.IsNullOrWhiteSpace(access) || IsExpired(access))
        {
            if (!string.IsNullOrWhiteSpace(refresh))
            {
                try
                {
                    var refreshed = await _authService.RefreshTokenAsync(refresh);
                    await _tokenStorage.SaveAsync(refreshed.AccessToken, refreshed.RefreshToken, refreshed.ExpiresAt);
                    access = refreshed.AccessToken;
                    expires = refreshed.ExpiresAt;
                    ScheduleProactiveRefresh(expires);
                    return new AuthenticationState(CreatePrincipal(access));
                }
                catch
                {
                    await _tokenStorage.ClearAsync();
                }
            }

            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        ScheduleProactiveRefresh(expires);
        return new AuthenticationState(CreatePrincipal(access));
    }

    public async Task SignInAsync(string accessToken, string refreshToken, DateTime expiresAt)
    {
        await _tokenStorage.SaveAsync(accessToken, refreshToken, expiresAt);
        ScheduleProactiveRefresh(expiresAt);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        var (_, refresh, _) = await _tokenStorage.GetAsync();
        if (!string.IsNullOrWhiteSpace(refresh))
        {
            try { await _authService.LogoutAsync(refresh); }
            catch { /* best effort */ }
        }

        _refreshCts?.Cancel();
        await _tokenStorage.ClearAsync();
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task<bool> HasPermission(string code)
    {
        var state = await GetAuthenticationStateAsync();
        return state.User.HasClaim(AuthClaimTypes.Permission, code);
    }

    private ClaimsPrincipal CreatePrincipal(string accessToken)
    {
        var token = _handler.ReadJwtToken(accessToken);
        var identity = new ClaimsIdentity(token.Claims, authenticationType: "jwt");
        return new ClaimsPrincipal(identity);
    }

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

    private void ScheduleProactiveRefresh(DateTime? expiresAt)
    {
        _refreshCts?.Cancel();
        if (expiresAt is null)
            return;

        _refreshCts = new CancellationTokenSource();
        var delay = expiresAt.Value - DateTime.UtcNow - TimeSpan.FromMinutes(2);
        if (delay < TimeSpan.FromSeconds(5))
            delay = TimeSpan.FromSeconds(5);

        var cts = _refreshCts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
                var (_, refresh, _) = await _tokenStorage.GetAsync(cts.Token);
                if (string.IsNullOrWhiteSpace(refresh))
                    return;

                var auth = await _authService.RefreshTokenAsync(refresh, cts.Token);
                await _tokenStorage.SaveAsync(auth.AccessToken, auth.RefreshToken, auth.ExpiresAt, cts.Token);
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
