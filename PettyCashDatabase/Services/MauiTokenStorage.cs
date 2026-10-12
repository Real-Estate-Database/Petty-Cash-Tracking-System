using PettyCashDatabase.Shared.Interfaces;

namespace PettyCashDatabase.Services;

public sealed class MauiTokenStorage : ITokenStorage
{
    private const string AccessKey = "pc.access";
    private const string RefreshKey = "pc.refresh";
    private const string ExpiresKey = "pc.expires";

    private string? _access;
    private string? _refresh;
    private DateTime? _expires;

    public async Task<(string? AccessToken, string? RefreshToken, DateTime? ExpiresAt)> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_access is not null || _refresh is not null)
            return (_access, _refresh, _expires);

        _access = await SecureStorage.Default.GetAsync(AccessKey);
        _refresh = await SecureStorage.Default.GetAsync(RefreshKey);
        var expires = await SecureStorage.Default.GetAsync(ExpiresKey);
        _expires = DateTime.TryParse(expires, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
        return (_access, _refresh, _expires);
    }

    public async Task SaveAsync(string accessToken, string refreshToken, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        _access = accessToken;
        _refresh = refreshToken;
        _expires = expiresAt;

        await SecureStorage.Default.SetAsync(AccessKey, accessToken);
        await SecureStorage.Default.SetAsync(RefreshKey, refreshToken);
        await SecureStorage.Default.SetAsync(ExpiresKey, expiresAt.ToString("O"));
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _access = null;
        _refresh = null;
        _expires = null;
        SecureStorage.Default.Remove(AccessKey);
        SecureStorage.Default.Remove(RefreshKey);
        SecureStorage.Default.Remove(ExpiresKey);
        return Task.CompletedTask;
    }
}
