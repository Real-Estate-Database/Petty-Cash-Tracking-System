using Microsoft.EntityFrameworkCore;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;
using PettyCashDatabase.Web.Data;

namespace PettyCashDatabase.Web.Services;

/// <summary>
/// Provides geographical lookup queries (Countries, Regions, Districts)
/// using parameterized raw SQL queries.
/// </summary>
public sealed class LocationLookupService : ILocationLookupService
{
    private readonly AppDbContext _db;

    public LocationLookupService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Fetches all active countries ordered by country name via raw SQL SELECT.
    /// </summary>
    public async Task<IReadOnlyList<CountryLookupDto>> GetCountriesAsync(
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch countries ordered by name using parameterized raw SQL SELECT
        return await _db.Database.SqlQuery<CountryLookupDto>($@"
            SELECT [ID] AS [Id], [Code], [CountryName] AS [Name] 
            FROM [Countries] 
            ORDER BY [CountryName];
        ").ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Fetches all regions belonging to a given country via raw SQL SELECT.
    /// </summary>
    public async Task<IReadOnlyList<RegionLookupDto>> GetRegionsAsync(
        int countryId,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch regions for selected country using parameterized raw SQL SELECT
        return await _db.Database.SqlQuery<RegionLookupDto>($@"
            SELECT [RegionID] AS [RegionId], [RegionName] AS [Name]
            FROM [Regions]
            WHERE [Country] = {countryId}
            ORDER BY [RegionName];
        ").ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Fetches districts filtered by region or country via raw SQL SELECT.
    /// </summary>
    public async Task<IReadOnlyList<DistrictLookupDto>> GetDistrictsAsync(
        int? countryId = null,
        int? regionId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Filter districts by region when regionId is provided
        if (regionId is > 0)
        {
            return await _db.Database.SqlQuery<DistrictLookupDto>($@"
                SELECT [DistrictID] AS [DistrictId], [Region] AS [RegionId], [DistrictName] AS [Name]
                FROM [Districts]
                WHERE [Region] = {regionId}
                ORDER BY [DistrictName];
            ").ToListAsync(cancellationToken);
        }

        // 2. Filter districts by country when region is null
        if (countryId is > 0)
        {
            return await _db.Database.SqlQuery<DistrictLookupDto>($@"
                SELECT [DistrictID] AS [DistrictId], [Region] AS [RegionId], [DistrictName] AS [Name]
                FROM [Districts]
                WHERE [Country] = {countryId}
                ORDER BY [DistrictName];
            ").ToListAsync(cancellationToken);
        }

        // 3. Fallback: fetch all districts ordered by name
        return await _db.Database.SqlQuery<DistrictLookupDto>($@"
            SELECT [DistrictID] AS [DistrictId], [Region] AS [RegionId], [DistrictName] AS [Name]
            FROM [Districts]
            ORDER BY [DistrictName];
        ").ToListAsync(cancellationToken);
    }
}
