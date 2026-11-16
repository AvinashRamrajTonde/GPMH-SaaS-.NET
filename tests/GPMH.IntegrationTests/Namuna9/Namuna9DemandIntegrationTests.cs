using System.Net;
using System.Net.Http.Json;
using System.Text;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Citizens.Commands;
using GPMH.Application.Features.Partitions.Commands;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Domain.ValueObjects;
using GPMH.Infrastructure.Persistence;
using GPMH.WebAPI.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Namuna9;

public sealed class Namuna9DemandIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static int _lgdSequence = 800000;

    private async Task<(Guid TenantId, Guid YearId, Guid GramsevakId, Guid OperatorId)> SeedNamuna9TenantAsync()
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
            NameEn = $"Namuna9 GP {lgdCode}",
            NameMr = $"नमुना ९ ग्रा.पं. {lgdCode}",
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
            TehsilName = "अंजनगाव सुर्जी",
            PanchayatNameEn = $"Chincholi Khurd {lgdCode}",
            PanchayatNameMr = $"चिंचोली खुर्द {lgdCode}",
            GramsevakName = "श्री. जी. एस. मोरे",
            SarpanchName = "सौ. सविता देशमुख",
            UpiId = $"gp{lgdCode}@sbi",
        });

        AssessmentYear activeYear = new()
        {
            TenantId = tenantId,
            YearLabel = "2026-27",
            StartYear = 2026,
            EndYear = 2027,
            IsActive = true,
            IsLocked = false,
        };
        db.AssessmentYears.Add(activeYear);

        await seeder.SeedDefaultsForTenantAsync(tenantId, CancellationToken.None);
        await db.SaveChangesAsync();

        return (tenantId, activeYear.Id, gramsevakId, operatorId);
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
    public async Task Namuna9Lifecycle_Publish_FeeAdjustments_Rebate_Rollover_Dashboard_And_Security()
    {
        (Guid tenantId, Guid year2026Id, Guid gramsevakId, Guid operatorId) = await SeedNamuna9TenantAsync();
        (Guid otherTenantId, _, Guid otherUserId, _) = await SeedNamuna9TenantAsync();
        using HttpClient client = factory.CreateClient();

        CitizenDto owner = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand("CIT-N9-1", "1", "ऋषिकेश सावरकर", "Rishikesh Savarkar", "Male", "9822000101", "222233334444", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        CitizenDto heirA = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand("CIT-N9-2", "1", "अजय सावरकर", "Ajay Savarkar", "Male", "9822000102", "222233334445", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        // 15x15 = 225 sq.ft RCC built in 2024 -> Chincholi Khurd Golden Vector: 66 + 40 + 40 + 100 = 246 INR
        HttpResponseMessage propRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: "1",
                PropertyNo: "1",
                BuiltUpAreaSqFt: 225m,
                OpenPlotAreaSqFt: 0m,
                BoundaryEast: "Road",
                BoundaryWest: "Plot 2",
                BoundaryNorth: "Lane",
                BoundarySouth: "Plot 3",
                WaterConnectionCode: "NONE",
                IsTaxExempt: false,
                Owners: [new PropertyOwnerRequestItem(owner.Id, 100m, true)],
                Floors: [new PropertyFloorRequestItem(0, "तळमजला", "HUT_MUD", "RESIDENTIAL", 15m, 15m, null, 2021)],
                CalculateTax: true)));
        propRes.EnsureSuccessStatusCode();
        PropertyDto prop = (await propRes.Content.ReadFromJsonAsync<PropertyDto>())!;
        Assert.Equal(246m, prop.TotalAnnualTax);

        // Verify PropertyAssessmentHelper populated CurrentByHeadJson on DraftAssessment
        HttpResponseMessage regDraftRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/namuna-9/register",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, regDraftRes.StatusCode);
        Namuna9WorkspaceDto? regDraft = await regDraftRes.Content.ReadFromJsonAsync<Namuna9WorkspaceDto>();
        Assert.NotNull(regDraft);
        Namuna9DemandRowDto row = Assert.Single(regDraft.Items);
        Assert.Equal(PropertyDemand.StatusDraft, row.Status);
        Assert.Equal(66m, row.HouseTax.Current);
        Assert.Equal(40m, row.LightingTax.Current);
        Assert.Equal(40m, row.HealthTax.Current);
        Assert.Equal(100m, row.SanitationTax.Current);
        Assert.Equal(246m, row.TotalCurrent);

        // GP_Operator cannot publish, adjust, apply rebate, or execute rollover -> 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9/publish",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new PublishDraftDemandsRequest(year2026Id)))).StatusCode);

        // Gramsevak publishes draft demands -> DemandPublished
        HttpResponseMessage pubRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9/publish",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new PublishDraftDemandsRequest(year2026Id)));
        pubRes.EnsureSuccessStatusCode();
        Namuna9PublishResultDto? pubDto = await pubRes.Content.ReadFromJsonAsync<Namuna9PublishResultDto>();
        Assert.NotNull(pubDto);
        Assert.Equal(1, pubDto.PublishedCount);

        // Apply 10% early rebate AFTER cutoff (Aug 5) -> 0 applied
        HttpResponseMessage lateRebateRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9/apply-rebate",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new ApplyEarlyRebateBatchRequest(year2026Id, new DateOnly(2026, 8, 5))));
        lateRebateRes.EnsureSuccessStatusCode();
        Namuna9RebateResultDto? lateRebate = await lateRebateRes.Content.ReadFromJsonAsync<Namuna9RebateResultDto>();
        Assert.NotNull(lateRebate);
        Assert.Equal(0, lateRebate.AppliedCount);

        // Apply 10% early rebate ON cutoff (July 31) -> 24.60 rebate, balance 221.40
        HttpResponseMessage onTimeRebateRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9/apply-rebate",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new ApplyEarlyRebateBatchRequest(year2026Id, new DateOnly(2026, 7, 31))));
        onTimeRebateRes.EnsureSuccessStatusCode();
        Namuna9RebateResultDto? onTimeRebate = await onTimeRebateRes.Content.ReadFromJsonAsync<Namuna9RebateResultDto>();
        Assert.NotNull(onTimeRebate);
        Assert.Equal(1, onTimeRebate.AppliedCount);
        Assert.Equal(24.60m, onTimeRebate.TotalRebateGranted);

        // Invalid Rebate > CurrentDemand returns 400 Validation Failed
        HttpResponseMessage badRebateRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/namuna-9/adjustments",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new BatchAdjustNamuna9FeesRequest(
                year2026Id,
                [new Namuna9FeeAdjustmentItem(prop.Id, 0m, 0m, 0m, RebateAmount: 500m)])));
        Assert.Equal(HttpStatusCode.BadRequest, badRebateRes.StatusCode);

        // Valid fee adjustments: Notice=10, Warrant=25, Penalty=20, Rebate=0 -> PendingBalance = 301
        HttpResponseMessage adjRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/namuna-9/adjustments",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new BatchAdjustNamuna9FeesRequest(
                year2026Id,
                [new Namuna9FeeAdjustmentItem(prop.Id, NoticeFee: 10m, WarrantFee: 25m, PenaltyAmount: 20m, RebateAmount: 0m, Remarks: "15-day notice + warrant")])));
        adjRes.EnsureSuccessStatusCode();
        Namuna9AdjustResultDto? adjDto = await adjRes.Content.ReadFromJsonAsync<Namuna9AdjustResultDto>();
        Assert.NotNull(adjDto);
        Assert.Equal(301m, Assert.Single(adjDto.Items).PendingBalance);

        // Regression check for Sprint 3 #27 Zero-Dues Gate with unpaid fees -> 409 ZERO_DUES_GATE_FAILED
        HttpResponseMessage partGateRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/properties/{prop.Id}/partition",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new ExecutePropertyPartitionRequest(
                ResolutionNo: "RES-GATE",
                ResolutionDate: new DateOnly(2026, 11, 16),
                ChildPartitions:
                [
                    new PartitionChildRequestItem("1/1", [new PropertyOwnerRequestItem(owner.Id, 100m, true)], 112.5m, 0m),
                    new PartitionChildRequestItem("1/2", [new PropertyOwnerRequestItem(heirA.Id, 100m, true)], 112.5m, 0m),
                ])));
        Assert.Equal(HttpStatusCode.Conflict, partGateRes.StatusCode);

        // Cross-tenant isolation: Tenant B cannot adjust or view Tenant A's assessment year -> 404
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9/register?yearId={year2026Id}",
            otherUserId,
            ApplicationRole.Gramsevak,
            otherTenantId))).StatusCode);

        // Execute FY Rollover: 2026-27 -> 2027-28
        HttpResponseMessage rolloverRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9/rollover",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new ExecuteFyRolloverRequest(year2026Id, "2027-28", CarryForwardCurrentTaxCalc: true)));
        rolloverRes.EnsureSuccessStatusCode();
        Namuna9RolloverResultDto? rolloverDto = await rolloverRes.Content.ReadFromJsonAsync<Namuna9RolloverResultDto>();
        Assert.NotNull(rolloverDto);
        Assert.Equal("2027-28", rolloverDto.TargetYearLabel);
        Assert.Equal(1, rolloverDto.PropertiesRolledOver);
        Assert.Equal(301m, rolloverDto.TotalCarriedArrears);
        Assert.Equal(246m, rolloverDto.TotalCurrentDemand);

        // Verify 2026-27 is locked (409 on edit/publish & 409 on double rollover)
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/namuna-9/adjustments",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new BatchAdjustNamuna9FeesRequest(
                year2026Id,
                [new Namuna9FeeAdjustmentItem(prop.Id, 0m, 0m, 0m, 0m)])))).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9/rollover",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new ExecuteFyRolloverRequest(year2026Id, "2027-28")))).StatusCode);

        // Verify 2027-28 register carries exact head arrears (301) + new current (246) = 547 total balance
        Namuna9WorkspaceDto? reg2027 = await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/namuna-9/register",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<Namuna9WorkspaceDto>();
        Assert.NotNull(reg2027);
        Assert.Equal("2027-28", reg2027.SelectedYear.YearLabel);
        Namuna9DemandRowDto row2027 = Assert.Single(reg2027.Items);
        Assert.Equal(301m, row2027.TotalArrears);
        Assert.Equal(66m, row2027.HouseTax.Arrears);
        Assert.Equal(40m, row2027.LightingTax.Arrears);
        Assert.Equal(40m, row2027.HealthTax.Arrears);
        Assert.Equal(100m, row2027.SanitationTax.Arrears);
        Assert.Equal(55m, row2027.CarriedFeesAndPenalty);
        Assert.Equal(246m, row2027.TotalCurrent);
        Assert.Equal(547m, row2027.PendingBalance);
        Assert.Equal(547m, reg2027.Kpis.TotalBalanceDues);

        // Dashboard reflects live OutstandingArrears = 301 and CurrentFyDemand = 246
        DashboardSummaryDto? dash = await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/dashboard/summary",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<DashboardSummaryDto>();
        Assert.NotNull(dash);
        Assert.Equal("2027-28", dash.ActiveAssessmentYear);
        Assert.Equal(301m, dash.OutstandingArrears);
        Assert.Equal(246m, dash.CurrentFyDemand);

        // Verify AuditLog trail captured fee adjustments and rollover
        using IServiceScope verifyScope = factory.Services.CreateScope();
        AppDbContext verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext verifyCtx = verifyScope.ServiceProvider.GetRequiredService<ITenantContext>();
        verifyCtx.SetTenant(tenantId);

        List<string> auditActions = await verifyDb.AuditLogs
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Action)
            .ToListAsync();
        Assert.Contains("Namuna9DemandsPublished", auditActions);
        Assert.Contains("Namuna9EarlyRebateApplied", auditActions);
        Assert.Contains("Namuna9FeesAdjusted", auditActions);
        Assert.Contains("FinancialYearRollover", auditActions);
    }

    [Fact]
    public async Task Namuna9PrintAndCsv_Supports_Formats_ABC_SpreadModes_Bw_LeftMargin_And_AuditLog()
    {
        (Guid tenantId, Guid yearId, Guid gramsevakId, Guid operatorId) = await SeedNamuna9TenantAsync();
        using HttpClient client = factory.CreateClient();

        CitizenDto owner = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand("CIT-N9-P1", "1", "बलवान पेंदिनाथ दिनकर", "Balwan Pendinath Dinkar", "Male", "9822000201", "222233334499", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        HttpResponseMessage propRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: "1",
                PropertyNo: "2",
                BuiltUpAreaSqFt: 225m,
                OpenPlotAreaSqFt: 0m,
                BoundaryEast: "Road",
                BoundaryWest: "Lane",
                BoundaryNorth: "Plot 1",
                BoundarySouth: "Plot 3",
                WaterConnectionCode: "GENERAL",
                IsTaxExempt: false,
                Owners: [new PropertyOwnerRequestItem(owner.Id, 100m, true)],
                Floors: [new PropertyFloorRequestItem(0, "तळमजला", "HUT_MUD", "RESIDENTIAL", 15m, 15m, null, 2021)],
                CalculateTax: true)));
        propRes.EnsureSuccessStatusCode();

        // Format A, B, C with Both spread & left margin
        foreach (string format in new[] { "A", "B", "C" })
        {
            HttpResponseMessage pdfRes = await client.SendAsync(CreateTenantRequest(
                HttpMethod.Get,
                $"/api/v1/namuna-9/register.pdf?yearId={yearId}&format={format}&part=Both&style=color&leftMarginMm=20",
                operatorId,
                ApplicationRole.GP_Operator,
                tenantId));

            Assert.Equal(HttpStatusCode.OK, pdfRes.StatusCode);
            Assert.Equal("application/pdf", pdfRes.Content.Headers.ContentType?.MediaType);

            byte[] pdfBytes = await pdfRes.Content.ReadAsByteArrayAsync();
            Assert.True(pdfBytes.Length > 10_000, $"Expected > 10 KB PDF for Format {format}, got {pdfBytes.Length}");
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdfBytes, 0, 5));
        }

        // Single part Demand + B&W style + 0 margin
        HttpResponseMessage bwDemandRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9/register.pdf?yearId={yearId}&format=C&part=Demand&style=bw&leftMarginMm=0",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, bwDemandRes.StatusCode);
        byte[] bwBytes = await bwDemandRes.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bwBytes, 0, 5));

        // Validation guards
        HttpResponseMessage badFormatRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9/register.pdf?yearId={yearId}&format=Z",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.BadRequest, badFormatRes.StatusCode);

        HttpResponseMessage badMarginRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9/register.pdf?yearId={yearId}&format=C&leftMarginMm=45",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.BadRequest, badMarginRes.StatusCode);

        // CSV Export (UTF-8 BOM)
        HttpResponseMessage csvRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9/export.csv?yearId={yearId}&format=C",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, csvRes.StatusCode);
        Assert.Equal("text/csv", csvRes.Content.Headers.ContentType?.MediaType);

        byte[] csvBytes = await csvRes.Content.ReadAsByteArrayAsync();
        Assert.True(csvBytes.Length > 50);
        Assert.Equal(0xEF, csvBytes[0]);
        Assert.Equal(0xBB, csvBytes[1]);
        Assert.Equal(0xBF, csvBytes[2]);
        string csvText = Encoding.UTF8.GetString(csvBytes, 3, csvBytes.Length - 3);
        Assert.Contains("बलवान पेंदिनाथ दिनकर", csvText);
        Assert.Contains("एकूण बेरीज", csvText);

        // AuditLog records Namuna9RegisterPrinted and Namuna9CsvExported
        using IServiceScope verifyScope = factory.Services.CreateScope();
        AppDbContext verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext verifyCtx = verifyScope.ServiceProvider.GetRequiredService<ITenantContext>();
        verifyCtx.SetTenant(tenantId);

        List<AuditLog> printAudits = await verifyDb.AuditLogs
            .Where(a => a.TenantId == tenantId && (a.Action == "Namuna9RegisterPrinted" || a.Action == "Namuna9CsvExported"))
            .ToListAsync();
        Assert.Contains(printAudits, a => a.Action == "Namuna9RegisterPrinted");
        Assert.Contains(printAudits, a => a.Action == "Namuna9CsvExported");
    }
}

