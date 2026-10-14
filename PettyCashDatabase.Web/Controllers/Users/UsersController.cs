using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Web.Services;

namespace PettyCashDatabase.Web.Controllers;

/// <summary>
/// Administrative endpoints for user management and security controls.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IServerAuthService _auth;

    public UsersController(IServerAuthService auth) => _auth = auth;

    /// <summary>
    /// Unlocks a locked tenant user account when executed by an authorized organization admin.
    /// </summary>
    /// <param name="id">The user ID of the account to unlock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id:int}/unlock")]
    [Authorize(Policy = Permissions.UsersUnlock)]
    public async Task<IActionResult> Unlock(int id, CancellationToken cancellationToken)
    {
        // 1. Resolve acting administrator user ID from authenticated claims
        var adminIdText = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!int.TryParse(adminIdText, out var adminId))
            return Unauthorized();

        // 2. Capture audit telemetry
        var client = new ClientRequestInfo(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            Request.Headers["X-Client-Type"].FirstOrDefault(),
            Request.Headers["X-Device-Id"].FirstOrDefault());

        // 3. Delegate unlock execution to authentication service
        await _auth.UnlockUserAsync(id, adminId, client, cancellationToken);

        // 4. Return confirmation response
        return Ok(new { message = "User unlocked." });
    }
}
