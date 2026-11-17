using System.Net;
using System.Net.Http.Json;
using System.Text;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Application.DTOs.Namuna9Ka;
using GPMH.Application.DTOs.Namuna10;
using GPMH.Application.DTOs.Partitions;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Citizens.Commands;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using GPMH.WebAPI.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Namuna10;

public sealed class Namuna10ReceiptPrintAndCancellationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static int _lgdSequence = 925000;
    private const string ValidGramsevakPassword = "Gramsevak#2026";

    private static readonly byte[] SamplePngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");

    private async Task<(Guid TenantId, Guid YearId, Guid GramsevakId, Guid OperatorId, Guid AuditorId)>
        SeedTenantWithCredentialsAndAssetsAsync()
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
        IPiiProtectionService pii = scope.ServiceProvider.GetRequiredService<IPiiProtectionService>();
        IPasswordHasherService hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantCode = $"GP-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = $"PrintCancel GP {lgdCode}",
            NameMr = $"पावती चाचणी ग्रा.पं. {lgdCode}",
            Status = TenantStatus.Active,
            SubscriptionExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(365),
        });

        db.Users.AddRange(
            new User
            {
                Id = gramsevakId,
                Username = $"gs-{lgdCode}",
                FullNameEn = "Gramsevak Verifier",
                FullNameMr = "ग्रामसेवक तपासणीस",
                PasswordHash = hasher.HashPassword(ValidGramsevakPassword),
                IsActive = true,
            },
            new User
            {
                Id = operatorId,
                Username = $"op-{lgdCode}",
                FullNameEn = "Operator Clerk",
                FullNameMr = "लिपिक",
                PasswordHash = hasher.HashPassword("Operator#2026"),
                IsActive = true,
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

        string gsKey = $"sig-gs-{lgdCode}";
        string opKey = $"sig-op-{lgdCode}";
        string stampKey = $"stamp-{lgdCode}";
        byte[] encPng = pii.EncryptBytes(SamplePngBytes);

        db.ProtectedAssets.AddRange(
            new ProtectedAsset
            {
                TenantId = tenantId,
                OwnerUserId = gramsevakId,
                AssetKey = gsKey,
                Category = "SIGNATURE",
                FileName = "gs.png",
                ContentType = "image/png",
                EncryptedContent = encPng,
                SizeBytes = SamplePngBytes.Length,
            },
            new ProtectedAsset
            {
                TenantId = tenantId,
                OwnerUserId = operatorId,
                AssetKey = opKey,
                Category = "SIGNATURE",
                FileName = "op.png",
                ContentType = "image/png",
                EncryptedContent = encPng,
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
                EncryptedContent = encPng,
                SizeBytes = SamplePngBytes.Length,
            });

        db.GramPanchayats.Add(new GramPanchayat
        {
            TenantId = tenantId,
            LgdCode = lgdCode,
            DistrictCode = "503",
            DistrictName = "अमरावती",
            TehsilCode = "4010",
            TehsilName = "चांदूर बाजार",
            PanchayatNameEn = $"PrintCancel GP {lgdCode}",
            PanchayatNameMr = $"पावती चाचणी {lgdCode}",
            GramsevakName = "श्री. आर. एस. देशमुख",
            SarpanchName = "सौ. अनिता मोहोड",
            OperatorName = "श्री. के. बी. इंगळे",
            GramsevakSignatureKey = gsKey,
            OperatorSignatureKey = opKey,
            OfficialStampKey = stampKey,
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
    public async Task DualReceiptPdf_PublicQrVerification_And_PasswordReauthenticatedCancellationReversal()
    {
        (Guid tenantId, Guid yearId, Guid gramsevakId, Guid operatorId, Guid auditorId) =
            await SeedTenantWithCredentialsAndAssetsAsync();

        using HttpClient client = factory.CreateClient();

        // 1. Register citizen (12-digit Aadhaar MUST NOT leak into PDF or public verify response) + property (₹966)
        const string secretAadhaar = "998877665544";
        CitizenDto cit = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand("CIT-N10-PRT1", "2", "सुनील वानखडे", "Sunil Wankhade", "Male", "9850123456", secretAadhaar, DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        PropertyDto prop = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: "2",
                PropertyNo: "505",
                BuiltUpAreaSqFt: 225m,
                OpenPlotAreaSqFt: 0m,
                BoundaryEast: "E",
                BoundaryWest: "W",
                BoundaryNorth: "N",
                BoundarySouth: "S",
                WaterConnectionCode: "GENERAL",
                IsTaxExempt: false,
                Owners: [new PropertyOwnerRequestItem(cit.Id, 100m, true)],
                Floors: [new PropertyFloorRequestItem(0, "तळमजला", "HUT_MUD", "RESIDENTIAL", 15m, 15m, null, 2021)],
                CalculateTax: true))))
            .Content.ReadFromJsonAsync<PropertyDto>())!;
        Assert.Equal(966m, prop.TotalAnnualTax);

        // Publish demand + generate Namuna 9-Ka bill + collect full ₹966 via UPI
        (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-9/publish",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new PublishDraftDemandsRequest(yearId))))
            .EnsureSuccessStatusCode();
        (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post, "/api/v1/namuna-9ka/bills/generate", gramsevakId, ApplicationRole.Gramsevak, tenantId,
            new GenerateNamuna9KaBillsRequest(yearId)))).EnsureSuccessStatusCode();

        DateOnly todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        HttpResponseMessage collectRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new CollectTaxRequest(
                PropertyId: prop.Id,
                AssessmentYearId: yearId,
                OperationId: Guid.NewGuid(),
                AmountPaid: 966m,
                PaymentMode: PaymentMode.Upi,
                ReceiptDate: todayUtc,
                BookNo: "BK-21",
                ReferenceNo: "UTR-PRINT-001",
                Remarks: "Full UPI settlement")));
        Assert.Equal(HttpStatusCode.Created, collectRes.StatusCode);
        TaxReceiptDto receipt = (await collectRes.Content.ReadFromJsonAsync<TaxReceiptDto>())!;
        Assert.Equal("RCP-2026-0001", receipt.ReceiptNo);
        Assert.Equal(0m, receipt.RemainingBalanceAfterPayment);

        // 2. Download Dual A4 Landscape QuestPDF Receipt (uses ProtectedAsset signatures/stamp & QR verify URL)
        HttpResponseMessage pdfRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-10/receipts/{receipt.ReceiptId}/dual.pdf",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, pdfRes.StatusCode);
        Assert.Equal("application/pdf", pdfRes.Content.Headers.ContentType?.MediaType);
        byte[] pdfBytes = await pdfRes.Content.ReadAsByteArrayAsync();
        Assert.True(pdfBytes.Length > 10_000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdfBytes, 0, 5));
        string pdfAscii = Encoding.ASCII.GetString(pdfBytes);
        Assert.Contains("/MediaBox [0 0 842 595]", pdfAscii);
        Assert.DoesNotContain(secretAadhaar, pdfAscii);

        // 3. Anonymous Public QR Receipt Verification & Domain Separation Checks
        using (IServiceScope tokScope = factory.Services.CreateScope())
        {
            IExtractVerificationTokenService tokenSvc =
                tokScope.ServiceProvider.GetRequiredService<IExtractVerificationTokenService>();

            string receiptToken = tokenSvc.GenerateReceiptToken(
                new ReceiptVerifyPayloadDto(tenantId, receipt.ReceiptId, DateTimeOffset.UtcNow));

            HttpResponseMessage verifyRes = await client.GetAsync($"/api/v1/public/verify/receipt/{Uri.EscapeDataString(receiptToken)}");
            Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);
            string rawVerifyJson = await verifyRes.Content.ReadAsStringAsync();
            Assert.DoesNotContain(secretAadhaar, rawVerifyJson);
            Assert.DoesNotContain("9850123456", rawVerifyJson);
            Assert.DoesNotContain("5544", rawVerifyJson);

            VerifiedReceiptPublicDto? verified = await verifyRes.Content.ReadFromJsonAsync<VerifiedReceiptPublicDto>();
            Assert.NotNull(verified);
            Assert.True(verified.IsValid);
            Assert.False(verified.IsCancelled);
            Assert.Equal("RCP-2026-0001", verified.ReceiptNo);
            Assert.Equal(966m, verified.TotalPaid);
            Assert.Equal("Upi", verified.PaymentMode);

            // Extract token must NOT validate on receipt verify endpoint
            string extractToken = tokenSvc.GenerateToken(
                new ExtractVerifyPayloadDto(tenantId, prop.Id, yearId, DateTimeOffset.UtcNow));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(
                $"/api/v1/public/verify/receipt/{Uri.EscapeDataString(extractToken)}")).StatusCode);

            // Tampered token rejected with 400
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(
                "/api/v1/public/verify/receipt/tampered-invalid-token")).StatusCode);
        }

        // 4. Cancellation RBAC, Validation & Password Re-authentication Guards
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/namuna-10/receipts/{receipt.ReceiptId}/cancel",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new CancelTaxReceiptRequest(ValidGramsevakPassword, "Operator attempting unauthorized cancel")))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/namuna-10/receipts/{receipt.ReceiptId}/cancel",
            auditorId,
            ApplicationRole.Auditor,
            tenantId,
            new CancelTaxReceiptRequest(ValidGramsevakPassword, "Auditor cannot cancel")))).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/namuna-10/receipts/{receipt.ReceiptId}/cancel",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CancelTaxReceiptRequest("WrongPassword!", "Wrong password check")))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/namuna-10/receipts/{receipt.ReceiptId}/cancel",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CancelTaxReceiptRequest(ValidGramsevakPassword, "no")))).StatusCode);

        // 5. Valid Gramsevak Cancellation -> Reverses Heads, Demand Balance, Bill Status, Dashboard KPIs & Zero-Dues Gate!
        const string cancelReason = "Incorrect UTR mode captured; re-issuing receipt.";
        HttpResponseMessage cancelRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/namuna-10/receipts/{receipt.ReceiptId}/cancel",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CancelTaxReceiptRequest(ValidGramsevakPassword, cancelReason)));
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        TaxReceiptDto cancelled = (await cancelRes.Content.ReadFromJsonAsync<TaxReceiptDto>())!;
        Assert.True(cancelled.IsCancelled);
        Assert.Equal(cancelReason, cancelled.CancelReason);
        Assert.NotNull(cancelled.CancelledAtUtc);
        Assert.Equal(966m, cancelled.RemainingBalanceAfterPayment);

        // Double cancellation rejected with 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/namuna-10/receipts/{receipt.ReceiptId}/cancel",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CancelTaxReceiptRequest(ValidGramsevakPassword, "Duplicate cancel check")))).StatusCode);

        // Public QR endpoint immediately flags receipt as CANCELLED
        using (IServiceScope tokScope2 = factory.Services.CreateScope())
        {
            IExtractVerificationTokenService tokenSvc =
                tokScope2.ServiceProvider.GetRequiredService<IExtractVerificationTokenService>();
            string verifyTok = tokenSvc.GenerateReceiptToken(
                new ReceiptVerifyPayloadDto(tenantId, receipt.ReceiptId, DateTimeOffset.UtcNow));

            VerifiedReceiptPublicDto? verifiedAfterCancel = await (await client.GetAsync(
                $"/api/v1/public/verify/receipt/{Uri.EscapeDataString(verifyTok)}"))
                .Content.ReadFromJsonAsync<VerifiedReceiptPublicDto>();
            Assert.NotNull(verifiedAfterCancel);
            Assert.True(verifiedAfterCancel.IsCancelled);
            Assert.Equal(cancelReason, verifiedAfterCancel.CancelReason);
        }

        // Cancelled Dual PDF renders valid CANCELLED receipt copy
        HttpResponseMessage cancelledPdfRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-10/receipts/{receipt.ReceiptId}/dual.pdf",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, cancelledPdfRes.StatusCode);
        byte[] cancelledPdfBytes = await cancelledPdfRes.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(cancelledPdfBytes, 0, 5));

        // Namuna 9-Ka bill restored to Issued, Dashboard KPIs reset to 0 collection / ₹966 arrears, Zero-Dues Gate re-locked!
        Namuna9KaWorkspaceDto billsAfterCancel = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-9ka/bills?propertyIds={prop.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<Namuna9KaWorkspaceDto>())!;
        Assert.Equal(PropertyDemandBill.StatusIssued, Assert.Single(billsAfterCancel.Items).BillStatus);

        DashboardSummaryDto dashAfterCancel = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/dashboard/summary",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<DashboardSummaryDto>())!;
        Assert.Equal(0m, dashAfterCancel.TodayCollection);
        Assert.Equal(0, dashAfterCancel.TodayReceipts);
        Assert.Equal(0m, dashAfterCancel.WaterAndSpecialCollection);
        Assert.Equal(966m, dashAfterCancel.CurrentFyDemand);

        Namuna10RegisterWorkspaceDto regAfterCancel = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/namuna-10/register?yearId={yearId}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<Namuna10RegisterWorkspaceDto>())!;
        Assert.Equal(0m, regAfterCancel.Kpis.TotalCollected);
        Assert.Equal(966m, regAfterCancel.Kpis.TotalBalance);

        PartitionEligibilityDto eligAfterCancel = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{prop.Id}/partition-eligibility",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<PartitionEligibilityDto>())!;
        Assert.False(eligAfterCancel.ZeroDuesGatePassed);
        Assert.Equal(966m, eligAfterCancel.PendingDues);

        // Re-collection works cleanly after reversal and verify Printed + Cancelled audit logs exist
        HttpResponseMessage rePayRes = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/namuna-10/collections",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CollectTaxRequest(prop.Id, yearId, Guid.NewGuid(), 246m, PaymentMode.Cash, todayUtc)));
        Assert.Equal(HttpStatusCode.Created, rePayRes.StatusCode);
        TaxReceiptDto reReceipt = (await rePayRes.Content.ReadFromJsonAsync<TaxReceiptDto>())!;
        Assert.Equal("RCP-2026-0002", reReceipt.ReceiptNo);
        Assert.Equal(720m, reReceipt.RemainingBalanceAfterPayment);

        // Locked Assessment Year blocks cancellation with 409 Conflict (ASSESSMENT_YEAR_LOCKED)
        using (IServiceScope lockScope = factory.Services.CreateScope())
        {
            AppDbContext lockDb = lockScope.ServiceProvider.GetRequiredService<AppDbContext>();
            ITenantContext lockCtx = lockScope.ServiceProvider.GetRequiredService<ITenantContext>();
            lockCtx.SetTenant(tenantId);

            AssessmentYear? yr = await lockDb.AssessmentYears.FirstOrDefaultAsync(y => y.Id == yearId);
            Assert.NotNull(yr);
            yr.IsLocked = true;
            await lockDb.SaveChangesAsync();

            Assert.Equal(2, await lockDb.AuditLogs.CountAsync(l => l.TenantId == tenantId && l.Action == "Namuna10ReceiptPrinted"));
            Assert.Equal(1, await lockDb.AuditLogs.CountAsync(l => l.TenantId == tenantId && l.Action == "Namuna10ReceiptCancelled"));
        }

        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/namuna-10/receipts/{reReceipt.ReceiptId}/cancel",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CancelTaxReceiptRequest(ValidGramsevakPassword, "Locked year check")))).StatusCode);
    }
}
