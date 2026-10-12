using System.Net.Http.Json;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Shared.Interfaces;

namespace PettyCashDatabase.Shared.Services;

public sealed class ApiLocationLookupService : ILocationLookupService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiLocationLookupService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Task<IReadOnlyList<CountryLookupDto>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CountryLookupDto>("api/auth/countries", cancellationToken);

    public Task<IReadOnlyList<RegionLookupDto>> GetRegionsAsync(
        int countryId,
        CancellationToken cancellationToken = default) =>
        GetAsync<RegionLookupDto>($"api/auth/regions?countryId={countryId}", cancellationToken);

    public Task<IReadOnlyList<DistrictLookupDto>> GetDistrictsAsync(
        int? countryId = null,
        int? regionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = regionId is > 0
            ? $"regionId={regionId}"
            : countryId is > 0
                ? $"countryId={countryId}"
                : null;

        return query is null
            ? GetAsync<DistrictLookupDto>("api/auth/districts", cancellationToken)
            : GetAsync<DistrictLookupDto>($"api/auth/districts?{query}", cancellationToken);
    }

    private async Task<IReadOnlyList<T>> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AuthApi");
        return await client.GetFromJsonAsync<List<T>>(url, cancellationToken) ?? [];
    }
}
