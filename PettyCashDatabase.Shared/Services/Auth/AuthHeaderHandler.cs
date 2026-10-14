using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;

namespace PettyCashDatabase.Shared.Services;

/// <summary>
/// HTTP message delegating handler that automatically attaches the JWT Bearer authorization header
/// to outgoing API requests and performs automatic retry upon 401 Unauthorized via refresh token rotation.
/// </summary>
public sealed class AuthHeaderHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private readonly ITokenStorage _tokenStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NavigationManager _navigation;

    public AuthHeaderHandler(
        ITokenStorage tokenStorage,
        IHttpClientFactory httpClientFactory,
        NavigationManager navigation)
    {
        _tokenStorage = tokenStorage;
        _httpClientFactory = httpClientFactory;
        _navigation = navigation;
    }

    /// <summary>
    /// Intercepts outgoing HTTP requests, attaches bearer token, and retries once if 401 is encountered.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. Attach cached JWT access token if present
        await AttachBearerAsync(request, cancellationToken);

        // 2. Dispatch request to inner handler
        var response = await base.SendAsync(request, cancellationToken);

        // 3. Return immediately if request succeeded or was not an unauthorized 401
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return response;

        // 4. Attempt thread-safe token refresh
        var refreshed = await TryRefreshAsync(cancellationToken);
        if (!refreshed)
        {
            // Clear credentials and redirect to login if refresh fails
            await _tokenStorage.ClearAsync(cancellationToken);
            _navigation.NavigateTo("/login", forceLoad: true);
            return response;
        }

        // 5. Clone request, attach newly acquired bearer token, and retry
        response.Dispose();
        var retry = await CloneRequestAsync(request);
        await AttachBearerAsync(retry, cancellationToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    /// <summary>
    /// Reads the stored access token and adds the Authorization: Bearer header.
    /// </summary>
    private async Task AttachBearerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (access, _, _) = await _tokenStorage.GetAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(access))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
    }

    /// <summary>
    /// Acquires a thread lock and requests a fresh token pair using the stored refresh token.
    /// </summary>
    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            // 1. Fetch current refresh token from storage
            var (_, refresh, _) = await _tokenStorage.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(refresh))
                return false;

            // 2. Dispatch refresh request via isolated HTTP client
            var client = _httpClientFactory.CreateClient("AuthApi");
            using var response = await client.PostAsJsonAsync(
                "api/auth/refresh-token",
                new RefreshTokenRequestDto { RefreshToken = refresh },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            // 3. Deserialize refreshed tokens and update storage
            var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken: cancellationToken);
            if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
                return false;

            await _tokenStorage.SaveAsync(auth.AccessToken, auth.RefreshToken, auth.ExpiresAt, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    /// <summary>
    /// Clones an HttpRequestMessage so it can be safely re-sent after a 401 response.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        clone.Version = request.Version;
        return clone;
    }
}
