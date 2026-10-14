using Microsoft.EntityFrameworkCore;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;
using PettyCashDatabase.Web.Data;

namespace PettyCashDatabase.Web.Services;

public sealed class LocationLookupService : ILocationLookupService
{
    private readonly AppDbContext _db;

    public LocationLookupService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CountryLookupDto>> GetCountriesAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Countries.AsNoTracking()
            .OrderBy(c => c.CountryName)
            .Select(c => new CountryLookupDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.CountryName
            })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RegionLookupDto>> GetRegionsAsync(
        int countryId,
        CancellationToken cancellationToken = default) =>
        await _db.Regions.AsNoTracking()
            .Where(r => r.Country == countryId)
            .OrderBy(r => r.RegionName)
            .Select(r => new RegionLookupDto
            {
                RegionId = r.RegionId,
                Name = r.RegionName
            })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DistrictLookupDto>> GetDistrictsAsync(
        int? countryId = null,
        int? regionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Districts.AsNoTracking().AsQueryable();

        if (regionId is > 0)
            query = query.Where(d => d.Region == regionId);
        else if (countryId is > 0)
            query = query.Where(d => d.Country == countryId);

        return await query
            .OrderBy(d => d.DistrictName)
            .Select(d => new DistrictLookupDto
            {
                DistrictId = d.DistrictId,
                RegionId = d.Region,
                Name = d.DistrictName
            })
            .ToListAsync(cancellationToken);
    }
}
