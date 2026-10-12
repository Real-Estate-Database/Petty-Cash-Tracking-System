using System.Net.Http.Json;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;

namespace PettyCashDatabase.Shared.Services;

public sealed class AuthHttpService : IAuthService
{
    private readonly HttpClient _http;

    public AuthHttpService(HttpClient http) => _http = http;

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/login", request, cancellationToken);
        return await ReadOrThrowAsync<AuthResponseDto>(response, cancellationToken);
    }

    public async Task RegisterTenantAsync(TenantOnboardingDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/register-tenant", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/forgot-password", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ResendResetEmailAsync(ResendResetEmailDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/resend-reset", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/reset-password", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/auth/refresh-token",
            new RefreshTokenRequestDto { RefreshToken = refreshToken },
            cancellationToken);
        return await ReadOrThrowAsync<AuthResponseDto>(response, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/auth/logout",
            new LogoutRequestDto { RefreshToken = refreshToken },
            cancellationToken);
        // Logout is best-effort; clear local tokens regardless.
        _ = response;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body,
            null,
            response.StatusCode);
    }

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body,
                null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from auth API.");
    }
}
