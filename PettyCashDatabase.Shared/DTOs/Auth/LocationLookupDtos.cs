namespace PettyCashDatabase.Shared.DTOs.Auth;

public sealed class CountryLookupDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public sealed class RegionLookupDto
{
    public int RegionId { get; set; }
    public string? Name { get; set; }
}

public sealed class DistrictLookupDto
{
    public int DistrictId { get; set; }
    public int? RegionId { get; set; }
    public string? Name { get; set; }
}
