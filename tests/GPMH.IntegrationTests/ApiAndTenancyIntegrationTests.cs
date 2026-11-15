using System.Net;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Lgd;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Domain.Services;
using GPMH.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests;

public sealed class ApiAndTenancyIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyJson()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task LgdLookup_InvalidCode_ReturnsRfc9457BadRequest()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/lgd/panchayat/abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Validation Failed", problem.Title);
    }

    [Fact]
    public async Task LgdLookup_PreservesApiV1PrefixOnOutboundRequestUri()
    {
        factory.LgdHandler.RecordedUris.Clear();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/lgd/panchayat/171391");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        LgdPanchayatDetailDto? detail = await response.Content.ReadFromJsonAsync<LgdPanchayatDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal("171391", detail.LgdCode);
        Assert.Single(factory.LgdHandler.RecordedUris);
        Assert.Equal("https://api.grampanchayatmh.in/api/v1/panchayat/171391", factory.LgdHandler.RecordedUris[0]?.ToString());
    }

    [Fact]
    public async Task TaxPreviewEndpoint_ReturnsChincholiKhurdGoldenValues()
    {
        HttpClient client = factory.CreateClient();
        PropertyTaxCalculationInput payload = new(
            AssessmentStartYear: 2026,
            Floors:
            [
                new FloorTaxInput(15m, 15m, null, 760m, 5852m, 1.0m, 0.5m, 2021),
            ],
            DepreciationSlabs:
            [
                new DepreciationSlabInput(0, 2, 100m),
                new DepreciationSlabInput(2, 5, 95m),
            ],
            AreaSlabTaxes: new AreaSlabTaxInput(40m, 40m, 100m, 0m));

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/tax/preview", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PropertyTaxCalculationResult? result = await response.Content.ReadFromJsonAsync<PropertyTaxCalculationResult>();
        Assert.NotNull(result);
        Assert.Equal(66m, result.BuildingTax);
        Assert.Equal(246m, result.TotalTax);
    }

    [Fact]
    public async Task TenantResolutionMiddleware_RejectsUnauthorizedCrossTenantHeaderWith403()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        Guid userA = Guid.NewGuid();

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TenantMemberships.Add(new TenantMembership
            {
                UserId = userA,
                TenantId = tenantA,
                Role = ApplicationRole.Gramsevak,
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }

        HttpClient client = factory.CreateClient();

        using HttpRequestMessage unauthorizedReq = new(HttpMethod.Get, "/health");
        unauthorizedReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userA.ToString("D"));
        unauthorizedReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));
        unauthorizedReq.Headers.Add("X-Tenant-ID", tenantB.ToString("D"));

        HttpResponseMessage denied = await client.SendAsync(unauthorizedReq);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using HttpRequestMessage authorizedReq = new(HttpMethod.Get, "/health");
        authorizedReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userA.ToString("D"));
        authorizedReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));
        authorizedReq.Headers.Add("X-Tenant-ID", tenantA.ToString("D"));

        HttpResponseMessage allowed = await client.SendAsync(authorizedReq);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task EfCoreGlobalQueryFilter_FailClosedAndAuditTrailTracksFieldDiffs()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        using (IServiceScope seedScope = factory.Services.CreateScope())
        {
            ITenantContext tenantCtx = seedScope.ServiceProvider.GetRequiredService<ITenantContext>();
            AppDbContext db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            tenantCtx.SetTenant(tenantA);
            GramPanchayat gpA = new()
            {
                LgdCode = "990001",
                DistrictCode = "1",
                DistrictName = "Amravati",
                TehsilCode = "10",
                TehsilName = "Morshi",
                PanchayatNameEn = "Alpha GP",
                PanchayatNameMr = "अल्फा ग्रा.पं.",
            };
            db.GramPanchayats.Add(gpA);
            await db.SaveChangesAsync();

            tenantCtx.SetTenant(tenantB);
            GramPanchayat gpB = new()
            {
                LgdCode = "990002",
                DistrictCode = "2",
                DistrictName = "Beed",
                TehsilCode = "20",
                TehsilName = "Dharur",
                PanchayatNameEn = "Beta GP",
                PanchayatNameMr = "बीटा ग्रा.पं.",
            };
            db.GramPanchayats.Add(gpB);
            await db.SaveChangesAsync();

            // Mutate gpB to trigger Modified AuditLog diff
            gpB.UpiId = "betagp@sbi";
            await db.SaveChangesAsync();
        }

        // Unresolved tenant context MUST return 0 rows (fail-closed)
        using (IServiceScope unsetScope = factory.Services.CreateScope())
        {
            AppDbContext db = unsetScope.ServiceProvider.GetRequiredService<AppDbContext>();
            int unsetCount = await db.GramPanchayats.CountAsync();
            Assert.Equal(0, unsetCount);
        }

        // Tenant A context MUST see ONLY Alpha GP and verify AuditLog entries exist
        using (IServiceScope verifyScope = factory.Services.CreateScope())
        {
            ITenantContext tenantCtx = verifyScope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantCtx.SetTenant(tenantA);
            AppDbContext db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

            List<GramPanchayat> visibleToA = await db.GramPanchayats.AsNoTracking().ToListAsync();
            Assert.Single(visibleToA);
            Assert.Equal("990001", visibleToA[0].LgdCode);

            List<AuditLog> betaModifiedLogs = await db.AuditLogs
                .AsNoTracking()
                .Where(l => l.TenantId == tenantB && l.EntityName == nameof(GramPanchayat) && l.Action == "Modified")
                .ToListAsync();

            Assert.Single(betaModifiedLogs);
            Assert.Contains("UpiId", betaModifiedLogs[0].ChangedColumnsJson, StringComparison.Ordinal);
            Assert.Contains("betagp@sbi", betaModifiedLogs[0].NewValuesJson, StringComparison.Ordinal);
        }
    }
}

