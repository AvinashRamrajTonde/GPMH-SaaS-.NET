using System.Net;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Lgd;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.LgdClient;

public sealed class LgdClientTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    [Fact]
    public async Task LgdEndpoints_CoverAllDirectoryRoutes_AndPreserveSonimoha171391Data()
    {
        factory.LgdHandler.RecordedUris.Clear();
        HttpClient client = factory.CreateClient();

        // 1. GET /api/v1/lgd/districts
        List<LgdDistrictDto>? districts = await client.GetFromJsonAsync<List<LgdDistrictDto>>("/api/v1/lgd/districts");
        Assert.NotNull(districts);
        Assert.Contains(districts, d => d.District == "Beed" && d.DistrictCode == "497");

        // 2. GET /api/v1/lgd/tehsils?district=Beed
        List<LgdTehsilDto>? tehsils = await client.GetFromJsonAsync<List<LgdTehsilDto>>("/api/v1/lgd/tehsils?district=Beed");
        Assert.NotNull(tehsils);
        Assert.Contains(tehsils, t => t.Tehsil == "Dharur" && t.TehsilCode == "4192");

        // 3. GET /api/v1/lgd/panchayats?district=Beed&tehsil=Dharur
        List<LgdPanchayatSummaryDto>? panchayats = await client.GetFromJsonAsync<List<LgdPanchayatSummaryDto>>(
            "/api/v1/lgd/panchayats?district=Beed&tehsil=Dharur");
        Assert.NotNull(panchayats);
        Assert.Contains(panchayats, p => p.LgdCode == "171391" && p.GpNameEn == "Sonimoha");

        // 4. GET /api/v1/lgd/panchayat/171391
        LgdPanchayatDetailDto? detail = await client.GetFromJsonAsync<LgdPanchayatDetailDto>("/api/v1/lgd/panchayat/171391");
        Assert.NotNull(detail);
        Assert.Equal("171391", detail.LgdCode);
        Assert.Equal("Sonimoha", detail.GpNameEn);
        Assert.Equal("सोनीमोहा", detail.GpNameMr);
        Assert.Equal("Beed", detail.District);
        Assert.Equal("Dharur", detail.Tehsil);
        Assert.Equal("431124", detail.Pincode);

        // 5. GET /api/v1/lgd/search?q=Sonimoha&limit=10
        List<LgdPanchayatSummaryDto>? searchResults = await client.GetFromJsonAsync<List<LgdPanchayatSummaryDto>>(
            "/api/v1/lgd/search?q=Sonimoha&limit=10");
        Assert.NotNull(searchResults);
        Assert.Contains(searchResults, r => r.LgdCode == "171391" && r.GpNameEn == "Sonimoha");

        // 6. GET /api/v1/lgd/stats
        LgdStatsDto? stats = await client.GetFromJsonAsync<LgdStatsDto>("/api/v1/lgd/stats");
        Assert.NotNull(stats);
        Assert.Equal("Maharashtra", stats.State);
        Assert.Equal(27, stats.StateCode);
        Assert.Equal(27931, stats.TotalPanchayats);

        // 7. Unknown LGD code -> 404; short search query -> 400
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/lgd/panchayat/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/lgd/search?q=a")).StatusCode);

        // Verify all outbound upstream URIs preserved /api/v1/ path segment
        Assert.All(factory.LgdHandler.RecordedUris, uri =>
            Assert.StartsWith("https://api.grampanchayatmh.in/api/v1/", uri?.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task LgdDirectoryClient_ResolvesAllTypedOperationsViaDI()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ILgdDirectoryClient lgdClient = scope.ServiceProvider.GetRequiredService<ILgdDirectoryClient>();

        IReadOnlyList<LgdDistrictDto> districts = await lgdClient.GetDistrictsAsync(CancellationToken.None);
        IReadOnlyList<LgdTehsilDto> tehsils = await lgdClient.GetTehsilsAsync("Beed", CancellationToken.None);
        IReadOnlyList<LgdPanchayatSummaryDto> gps = await lgdClient.GetPanchayatsAsync("Beed", "Dharur", CancellationToken.None);
        LgdPanchayatDetailDto? detail = await lgdClient.GetPanchayatByLgdCodeAsync("171391", CancellationToken.None);
        IReadOnlyList<LgdPanchayatSummaryDto> search = await lgdClient.SearchPanchayatsAsync("Sonimoha", 10, CancellationToken.None);
        LgdStatsDto? stats = await lgdClient.GetStatsAsync(CancellationToken.None);

        Assert.NotEmpty(districts);
        Assert.NotEmpty(tehsils);
        Assert.NotEmpty(gps);
        Assert.NotNull(detail);
        Assert.Equal("Sonimoha", detail.GpNameEn);
        Assert.NotEmpty(search);
        Assert.NotNull(stats);
    }
}

