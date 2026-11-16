using System.Net;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Commands;
using GPMH.Application.Features.Tax.Queries;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using GPMH.WebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Formula;

public sealed class FormulaMastersIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static int _lgdSequence = 550000;

    private async Task<(Guid TenantId, Guid GramsevakUserId, Guid OperatorUserId)> SeedTenantWithFormulasAsync()
    {
        Guid tenantId = Guid.NewGuid();
        Guid gramsevakUserId = Guid.NewGuid();
        Guid operatorUserId = Guid.NewGuid();
        string lgdCode = Interlocked.Increment(ref _lgdSequence).ToString();

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        IDefaultFormulaSeeder formulaSeeder = scope.ServiceProvider.GetRequiredService<IDefaultFormulaSeeder>();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantCode = $"gp-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = $"Formula GP {lgdCode}",
            NameMr = $"सूत्र ग्रा.पं. {lgdCode}",
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
                UserId = operatorUserId,
                Role = ApplicationRole.GP_Operator,
                IsDefault = true,
                IsActive = true,
            });

        tenantContext.SetTenant(tenantId);
        await formulaSeeder.SeedDefaultsForTenantAsync(tenantId, CancellationToken.None);
        await db.SaveChangesAsync();

        return (tenantId, gramsevakUserId, operatorUserId);
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
    public async Task SeededTenant_LoadsWorkspaceWith12ConstructionTypes_AndLegacyAliasParity()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedTenantWithFormulasAsync();
        HttpClient client = factory.CreateClient();

        using HttpRequestMessage workspaceReq = CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/formula/workspace",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId);

        HttpResponseMessage workspaceResp = await client.SendAsync(workspaceReq);
        Assert.Equal(HttpStatusCode.OK, workspaceResp.StatusCode);

        FormulaWorkspaceDto? workspace = await workspaceResp.Content.ReadFromJsonAsync<FormulaWorkspaceDto>();
        Assert.NotNull(workspace);
        Assert.NotNull(workspace.ActiveSetting);
        Assert.Equal("2026-27 ते 2029-30", workspace.ActiveSetting.RatePeriodName);
        Assert.Equal(1000m, workspace.ActiveSetting.TaxDivisor);
        Assert.Equal(12, workspace.ConstructionTypes.Count);
        Assert.Equal(9, workspace.Summary.DepreciationSlabsCount);
        Assert.Equal(3, workspace.Summary.UsageWeightagesCount);
        Assert.Equal(3, workspace.Summary.AreaSlabTaxesCount);
        Assert.Equal(3, workspace.Summary.WaterTaxesCount);
        Assert.Contains(workspace.ConstructionTypes, c => c.Code == "HUT_MUD");
        Assert.Contains(workspace.ConstructionTypes, c => c.Code == "OTHER_COMMERCIAL");

        using HttpRequestMessage legacyReq = CreateTenantRequest(
            HttpMethod.Get,
            "/api/Setting/GetAllConstructionTypeAsync",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId);

        HttpResponseMessage legacyResp = await client.SendAsync(legacyReq);
        Assert.Equal(HttpStatusCode.OK, legacyResp.StatusCode);

        List<ConstructionTypeMasterDto>? legacyItems = await legacyResp.Content
            .ReadFromJsonAsync<List<ConstructionTypeMasterDto>>();
        Assert.NotNull(legacyItems);
        Assert.Equal(12, legacyItems.Count);
    }

    [Fact]
    public async Task UpdateConstructionTypeAndSettings_EvictsCache_PersistsAndEmitsAuditLog()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedTenantWithFormulasAsync();
        HttpClient client = factory.CreateClient();

        // Warm workspace cache first.
        using HttpRequestMessage warmReq = CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/formula/workspace",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId);
        FormulaWorkspaceDto? initial = await (await client.SendAsync(warmReq))
            .Content.ReadFromJsonAsync<FormulaWorkspaceDto>();
        Assert.NotNull(initial?.ActiveSetting);
        ConstructionTypeMasterDto rcc = Assert.Single(initial.ConstructionTypes, c => c.Code == "RCC_PUCCA");

        // Update RCC_PUCCA Ready Reckoner rate via PUT.
        SaveConstructionTypeRequest updateRccPayload = new(
            Id: rcc.Id,
            Code: rcc.Code,
            TypeName: rcc.TypeName,
            Category: rcc.Category,
            LandAnnualValueRate: 810m,
            ConstructionReadyReckonerRate: 19500m,
            UsageWeightageFactor: rcc.UsageWeightageFactor,
            TaxRate: 1.75m,
            IsActive: true,
            ResolutionNo: "GP-RES/2026/08",
            ResolutionDate: new DateOnly(2026, 11, 10),
            ChangeReason: "Annual Ready Reckoner revision");

        using HttpRequestMessage putReq = CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/formula/construction-types/{rcc.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            updateRccPayload);

        HttpResponseMessage putResp = await client.SendAsync(putReq);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // Save custom construction type via legacy compatibility route (upsert/create).
        SaveConstructionTypeRequest customPayload = new(
            Id: null,
            Code: "SOLAR_SHED",
            TypeName: "सौर ऊर्जा शेड",
            Category: "SPECIAL",
            LandAnnualValueRate: 760m,
            ConstructionReadyReckonerRate: 6200m,
            UsageWeightageFactor: 1.0m,
            TaxRate: 1.25m,
            IsActive: true,
            ResolutionNo: "GP-RES/2026/09",
            ResolutionDate: new DateOnly(2026, 11, 11),
            ChangeReason: "Added Solar Shed category");

        using HttpRequestMessage legacySaveReq = CreateTenantRequest(
            HttpMethod.Post,
            "/api/Setting/SaveConstructionTypeAsync",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            customPayload);
        HttpResponseMessage legacySaveResp = await client.SendAsync(legacySaveReq);
        Assert.Equal(HttpStatusCode.OK, legacySaveResp.StatusCode);

        // Update FormulaSetting with mandatory GP Resolution metadata.
        SaveFormulaSettingRequest updateSettingsPayload = new(
            Id: initial.ActiveSetting.Id,
            RatePeriodName: "2026-27 ते 2029-30",
            RateStartYear: 2026,
            RateEndYear: 2029,
            TaxDivisor: 1000m,
            NoticeGraceDays: 21,
            EarlyRebatePct: 12.5m,
            LatePenaltyPct: 10.0m,
            ApplyLightingHealthOnOpenPlot: true,
            RoundArea: true,
            IsActive: true,
            ResolutionNo: "GP-RES/2026/10",
            ResolutionDate: new DateOnly(2026, 11, 12),
            ResolutionReason: "Extended notice window to 21 days by Gram Sabha resolution");

        using HttpRequestMessage settingsPutReq = CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/formula/settings",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            updateSettingsPayload);
        HttpResponseMessage settingsPutResp = await client.SendAsync(settingsPutReq);
        Assert.Equal(HttpStatusCode.OK, settingsPutResp.StatusCode);

        // Verify workspace cache evicted cleanly and reflects all updates.
        using HttpRequestMessage verifyReq = CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/formula/workspace",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId);
        FormulaWorkspaceDto? updated = await (await client.SendAsync(verifyReq))
            .Content.ReadFromJsonAsync<FormulaWorkspaceDto>();
        Assert.NotNull(updated?.ActiveSetting);
        Assert.Equal(13, updated.ConstructionTypes.Count);
        Assert.Equal(21, updated.ActiveSetting.NoticeGraceDays);
        Assert.Equal(12.5m, updated.ActiveSetting.EarlyRebatePct);
        Assert.Equal("GP-RES/2026/10", updated.ActiveSetting.LastResolutionNo);

        ConstructionTypeMasterDto updatedRcc = Assert.Single(updated.ConstructionTypes, c => c.Code == "RCC_PUCCA");
        Assert.Equal(19500m, updatedRcc.ConstructionReadyReckonerRate);
        Assert.Equal(810m, updatedRcc.LandAnnualValueRate);
        Assert.Equal("GP-RES/2026/08", updatedRcc.LastResolutionNo);

        // Verify immutable AuditLog entries recorded both updates.
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<AuditLog> formulaAuditLogs = await db.AuditLogs
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId &&
                        l.Action == "Modified" &&
                        (l.EntityName == nameof(Domain.Entities.Taxation.ConstructionTypeMaster) ||
                         l.EntityName == nameof(Domain.Entities.Taxation.FormulaSetting)))
            .ToListAsync();

        Assert.Contains(formulaAuditLogs, l => l.EntityId == rcc.Id.ToString() &&
                                               l.NewValuesJson != null &&
                                               l.NewValuesJson.Contains("GP-RES/2026/08", StringComparison.Ordinal));
        Assert.Contains(formulaAuditLogs, l => l.EntityId == initial.ActiveSetting.Id.ToString() &&
                                               l.NewValuesJson != null &&
                                               l.NewValuesJson.Contains("GP-RES/2026/10", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CrossTenantAndRbacIsolation_BlocksUnauthorizedReadsAndWrites()
    {
        (Guid tenantA, Guid gramsevakA, Guid operatorA) = await SeedTenantWithFormulasAsync();
        (Guid tenantB, Guid gramsevakB, _) = await SeedTenantWithFormulasAsync();
        HttpClient client = factory.CreateClient();

        using HttpRequestMessage listAReq = CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/formula/construction-types",
            operatorA,
            ApplicationRole.GP_Operator,
            tenantA);
        HttpResponseMessage listAResp = await client.SendAsync(listAReq);
        Assert.Equal(HttpStatusCode.OK, listAResp.StatusCode);

        List<ConstructionTypeMasterDto>? listA = await listAResp.Content
            .ReadFromJsonAsync<List<ConstructionTypeMasterDto>>();
        Assert.NotNull(listA);
        ConstructionTypeMasterDto targetInA = Assert.Single(listA, c => c.Code == "HUT_MUD");

        // GP_Operator has read permission only -> PUT must be 403 Forbidden.
        SaveConstructionTypeRequest opMutate = new(
            Id: targetInA.Id,
            Code: targetInA.Code,
            TypeName: targetInA.TypeName,
            Category: targetInA.Category,
            LandAnnualValueRate: 900m,
            ConstructionReadyReckonerRate: 6000m,
            UsageWeightageFactor: 1m,
            TaxRate: 0.5m);
        using HttpRequestMessage opReq = CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/formula/construction-types/{targetInA.Id}",
            operatorA,
            ApplicationRole.GP_Operator,
            tenantA,
            opMutate);
        HttpResponseMessage opResp = await client.SendAsync(opReq);
        Assert.Equal(HttpStatusCode.Forbidden, opResp.StatusCode);

        // Gramsevak B using own tenant X-Tenant-ID trying to edit Tenant A row ID -> 404 NotFound.
        using HttpRequestMessage crossIdReq = CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/formula/construction-types/{targetInA.Id}",
            gramsevakB,
            ApplicationRole.Gramsevak,
            tenantB,
            opMutate);
        HttpResponseMessage crossIdResp = await client.SendAsync(crossIdReq);
        Assert.Equal(HttpStatusCode.NotFound, crossIdResp.StatusCode);

        // Gramsevak B forging X-Tenant-ID = Tenant A -> 403 Forbidden via TenantResolutionMiddleware.
        using HttpRequestMessage crossHeaderReq = CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/formula/construction-types/{targetInA.Id}",
            gramsevakB,
            ApplicationRole.Gramsevak,
            tenantA,
            opMutate);
        HttpResponseMessage crossHeaderResp = await client.SendAsync(crossHeaderReq);
        Assert.Equal(HttpStatusCode.Forbidden, crossHeaderResp.StatusCode);
    }

    [Fact]
    public async Task InvalidRatesAndMissingResolution_ReturnsRfc9457ValidationProblem()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedTenantWithFormulasAsync();
        HttpClient client = factory.CreateClient();

        SaveConstructionTypeRequest badConstruction = new(
            Id: null,
            Code: "BAD_NEG",
            TypeName: "Negative Rate",
            Category: "INVALID_CAT",
            LandAnnualValueRate: -50m,
            ConstructionReadyReckonerRate: -10m,
            UsageWeightageFactor: 1m,
            TaxRate: 0.5m);

        using HttpRequestMessage badConstReq = CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/formula/construction-types",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            badConstruction);
        HttpResponseMessage badConstResp = await client.SendAsync(badConstReq);
        Assert.Equal(HttpStatusCode.BadRequest, badConstResp.StatusCode);

        ValidationProblemDetails? problem1 = await badConstResp.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem1);
        Assert.NotEmpty(problem1.Errors);

        SaveFormulaSettingRequest missingResSettings = new(
            Id: null,
            RatePeriodName: "2026-27 ते 2029-30",
            RateStartYear: 2026,
            RateEndYear: 2029,
            TaxDivisor: 1000m,
            NoticeGraceDays: 15,
            EarlyRebatePct: 10m,
            LatePenaltyPct: 10m,
            ApplyLightingHealthOnOpenPlot: true,
            RoundArea: true,
            IsActive: true,
            ResolutionNo: "",
            ResolutionDate: null,
            ResolutionReason: "");

        using HttpRequestMessage badSettingsReq = CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/formula/settings",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            missingResSettings);
        HttpResponseMessage badSettingsResp = await client.SendAsync(badSettingsReq);
        Assert.Equal(HttpStatusCode.BadRequest, badSettingsResp.StatusCode);

        ValidationProblemDetails? problem2 = await badSettingsResp.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem2);
        Assert.True(problem2.Errors.ContainsKey("ResolutionNo"));
        Assert.True(problem2.Errors.ContainsKey("ResolutionReason"));
    }

    [Fact]
    public async Task ResetDefaults_RestoresCanonicalAdr016RatesAndEvictsCache()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedTenantWithFormulasAsync();
        HttpClient client = factory.CreateClient();

        using HttpRequestMessage mutateReq = CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/formula/construction-types",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new SaveConstructionTypeRequest(
                Id: null,
                Code: "HUT_MUD",
                TypeName: "Custom Temporary Name",
                Category: "HUT_MUD",
                LandAnnualValueRate: 999m,
                ConstructionReadyReckonerRate: 9999m,
                UsageWeightageFactor: 1.5m,
                TaxRate: 2.0m));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(mutateReq)).StatusCode);

        using HttpRequestMessage resetReq = CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/formula/reset-defaults",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new ResetFormulaDefaultsRequest("GP-RESET/2026", new DateOnly(2026, 11, 16), "Reset to canonical seeds"));
        HttpResponseMessage resetResp = await client.SendAsync(resetReq);
        Assert.Equal(HttpStatusCode.OK, resetResp.StatusCode);

        FormulaWorkspaceDto? restored = await resetResp.Content.ReadFromJsonAsync<FormulaWorkspaceDto>();
        Assert.NotNull(restored?.ActiveSetting);
        Assert.Equal("GP-RESET/2026", restored.ActiveSetting.LastResolutionNo);
        Assert.Equal(1000m, restored.ActiveSetting.TaxDivisor);

        ConstructionTypeMasterDto hut = Assert.Single(restored.ConstructionTypes, c => c.Code == "HUT_MUD");
        Assert.Equal("झोपडी किंवा मातीची इमारत", hut.TypeName);
        Assert.Equal(760m, hut.LandAnnualValueRate);
        Assert.Equal(5852m, hut.ConstructionReadyReckonerRate);
        Assert.Equal(0.5m, hut.TaxRate);
    }

    [Fact]
    public async Task SecondaryMasters_RejectsGapsOverlapsAndIncompleteRanges_WithRfc9457()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedTenantWithFormulasAsync();
        HttpClient client = factory.CreateClient();

        // Gap between AgeTo = 2 and AgeFrom = 3 -> 400 Bad Request
        SaveDepreciationSlabsBatchRequest gappedDep = new(
            Slabs:
            [
                new DepreciationSlabBatchItem(0, 2, 100m, 100m),
                new DepreciationSlabBatchItem(3, 100, 50m, 60m),
            ],
            ResolutionNo: "GP-RES/2026/21",
            ResolutionDate: new DateOnly(2026, 11, 15),
            ChangeReason: "Invalid gapped slabs");

        using HttpRequestMessage gappedDepReq = CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/formula/depreciation-slabs",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            gappedDep);
        HttpResponseMessage gappedDepResp = await client.SendAsync(gappedDepReq);
        Assert.Equal(HttpStatusCode.BadRequest, gappedDepResp.StatusCode);
        ValidationProblemDetails? depProblem = await gappedDepResp.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(depProblem);
        Assert.True(depProblem.Errors.ContainsKey("Slabs"));

        // Area slabs missing continuity (1..300 then 305..999999) -> 400 Bad Request
        SaveAreaSlabTaxesBatchRequest gappedArea = new(
            Slabs:
            [
                new AreaSlabTaxBatchItem(1, 300, 40m, 40m, 100m, 0m),
                new AreaSlabTaxBatchItem(305, 999999, 100m, 100m, 100m, 0m),
            ],
            ResolutionNo: "GP-RES/2026/22",
            ResolutionDate: new DateOnly(2026, 11, 15),
            ChangeReason: "Invalid gapped area slabs");

        using HttpRequestMessage gappedAreaReq = CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/formula/area-slabs",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            gappedArea);
        HttpResponseMessage gappedAreaResp = await client.SendAsync(gappedAreaReq);
        Assert.Equal(HttpStatusCode.BadRequest, gappedAreaResp.StatusCode);
        ValidationProblemDetails? areaProblem = await gappedAreaResp.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(areaProblem);
        Assert.True(areaProblem.Errors.ContainsKey("Slabs"));
    }

    [Fact]
    public async Task SecondaryMasters_CrudUpdates_EvictCache_EmitAuditLogs_AndEnforceIsolation()
    {
        (Guid tenantA, Guid gramsevakA, Guid operatorA) = await SeedTenantWithFormulasAsync();
        (Guid tenantB, Guid gramsevakB, _) = await SeedTenantWithFormulasAsync();
        HttpClient client = factory.CreateClient();

        // Warm workspace cache on Tenant A
        FormulaWorkspaceDto? wsA = await (await client.SendAsync(
            CreateTenantRequest(HttpMethod.Get, "/api/v1/formula/workspace", gramsevakA, ApplicationRole.Gramsevak, tenantA)))
            .Content.ReadFromJsonAsync<FormulaWorkspaceDto>();
        Assert.NotNull(wsA?.DepreciationSlabs);
        Assert.Equal(9, wsA.DepreciationSlabs.Count);

        // Update Depreciation Slabs (keeping 9 contiguous slabs with updated 2..5 HutMud 92%)
        List<DepreciationSlabBatchItem> modifiedDep =
        [
            .. wsA.DepreciationSlabs.Select(s =>
                s.AgeFrom == 2 && s.AgeTo == 5
                    ? new DepreciationSlabBatchItem(2, 5, 92m, 95m)
                    : new DepreciationSlabBatchItem(s.AgeFrom, s.AgeTo, s.HutMudPercent, s.StoneBrickRccPercent)),
        ];
        HttpResponseMessage depSaveResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/formula/depreciation-slabs",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new SaveDepreciationSlabsBatchRequest(modifiedDep, "GP-RES/2026/31", new DateOnly(2026, 11, 16), "Updated 2-5 slab")));
        Assert.Equal(HttpStatusCode.OK, depSaveResp.StatusCode);

        // Update Usage Weightage (COMMERCIAL 1.25 -> 1.50) + Add custom usage
        UsageWeightageDto commercial = Assert.Single(wsA.UsageWeightages!, u => u.Code == "COMMERCIAL");
        HttpResponseMessage usagePutResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/formula/usage-weightages/{commercial.Id}",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new SaveUsageWeightageRequest(
                commercial.Id,
                "COMMERCIAL",
                "वाणिज्य (सुधारित)",
                1.50m,
                true,
                "GP-RES/2026/32",
                new DateOnly(2026, 11, 16),
                "Raised commercial factor")));
        Assert.Equal(HttpStatusCode.OK, usagePutResp.StatusCode);

        // Update Water Tax (SPECIAL 720 -> 960)
        WaterTaxMasterDto specialWater = Assert.Single(wsA.WaterTaxes!, w => w.Code == "SPECIAL");
        HttpResponseMessage waterPutResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/formula/water-taxes/{specialWater.Id}",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new SaveWaterTaxMasterRequest(
                specialWater.Id,
                "SPECIAL",
                "विशेष पाणी कर",
                960m,
                true,
                "GP-RES/2026/33",
                new DateOnly(2026, 11, 16),
                "Revised special tap fee")));
        Assert.Equal(HttpStatusCode.OK, waterPutResp.StatusCode);

        // Update Area Slab Taxes (custom boundary shift to prove index-safe batch replace)
        HttpResponseMessage areaPutResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/formula/area-slabs",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new SaveAreaSlabTaxesBatchRequest(
                [
                    new AreaSlabTaxBatchItem(1, 350, 50m, 50m, 100m, 0m),
                    new AreaSlabTaxBatchItem(351, 999999, 120m, 120m, 100m, 30m),
                ],
                "GP-RES/2026/34",
                new DateOnly(2026, 11, 16),
                "Consolidated to 2 area brackets")));
        Assert.Equal(HttpStatusCode.OK, areaPutResp.StatusCode);

        // Verify Tenant A cache evicted and Tenant B masters untouched
        FormulaWorkspaceDto? updatedWsA = await (await client.SendAsync(
            CreateTenantRequest(HttpMethod.Get, "/api/v1/formula/workspace", gramsevakA, ApplicationRole.Gramsevak, tenantA)))
            .Content.ReadFromJsonAsync<FormulaWorkspaceDto>();
        Assert.Equal(92m, Assert.Single(updatedWsA!.DepreciationSlabs!, s => s.AgeFrom == 2).HutMudPercent);
        Assert.Equal(1.50m, Assert.Single(updatedWsA.UsageWeightages!, u => u.Code == "COMMERCIAL").Weightage);
        Assert.Equal(960m, Assert.Single(updatedWsA.WaterTaxes!, w => w.Code == "SPECIAL").Amount);
        Assert.Equal(2, updatedWsA.AreaSlabTaxes!.Count);

        FormulaWorkspaceDto? wsB = await (await client.SendAsync(
            CreateTenantRequest(HttpMethod.Get, "/api/v1/formula/workspace", gramsevakB, ApplicationRole.Gramsevak, tenantB)))
            .Content.ReadFromJsonAsync<FormulaWorkspaceDto>();
        Assert.Equal(95m, Assert.Single(wsB!.DepreciationSlabs!, s => s.AgeFrom == 2).HutMudPercent);
        Assert.Equal(1.25m, Assert.Single(wsB.UsageWeightages!, u => u.Code == "COMMERCIAL").Weightage);

        // GP_Operator write blocked with 403; cross-tenant UsageWeightage ID blocked with 404
        HttpResponseMessage opForbidden = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/formula/water-taxes/{specialWater.Id}",
            operatorA,
            ApplicationRole.GP_Operator,
            tenantA,
            new SaveWaterTaxMasterRequest(specialWater.Id, "SPECIAL", "X", 100m, true, "R1", null, "Blocked")));
        Assert.Equal(HttpStatusCode.Forbidden, opForbidden.StatusCode);

        HttpResponseMessage crossTenantNotFound = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/formula/usage-weightages/{commercial.Id}",
            gramsevakB,
            ApplicationRole.Gramsevak,
            tenantB,
            new SaveUsageWeightageRequest(commercial.Id, "COMMERCIAL", "X", 2.0m, true, "R2", null, "Cross tenant")));
        Assert.Equal(HttpStatusCode.NotFound, crossTenantNotFound.StatusCode);

        // AuditLogs recorded for secondary entities
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<AuditLog> secLogs = await db.AuditLogs
            .AsNoTracking()
            .Where(l => l.TenantId == tenantA)
            .ToListAsync();
        Assert.Contains(secLogs, l => l.EntityName == nameof(Domain.Entities.Taxation.UsageWeightage) &&
                                      l.NewValuesJson != null &&
                                      l.NewValuesJson.Contains("GP-RES/2026/32", StringComparison.Ordinal));
        Assert.Contains(secLogs, l => l.EntityName == nameof(Domain.Entities.Taxation.WaterTaxMaster) &&
                                      l.NewValuesJson != null &&
                                      l.NewValuesJson.Contains("GP-RES/2026/33", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CalculateFromMastersEndpoint_ResolvesLiveDbRates_MatchesGoldenVectorAndOpenPlotPolicy()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedTenantWithFormulasAsync();
        HttpClient client = factory.CreateClient();

        // 1) Chincholi Khurd Golden Vector through live DB masters -> 66 Building + 180 Slab = 246 Total
        CalculatePropertyTaxFromMastersQuery goldenQuery = new(
            AssessmentStartYear: 2026,
            Floors:
            [
                new MasterFloorTaxRequest(
                    ConstructionTypeCode: "HUT_MUD",
                    UsageCode: "RESIDENTIAL",
                    ConstructionStartYear: 2021,
                    LengthFt: 15m,
                    WidthFt: 15m),
            ],
            WaterConnectionCode: "NONE");

        HttpResponseMessage goldenResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/tax/calculate",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            goldenQuery));
        Assert.Equal(HttpStatusCode.OK, goldenResp.StatusCode);

        CalculatedPropertyTaxDto? goldenResult = await goldenResp.Content.ReadFromJsonAsync<CalculatedPropertyTaxDto>();
        Assert.NotNull(goldenResult);
        Assert.Equal(225m, goldenResult.TotalBuiltUpSqFt);
        Assert.Equal(20.90m, goldenResult.TotalBuiltUpSqM);
        Assert.Equal("1-300", goldenResult.AppliedAreaSlabRange);
        Assert.Equal(66m, goldenResult.Calculation.BuildingTax);
        Assert.Equal(180m, goldenResult.Calculation.LightingTax + goldenResult.Calculation.HealthTax + goldenResult.Calculation.SanitationTax + goldenResult.Calculation.ToiletTax);
        Assert.Equal(246m, goldenResult.Calculation.TotalTax);

        // 2) Update SPECIAL water rate in DB (720 -> 960) and confirm POST /api/v1/tax/calculate picks up new live rate
        FormulaWorkspaceDto? ws = await (await client.SendAsync(
            CreateTenantRequest(HttpMethod.Get, "/api/v1/formula/workspace", gramsevakId, ApplicationRole.Gramsevak, tenantId)))
            .Content.ReadFromJsonAsync<FormulaWorkspaceDto>();
        WaterTaxMasterDto special = Assert.Single(ws!.WaterTaxes!, w => w.Code == "SPECIAL");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/formula/water-taxes/{special.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new SaveWaterTaxMasterRequest(special.Id, "SPECIAL", "विशेष पाणी कर", 960m, true, "GP-RES/2026/40", null, "Live test")))).StatusCode);

        CalculatePropertyTaxFromMastersQuery updatedWaterQuery = goldenQuery with { WaterConnectionCode = "SPECIAL" };
        CalculatedPropertyTaxDto? afterWaterEdit = await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/tax/calculate",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            updatedWaterQuery))).Content.ReadFromJsonAsync<CalculatedPropertyTaxDto>();
        Assert.Equal(960m, afterWaterEdit!.Calculation.WaterTax);
        Assert.Equal(1206m, afterWaterEdit.Calculation.TotalTax);

        // 3) Open-plot-only property respects ApplyLightingHealthOnOpenPlot = true (Lighting 40 + Health 40, Sanitation 0)
        CalculatePropertyTaxFromMastersQuery openPlotQuery = new(
            AssessmentStartYear: 2026,
            Floors: [],
            OpenAreaSqFt: 250m,
            OpenPlotConstructionCode: "OPEN_PLOT",
            WaterConnectionCode: "NONE");
        CalculatedPropertyTaxDto? openPlotResult = await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/tax/calculate",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            openPlotQuery))).Content.ReadFromJsonAsync<CalculatedPropertyTaxDto>();
        Assert.Equal(40m, openPlotResult!.Calculation.LightingTax);
        Assert.Equal(40m, openPlotResult.Calculation.HealthTax);
        Assert.Equal(0m, openPlotResult.Calculation.SanitationTax);
    }
}

