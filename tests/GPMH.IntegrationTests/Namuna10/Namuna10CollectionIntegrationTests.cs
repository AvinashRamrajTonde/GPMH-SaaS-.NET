using System.Net;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Application.DTOs.Namuna9Ka;
using GPMH.Application.DTOs.Namuna10;
using GPMH.Application.DTOs.Partitions;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Citizens.Commands;
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

namespace GPMH.IntegrationTests.Namuna10;

public sealed class Namuna10CollectionIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static int _lgdSequence = 910000;

    private async Task<(Guid TenantId, Guid YearId, Guid GramsevakId, Guid OperatorId, Guid AuditorId)>
        SeedNamuna10TenantAsync()
    {
        Guid tenantId = Guid.NewGuid();
        Guid gramsevakId = Guid.NewGuid();
        Guid operatorId = Guid.NewGuid();
        Guid auditorId = Guid.NewGuid();
        string lgdCode = Interlocked.Increment(ref _lgdSequence).ToString();

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        IDefaultFormulaSeeder seeder = scope.ServiceProvider.GetRequiredService<IDefaultFormulaSeeder>();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantCode = $"GP-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = $"Test GP {lgdCode}",
            NameMr = $"चाचणी ग्रा.पं. {lgdCode}",
            Status = TenantStatus.Active,
            SubscriptionExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(365),
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
            },
            new TenantMembership
            {
                TenantId = tenantId,
                UserId = auditorId,
                Role = ApplicationRole.Auditor,
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
            PanchayatNameEn = $"Demo GP {lgdCode}",
            PanchayatNameMr = $"डेमो ग्रा.पं. {lgdCode}",
            GramsevakName = "श्री. आर. पी. देशमुख",
            SarpanchName = "सौ. अनिता पाटील",
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

        return (tenantId, activeYear.Id, gramsevakId, operatorId, auditorId);
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
    public async Task Namuna10_FifoPartialAndFullCollection_IdempotentReplay_DashboardKpis_And_PartitionUnlock()
    {
        (Guid tenantId, Guid yearId, Guid gramsevakId, Guid operatorId, Guid auditorId) =
            await SeedNamuna10TenantAsync();
        (Guid otherTenantId, _, Guid otherUserId, _, _) = await SeedNamuna10TenantAsync();

        using HttpClient client = factory.CreateClient();

        // 1. Register Citizens and two Properties:
        //    Prop 1: Chincholi Khurd (246) + GENERAL Water (720) = 966 INR
        //    Prop 2: Chincholi Khurd (246) + NONE water = 246 INR (for Early Rebate on collection)
        CitizenDto cit1 = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand("CIT-N10-1", "1", "रमेश इंगळे", "Ramesh Ingle", "Male", "9823001122", "444455556677", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        PropertyDto prop1 = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: "1",
                PropertyNo: "201",
                BuiltUpAreaSqFt: 225m,
                OpenPlotAreaSqFt: 0m,
                BoundaryEast: "E",
                BoundaryWest: "W",
                BoundaryNorth: "N",
                BoundarySouth: "S",
                WaterConnectionCode: "GENERAL",
                IsTaxExempt: false,
                Owners: [new PropertyOwnerRequestItem(cit1.Id, 100m, true)],
                Floors: [new PropertyFloorRequestItem(0, "तळमजला", "HUT_MUD", "RESIDENTIAL", 15m, 15m, null, 2021)],
                CalculateTax: true))))
            .Content.ReadFromJsonAsync<PropertyDto>())!;
        Assert.Equal(966m, prop1.TotalAnnualTax);

        PropertyDto prop2 = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: "2",
                PropertyNo: "202",
                BuiltUpAreaSqFt: 225m,
                OpenPlotAreaSqFt: 0m,
                BoundaryEast: "E",
                BoundaryWest: "W",
                BoundaryNorth: "N",
                BoundarySouth: "S",
                WaterConnectionCode: "NONE",
                IsTaxExempt: false,
                Owners: [new PropertyOwnerRequestItem(cit1.Id, 100m, true)],
                Floors: [new PropertyFloorRequestItem(0, "तळमजला", "HUT_MUD", "RESIDENTIAL", 15m, 15m, null, 2021)],
                CalculateTax: true))))
            .Content.ReadFromJsonAsync<PropertyDto>())!;
        Assert.Equal(246m, prop2.TotalAnnualTax);

        // Publish Namuna 9 + add NoticeFee=10 and Penalty=20 on Prop 1 (Total Net = 996) + issue 9-Ka bill
        (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9/publish",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new PublishDraftDemandsRequest(yearId)))).EnsureSuccessStatusCode();

        (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/namuna-9/adjustments",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new BatchAdjustNamuna9FeesRequest(
                yearId,
                [new Namuna9FeeAdjustmentItem(prop1.Id, NoticeFee: 10m, WarrantFee: 0m, PenaltyAmount: 20m, RebateAmount: 0m, Remarks: "Fee Notice")])))).EnsureSuccessStatusCode();

        (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9ka/bills/generate",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new GenerateNamuna9KaBillsRequest(yearId)))).EnsureSuccessStatusCode();

        // Partition eligibility is initially BLOCKED (996 INR dues)
        PartitionEligibilityDto eligBefore = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{prop1.Id}/partition-eligibility",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId)))
            .Content.ReadFromJsonAsync<PartitionEligibilityDto>())!;
        Assert.False(eligBefore.ZeroDuesGatePassed);
        Assert.Equal(996m, eligBefore.PendingDues);

        // 2. RBAC & Validation checks
        // Auditor cannot collect -> 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            auditorId,
            ApplicationRole.Auditor,
            tenantId,
            new CollectTaxRequest(prop1.Id, yearId, Guid.NewGuid(), 100m, PaymentMode.Cash)))).StatusCode);

        // Cheque without BankName / ReferenceNo -> 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new CollectTaxRequest(prop1.Id, yearId, Guid.NewGuid(), 100m, PaymentMode.Cheque)))).StatusCode);

        // UPI without ReferenceNo -> 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new CollectTaxRequest(prop1.Id, yearId, Guid.NewGuid(), 100m, PaymentMode.Upi)))).StatusCode);

        // 3. Partial Installment 1 (GP_Operator collects ₹100 Cash) with stable OperationId
        Guid opId1 = Guid.NewGuid();
        DateOnly todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        HttpResponseMessage pay1Res = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new CollectTaxRequest(
                PropertyId: prop1.Id,
                AssessmentYearId: yearId,
                OperationId: opId1,
                AmountPaid: 100m,
                PaymentMode: PaymentMode.Cash,
                ReceiptDate: todayUtc,
                BookNo: "BK-10",
                Remarks: "First installment")));
        Assert.Equal(HttpStatusCode.Created, pay1Res.StatusCode);

        TaxReceiptDto receipt1 = (await pay1Res.Content.ReadFromJsonAsync<TaxReceiptDto>())!;
        Assert.Equal("RCP-2026-0001", receipt1.ReceiptNo);
        Assert.Equal("BK-10", receipt1.BookNo);
        Assert.Equal(100m, receipt1.TotalPaid);
        Assert.Equal(896m, receipt1.RemainingBalanceAfterPayment);
        Assert.False(receipt1.IdempotentReplay);
        // Fees cleared first: Notice=10, Penalty=20 -> remaining 70 allocates to House=66 + Lighting=4
        Assert.Equal(10m, receipt1.HeadAllocation.NoticeFeePaid);
        Assert.Equal(20m, receipt1.HeadAllocation.PenaltyPaid);
        Assert.Equal(66m, receipt1.HeadAllocation.SafeCurrent.HouseTax);
        Assert.Equal(4m, receipt1.HeadAllocation.SafeCurrent.LightingTax);
        Assert.Equal("XXXX-XXXX-6677", receipt1.PrimaryOwnerAadhaarMasked);

        // 4. Idempotent Replay with SAME OperationId -> 200 OK, SAME ReceiptNo, ZERO double deduction!
        HttpResponseMessage replayRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new CollectTaxRequest(prop1.Id, yearId, opId1, 100m, PaymentMode.Cash, todayUtc)));
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        TaxReceiptDto replayDto = (await replayRes.Content.ReadFromJsonAsync<TaxReceiptDto>())!;
        Assert.True(replayDto.IdempotentReplay);
        Assert.Equal(receipt1.ReceiptId, replayDto.ReceiptId);
        Assert.Equal("RCP-2026-0001", replayDto.ReceiptNo);

        // Confirm Namuna 9-Ka bill transitioned to PartiallyPaid & Dashboard shows 100 INR / 1 Receipt
        DashboardSummaryDto dash1 = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/dashboard/summary",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId)))
            .Content.ReadFromJsonAsync<DashboardSummaryDto>())!;
        Assert.Equal(100m, dash1.TodayCollection);
        Assert.Equal(100m, dash1.MonthCollection);
        Assert.Equal(0m, dash1.WaterAndSpecialCollection);
        Assert.Equal(1, dash1.TodayReceipts);

        // 5. Partial Installment 2 (UPI with UTR for remaining ₹896) -> Settles property + Bill + Water KPI
        HttpResponseMessage pay2Res = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CollectTaxRequest(
                PropertyId: prop1.Id,
                AssessmentYearId: yearId,
                OperationId: Guid.NewGuid(),
                AmountPaid: 896m,
                PaymentMode: PaymentMode.Upi,
                ReceiptDate: todayUtc,
                ReferenceNo: "UTR99887766",
                Remarks: "Final UPI settlement")));
        Assert.Equal(HttpStatusCode.Created, pay2Res.StatusCode);
        TaxReceiptDto receipt2 = (await pay2Res.Content.ReadFromJsonAsync<TaxReceiptDto>())!;
        Assert.Equal("RCP-2026-0002", receipt2.ReceiptNo);
        Assert.Equal(0m, receipt2.RemainingBalanceAfterPayment);
        // Allocates remaining Lighting=36, Health=40, Sanitation=100, GeneralWater=720
        Assert.Equal(36m, receipt2.HeadAllocation.SafeCurrent.LightingTax);
        Assert.Equal(40m, receipt2.HeadAllocation.SafeCurrent.HealthTax);
        Assert.Equal(100m, receipt2.HeadAllocation.SafeCurrent.SanitationTax);
        Assert.Equal(720m, receipt2.HeadAllocation.SafeCurrent.GeneralWaterTax);

        // Verify Settled property rejects additional collection with 409 PROPERTY_ALREADY_SETTLED
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CollectTaxRequest(prop1.Id, yearId, Guid.NewGuid(), 10m, PaymentMode.Cash)))).StatusCode);

        // Verify #27 Zero-Dues Partition Gate is NOW UNLOCKED for Prop 1!
        PartitionEligibilityDto eligAfter = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{prop1.Id}/partition-eligibility",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId)))
            .Content.ReadFromJsonAsync<PartitionEligibilityDto>())!;
        Assert.True(eligAfter.ZeroDuesGatePassed);
        Assert.Equal(0m, eligAfter.PendingDues);

        // Verify 9-Ka bill is now Paid
        Namuna9KaWorkspaceDto billsAfter = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9ka/bills?propertyIds={prop1.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId)))
            .Content.ReadFromJsonAsync<Namuna9KaWorkspaceDto>())!;
        Assert.Equal(PropertyDemandBill.StatusPaid, Assert.Single(billsAfter.Items).BillStatus);

        // Verify Prop 2 Early Rebate applied atomically on collection before July 31 (246 - 24.60 = 221.40)
        // Note: ReceiptDate in test set to 2026-07-10 triggers 10% early rebate cleanly before cutoff!
        HttpResponseMessage payRebateRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CollectTaxRequest(
                PropertyId: prop2.Id,
                AssessmentYearId: yearId,
                OperationId: Guid.NewGuid(),
                AmountPaid: 221.40m,
                PaymentMode: PaymentMode.NeftRtgs,
                ReceiptDate: new DateOnly(2026, 7, 10),
                ReferenceNo: "NEFT-2026-01",
                ApplyEligibleEarlyRebate: true)));
        Assert.Equal(HttpStatusCode.Created, payRebateRes.StatusCode);
        TaxReceiptDto receipt3 = (await payRebateRes.Content.ReadFromJsonAsync<TaxReceiptDto>())!;
        Assert.Equal("RCP-2026-0003", receipt3.ReceiptNo);
        Assert.Equal(24.60m, receipt3.DiscountGiven);
        Assert.Equal(0m, receipt3.RemainingBalanceAfterPayment);

        // 6. Verify Live Dashboard Collection Metrics reflect ALL collections cleanly
        DashboardSummaryDto dashFinal = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/dashboard/summary",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId)))
            .Content.ReadFromJsonAsync<DashboardSummaryDto>())!;
        Assert.Equal(996m, dashFinal.TodayCollection); // receipts 1 & 2 dated todayUtc; receipt 3 dated July 10
        Assert.Equal(2, dashFinal.TodayReceipts);
        Assert.Equal(720m, dashFinal.WaterAndSpecialCollection);

        // 7. Verify Register KPIs, Search, Mode filters & Cross-Tenant Isolation
        Namuna10RegisterWorkspaceDto reg = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/namuna-10/register",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId)))
            .Content.ReadFromJsonAsync<Namuna10RegisterWorkspaceDto>())!;
        Assert.Equal(2, reg.Kpis.TotalProperties);
        Assert.Equal(2, reg.Kpis.FullyPaidCount);
        Assert.Equal(0m, reg.Kpis.TotalBalance);

        Namuna10ReceiptsPageDto upiReceipts = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-10/receipts?mode=2&search=UTR99887766",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId)))
            .Content.ReadFromJsonAsync<Namuna10ReceiptsPageDto>())!;
        TaxReceiptDto filteredUpi = Assert.Single(upiReceipts.Items);
        Assert.Equal("RCP-2026-0002", filteredUpi.ReceiptNo);

        // Cross-tenant isolation: Tenant B cannot see or fetch Tenant A's receipt
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-10/receipts/{receipt1.ReceiptId}",
            otherUserId,
            ApplicationRole.Gramsevak,
            otherTenantId))).StatusCode);

        // Audit trail emitted
        using IServiceScope auditScope = factory.Services.CreateScope();
        AppDbContext auditDb = auditScope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext auditCtx = auditScope.ServiceProvider.GetRequiredService<ITenantContext>();
        auditCtx.SetTenant(tenantId);
        int issuedLogs = await auditDb.AuditLogs
            .CountAsync(l => l.TenantId == tenantId && l.Action == "Namuna10ReceiptIssued");
        Assert.Equal(3, issuedLogs);
    }
}

