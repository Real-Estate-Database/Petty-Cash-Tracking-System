using PettyCashDatabase.Shared.DTOs.Auth;

namespace PettyCashDatabase.Shared.Interfaces;

public interface ILocationLookupService
{
    Task<IReadOnlyList<CountryLookupDto>> GetCountriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegionLookupDto>> GetRegionsAsync(int countryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DistrictLookupDto>> GetDistrictsAsync(
        int? countryId = null,
        int? regionId = null,
        CancellationToken cancellationToken = default);
}
