using Microsoft.JSInterop;
using PettyCashDatabase.Shared.Interfaces;

namespace PettyCashDatabase.Web.Client.Authentication;

public sealed class WebTokenStorage : ITokenStorage
{
    private const string AccessKey = "pc.access";
    private const string RefreshKey = "pc.refresh";
    private const string ExpiresKey = "pc.expires";

    private readonly IJSRuntime _js;
    private string? _access;
    private string? _refresh;
    private DateTime? _expires;

    public WebTokenStorage(IJSRuntime js) => _js = js;

    public async Task<(string? AccessToken, string? RefreshToken, DateTime? ExpiresAt)> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_access is not null || _refresh is not null)
            return (_access, _refresh, _expires);

        try
        {
            _access = await _js.InvokeAsync<string?>("blazorStorage.get", cancellationToken, AccessKey);
            _refresh = await _js.InvokeAsync<string?>("blazorStorage.get", cancellationToken, RefreshKey);
            var expires = await _js.InvokeAsync<string?>("blazorStorage.get", cancellationToken, ExpiresKey);
            _expires = DateTime.TryParse(expires, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
        }
        catch
        {
            try
            {
                _access = await _js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, AccessKey);
                _refresh = await _js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, RefreshKey);
                var expires = await _js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, ExpiresKey);
                _expires = DateTime.TryParse(expires, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
            }
            catch
            {
                // Storage unavailable or JS not yet ready
            }
        }

        return (_access, _refresh, _expires);
    }

    public async Task SaveAsync(string accessToken, string refreshToken, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        _access = accessToken;
        _refresh = refreshToken;
        _expires = expiresAt;

        try
        {
            await _js.InvokeVoidAsync("blazorStorage.set", cancellationToken, AccessKey, accessToken);
            await _js.InvokeVoidAsync("blazorStorage.set", cancellationToken, RefreshKey, refreshToken);
            await _js.InvokeVoidAsync("blazorStorage.set", cancellationToken, ExpiresKey, expiresAt.ToString("O"));
        }
        catch
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.setItem", cancellationToken, AccessKey, accessToken);
                await _js.InvokeVoidAsync("localStorage.setItem", cancellationToken, RefreshKey, refreshToken);
                await _js.InvokeVoidAsync("localStorage.setItem", cancellationToken, ExpiresKey, expiresAt.ToString("O"));
            }
            catch
            {
            }
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _access = null;
        _refresh = null;
        _expires = null;

        try
        {
            await _js.InvokeVoidAsync("blazorStorage.remove", cancellationToken, AccessKey);
            await _js.InvokeVoidAsync("blazorStorage.remove", cancellationToken, RefreshKey);
            await _js.InvokeVoidAsync("blazorStorage.remove", cancellationToken, ExpiresKey);
        }
        catch
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", cancellationToken, AccessKey);
                await _js.InvokeVoidAsync("localStorage.removeItem", cancellationToken, RefreshKey);
                await _js.InvokeVoidAsync("localStorage.removeItem", cancellationToken, ExpiresKey);
            }
            catch
            {
            }
        }
    }
}
