using Microsoft.EntityFrameworkCore;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Web.Data;
using PettyCashDatabase.Web.Data.Models;

namespace PettyCashDatabase.Web.Services;

/// <summary>
/// Idempotent startup sync and runtime assignment of permissions using parameterized raw SQL queries.
/// </summary>
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

    /// <summary>
    /// Synchronizes hardcoded permissions to the database and ensures all Organization Admins have them.
    /// </summary>
    public static async Task SyncAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        // 1. Begin transactional boundary
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        // 2. Discover and register new permissions via raw SQL INSERT
        foreach (var code in Permissions.All)
        {
            var exists = await db.Database.SqlQuery<int>($@"
                SELECT COUNT(1) AS Value FROM [Permissions] WHERE [PermissionName] = {code};
            ").SingleAsync(cancellationToken);

            if (exists == 0)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO [Permissions] ([PermissionName], [Description])
                    VALUES ({code}, {code});
                ", cancellationToken);
            }
        }

        // 3. Fetch all active permissions from the database via raw SQL SELECT
        var allPermissions = await db.Permissions
            .FromSqlInterpolated($@"SELECT * FROM [Permissions]")
            .ToListAsync(cancellationToken);

        // 4. Fetch all active Organization Admins via raw SQL SELECT
        var admins = await db.Database.SqlQuery<int>($@"
            SELECT [UserId] AS Value FROM [Users] WHERE [IsOrgAdmin] = 1 AND [Deleted] = 0;
        ").ToListAsync(cancellationToken);

        var syncNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneCatalog.Resolve(null, 3));

        // 5. Grant missing permissions to each admin via raw SQL INSERT
        foreach (var adminId in admins)
        {
            var granted = await db.Database.SqlQuery<int>($@"
                SELECT [PermissionId] AS Value FROM [UserPermissions] WHERE [UserId] = {adminId};
            ").ToListAsync(cancellationToken);

            foreach (var permission in allPermissions)
            {
                if (granted.Contains(permission.PermissionId))
                    continue;

                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO [UserPermissions] ([UserId], [PermissionId], [EntryDate])
                    VALUES ({adminId}, {permission.PermissionId}, {syncNow});
                ", cancellationToken);
            }
        }

        // 6. Commit transaction
        await tx.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Grants all registered permissions to a newly created tenant administrator user via raw SQL.
    /// </summary>
    public static async Task GrantAllPermissionsAsync(AppDbContext db, int userId, CancellationToken cancellationToken = default)
    {
        // 1. Fetch all system permissions via raw SQL SELECT
        var allPermissions = await db.Permissions
            .FromSqlInterpolated($@"SELECT * FROM [Permissions]")
            .ToListAsync(cancellationToken);

        // 2. Fetch any already-granted permissions for this user via raw SQL SELECT
        var granted = await db.Database.SqlQuery<int>($@"
            SELECT [PermissionId] AS Value FROM [UserPermissions] WHERE [UserId] = {userId};
        ").ToListAsync(cancellationToken);

        // 3. Resolve user timezone for correct audit timestamp
        var user = await db.Users
            .FromSqlInterpolated($@"SELECT * FROM [Users] WHERE [UserId] = {userId}")
            .FirstOrDefaultAsync(cancellationToken);

        var syncNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneCatalog.Resolve(user?.TimeZoneId, user?.TimeZone ?? 3));

        // 4. Insert each missing permission via raw SQL INSERT
        foreach (var permission in allPermissions)
        {
            if (granted.Contains(permission.PermissionId))
                continue;

            await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO [UserPermissions] ([UserId], [PermissionId], [EntryDate])
                VALUES ({userId}, {permission.PermissionId}, {syncNow});
            ", cancellationToken);
        }
    }
}
