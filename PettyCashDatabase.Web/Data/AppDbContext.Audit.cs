using Microsoft.EntityFrameworkCore;

namespace PettyCashDatabase.Web.Data;

public partial class AppDbContext
{
    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        if (_tenantContext == null) return;

        var now = _tenantContext.GetLocalNow();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                var entryDateProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "EntryDate");
                if (entryDateProp != null && entryDateProp.CurrentValue is DateTime ed && ed == default)
                {
                    entryDateProp.CurrentValue = now;
                }

                var createdAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "CreatedAt");
                if (createdAtProp != null && createdAtProp.CurrentValue is DateTime cd && cd == default)
                {
                    createdAtProp.CurrentValue = now;
                }

                var updatedAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
                if (updatedAtProp != null && updatedAtProp.CurrentValue is DateTime ud && ud == default)
                {
                    updatedAtProp.CurrentValue = now;
                }

                var tzProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "TimeZoneId");
                if (tzProp != null && string.IsNullOrWhiteSpace(tzProp.CurrentValue as string) && !string.IsNullOrWhiteSpace(_tenantContext.TimeZoneId))
                {
                    tzProp.CurrentValue = _tenantContext.TimeZoneId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                var updatedAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
                if (updatedAtProp != null)
                {
                    updatedAtProp.CurrentValue = now;
                }

                var lastUpdatedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "LastUpdated" || p.Metadata.Name == "LastUpdatedDate");
                if (lastUpdatedProp != null)
                {
                    lastUpdatedProp.CurrentValue = now;
                }
            }
        }
    }
}
