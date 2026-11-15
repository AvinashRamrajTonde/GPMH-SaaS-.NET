using System.Net;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Properties;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.MultiTenancy;

public sealed class CrossTenantAccessTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    [Fact]
    public async Task PropertiesEndpoint_Unauthenticated_Returns401Unauthorized()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/properties");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PropertiesEndpoint_CrossTenantReadUpdateDelete_AndHeaderSpoofing_AreBlocked()
    {
        Guid tenantAlphaId = Guid.NewGuid();
        Guid tenantBetaId = Guid.NewGuid();
        Guid userAlphaId = Guid.NewGuid();
        Guid userBetaId = Guid.NewGuid();

        using (IServiceScope seedScope = factory.Services.CreateScope())
        {
            AppDbContext db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Tenants.AddRange(
                new Tenant
                {
                    Id = tenantAlphaId,
                    TenantCode = $"sonimoha-{tenantAlphaId:N}"[..24],
                    LgdCode = $"171{Random.Shared.Next(100, 999)}",
                    NameEn = "Sonimoha GP",
                    NameMr = "सोनीमोहा ग्रा.पं.",
                    Status = TenantStatus.Active,
                },
                new Tenant
                {
                    Id = tenantBetaId,
                    TenantCode = $"kundewadi-{tenantBetaId:N}"[..24],
                    LgdCode = $"172{Random.Shared.Next(100, 999)}",
                    NameEn = "Kundewadi GP",
                    NameMr = "कुंडेवाडी ग्रा.पं.",
                    Status = TenantStatus.Active,
                });

            db.TenantMemberships.AddRange(
                new TenantMembership
                {
                    UserId = userAlphaId,
                    TenantId = tenantAlphaId,
                    Role = ApplicationRole.Gramsevak,
                    IsActive = true,
                },
                new TenantMembership
                {
                    UserId = userBetaId,
                    TenantId = tenantBetaId,
                    Role = ApplicationRole.Gramsevak,
                    IsActive = true,
                });

            await db.SaveChangesAsync();
        }

        HttpClient client = factory.CreateClient();

        // 1. Alpha creates Property P_A
        using HttpRequestMessage createReq = new(HttpMethod.Post, "/api/v1/properties")
        {
            Content = JsonContent.Create(new
            {
                wardNo = "W-01",
                propertyNo = "101",
                builtUpAreaSqFt = 650m,
                openPlotAreaSqFt = 350m,
                waterConnectionCode = "SINGLE",
                isTaxExempt = false,
            }),
        };
        createReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userAlphaId.ToString("D"));
        createReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));
        createReq.Headers.Add("X-Tenant-ID", tenantAlphaId.ToString("D"));

        HttpResponseMessage createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        PropertyDto? created = await createResp.Content.ReadFromJsonAsync<PropertyDto>();
        Assert.NotNull(created);
        Assert.Equal(tenantAlphaId, created.TenantId);
        Assert.Equal(1000m, created.TotalAreaSqFt);

        // 2. Spoofing X-Tenant-ID header -> 403 Forbidden
        using HttpRequestMessage spoofReq = new(HttpMethod.Get, "/api/v1/properties");
        spoofReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userAlphaId.ToString("D"));
        spoofReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));
        spoofReq.Headers.Add("X-Tenant-ID", tenantBetaId.ToString("D"));

        HttpResponseMessage spoofResp = await client.SendAsync(spoofReq);
        Assert.Equal(HttpStatusCode.Forbidden, spoofResp.StatusCode);

        // 3. Authenticated without any resolved tenant -> 403 Forbidden
        using HttpRequestMessage noTenantReq = new(HttpMethod.Get, "/api/v1/properties");
        noTenantReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userBetaId.ToString("D"));
        noTenantReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));

        HttpResponseMessage noTenantResp = await client.SendAsync(noTenantReq);
        Assert.Equal(HttpStatusCode.Forbidden, noTenantResp.StatusCode);

        // 4. Beta listing properties sees ZERO Alpha rows
        using HttpRequestMessage listBetaReq = new(HttpMethod.Get, "/api/v1/properties");
        listBetaReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userBetaId.ToString("D"));
        listBetaReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));
        listBetaReq.Headers.Add("X-Tenant-ID", tenantBetaId.ToString("D"));

        HttpResponseMessage listBetaResp = await client.SendAsync(listBetaReq);
        Assert.Equal(HttpStatusCode.OK, listBetaResp.StatusCode);
        List<PropertyDto>? betaProperties = await listBetaResp.Content.ReadFromJsonAsync<List<PropertyDto>>();
        Assert.NotNull(betaProperties);
        Assert.Empty(betaProperties);

        // 5. Beta attempting GET / PUT / DELETE by exact ID of Alpha property receives 404
        using HttpRequestMessage getByIdReq = new(HttpMethod.Get, $"/api/v1/properties/{created.Id:D}");
        getByIdReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userBetaId.ToString("D"));
        getByIdReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));
        getByIdReq.Headers.Add("X-Tenant-ID", tenantBetaId.ToString("D"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(getByIdReq)).StatusCode);

        using HttpRequestMessage updateReq = new(HttpMethod.Put, $"/api/v1/properties/{created.Id:D}")
        {
            Content = JsonContent.Create(new
            {
                builtUpAreaSqFt = 999m,
                openPlotAreaSqFt = 1m,
                waterConnectionCode = "NONE",
                isTaxExempt = true,
            }),
        };
        updateReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userBetaId.ToString("D"));
        updateReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));
        updateReq.Headers.Add("X-Tenant-ID", tenantBetaId.ToString("D"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(updateReq)).StatusCode);

        using HttpRequestMessage deleteReq = new(HttpMethod.Delete, $"/api/v1/properties/{created.Id:D}");
        deleteReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userBetaId.ToString("D"));
        deleteReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));
        deleteReq.Headers.Add("X-Tenant-ID", tenantBetaId.ToString("D"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(deleteReq)).StatusCode);

        // 6. Verify Alpha property was completely untouched
        using HttpRequestMessage verifyAlphaReq = new(HttpMethod.Get, $"/api/v1/properties/{created.Id:D}");
        verifyAlphaReq.Headers.Add(TestAuthHandler.TestUserIdHeader, userAlphaId.ToString("D"));
        verifyAlphaReq.Headers.Add(TestAuthHandler.TestUserRoleHeader, nameof(ApplicationRole.Gramsevak));
        verifyAlphaReq.Headers.Add("X-Tenant-ID", tenantAlphaId.ToString("D"));

        HttpResponseMessage verifyAlphaResp = await client.SendAsync(verifyAlphaReq);
        Assert.Equal(HttpStatusCode.OK, verifyAlphaResp.StatusCode);
        PropertyDto? intact = await verifyAlphaResp.Content.ReadFromJsonAsync<PropertyDto>();
        Assert.NotNull(intact);
        Assert.Equal(650m, intact.BuiltUpAreaSqFt);
    }

    [Fact]
    public async Task DbContext_FailClosedWithoutTenantContext_ReturnsZeroCitizensAndProperties()
    {
        Guid tenantId = Guid.NewGuid();

        using (IServiceScope seedScope = factory.Services.CreateScope())
        {
            ITenantContext tenantContext = seedScope.ServiceProvider.GetRequiredService<ITenantContext>();
            AppDbContext db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            tenantContext.SetTenant(tenantId);

            db.Citizens.Add(new Domain.Entities.Assessments.Citizen
            {
                CitizenCode = $"CIT-{tenantId:N}"[..16],
                FullNameMr = "रामराव पाटील",
                FullNameEn = "Ramrao Patil",
                Gender = "Male",
            });

            db.Properties.Add(new Domain.Entities.Assessments.Property
            {
                WardNo = "W-02",
                PropertyNo = $"P-{Random.Shared.Next(100, 999)}",
                BuiltUpAreaSqFt = 400m,
                OpenPlotAreaSqFt = 100m,
                TotalAreaSqFt = 500m,
            });

            await db.SaveChangesAsync();
        }

        using IServiceScope unsetScope = factory.Services.CreateScope();
        AppDbContext unsetDb = unsetScope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(0, await unsetDb.Citizens.CountAsync());
        Assert.Equal(0, await unsetDb.Properties.CountAsync());
    }
}

