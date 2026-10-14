using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Web.Data;
using PettyCashDatabase.Web.Data.Models;

namespace PettyCashDatabase.Web.Authentication;

public sealed class CustomClaimsFactory
{
    private readonly AppDbContext _db;

    public CustomClaimsFactory(AppDbContext db) => _db = db;

    public async Task<List<Claim>> CreateClaimsAsync(User user, CancellationToken cancellationToken = default)
    {
        var permissionNames = await _db.UserPermissions
            .AsNoTracking()
            .Where(up => up.UserId == user.UserId)
            .Select(up => up.Permission.PermissionName)
            .ToListAsync(cancellationToken);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(AuthClaimTypes.OrgId, user.OrganizationId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim())
        };

        if (user.CountryId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.CountryId, user.CountryId.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(user.TimeZoneId))
            claims.Add(new Claim(AuthClaimTypes.TimeZoneId, user.TimeZoneId));

        foreach (var code in permissionNames.Distinct(StringComparer.Ordinal))
            claims.Add(new Claim(AuthClaimTypes.Permission, code));

        return claims;
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserPermissions
            .AsNoTracking()
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.PermissionName)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
