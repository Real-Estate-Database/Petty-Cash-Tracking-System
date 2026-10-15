using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;
using PettyCashDatabase.Web.Services;

namespace PettyCashDatabase.Web.Controllers;

/// <summary>
/// API endpoints for tenant onboarding, authentication, password reset, and geographic data lookup.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IServerAuthService _auth;
    private readonly ILocationLookupService _locations;

    public AuthController(IServerAuthService auth, ILocationLookupService locations)
    {
        _auth = auth;
        _locations = locations;
    }

    /// <summary>
    /// Authenticates a user with email/phone and password, returning JWT access and refresh tokens.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        // 1. Capture client telemetry (IP address, user agent, client device)
        var client = GetClientInfo();

        // 2. Delegate authentication to server service
        var result = await _auth.LoginAsync(request, client, cancellationToken);

        // 3. Return authenticated response payload
        return Ok(result);
    }

    /// <summary>
    /// Onboards a new tenant organization and its primary administrator account.
    /// </summary>
    [HttpPost("register-tenant")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterTenant([FromBody] TenantOnboardingDto request, CancellationToken cancellationToken)
    {
        // 1. Capture client telemetry
        var client = GetClientInfo();

        // 2. Execute transactional tenant onboarding
        await _auth.RegisterTenantAsync(request, client, cancellationToken);

        // 3. Return confirmation response
        return Ok(new { message = "Organization registered. You can sign in." });
    }

    /// <summary>
    /// Initiates password reset by dispatching a secure reset email if the account exists.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request, CancellationToken cancellationToken)
    {
        // 1. Capture client telemetry
        var client = GetClientInfo();

        // 2. Process password reset request
        await _auth.ForgotPasswordAsync(request, client, cancellationToken);

        // 3. Return generic response to prevent account enumeration
        return Ok(new { message = "If this email exists, a reset link has been sent." });
    }

    /// <summary>
    /// Re-sends a password reset link while enforcing rate limits.
    /// </summary>
    [HttpPost("resend-reset")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendReset([FromBody] ResendResetEmailDto request, CancellationToken cancellationToken)
    {
        // 1. Capture client telemetry
        var client = GetClientInfo();

        // 2. Process rate-limited resend request
        await _auth.ResendResetEmailAsync(request, client, cancellationToken);

        // 3. Return confirmation message
        return Ok(new { message = "If this email exists, a reset link has been sent." });
    }

    /// <summary>
    /// Validates reset token and sets a new password.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request, CancellationToken cancellationToken)
    {
        // 1. Capture client telemetry
        var client = GetClientInfo();

        // 2. Update user credentials and revoke sessions
        await _auth.ResetPasswordAsync(request, client, cancellationToken);

        // 3. Return success response
        return Ok(new { message = "Password updated. You can sign in." });
    }

    /// <summary>
    /// Exchanges an active refresh token for a new JWT access and refresh token pair.
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        // 1. Capture client telemetry
        var client = GetClientInfo();

        // 2. Perform refresh rotation
        var result = await _auth.RefreshTokenAsync(request.RefreshToken, client, cancellationToken);

        // 3. Return refreshed token pair
        return Ok(result);
    }

    /// <summary>
    /// Invalidates the specified refresh token and signs out the session.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request, CancellationToken cancellationToken)
    {
        // 1. Capture client telemetry
        var client = GetClientInfo();

        // 2. Revoke active session in audit trail
        await _auth.LogoutAsync(request.RefreshToken, client, cancellationToken);

        // 3. Return empty acknowledgment
        return Ok();
    }

    /// <summary>
    /// Fetches all active countries for onboarding dropdowns.
    /// </summary>
    [HttpGet("countries")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> Countries(CancellationToken cancellationToken)
    {
        return Ok(await _locations.GetCountriesAsync(cancellationToken));
    }

    /// <summary>
    /// Fetches regions belonging to a given country.
    /// </summary>
    [HttpGet("regions")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> Regions([FromQuery] int countryId, CancellationToken cancellationToken)
    {
        return Ok(await _locations.GetRegionsAsync(countryId, cancellationToken));
    }

    /// <summary>
    /// Fetches districts filtered by country or region.
    /// </summary>
    [HttpGet("districts")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> Districts(
        [FromQuery] int? countryId,
        [FromQuery] int? regionId,
        CancellationToken cancellationToken)
    {
        return Ok(await _locations.GetDistrictsAsync(countryId, regionId, cancellationToken));
    }

    /// <summary>
    /// Extracts caller metadata from HttpContext headers for tamper-evidence logging.
    /// </summary>
    private ClientRequestInfo GetClientInfo()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var clientType = Request.Headers["X-Client-Type"].FirstOrDefault();
        var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
        return new ClientRequestInfo(ip, ua, clientType, deviceId);
    }
}
