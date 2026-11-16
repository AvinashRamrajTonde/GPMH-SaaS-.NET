using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Application.DTOs.Namuna9Ka;
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

namespace GPMH.IntegrationTests.Namuna9;

public sealed class Namuna9KaBillIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static int _lgdSequence = 900000;

    // Minimal valid 1x1 PNG so QuestPDF decodes real ProtectedAsset signatures & stamp bytes
    private static readonly byte[] SamplePngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");

    private async Task<(Guid TenantId, Guid YearId, Guid GramsevakId, Guid OperatorId)> SeedNamuna9KaTenantAsync(
        bool configureBankUpi = true,
        bool configureSignatures = true)
    {
        Guid tenantId = Guid.NewGuid();
        Guid gramsevakId = Guid.NewGuid();
        Guid operatorId = Guid.NewGuid();
        string lgdCode = Interlocked.Increment(ref _lgdSequence).ToString();

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        IDefaultFormulaSeeder seeder = scope.ServiceProvider.GetRequiredService<IDefaultFormulaSeeder>();
        IPiiProtectionService pii = scope.ServiceProvider.GetRequiredService<IPiiProtectionService>();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantCode = $"gp9ka-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = $"Chincholi Khurd {lgdCode}",
            NameMr = $"चिंचोली खुर्द {lgdCode}",
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

        string? gsSigKey = null;
        string? spSigKey = null;
        string? stampKey = null;

        if (configureSignatures)
        {
            gsSigKey = $"sig-gs-{lgdCode}";
            spSigKey = $"sig-sp-{lgdCode}";
            stampKey = $"stamp-{lgdCode}";

            byte[] encryptedImg = pii.EncryptBytes(SamplePngBytes);
            db.ProtectedAssets.AddRange(
                new ProtectedAsset
                {
                    TenantId = tenantId,
                    OwnerUserId = gramsevakId,
                    AssetKey = gsSigKey,
                    Category = "SIGNATURE",
                    FileName = "gs.png",
                    ContentType = "image/png",
                    EncryptedContent = encryptedImg,
                    SizeBytes = SamplePngBytes.Length,
                },
                new ProtectedAsset
                {
                    TenantId = tenantId,
                    OwnerUserId = gramsevakId,
                    AssetKey = spSigKey,
                    Category = "SIGNATURE",
                    FileName = "sp.png",
                    ContentType = "image/png",
                    EncryptedContent = encryptedImg,
                    SizeBytes = SamplePngBytes.Length,
                },
                new ProtectedAsset
                {
                    TenantId = tenantId,
                    OwnerUserId = gramsevakId,
                    AssetKey = stampKey,
                    Category = "STAMP",
                    FileName = "stamp.png",
                    ContentType = "image/png",
                    EncryptedContent = encryptedImg,
                    SizeBytes = SamplePngBytes.Length,
                });
        }

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
            GramsevakName = "श्री. आर. बी. कुलकर्णी",
            SarpanchName = "सौ. मीनाताई पाटील",
            OperatorName = "श्री. ए. के. जाधव",
            GramsevakSignatureKey = gsSigKey,
            SarpanchSignatureKey = spSigKey,
            OfficialStampKey = stampKey,
            UpiId = null,
        });

        if (configureBankUpi)
        {
            db.BankAccounts.AddRange(
                new BankAccount
                {
                    TenantId = tenantId,
                    FundType = "WATER_SUPPLY",
                    BankName = "SBI Water",
                    BranchName = "Anjangaon",
                    AccountNumber = $"2000{lgdCode}",
                    IfscCode = "SBIN0000123",
                    UpiId = $"water{lgdCode}@sbi",
                    UpiPayeeName = "GP Water Fund",
                    IsDefault = false,
                    IsActive = true,
                },
                new BankAccount
                {
                    TenantId = tenantId,
                    FundType = "GRAM_NIDHI",
                    BankName = "SBI Gram Nidhi",
                    BranchName = "Anjangaon",
                    AccountNumber = $"1000{lgdCode}",
                    IfscCode = "SBIN0000123",
                    UpiId = $"gramnidhi{lgdCode}@sbi",
                    UpiPayeeName = $"GP Chincholi {lgdCode}",
                    IsDefault = true,
                    IsActive = true,
                });
        }

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
    public async Task Namuna9Ka_BillGeneration_Numbering_Idempotency_Superseded_UpiQr_And_PdfLayouts()
    {
        (Guid tenantId, Guid yearId, Guid gramsevakId, Guid operatorId) =
            await SeedNamuna9KaTenantAsync(configureBankUpi: true, configureSignatures: true);
        (Guid noUpiTenantId, Guid noUpiYearId, Guid noUpiGramsevakId, _) =
            await SeedNamuna9KaTenantAsync(configureBankUpi: false, configureSignatures: false);

        using HttpClient client = factory.CreateClient();

        CitizenDto owner = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand("CIT-9KA-1", "1", "ऋषिकेश सावरकर", "Rishikesh Savarkar", "Male", "9822000101", "222233334444", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        PropertyDto prop1 = (await (await client.SendAsync(CreateTenantRequest(
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
                CalculateTax: true))))
            .Content.ReadFromJsonAsync<PropertyDto>())!;
        Assert.Equal(246m, prop1.TotalAnnualTax);

        PropertyDto prop2 = (await (await client.SendAsync(CreateTenantRequest(
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
                BoundaryWest: "Plot 3",
                BoundaryNorth: "Lane",
                BoundarySouth: "Plot 4",
                WaterConnectionCode: "GENERAL",
                IsTaxExempt: false,
                Owners: [new PropertyOwnerRequestItem(owner.Id, 100m, true)],
                Floors: [new PropertyFloorRequestItem(0, "तळमजला", "HUT_MUD", "RESIDENTIAL", 15m, 15m, null, 2021)],
                CalculateTax: true))))
            .Content.ReadFromJsonAsync<PropertyDto>())!;
        Assert.Equal(966m, prop2.TotalAnnualTax);

        // RBAC check: GP_Operator cannot generate bills -> 403 Forbidden
        HttpResponseMessage forbiddenGen = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9ka/bills/generate",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new GenerateNamuna9KaBillsRequest(yearId)));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenGen.StatusCode);

        // Generate bills as Gramsevak
        DateOnly issueDate = new(2026, 7, 10);
        HttpResponseMessage genRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9ka/bills/generate",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new GenerateNamuna9KaBillsRequest(yearId, IssueDate: issueDate)));
        genRes.EnsureSuccessStatusCode();

        Namuna9KaGenerateResultDto? genDto = await genRes.Content.ReadFromJsonAsync<Namuna9KaGenerateResultDto>();
        Assert.NotNull(genDto);
        Assert.Equal("2026-27", genDto.YearLabel);
        Assert.Equal(2, genDto.GeneratedCount);
        Assert.Equal(0, genDto.ReusedCount);
        Assert.Equal(0, genDto.SupersededCount);
        Assert.True(genDto.HasUpiConfigured);
        Assert.StartsWith("gramnidhi", genDto.ConfiguredUpiId);
        Assert.Null(genDto.WarningMessage);
        Assert.Equal(2, genDto.Bills.Count);

        Namuna9KaGeneratedBillRefDto bill1 = genDto.Bills[0];
        Namuna9KaGeneratedBillRefDto bill2 = genDto.Bills[1];
        Assert.Equal("BILL-2026-0001", bill1.BillNo);
        Assert.Equal(1, bill1.BillSequence);
        Assert.Equal(issueDate, bill1.IssueDate);
        Assert.Equal(issueDate.AddDays(15), bill1.DueDate);
        Assert.Equal(246m, bill1.NetPayableAmount);
        Assert.NotNull(bill1.UpiPayUri);
        Assert.Contains("pa=gramnidhi", bill1.UpiPayUri);
        Assert.Contains("am=246.00", bill1.UpiPayUri);
        Assert.Contains("tr=BILL-2026-0001", bill1.UpiPayUri);
        Assert.Contains("tn=TaxBill-1-1", bill1.UpiPayUri);
        Assert.Contains("cu=INR", bill1.UpiPayUri);

        Assert.Equal("BILL-2026-0002", bill2.BillNo);
        Assert.Equal(2, bill2.BillSequence);
        Assert.Equal(966m, bill2.NetPayableAmount);

        // Fetch workspace & verify Marathi words + Aadhaar masking (never plaintext!)
        HttpResponseMessage listRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9ka/bills?yearId={yearId}",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        listRes.EnsureSuccessStatusCode();
        string rawJson = await listRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("222233334444", rawJson);

        Namuna9KaWorkspaceDto? ws = JsonSerializer.Deserialize<Namuna9KaWorkspaceDto>(
            rawJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(ws);
        Assert.Equal(2, ws.BillsIssuedCount);
        Assert.Equal(0, ws.PendingGenerationCount);
        Assert.Equal(1212m, ws.TotalNetPayable);
        Namuna9KaBillItemDto row1 = ws.Items.First(i => i.PropertyId == prop1.Id);
        Assert.Equal("दोनशे शेचाळीस रुपये फक्त", row1.AmountInWordsMr);
        Assert.Equal("XXXX-XXXX-4444", row1.PrimaryOwnerAadhaarMasked);
        Assert.False(row1.IsStaleBill);

        // Idempotent second call reuses BILL-2026-0001 and BILL-2026-0002 without bumping counter
        HttpResponseMessage idempRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9ka/bills/generate",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new GenerateNamuna9KaBillsRequest(yearId)));
        idempRes.EnsureSuccessStatusCode();
        Namuna9KaGenerateResultDto? idempDto = await idempRes.Content.ReadFromJsonAsync<Namuna9KaGenerateResultDto>();
        Assert.NotNull(idempDto);
        Assert.Equal(0, idempDto.GeneratedCount);
        Assert.Equal(2, idempDto.ReusedCount);
        Assert.Equal(0, idempDto.SupersededCount);

        // Adjust NoticeFee (+10) on prop1 -> stale detection & RegenerateIfChanged creates BILL-2026-0003 & supersedes BILL-2026-0001
        HttpResponseMessage adjRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/namuna-9/adjustments",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new BatchAdjustNamuna9FeesRequest(
                yearId,
                [new Namuna9FeeAdjustmentItem(prop1.Id, NoticeFee: 10m, WarrantFee: 0m, PenaltyAmount: 0m, RebateAmount: 0m)])));
        adjRes.EnsureSuccessStatusCode();

        Namuna9KaWorkspaceDto? wsStale = await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9ka/bills?yearId={yearId}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<Namuna9KaWorkspaceDto>();
        Assert.NotNull(wsStale);
        Assert.True(wsStale.Items.First(i => i.PropertyId == prop1.Id).IsStaleBill);
        Assert.False(wsStale.Items.First(i => i.PropertyId == prop2.Id).IsStaleBill);

        HttpResponseMessage regenRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9ka/bills/generate",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new GenerateNamuna9KaBillsRequest(yearId, RegenerateIfChanged: true)));
        regenRes.EnsureSuccessStatusCode();
        Namuna9KaGenerateResultDto? regenDto = await regenRes.Content.ReadFromJsonAsync<Namuna9KaGenerateResultDto>();
        Assert.NotNull(regenDto);
        Assert.Equal(1, regenDto.GeneratedCount);
        Assert.Equal(1, regenDto.ReusedCount);
        Assert.Equal(1, regenDto.SupersededCount);

        Namuna9KaGeneratedBillRefDto bill3 = regenDto.Bills.First(b => b.PropertyId == prop1.Id);
        Assert.Equal("BILL-2026-0003", bill3.BillNo);
        Assert.Equal(3, bill3.BillSequence);
        Assert.True(bill3.NewlyIssued);
        Assert.True(bill3.ReplacedSuperseded);
        Assert.Equal(256m, bill3.NetPayableAmount);
        Assert.Contains("am=256.00", bill3.UpiPayUri);
        Assert.Contains("tr=BILL-2026-0003", bill3.UpiPayUri);

        // Verify QuestPDF endpoints: 2in1_landscape (counterfoil + batch2in1) and a5
        HttpResponseMessage pdfCounterfoilRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9ka/bills.pdf?yearId={yearId}&layout=2in1_landscape&splitMode=counterfoil",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, pdfCounterfoilRes.StatusCode);
        Assert.Equal("application/pdf", pdfCounterfoilRes.Content.Headers.ContentType?.MediaType);
        byte[] counterfoilBytes = await pdfCounterfoilRes.Content.ReadAsByteArrayAsync();
        Assert.True(counterfoilBytes.Length > 10_000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(counterfoilBytes, 0, 5));
        string counterfoilAscii = Encoding.ASCII.GetString(counterfoilBytes);
        Assert.Contains("/MediaBox [0 0 842 595]", counterfoilAscii);

        HttpResponseMessage pdfBatch2Res = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9ka/bills.pdf?yearId={yearId}&layout=2in1_landscape&splitMode=batch2in1",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, pdfBatch2Res.StatusCode);
        byte[] batch2Bytes = await pdfBatch2Res.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(batch2Bytes, 0, 5));
        Assert.Contains("/MediaBox [0 0 842 595]", Encoding.ASCII.GetString(batch2Bytes));

        HttpResponseMessage pdfA5Res = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9ka/bills.pdf?yearId={yearId}&layout=a5",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, pdfA5Res.StatusCode);
        byte[] a5Bytes = await pdfA5Res.Content.ReadAsByteArrayAsync();
        Assert.True(a5Bytes.Length > 10_000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(a5Bytes, 0, 5));
        Assert.Contains("/MediaBox [0 0 420 595]", Encoding.ASCII.GetString(a5Bytes));

        // Validation guard for invalid layout
        HttpResponseMessage badLayout = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9ka/bills.pdf?yearId={yearId}&layout=letter",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.BadRequest, badLayout.StatusCode);

        // Tenant with no UPI configured issues bill cleanly with warning & null QR
        CitizenDto otherOwner = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            noUpiGramsevakId,
            ApplicationRole.Gramsevak,
            noUpiTenantId,
            new CreateCitizenCommand("CIT-9KA-NOUPI", "1", "बलवान दिनकर", "Balwan Dinkar", "Male", "9822000222", "555566667777", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            noUpiGramsevakId,
            ApplicationRole.Gramsevak,
            noUpiTenantId,
            new CreatePropertyCommand(
                WardNo: "1",
                PropertyNo: "1",
                BuiltUpAreaSqFt: 225m,
                OpenPlotAreaSqFt: 0m,
                BoundaryEast: "Road",
                BoundaryWest: "Lane",
                BoundaryNorth: "North",
                BoundarySouth: "South",
                WaterConnectionCode: "NONE",
                IsTaxExempt: false,
                Owners: [new PropertyOwnerRequestItem(otherOwner.Id, 100m, true)],
                Floors: [new PropertyFloorRequestItem(0, "तळमजला", "HUT_MUD", "RESIDENTIAL", 15m, 15m, null, 2021)],
                CalculateTax: true))))
            .Content.ReadFromJsonAsync<PropertyDto>();

        HttpResponseMessage noUpiGen = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9ka/bills/generate",
            noUpiGramsevakId,
            ApplicationRole.Gramsevak,
            noUpiTenantId,
            new GenerateNamuna9KaBillsRequest(noUpiYearId)));
        noUpiGen.EnsureSuccessStatusCode();
        Namuna9KaGenerateResultDto? noUpiDto = await noUpiGen.Content.ReadFromJsonAsync<Namuna9KaGenerateResultDto>();
        Assert.NotNull(noUpiDto);
        Assert.False(noUpiDto.HasUpiConfigured);
        Assert.NotNull(noUpiDto.WarningMessage);
        Assert.Null(Assert.Single(noUpiDto.Bills).UpiPayUri);

        // Cross-tenant isolation -> 404
        HttpResponseMessage crossTenantGet = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9ka/bills?yearId={yearId}",
            noUpiGramsevakId,
            ApplicationRole.Gramsevak,
            noUpiTenantId));
        Assert.Equal(HttpStatusCode.NotFound, crossTenantGet.StatusCode);

        // Verify DB Superseded bill status, BillCounter sequence = 3, and AuditLogs
        using IServiceScope verifyScope = factory.Services.CreateScope();
        AppDbContext verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext verifyCtx = verifyScope.ServiceProvider.GetRequiredService<ITenantContext>();
        verifyCtx.SetTenant(tenantId);

        List<PropertyDemandBill> storedBills = await verifyDb.PropertyDemandBills
            .OrderBy(b => b.BillSequence)
            .ToListAsync();
        Assert.Equal(3, storedBills.Count);
        Assert.Equal(PropertyDemandBill.StatusSuperseded, storedBills[0].Status);
        Assert.Equal("BILL-2026-0001", storedBills[0].BillNo);
        Assert.Equal(PropertyDemandBill.StatusIssued, storedBills[1].Status);
        Assert.Equal(PropertyDemandBill.StatusIssued, storedBills[2].Status);

        BillCounter? counter = await verifyDb.BillCounters.FirstOrDefaultAsync(c => c.FinancialYear == "2026-27");
        Assert.NotNull(counter);
        Assert.Equal(3, counter.LastSequence);

        List<string> auditActions = await verifyDb.AuditLogs
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Action)
            .ToListAsync();
        Assert.Contains("Namuna9KaBillsGenerated", auditActions);
        Assert.Contains("Namuna9KaBillsPrinted", auditActions);
    }
}
