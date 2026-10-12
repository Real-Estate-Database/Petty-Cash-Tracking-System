using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PettyCashDatabase.Web.Services;

/// <summary>
/// Uses the application services directly for Interactive Server components.
/// Server-rendered pages must not make an HTTPS request back to their own app.
/// </summary>
public sealed class ServerAuthServiceAdapter : IAuthService
{
    private readonly IServerAuthService _serverAuth;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ServerAuthServiceAdapter(
        IServerAuthService serverAuth,
        IHttpContextAccessor httpContextAccessor)
    {
        _serverAuth = serverAuth;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<AuthResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default) =>
        _serverAuth.LoginAsync(request, GetClientInfo(), cancellationToken);

    public async Task RegisterTenantAsync(
        TenantOnboardingDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _serverAuth.RegisterTenantAsync(request, GetClientInfo(), cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "Registration could not be completed because PettyCashDB rejected the database update. " +
                "Apply the pending database migrations and verify the database connection.",
                exception);
        }
    }

    public Task ForgotPasswordAsync(
        ForgotPasswordDto request,
        CancellationToken cancellationToken = default) =>
        _serverAuth.ForgotPasswordAsync(request, GetClientInfo(), cancellationToken);

    public Task ResendResetEmailAsync(
        ResendResetEmailDto request,
        CancellationToken cancellationToken = default) =>
        _serverAuth.ResendResetEmailAsync(request, GetClientInfo(), cancellationToken);

    public Task ResetPasswordAsync(
        ResetPasswordDto request,
        CancellationToken cancellationToken = default) =>
        _serverAuth.ResetPasswordAsync(request, GetClientInfo(), cancellationToken);

    public Task<AuthResponseDto> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default) =>
        _serverAuth.RefreshTokenAsync(refreshToken, GetClientInfo(), cancellationToken);

    public Task LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default) =>
        _serverAuth.LogoutAsync(refreshToken, GetClientInfo(), cancellationToken);

    private ClientRequestInfo GetClientInfo()
    {
        var context = _httpContextAccessor.HttpContext;
        return new ClientRequestInfo(
            context?.Connection.RemoteIpAddress?.ToString(),
            context?.Request.Headers.UserAgent.ToString(),
            context?.Request.Headers["X-Client-Type"].FirstOrDefault(),
            context?.Request.Headers["X-Device-Id"].FirstOrDefault());
    }
}
