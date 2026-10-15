using System.Net.Http.Json;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;

namespace PettyCashDatabase.Shared.Services;

/// <summary>
/// HTTP client implementation of IAuthService used by WebAssembly and MAUI clients
/// to communicate with the server-side authentication API.
/// </summary>
public sealed class AuthHttpService : IAuthService
{
    private readonly HttpClient _http;

    public AuthHttpService(HttpClient http) => _http = http;

    /// <summary>
    /// Sends credentials to the login endpoint and retrieves tokens.
    /// </summary>
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        // 1. Dispatch POST request to api/auth/login
        using var response = await _http.PostAsJsonAsync("api/auth/login", request, cancellationToken);

        // 2. Read authenticated payload or throw structured HttpRequestException
        return await ReadOrThrowAsync<AuthResponseDto>(response, cancellationToken);
    }

    /// <summary>
    /// Sends onboarding details to create a new organization and admin account.
    /// </summary>
    public async Task RegisterTenantAsync(TenantOnboardingDto request, CancellationToken cancellationToken = default)
    {
        // 1. Dispatch POST request to api/auth/register-tenant
        using var response = await _http.PostAsJsonAsync("api/auth/register-tenant", request, cancellationToken);

        // 2. Validate response code
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// Dispatches a password reset email request.
    /// </summary>
    public async Task ForgotPasswordAsync(ForgotPasswordDto request, CancellationToken cancellationToken = default)
    {
        // 1. Dispatch POST request to api/auth/forgot-password
        using var response = await _http.PostAsJsonAsync("api/auth/forgot-password", request, cancellationToken);

        // 2. Validate response code
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// Re-sends a password reset link.
    /// </summary>
    public async Task ResendResetEmailAsync(ResendResetEmailDto request, CancellationToken cancellationToken = default)
    {
        // 1. Dispatch POST request to api/auth/resend-reset
        using var response = await _http.PostAsJsonAsync("api/auth/resend-reset", request, cancellationToken);

        // 2. Validate response code
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// Submits a new password along with the verification token.
    /// </summary>
    public async Task ResetPasswordAsync(ResetPasswordDto request, CancellationToken cancellationToken = default)
    {
        // 1. Dispatch POST request to api/auth/reset-password
        using var response = await _http.PostAsJsonAsync("api/auth/reset-password", request, cancellationToken);

        // 2. Validate response code
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// Exchanges an existing refresh token for a newly issued access token.
    /// </summary>
    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // 1. Dispatch POST request with refresh token payload
        using var response = await _http.PostAsJsonAsync(
            "api/auth/refresh-token",
            new RefreshTokenRequestDto { RefreshToken = refreshToken },
            cancellationToken);

        // 2. Read refreshed token response
        return await ReadOrThrowAsync<AuthResponseDto>(response, cancellationToken);
    }

    /// <summary>
    /// Revokes the current session refresh token on the server.
    /// </summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // 1. Dispatch best-effort logout request to server
        using var response = await _http.PostAsJsonAsync(
            "api/auth/logout",
            new LogoutRequestDto { RefreshToken = refreshToken },
            cancellationToken);

        // 2. Discard response; local client storage clears regardless
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
