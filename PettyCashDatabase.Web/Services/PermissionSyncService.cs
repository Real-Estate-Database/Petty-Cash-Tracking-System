using Microsoft.EntityFrameworkCore;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Web.Data;
using PettyCashDatabase.Web.Data.Models;

namespace PettyCashDatabase.Web.Services;

/// <summary>Idempotent startup sync of permission catalog and org-admin grants.</summary>
public sealed class PermissionSyncService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PermissionSyncService> _logger;

    public PermissionSyncService(IServiceScopeFactory scopeFactory, ILogger<PermissionSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SyncAsync(db, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Permission sync skipped (database may not be updated yet).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static async Task SyncAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var code in Permissions.All)
        {
            var existing = await db.Permissions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.PermissionName == code, cancellationToken);

            if (existing is null)
            {
                db.Permissions.Add(new Permission
                {
                    PermissionName = code,
                    Description = code
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var allPermissions = await db.Permissions
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var admins = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.IsOrgAdmin && !u.Deleted)
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);

        foreach (var adminId in admins)
        {
            var granted = await db.UserPermissions
                .IgnoreQueryFilters()
                .Where(up => up.UserId == adminId)
                .Select(up => up.PermissionId)
                .ToListAsync(cancellationToken);

            var syncNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneCatalog.Resolve(null, 3));

            foreach (var permission in allPermissions)
            {
                if (granted.Contains(permission.PermissionId))
                    continue;

                db.UserPermissions.Add(new UserPermission
                {
                    UserId = adminId,
                    PermissionId = permission.PermissionId,
                    EntryDate = syncNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public static async Task GrantAllPermissionsAsync(AppDbContext db, int userId, CancellationToken cancellationToken = default)
    {
        // Dynamically fetch all permissions currently defined in the database
        var allPermissions = await db.Permissions
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var granted = await db.UserPermissions
            .IgnoreQueryFilters()
            .Where(up => up.UserId == userId)
            .Select(up => up.PermissionId)
            .ToListAsync(cancellationToken);

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        var syncNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneCatalog.Resolve(user?.TimeZoneId, user?.TimeZone ?? 3));

        foreach (var permission in allPermissions)
        {
            if (granted.Contains(permission.PermissionId))
                continue;

            db.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                PermissionId = permission.PermissionId,
                EntryDate = syncNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
