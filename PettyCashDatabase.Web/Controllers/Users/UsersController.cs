using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Web.Services;

namespace PettyCashDatabase.Web.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IServerAuthService _auth;

    public UsersController(IServerAuthService auth) => _auth = auth;

    [HttpPost("{id:int}/unlock")]
    [Authorize(Policy = Permissions.UsersUnlock)]
    public async Task<IActionResult> Unlock(int id, CancellationToken cancellationToken)
    {
        var adminIdText = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!int.TryParse(adminIdText, out var adminId))
            return Unauthorized();

        var client = new ClientRequestInfo(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            Request.Headers["X-Client-Type"].FirstOrDefault(),
            Request.Headers["X-Device-Id"].FirstOrDefault());

        await _auth.UnlockUserAsync(id, adminId, client, cancellationToken);
        return Ok(new { message = "User unlocked." });
    }
}
