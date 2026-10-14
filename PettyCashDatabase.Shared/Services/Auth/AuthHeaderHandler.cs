using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;

namespace PettyCashDatabase.Shared.Services;

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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await AttachBearerAsync(request, cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return response;

        var refreshed = await TryRefreshAsync(cancellationToken);
        if (!refreshed)
        {
            await _tokenStorage.ClearAsync(cancellationToken);
            _navigation.NavigateTo("/login", forceLoad: true);
            return response;
        }

        response.Dispose();

        var retry = await CloneRequestAsync(request);
        await AttachBearerAsync(retry, cancellationToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task AttachBearerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (access, _, _) = await _tokenStorage.GetAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(access))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var (_, refresh, _) = await _tokenStorage.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(refresh))
                return false;

            var client = _httpClientFactory.CreateClient("AuthApi");
            using var response = await client.PostAsJsonAsync(
                "api/auth/refresh-token",
                new RefreshTokenRequestDto { RefreshToken = refresh },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

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
