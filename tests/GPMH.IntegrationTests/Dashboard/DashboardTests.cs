using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auth;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Dashboard;

public sealed class DashboardTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    [Fact]
    public async Task SummaryAndSearch_ReturnTenantScopedKpisChecklistActivityAndMaskedCitizenPii()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        string userAName = $"dash.a.{Guid.NewGuid():N}"[..20];
        const string pass = "Dash@2026!";

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            IPasswordHasherService hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

            db.Tenants.AddRange(
                new Tenant { Id = tenantA, TenantCode = $"dasha-{tenantA:N}"[..20], LgdCode = $"31{Random.Shared.Next(1000, 9999)}", NameEn = "Dash Alpha GP", NameMr = "डॅश अल्फा", Status = TenantStatus.Active },
                new Tenant { Id = tenantB, TenantCode = $"dashb-{tenantB:N}"[..20], LgdCode = $"32{Random.Shared.Next(1000, 9999)}", NameEn = "Dash Beta GP", NameMr = "डॅश बीटा", Status = TenantStatus.Active });

            User userA = new()
            {
                Username = userAName,
                FullNameEn = "Dash Clerk",
                FullNameMr = "लिपिक",
                PasswordHash = hasher.HashPassword(pass),
            };
            db.Users.Add(userA);
            db.TenantMemberships.Add(new TenantMembership { UserId = userA.Id, TenantId = tenantA, Role = ApplicationRole.Gramsevak, IsDefault = true });

            tenantContext.SetTenant(tenantA);
            db.Properties.Add(new Property
            {
                TenantId = tenantA,
                WardNo = "W-01",
                PropertyNo = "101",
                BuiltUpAreaSqFt = 225m,
                OpenPlotAreaSqFt = 0m,
                TotalAreaSqFt = 225m,
            });
            db.Citizens.Add(new Citizen
            {
                TenantId = tenantA,
                CitizenCode = "CIT-101",
                WardNo = "W-01",
                FullNameEn = "Rameshwar Patil",
                FullNameMr = "रामेश्वर पाटील",
                MobileNo = "9822001122",
                Gender = "Male",
                AadhaarLast4 = "4567",
            });
            await db.SaveChangesAsync();

            tenantContext.SetTenant(tenantB);
            db.Citizens.Add(new Citizen
            {
                TenantId = tenantB,
                CitizenCode = "CIT-BETA",
                WardNo = "W-09",
                FullNameEn = "Rameshwar Secret Beta",
                FullNameMr = "बीटा नागरिक",
                Gender = "Male",
                AadhaarLast4 = "9999",
            });
            await db.SaveChangesAsync();
            tenantContext.Clear();
        }

        HttpClient client = factory.CreateClient();
        HttpResponseMessage loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = userAName, password = pass, tenantId = tenantA });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        string jwt = (await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;

        // 1. Summary KPIs
        using HttpRequestMessage summaryReq = new(HttpMethod.Get, "/api/v1/dashboard/summary");
        summaryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        HttpResponseMessage summaryResp = await client.SendAsync(summaryReq);
        Assert.Equal(HttpStatusCode.OK, summaryResp.StatusCode);

        DashboardSummaryDto summary = (await summaryResp.Content.ReadFromJsonAsync<DashboardSummaryDto>())!;
        Assert.Equal(tenantA, summary.TenantId);
        Assert.Equal(1, summary.TotalProperties);
        Assert.Equal(5, summary.ChecklistSteps.Count);
        Assert.NotEmpty(summary.RecentActivity);

        // 2. Global search matches Alpha citizen with masked Aadhaar and leaks zero Beta citizens
        using HttpRequestMessage searchReq = new(HttpMethod.Get, "/api/v1/dashboard/search?q=Rameshwar");
        searchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        HttpResponseMessage searchResp = await client.SendAsync(searchReq);
        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);

        DashboardSearchResultDto search = (await searchResp.Content.ReadFromJsonAsync<DashboardSearchResultDto>())!;
        var hit = Assert.Single(search.Citizens);
        Assert.Equal("CIT-101", hit.CitizenCode);
        Assert.Equal("XXXX-XXXX-4567", hit.AadhaarMasked);
    }
}

