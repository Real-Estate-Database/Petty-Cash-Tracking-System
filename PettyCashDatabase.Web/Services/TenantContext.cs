namespace PettyCashDatabase.Web.Services;

public sealed class TenantContext : ITenantContext
{
    private readonly IUserTimeZoneService _timeZoneService;

    public TenantContext(IUserTimeZoneService timeZoneService)
    {
        _timeZoneService = timeZoneService;
    }

    public int? OrganizationId { get; private set; }
    public string? TimeZoneId { get; private set; }
    public short? TimeZoneOffset { get; private set; }

    public void SetOrganization(int organizationId, string? timeZoneId = null, short? timeZoneOffset = null)
    {
        OrganizationId = organizationId;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
            TimeZoneId = timeZoneId;
        if (timeZoneOffset.HasValue)
            TimeZoneOffset = timeZoneOffset;
    }

    public void SetTimeZone(string? timeZoneId, short? timeZoneOffset = null)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
            TimeZoneId = timeZoneId;
        if (timeZoneOffset.HasValue)
            TimeZoneOffset = timeZoneOffset;
    }

    public DateTime GetLocalNow() =>
        _timeZoneService.GetCurrentTime(TimeZoneId, TimeZoneOffset ?? 3);

    public void Clear()
    {
        OrganizationId = null;
        TimeZoneId = null;
        TimeZoneOffset = null;
    }
}
