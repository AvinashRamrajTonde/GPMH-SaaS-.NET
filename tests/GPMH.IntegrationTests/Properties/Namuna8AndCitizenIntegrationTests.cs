using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Citizens.Commands;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using GPMH.WebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Properties;

public sealed class Namuna8AndCitizenIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private async Task<(Guid TenantId, Guid GramsevakUserId, Guid AuditorUserId)> SeedTenantAsync(bool seedFormulasAndYear = true)
    {
        Guid tenantId = Guid.NewGuid();
        Guid gramsevakUserId = Guid.NewGuid();
        Guid auditorUserId = Guid.NewGuid();
        string lgdCode = $"57{Random.Shared.Next(1000, 9999)}";

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        IDefaultFormulaSeeder formulaSeeder = scope.ServiceProvider.GetRequiredService<IDefaultFormulaSeeder>();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantCode = $"gp-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = $"Namuna8 GP {lgdCode}",
            NameMr = $"नमुना८ ग्रा.पं. {lgdCode}",
            Status = TenantStatus.Active,
        });

        db.TenantMemberships.AddRange(
            new TenantMembership
            {
                TenantId = tenantId,
                UserId = gramsevakUserId,
                Role = ApplicationRole.Gramsevak,
                IsDefault = true,
                IsActive = true,
            },
            new TenantMembership
            {
                TenantId = tenantId,
                UserId = auditorUserId,
                Role = ApplicationRole.Auditor,
                IsDefault = true,
                IsActive = true,
            });

        tenantContext.SetTenant(tenantId);

        if (seedFormulasAndYear)
        {
            db.AssessmentYears.Add(new AssessmentYear
            {
                TenantId = tenantId,
                YearLabel = "2026-27",
                StartYear = 2026,
                EndYear = 2027,
                IsActive = true,
                IsLocked = false,
            });

            await formulaSeeder.SeedDefaultsForTenantAsync(tenantId, CancellationToken.None);
        }

        await db.SaveChangesAsync();
        return (tenantId, gramsevakUserId, auditorUserId);
    }

    private static HttpRequestMessage CreateTenantRequest(
        HttpMethod method,
        string url,
        Guid userId,
        ApplicationRole role,
        Guid tenantId,
        object? jsonBody = null)
    {
        HttpRequestMessage req = new(method, url);
        req.Headers.Add(TestAuthHandler.TestUserIdHeader, userId.ToString("D"));
        req.Headers.Add(TestAuthHandler.TestUserRoleHeader, role.ToString());
        req.Headers.Add("X-Tenant-ID", tenantId.ToString("D"));

        if (jsonBody is not null)
        {
            req.Content = JsonContent.Create(jsonBody);
        }

        return req;
    }

    [Fact]
    public async Task CitizenRegister_DpdpAadhaarEncryption_BlindIndexSearch_AndMasking()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedTenantAsync(seedFormulasAndYear: false);
        using HttpClient client = factory.CreateClient();

        const string rawAadhaar = "987654321012";

        // 1. Missing DPDP consent must fail validation
        HttpResponseMessage consentFail = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(
                CitizenCode: "CIT-DPDP-FAIL",
                WardNo: "1",
                FullNameMr: "रमेश कुलकर्णी",
                FullNameEn: "Ramesh Kulkarni",
                Gender: "Male",
                MobileNo: "9876543210",
                AadhaarNumber: rawAadhaar,
                SocialCategory: "General",
                ExemptionCategory: "NotExempted",
                DpdpConsentAccepted: false)));

        Assert.Equal(HttpStatusCode.BadRequest, consentFail.StatusCode);

        // 2. Valid registration encrypts Aadhaar, computes blind index, and masks DTO
        HttpResponseMessage createResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(
                CitizenCode: "CIT-0101",
                WardNo: "1",
                FullNameMr: "रमेश कुलकर्णी",
                FullNameEn: "Ramesh Kulkarni",
                Gender: "Male",
                MobileNo: "9876543210",
                AadhaarNumber: "9876 5432 1012",
                SocialCategory: "General",
                ExemptionCategory: "NotExempted",
                DpdpConsentAccepted: true)));

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        string rawResponseJson = await createResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain(rawAadhaar, rawResponseJson, StringComparison.Ordinal);

        CitizenDto? created = JsonSerializer.Deserialize<CitizenDto>(
            rawResponseJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(created);
        Assert.Equal("XXXX-XXXX-1012", created.AadhaarMasked);

        // Verify persisted cipher & blind index in DB
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);

            Citizen? entity = await db.Citizens.AsNoTracking().FirstOrDefaultAsync(c => c.Id == created.Id);
            Assert.NotNull(entity);
            Assert.NotNull(entity.AadhaarEncrypted);
            Assert.True(entity.AadhaarEncrypted.Length > rawAadhaar.Length);
            Assert.False(entity.AadhaarEncrypted.SequenceEqual(Encoding.UTF8.GetBytes(rawAadhaar)));
            Assert.False(string.IsNullOrWhiteSpace(entity.AadhaarBlindIndex));
            Assert.Equal("1012", entity.AadhaarLast4);
        }

        // 3. Blind-index exact search using 12-digit Aadhaar works cleanly
        HttpResponseMessage searchResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/citizens?search={rawAadhaar}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));

        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        PagedCitizensDto? page = await searchResp.Content.ReadFromJsonAsync<PagedCitizensDto>();
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(created.Id, page.Items[0].Id);
        Assert.Equal("XXXX-XXXX-1012", page.Items[0].AadhaarMasked);

        // 4. Duplicate Aadhaar in the same tenant must fail with 400 Bad Request
        HttpResponseMessage dupResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(
                CitizenCode: "CIT-0102",
                WardNo: "1",
                FullNameMr: "अमित देशमुख",
                FullNameEn: "Amit Deshmukh",
                Gender: "Male",
                MobileNo: "9876543211",
                AadhaarNumber: rawAadhaar,
                DpdpConsentAccepted: true)));

        Assert.Equal(HttpStatusCode.BadRequest, dupResp.StatusCode);

        // 5. Update without Aadhaar preserves encrypted vault; update with new Aadhaar replaces mask
        HttpResponseMessage keepResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/citizens/{created.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new UpdateCitizenRequest(
                WardNo: "2",
                FullNameMr: "रमेश शामराव कुलकर्णी",
                FullNameEn: "Ramesh Shamrao Kulkarni",
                Gender: "Male",
                MobileNo: "9876543210",
                AadhaarNumber: null,
                SocialCategory: "General",
                ExemptionCategory: "SeniorCitizen",
                IsActive: true)));

        Assert.Equal(HttpStatusCode.OK, keepResp.StatusCode);
        CitizenDto? kept = await keepResp.Content.ReadFromJsonAsync<CitizenDto>();
        Assert.NotNull(kept);
        Assert.Equal("XXXX-XXXX-1012", kept.AadhaarMasked);
        Assert.Equal("SeniorCitizen", kept.ExemptionCategory);

        HttpResponseMessage replaceResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/citizens/{created.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new UpdateCitizenRequest(
                WardNo: "2",
                FullNameMr: "रमेश शामराव कुलकर्णी",
                FullNameEn: "Ramesh Shamrao Kulkarni",
                Gender: "Male",
                MobileNo: "9876543210",
                AadhaarNumber: "123456789098",
                SocialCategory: "General",
                ExemptionCategory: "SeniorCitizen",
                IsActive: true)));

        Assert.Equal(HttpStatusCode.OK, replaceResp.StatusCode);
        CitizenDto? replaced = await replaceResp.Content.ReadFromJsonAsync<CitizenDto>();
        Assert.NotNull(replaced);
        Assert.Equal("XXXX-XXXX-9098", replaced.AadhaarMasked);

        using IServiceScope auditScope = factory.Services.CreateScope();
        AppDbContext auditDb = auditScope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<AuditLog> citizenAudits = await auditDb.AuditLogs
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.EntityName == nameof(Citizen) && l.EntityId == created.Id.ToString())
            .ToListAsync();
        Assert.True(citizenAudits.Count >= 2);
    }

    [Fact]
    public async Task Namuna8MultiFloorRegistration_AndUpdate_PersistsOwnersFloorsTaxAndDraftDemand()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedTenantAsync(seedFormulasAndYear: true);
        using HttpClient client = factory.CreateClient();

        // Create 2 joint owners
        HttpResponseMessage c1Resp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(
                CitizenCode: "CIT-A",
                WardNo: "3",
                FullNameMr: "विजय पाटील",
                FullNameEn: "Vijay Patil",
                Gender: "Male",
                MobileNo: "9822001100",
                AadhaarNumber: "111122223333",
                DpdpConsentAccepted: true)));
        CitizenDto citizen1 = (await c1Resp.Content.ReadFromJsonAsync<CitizenDto>())!;

        HttpResponseMessage c2Resp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(
                CitizenCode: "CIT-B",
                WardNo: "3",
                FullNameMr: "सुनिता पाटील",
                FullNameEn: "Sunita Patil",
                Gender: "Female",
                MobileNo: "9822001101",
                AadhaarNumber: "444455556666",
                DpdpConsentAccepted: true)));
        CitizenDto citizen2 = (await c2Resp.Content.ReadFromJsonAsync<CitizenDto>())!;

        // 1. Create full Multi-Floor Namuna 8 assessment (60% + 40% owners, 2 floors + open plot)
        HttpResponseMessage createPropResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: "3",
                PropertyNo: "108/A",
                BuiltUpAreaSqFt: 400m,
                OpenPlotAreaSqFt: 200m,
                BoundaryEast: "Main Road",
                BoundaryWest: "Plot 107",
                BoundaryNorth: "Gram Panchayat Lane",
                BoundarySouth: "Nullah",
                WaterConnectionCode: "SPECIAL",
                IsTaxExempt: false,
                StreetOrLaneName: "Shivaji Chowk Road",
                CitySurveyOrGutNo: "CTS-412/1",
                OccupierName: "Self + Grocery Shop",
                PropertyDescription: "Two-storey residential cum commercial house",
                OtherTax: 150m,
                OpenPlotConstructionCode: "OPEN_PLOT",
                Owners:
                [
                    new(citizen1.Id, 60m, true),
                    new(citizen2.Id, 40m, false),
                ],
                Floors:
                [
                    new(0, "Ground Floor", "RCC_PUCCA", "RESIDENTIAL", 20m, 15m, null, 2022),
                    new(1, "First Floor", "HUT_MUD", "COMMERCIAL", 10m, 10m, null, 2020),
                ],
                CalculateTax: true)));

        Assert.Equal(HttpStatusCode.Created, createPropResp.StatusCode);
        PropertyDto? createdProp = await createPropResp.Content.ReadFromJsonAsync<PropertyDto>();
        Assert.NotNull(createdProp);
        Assert.Equal(400m, createdProp.BuiltUpAreaSqFt);
        Assert.Equal(600m, createdProp.TotalAreaSqFt);
        Assert.Equal(2, createdProp.FloorCount);
        Assert.NotNull(createdProp.TotalAnnualTax);
        Assert.True(createdProp.TotalAnnualTax > 0m);
        Assert.Equal("Vijay Patil", createdProp.PrimaryOwnerNameEn);

        // 2. Verify Property 360 detail endpoint returns owners, constructions, TaxCalc & Draft Demand
        HttpResponseMessage detailResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{createdProp.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);
        PropertyDetailDto? detail = await detailResp.Content.ReadFromJsonAsync<PropertyDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(2, detail.Owners.Count);
        Assert.Equal(2, detail.Constructions.Count);
        Assert.All(detail.Constructions, c => Assert.True(c.AreaSqM > 0m));
        Assert.NotNull(detail.TaxCalc);
        Assert.Equal(createdProp.TotalAnnualTax, detail.TaxCalc.TotalTax);
        Assert.Contains("floorBreakdowns", detail.TaxCalc.CalculationSnapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(detail.Demand);
        Assert.Equal("DraftAssessment", detail.Demand.Status);
        Assert.Equal(detail.TaxCalc.TotalTax, detail.Demand.CurrentDemand);
        Assert.Equal(detail.TaxCalc.TotalTax, detail.Demand.PendingBalance);

        // 3. Verify Hub KPIs & Dashboard Summary reflect assessed current FY demand
        HttpResponseMessage hubResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/properties/hub-summary",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, hubResp.StatusCode);
        PropertyHubKpisDto? hubKpis = await hubResp.Content.ReadFromJsonAsync<PropertyHubKpisDto>();
        Assert.NotNull(hubKpis);
        Assert.Equal(1, hubKpis.TotalProperties);
        Assert.Equal(detail.TaxCalc.TotalTax, hubKpis.TotalAssessedDemand);

        HttpResponseMessage dashResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/dashboard/summary",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, dashResp.StatusCode);
        DashboardSummaryDto? dash = await dashResp.Content.ReadFromJsonAsync<DashboardSummaryDto>();
        Assert.NotNull(dash);
        Assert.Equal(1, dash.TotalProperties);
        Assert.Equal(detail.TaxCalc.TotalTax, dash.CurrentFyDemand);

        // 4. Update property: mark Tax Exempt = true, switch to 1 owner (100%) and 1 floor (300 sq.ft)
        HttpResponseMessage updateResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/properties/{createdProp.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new UpdatePropertyRequest(
                BuiltUpAreaSqFt: 300m,
                OpenPlotAreaSqFt: 0m,
                BoundaryEast: "Main Road",
                BoundaryWest: "Plot 107",
                BoundaryNorth: "Gram Panchayat Lane",
                BoundarySouth: "Nullah",
                WaterConnectionCode: "GENERAL",
                IsTaxExempt: true,
                StreetOrLaneName: "Shivaji Chowk Road",
                CitySurveyOrGutNo: "CTS-412/1",
                OccupierName: null,
                PropertyDescription: "Exempted heritage structure",
                OtherTax: 0m,
                OpenPlotConstructionCode: null,
                Owners: [new(citizen1.Id, 100m, true)],
                Floors: [new(0, "Ground Floor", "RCC_PUCCA", "RESIDENTIAL", 20m, 15m, null, 2022)],
                CalculateTax: true)));

        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        HttpResponseMessage updatedDetailResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{createdProp.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        PropertyDetailDto? updatedDetail = await updatedDetailResp.Content.ReadFromJsonAsync<PropertyDetailDto>();
        Assert.NotNull(updatedDetail);
        Assert.Single(updatedDetail.Owners);
        Assert.Single(updatedDetail.Constructions);
        Assert.True(updatedDetail.IsTaxExempt);
        Assert.NotNull(updatedDetail.Demand);
        Assert.Equal(0m, updatedDetail.Demand.CurrentDemand);
        Assert.Equal(0m, updatedDetail.Demand.PendingBalance);
    }

    [Fact]
    public async Task Namuna8Validation_TenantIsolation_MinimalLegacyCompatibility_AndRbac()
    {
        (Guid tenantA, Guid gramsevakA, Guid auditorA) = await SeedTenantAsync(seedFormulasAndYear: true);
        (Guid tenantB, Guid gramsevakB, _) = await SeedTenantAsync(seedFormulasAndYear: true);
        using HttpClient client = factory.CreateClient();

        // Create citizen in Tenant A & Tenant B
        CitizenDto citizenA = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new CreateCitizenCommand(null, "1", "नागरिक अ", "Citizen A", "Male", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        CitizenDto citizenB = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakB,
            ApplicationRole.Gramsevak,
            tenantB,
            new CreateCitizenCommand(null, "1", "नागरिक ब", "Citizen B", "Female", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        // 1. Auditor role cannot write citizen or property
        HttpResponseMessage auditorWriteCitizen = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            auditorA,
            ApplicationRole.Auditor,
            tenantA,
            new CreateCitizenCommand(null, "1", "ऑडिट चाचणी", "Audit Test", "Male", DpdpConsentAccepted: true)));
        Assert.Equal(HttpStatusCode.Forbidden, auditorWriteCitizen.StatusCode);

        // 2. Ownership share total != 100% must return 400 Bad Request
        HttpResponseMessage badShareResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new CreatePropertyCommand(
                WardNo: "5",
                PropertyNo: "201",
                BuiltUpAreaSqFt: 300m,
                OpenPlotAreaSqFt: 100m,
                Owners: [new(citizenA.Id, 75m, true)],
                Floors: [new(0, "Ground", "RCC_PUCCA", "RESIDENTIAL", 20m, 15m, null, 2023)],
                CalculateTax: true)));
        Assert.Equal(HttpStatusCode.BadRequest, badShareResp.StatusCode);

        // 3. Cross-tenant CitizenId in Owners must return 400 Bad Request
        HttpResponseMessage crossCitizenOwnerResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new CreatePropertyCommand(
                WardNo: "5",
                PropertyNo: "202",
                BuiltUpAreaSqFt: 300m,
                OpenPlotAreaSqFt: 100m,
                Owners: [new(citizenB.Id, 100m, true)],
                Floors: [new(0, "Ground", "RCC_PUCCA", "RESIDENTIAL", 20m, 15m, null, 2023)],
                CalculateTax: true)));
        Assert.Equal(HttpStatusCode.BadRequest, crossCitizenOwnerResp.StatusCode);

        // 4. Minimal legacy call (no Owners/Floors) succeeds and rejects duplicate Ward/PropertyNo
        HttpResponseMessage minimalResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new CreatePropertyCommand(
                WardNo: "5",
                PropertyNo: "205",
                BuiltUpAreaSqFt: 250m,
                OpenPlotAreaSqFt: 50m)));
        Assert.Equal(HttpStatusCode.Created, minimalResp.StatusCode);
        PropertyDto createdA = (await minimalResp.Content.ReadFromJsonAsync<PropertyDto>())!;

        HttpResponseMessage dupPropResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new CreatePropertyCommand(
                WardNo: "5",
                PropertyNo: "205",
                BuiltUpAreaSqFt: 250m,
                OpenPlotAreaSqFt: 50m)));
        Assert.Equal(HttpStatusCode.BadRequest, dupPropResp.StatusCode);

        // 5. Cross-tenant reads on citizen and property fail with 404 NotFound
        HttpResponseMessage crossCitizenGet = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/citizens/{citizenB.Id}",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA));
        Assert.Equal(HttpStatusCode.NotFound, crossCitizenGet.StatusCode);

        HttpResponseMessage crossPropGet = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{createdA.Id}",
            gramsevakB,
            ApplicationRole.Gramsevak,
            tenantB));
        Assert.Equal(HttpStatusCode.NotFound, crossPropGet.StatusCode);
    }
}

