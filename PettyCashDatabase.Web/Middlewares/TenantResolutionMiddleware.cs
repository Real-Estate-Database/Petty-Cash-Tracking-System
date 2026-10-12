using System.Security.Claims;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Web.Services;

namespace PettyCashDatabase.Web.Middlewares;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var orgClaim = context.User.FindFirstValue(AuthClaimTypes.OrgId);
            var tzClaim = context.User.FindFirstValue(AuthClaimTypes.TimeZoneId);

            if (int.TryParse(orgClaim, out var orgId) && orgId > 0)
            {
                tenantContext.SetOrganization(orgId, tzClaim);
            }
            else if (!string.IsNullOrWhiteSpace(tzClaim))
            {
                tenantContext.SetTimeZone(tzClaim);
            }
        }

        await _next(context);
    }
}
