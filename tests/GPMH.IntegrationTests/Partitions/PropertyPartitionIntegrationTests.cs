using System.Net;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Application.DTOs.Partitions;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Citizens.Commands;
using GPMH.Application.Features.Partitions.Commands;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using GPMH.WebAPI.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Partitions;

public sealed class PropertyPartitionIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static int _lgdSequence = 700000;

    private async Task<(Guid TenantId, Guid GramsevakId, Guid OperatorId)> SeedPartitionTenantAsync()
    {
        Guid tenantId = Guid.NewGuid();
        Guid gramsevakId = Guid.NewGuid();
        Guid operatorId = Guid.NewGuid();
        string lgdCode = Interlocked.Increment(ref _lgdSequence).ToString();

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        IDefaultFormulaSeeder seeder = scope.ServiceProvider.GetRequiredService<IDefaultFormulaSeeder>();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantCode = $"gp-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = $"Partition GP {lgdCode}",
            NameMr = $"विभाजन ग्रा.पं. {lgdCode}",
            Status = TenantStatus.Active,
        });

        db.TenantMemberships.AddRange(
            new TenantMembership
            {
                TenantId = tenantId,
                UserId = gramsevakId,
                Role = ApplicationRole.Gramsevak,
                IsDefault = true,
                IsActive = true,
            },
            new TenantMembership
            {
                TenantId = tenantId,
                UserId = operatorId,
                Role = ApplicationRole.GP_Operator,
                IsDefault = true,
                IsActive = true,
            });

        tenantContext.SetTenant(tenantId);

        db.GramPanchayats.Add(new GramPanchayat
        {
            TenantId = tenantId,
            LgdCode = lgdCode,
            DistrictCode = "503",
            DistrictName = "अमरावती",
            TehsilCode = "4010",
            TehsilName = "चांदूर बाजार",
            PanchayatNameEn = $"Partition Village {lgdCode}",
            PanchayatNameMr = $"सावळी {lgdCode}",
            GramsevakName = "श्री. आर. डी. कदम",
        });

        db.AssessmentYears.Add(new AssessmentYear
        {
            TenantId = tenantId,
            YearLabel = "2026-27",
            StartYear = 2026,
            EndYear = 2027,
            IsActive = true,
            IsLocked = false,
        });

        await seeder.SeedDefaultsForTenantAsync(tenantId, CancellationToken.None);
        await db.SaveChangesAsync();

        return (tenantId, gramsevakId, operatorId);
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

    private async Task<CitizenDto> CreateCitizenAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        string citizenCode,
        string nameEn,
        string nameMr,
        string aadhaar,
        string mobile)
    {
        HttpResponseMessage res = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            userId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(citizenCode, "W-01", nameMr, nameEn, "Male", mobile, aadhaar, DpdpConsentAccepted: true)));
        res.EnsureSuccessStatusCode();
        CitizenDto? dto = await res.Content.ReadFromJsonAsync<CitizenDto>();
        Assert.NotNull(dto);
        return dto;
    }

    private async Task<PropertyDto> CreateAssessedPropertyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid primaryCitizenId,
        string propertyNo,
        decimal builtUpSqFt,
        decimal openPlotSqFt)
    {
        HttpResponseMessage res = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            userId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: "W-01",
                PropertyNo: propertyNo,
                BuiltUpAreaSqFt: builtUpSqFt,
                OpenPlotAreaSqFt: openPlotSqFt,
                BoundaryEast: "Main Road",
                BoundaryWest: "Plot 102",
                BoundaryNorth: "Lane",
                BoundarySouth: "Nullah",
                WaterConnectionCode: "NONE",
                IsTaxExempt: false,
                StreetOrLaneName: "Shivaji Chowk",
                CitySurveyOrGutNo: "CTS-44",
                OtherTax: 0m,
                Owners:
                [
                    new PropertyOwnerRequestItem(primaryCitizenId, 100m, true),
                ],
                Floors:
                [
                    new PropertyFloorRequestItem(
                        FloorNo: 0,
                        FloorLabel: "तळमजला",
                        ConstructionTypeCode: "RCC_PUCCA",
                        UsageCode: "RESIDENTIAL",
                        LengthFt: 0m,
                        WidthFt: 0m,
                        AreaSqFt: builtUpSqFt,
                        ConstructionStartYear: 2024),
                ],
                CalculateTax: true)));

        res.EnsureSuccessStatusCode();
        PropertyDto? dto = await res.Content.ReadFromJsonAsync<PropertyDto>();
        Assert.NotNull(dto);
        return dto;
    }

    private async Task SettlePropertyDuesInDbAsync(Guid tenantId, Guid propertyId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(tenantId);

        List<PropertyDemand> demands = await db.PropertyDemands
            .Where(d => d.PropertyId == propertyId)
            .ToListAsync();

        foreach (PropertyDemand d in demands)
        {
            d.PaidAmount = d.ArrearsBalance + d.CurrentDemand;
            d.PendingBalance = 0m;
            d.Status = "Settled";
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PartitionGuards_ZeroDuesGate_AreaConservation_AndRolePolicy_EnforcedStrictly()
    {
        (Guid tenantId, Guid gramsevakId, Guid operatorId) = await SeedPartitionTenantAsync();
        using HttpClient client = factory.CreateClient();

        CitizenDto parentOwner = await CreateCitizenAsync(
            client, tenantId, gramsevakId, "CIT-P1", "Balwan Dinkar", "बलवान दिनकर", "555511112222", "9822112201");
        CitizenDto heir1 = await CreateCitizenAsync(
            client, tenantId, gramsevakId, "CIT-H1", "Suresh Dinkar", "सुरेश दिनकर", "555511112223", "9822112202");
        CitizenDto heir2 = await CreateCitizenAsync(
            client, tenantId, gramsevakId, "CIT-H2", "Mahesh Dinkar", "महेश दिनकर", "555511112224", "9822112203");

        PropertyDto property = await CreateAssessedPropertyAsync(
            client, tenantId, gramsevakId, parentOwner.Id, "201", builtUpSqFt: 400m, openPlotSqFt: 200m);

        HttpResponseMessage eligBlocked = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{property.Id}/partition-eligibility",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, eligBlocked.StatusCode);
        PartitionEligibilityDto? eligDto = await eligBlocked.Content.ReadFromJsonAsync<PartitionEligibilityDto>();
        Assert.NotNull(eligDto);
        Assert.False(eligDto.ZeroDuesGatePassed);
        Assert.True(eligDto.PendingDues > 0m);

        ExecutePropertyPartitionRequest operatorReq = new(
            ResolutionNo: "RES-27/1",
            ResolutionDate: new DateOnly(2026, 11, 16),
            ChildPartitions:
            [
                new PartitionChildRequestItem("201/1", [new PropertyOwnerRequestItem(heir1.Id, 100m, true)], 200m, 100m),
                new PartitionChildRequestItem("201/2", [new PropertyOwnerRequestItem(heir2.Id, 100m, true)], 200m, 100m),
            ]);

        HttpResponseMessage forbiddenRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/properties/{property.Id}/partition",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            operatorReq));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        HttpResponseMessage zeroDuesRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/properties/{property.Id}/partition",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            operatorReq));
        Assert.Equal(HttpStatusCode.Conflict, zeroDuesRes.StatusCode);
        string zeroDuesBody = await zeroDuesRes.Content.ReadAsStringAsync();
        Assert.Contains("ZERO_DUES_GATE_FAILED", zeroDuesBody, StringComparison.Ordinal);

        await SettlePropertyDuesInDbAsync(tenantId, property.Id);

        ExecutePropertyPartitionRequest badAreaReq = new(
            ResolutionNo: "RES-27/2",
            ResolutionDate: new DateOnly(2026, 11, 16),
            ChildPartitions:
            [
                new PartitionChildRequestItem("201/1", [new PropertyOwnerRequestItem(heir1.Id, 100m, true)], 300m, 100m),
                new PartitionChildRequestItem("201/2", [new PropertyOwnerRequestItem(heir2.Id, 100m, true)], 250m, 100m),
            ]);

        HttpResponseMessage areaViolationRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/properties/{property.Id}/partition",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            badAreaReq));
        Assert.Equal(HttpStatusCode.BadRequest, areaViolationRes.StatusCode);
        string badAreaBody = await areaViolationRes.Content.ReadAsStringAsync();
        Assert.Contains("AREA_CONSERVATION_VIOLATED", badAreaBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecutePartition_SucceedsAtomically_ArchivesParent_CreatesChildPropertiesDemandsAndAudit()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedPartitionTenantAsync();
        using HttpClient client = factory.CreateClient();

        CitizenDto parentOwner = await CreateCitizenAsync(
            client, tenantId, gramsevakId, "CIT-P2", "Raghav Patil", "राघव पाटील", "666611112222", "9822334401");
        CitizenDto heir1 = await CreateCitizenAsync(
            client, tenantId, gramsevakId, "CIT-H3", "Nitin Patil", "नितीन पाटील", "666611112223", "9822334402");
        CitizenDto heir2 = await CreateCitizenAsync(
            client, tenantId, gramsevakId, "CIT-H4", "Kiran Patil", "किरण पाटील", "666611112224", "9822334403");

        PropertyDto parentProp = await CreateAssessedPropertyAsync(
            client, tenantId, gramsevakId, parentOwner.Id, "101", builtUpSqFt: 300m, openPlotSqFt: 100m);

        await SettlePropertyDuesInDbAsync(tenantId, parentProp.Id);

        HttpResponseMessage eligRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{parentProp.Id}/partition-eligibility",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, eligRes.StatusCode);
        PartitionEligibilityDto? elig = await eligRes.Content.ReadFromJsonAsync<PartitionEligibilityDto>();
        Assert.NotNull(elig);
        Assert.True(elig.ZeroDuesGatePassed);
        Assert.Equal(0m, elig.PendingDues);
        Assert.Equal(400m, elig.TotalAreaSqFt);

        ExecutePropertyPartitionRequest partitionReq = new(
            ResolutionNo: "ठराव क्र. ९/१",
            ResolutionDate: new DateOnly(2026, 11, 16),
            PartitionReason: "Family Partition Deed",
            DocumentReferenceNo: "DEED-12/2026",
            DocumentDate: new DateOnly(2026, 11, 14),
            Remarks: "Zero dues verified and split among two heirs",
            ChildPartitions:
            [
                new PartitionChildRequestItem(
                    ChildPropertyNo: "101",
                    Owners: [new PropertyOwnerRequestItem(heir1.Id, 100m, true)],
                    BuiltUpAreaSqFt: 200m,
                    OpenPlotAreaSqFt: 50m,
                    BoundaryEast: "Main Road",
                    BoundaryWest: "101/1"),
                new PartitionChildRequestItem(
                    ChildPropertyNo: "101/1",
                    Owners: [new PropertyOwnerRequestItem(heir2.Id, 100m, true)],
                    BuiltUpAreaSqFt: 100m,
                    OpenPlotAreaSqFt: 50m,
                    BoundaryEast: "101",
                    BoundaryWest: "Plot 102"),
            ]);

        HttpResponseMessage partitionRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/properties/{parentProp.Id}/partition",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            partitionReq));

        Assert.Equal(HttpStatusCode.OK, partitionRes.StatusCode);
        ExecutePartitionResultDto? result = await partitionRes.Content.ReadFromJsonAsync<ExecutePartitionResultDto>();
        Assert.NotNull(result);
        Assert.Equal(parentProp.Id, result.ParentPropertyId);
        Assert.Equal("101", result.OriginalParentPropertyNo);
        Assert.Contains("#SPLIT-", result.ArchivedParentPropertyNo, StringComparison.Ordinal);
        Assert.Equal(2, result.ChildProperties.Count);
        Assert.All(result.ChildProperties, c => Assert.True(c.TotalAnnualTax > 0m));

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetTenant(tenantId);

            Property? archivedParent = await db.Properties.FirstOrDefaultAsync(p => p.Id == parentProp.Id);
            Assert.NotNull(archivedParent);
            Assert.Equal("Partitioned", archivedParent.Status);
            Assert.False(archivedParent.IsActive);
            Assert.Equal(result.ArchivedParentPropertyNo, archivedParent.PropertyNo);

            List<Property> children = await db.Properties
                .Where(p => p.ParentPropertyId == parentProp.Id)
                .ToListAsync();
            Assert.Equal(2, children.Count);
            Assert.Contains(children, c => c.PropertyNo == "101" && c.IsActive && c.Status == "Active");
            Assert.Contains(children, c => c.PropertyNo == "101/1" && c.IsActive && c.Status == "Active");

            List<Guid> childIds = [.. children.Select(c => c.Id)];
            Assert.Equal(2, await db.PropertyTaxCalcs.CountAsync(t => childIds.Contains(t.PropertyId)));
            Assert.Equal(2, await db.PropertyDemands.CountAsync(d => childIds.Contains(d.PropertyId)));
            Assert.Equal(2, await db.PropertyConstructions.CountAsync(f => childIds.Contains(f.PropertyId)));
            Assert.Equal(2, await db.PropertyOwners.CountAsync(o => childIds.Contains(o.PropertyId)));

            PropertyMutation? mut = await db.PropertyMutations
                .FirstOrDefaultAsync(m => m.Id == result.PartitionMutationId);
            Assert.NotNull(mut);
            Assert.Equal("Partition", mut.MutationType);
            Assert.Equal("Approved", mut.Status);
            Assert.Equal("ठराव क्र. ९/१", mut.ResolutionNo);

            List<AuditLog> audits = await db.AuditLogs
                .Where(a => a.TenantId == tenantId && a.Action == "PropertyPartitioned")
                .ToListAsync();
            Assert.Single(audits);
        }

        HttpResponseMessage hubRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/properties/hub-summary",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        hubRes.EnsureSuccessStatusCode();
        PropertyHubKpisDto? kpis = await hubRes.Content.ReadFromJsonAsync<PropertyHubKpisDto>();
        Assert.NotNull(kpis);
        Assert.Equal(1, kpis.PartitionedCount);

        HttpResponseMessage repeatRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/properties/{parentProp.Id}/partition",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            partitionReq));
        Assert.Equal(HttpStatusCode.Conflict, repeatRes.StatusCode);
    }
}
