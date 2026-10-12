using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;
using PettyCashDatabase.Web.Services;

namespace PettyCashDatabase.Web.Controllers;

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

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _auth.LoginAsync(request, GetClientInfo(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("register-tenant")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterTenant([FromBody] TenantOnboardingDto request, CancellationToken cancellationToken)
    {
        await _auth.RegisterTenantAsync(request, GetClientInfo(), cancellationToken);
        return Ok(new { message = "Organization registered. You can sign in." });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request, CancellationToken cancellationToken)
    {
        await _auth.ForgotPasswordAsync(request, GetClientInfo(), cancellationToken);
        return Ok(new { message = "If this email exists, a reset link has been sent." });
    }

    [HttpPost("resend-reset")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendReset([FromBody] ResendResetEmailDto request, CancellationToken cancellationToken)
    {
        await _auth.ResendResetEmailAsync(request, GetClientInfo(), cancellationToken);
        return Ok(new { message = "If this email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request, CancellationToken cancellationToken)
    {
        await _auth.ResetPasswordAsync(request, GetClientInfo(), cancellationToken);
        return Ok(new { message = "Password updated. You can sign in." });
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _auth.RefreshTokenAsync(request.RefreshToken, GetClientInfo(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request, CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(request.RefreshToken, GetClientInfo(), cancellationToken);
        return Ok();
    }

    [HttpGet("countries")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> Countries(CancellationToken cancellationToken)
    {
        return Ok(await _locations.GetCountriesAsync(cancellationToken));
    }

    [HttpGet("regions")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> Regions([FromQuery] int countryId, CancellationToken cancellationToken)
    {
        return Ok(await _locations.GetRegionsAsync(countryId, cancellationToken));
    }

    [HttpGet("districts")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> Districts(
        [FromQuery] int? countryId,
        [FromQuery] int? regionId,
        CancellationToken cancellationToken)
    {
        return Ok(await _locations.GetDistrictsAsync(countryId, regionId, cancellationToken));
    }

    private ClientRequestInfo GetClientInfo()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var clientType = Request.Headers["X-Client-Type"].FirstOrDefault();
        var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
        return new ClientRequestInfo(ip, ua, clientType, deviceId);
    }
}
