using Microsoft.EntityFrameworkCore;

namespace PettyCashDatabase.Web.Data;

/// <summary>
/// Partial DbContext responsible for automatic audit timestamping and tenant timezone conversion
/// across entity insertions and updates.
/// </summary>
public partial class AppDbContext
{
    /// <summary>
    /// Synchronously saves all changes made in this context to the database,
    /// intercepting entity states to automatically stamp local timestamps.
    /// </summary>
    public override int SaveChanges()
    {
        // 1. Intercept change tracker and apply local audit timestamps
        ApplyAuditTimestamps();

        // 2. Delegate to the underlying Entity Framework implementation
        return base.SaveChanges();
    }

    /// <summary>
    /// Asynchronously saves all changes made in this context to the database,
    /// intercepting entity states to automatically stamp local timestamps.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Intercept change tracker and apply local audit timestamps
        ApplyAuditTimestamps();

        // 2. Delegate to the underlying Entity Framework implementation
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Iterates through tracked entities and applies EntryDate, CreatedAt, UpdatedAt,
    /// and TimeZoneId based on the current tenant's configured timezone.
    /// </summary>
    private void ApplyAuditTimestamps()
    {
        // 1. Skip if tenant context is uninitialized
        if (_tenantContext == null) return;

        // 2. Resolve current date & time with tenant's timezone offset applied
        var now = _tenantContext.GetLocalNow();

        // 3. Inspect tracked entities in the current ChangeTracker
        foreach (var entry in ChangeTracker.Entries())
        {
            // Case A: Newly added entity (INSERT)
            if (entry.State == EntityState.Added)
            {
                // Auto-populate EntryDate if default
                var entryDateProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "EntryDate");
                if (entryDateProp != null && entryDateProp.CurrentValue is DateTime ed && ed == default)
                {
                    entryDateProp.CurrentValue = now;
                }

                // Auto-populate CreatedAt if default
                var createdAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "CreatedAt");
                if (createdAtProp != null && createdAtProp.CurrentValue is DateTime cd && cd == default)
                {
                    createdAtProp.CurrentValue = now;
                }

                // Auto-populate UpdatedAt if default
                var updatedAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
                if (updatedAtProp != null && updatedAtProp.CurrentValue is DateTime ud && ud == default)
                {
                    updatedAtProp.CurrentValue = now;
                }

                // Auto-populate TimeZoneId from tenant context
                var tzProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "TimeZoneId");
                if (tzProp != null && string.IsNullOrWhiteSpace(tzProp.CurrentValue as string) && !string.IsNullOrWhiteSpace(_tenantContext.TimeZoneId))
                {
                    tzProp.CurrentValue = _tenantContext.TimeZoneId;
                }
            }
            // Case B: Modified existing entity (UPDATE)
            else if (entry.State == EntityState.Modified)
            {
                // Auto-update UpdatedAt timestamp
                var updatedAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
                if (updatedAtProp != null)
                {
                    updatedAtProp.CurrentValue = now;
                }

                // Auto-update LastUpdated / LastUpdatedDate timestamp
                var lastUpdatedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "LastUpdated" || p.Metadata.Name == "LastUpdatedDate");
                if (lastUpdatedProp != null)
                {
                    lastUpdatedProp.CurrentValue = now;
                }
            }
        }
    }
}
