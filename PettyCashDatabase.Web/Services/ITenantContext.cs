namespace PettyCashDatabase.Web.Services;

public interface ITenantContext
{
    int? OrganizationId { get; }
    string? TimeZoneId { get; }
    short? TimeZoneOffset { get; }

    void SetOrganization(int organizationId, string? timeZoneId = null, short? timeZoneOffset = null);
    void SetTimeZone(string? timeZoneId, short? timeZoneOffset = null);
    DateTime GetLocalNow();
    void Clear();
}
