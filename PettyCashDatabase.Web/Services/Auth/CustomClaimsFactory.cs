using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Web.Data;
using PettyCashDatabase.Web.Data.Models;

namespace PettyCashDatabase.Web.Authentication;

/// <summary>
/// Constructs ClaimsPrincipal claims and resolves user permissions for JWT generation.
/// </summary>
public sealed class CustomClaimsFactory
{
    private readonly AppDbContext _db;

    public CustomClaimsFactory(AppDbContext db) => _db = db;

    /// <summary>
    /// Generates identity, tenant, and permission claims for the authenticated user.
    /// </summary>
    public async Task<List<Claim>> CreateClaimsAsync(User user, CancellationToken cancellationToken = default)
    {
        // 1. Fetch user permissions via parameterized raw SQL SELECT
        var permissionNames = await _db.Database.SqlQuery<string>($@"
            SELECT DISTINCT p.[PermissionName] AS Value
            FROM [UserPermissions] up
            INNER JOIN [Permissions] p ON up.[PermissionId] = p.[PermissionId]
            WHERE up.[UserId] = {user.UserId};
        ").ToListAsync(cancellationToken);

        // 2. Build core identity and tenant claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(AuthClaimTypes.OrgId, user.OrganizationId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim())
        };

        // 3. Attach optional geographical and timezone claims
        if (user.CountryId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.CountryId, user.CountryId.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(user.TimeZoneId))
            claims.Add(new Claim(AuthClaimTypes.TimeZoneId, user.TimeZoneId));

        // 4. Attach distinct authorization permission claims
        foreach (var code in permissionNames.Distinct(StringComparer.Ordinal))
            claims.Add(new Claim(AuthClaimTypes.Permission, code));

        return claims;
    }

    /// <summary>
    /// Resolves all permission codes granted to the specified user.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(int userId, CancellationToken cancellationToken = default)
    {
        // 1. Fetch distinct permission codes via parameterized raw SQL SELECT
        return await _db.Database.SqlQuery<string>($@"
            SELECT DISTINCT p.[PermissionName] AS Value
            FROM [UserPermissions] up
            INNER JOIN [Permissions] p ON up.[PermissionId] = p.[PermissionId]
            WHERE up.[UserId] = {userId};
        ").ToListAsync(cancellationToken);
    }
}
