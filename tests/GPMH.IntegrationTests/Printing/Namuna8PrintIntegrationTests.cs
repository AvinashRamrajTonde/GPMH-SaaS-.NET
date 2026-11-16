using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Citizens.Commands;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Printing;

public sealed class Namuna8PrintIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static int _lgdSequence = 580000;

    private static readonly byte[] MinimalValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private async Task<(Guid TenantId, Guid GramsevakUserId)> SeedPrintableTenantAsync(bool includeSignatures = true)
    {
        Guid tenantId = Guid.NewGuid();
        Guid gramsevakId = Guid.NewGuid();
        string lgdCode = Interlocked.Increment(ref _lgdSequence).ToString();
        string gramSigKey = $"sig-gs-{tenantId:N}";
        string stampKey = $"stamp-gp-{tenantId:N}";

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        IDefaultFormulaSeeder formulaSeeder = scope.ServiceProvider.GetRequiredService<IDefaultFormulaSeeder>();
        IPiiProtectionService pii = scope.ServiceProvider.GetRequiredService<IPiiProtectionService>();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantCode = $"gp-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = $"Print GP {lgdCode}",
            NameMr = $"प्रिंट ग्रामपंचायत {lgdCode}",
            Status = TenantStatus.Active,
        });

        db.TenantMemberships.Add(new TenantMembership
        {
            TenantId = tenantId,
            UserId = gramsevakId,
            Role = ApplicationRole.Gramsevak,
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
            PanchayatNameEn = $"Chincholi {lgdCode}",
            PanchayatNameMr = $"चिंचोली बुद्रुक {lgdCode}",
            Pincode = "444705",
            GramsevakName = "श्री. आर. एस. देशमुख",
            SarpanchName = "सौ. एम. व्ही. पाटील",
            OperatorName = "के. पी. जोशी",
            GramsevakSignatureKey = includeSignatures ? gramSigKey : null,
            OfficialStampKey = includeSignatures ? stampKey : null,
            UpiId = "gpchincholi@sbi",
            UpiPayeeName = "Gram Panchayat Chincholi",
        });

        db.BankAccounts.Add(new BankAccount
        {
            TenantId = tenantId,
            FundType = "GRAM_NIDHI",
            BankName = "State Bank of India",
            BranchName = "Anjangaon Surji",
            AccountNumber = "12345678901",
            IfscCode = "SBIN0001234",
            UpiId = "gpchincholi@sbi",
            UpiPayeeName = "Gram Panchayat Chincholi",
            IsDefault = true,
            IsActive = true,
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

        if (includeSignatures)
        {
            byte[] encryptedPng = pii.EncryptBytes(MinimalValidPngBytes);
            db.ProtectedAssets.AddRange(
                new ProtectedAsset
                {
                    AssetKey = gramSigKey,
                    TenantId = tenantId,
                    OwnerUserId = gramsevakId,
                    Category = "GRAMSEVAK_SIGN",
                    FileName = "gs-sign.png",
                    ContentType = "image/png",
                    EncryptedContent = encryptedPng,
                    SizeBytes = MinimalValidPngBytes.LongLength,
                },
                new ProtectedAsset
                {
                    AssetKey = stampKey,
                    TenantId = tenantId,
                    OwnerUserId = gramsevakId,
                    Category = "OFFICIAL_STAMP",
                    FileName = "gp-stamp.png",
                    ContentType = "image/png",
                    EncryptedContent = encryptedPng,
                    SizeBytes = MinimalValidPngBytes.LongLength,
                });
        }

        await formulaSeeder.SeedDefaultsForTenantAsync(tenantId, CancellationToken.None);
        await db.SaveChangesAsync();

        return (tenantId, gramsevakId);
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

    private static async Task<PropertyDto> CreateAssessedPropertyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        string wardNo,
        string propertyNo,
        Guid citizenId)
    {
        HttpResponseMessage resp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            userId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: wardNo,
                PropertyNo: propertyNo,
                BuiltUpAreaSqFt: 225m,
                OpenPlotAreaSqFt: 100m,
                BoundaryEast: "मुख्य रस्ता",
                BoundaryWest: "प्लॉट क्र. १२",
                BoundaryNorth: "ग्रामपंचायत गल्ली",
                BoundarySouth: "नदी पात्र",
                WaterConnectionCode: "GENERAL",
                StreetOrLaneName: "सावरकर चौक",
                CitySurveyOrGutNo: $"CTS-{propertyNo}",
                OccupierName: "स्वतः",
                PropertyDescription: "आरसीसी पक्के घर",
                Owners: [new(citizenId, 100m, true)],
                Floors: [new(0, "तळमजला", "RCC_PUCCA", "RESIDENTIAL", 15m, 15m, null, 2022)],
                CalculateTax: true)));

        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PropertyDto>())!;
    }

    [Fact]
    public async Task Namuna8ExtractPdf_SingleAndWardBatch_WithProtectedSignatureAssets_GeneratesValidPdf_AndAudits()
    {
        (Guid tenantId, Guid gramsevakId) = await SeedPrintableTenantAsync(includeSignatures: true);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage citResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(
                CitizenCode: "CIT-PRT-1",
                WardNo: "1",
                FullNameMr: "रमेश गजानन पाटील",
                FullNameEn: "Ramesh Gajanan Patil",
                Gender: "Male",
                MobileNo: "9822334455",
                AadhaarNumber: "987654321012",
                DpdpConsentAccepted: true)));
        CitizenDto citizen = (await citResp.Content.ReadFromJsonAsync<CitizenDto>())!;

        PropertyDto prop1 = await CreateAssessedPropertyAsync(client, tenantId, gramsevakId, "1", "101", citizen.Id);
        PropertyDto prop2 = await CreateAssessedPropertyAsync(client, tenantId, gramsevakId, "1", "102", citizen.Id);

        // 1. Download single property 29-col QuestPDF extract
        HttpResponseMessage singlePdfResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{prop1.Id}/namuna8-extract.pdf",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));

        Assert.Equal(HttpStatusCode.OK, singlePdfResp.StatusCode);
        Assert.Equal("application/pdf", singlePdfResp.Content.Headers.ContentType?.MediaType);
        Assert.Contains("inline", singlePdfResp.Content.Headers.ContentDisposition?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

        byte[] singlePdfBytes = await singlePdfResp.Content.ReadAsByteArrayAsync();
        Assert.True(singlePdfBytes.Length > 5_000, $"Expected > 5KB PDF, got {singlePdfBytes.Length}");
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(singlePdfBytes, 0, 5));

        // 2. Download Ward batch 29-col QuestPDF extract
        HttpResponseMessage batchPdfResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/namuna8-extract/batch.pdf?wardNo=1&propertyIds={prop1.Id},{prop2.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));

        Assert.Equal(HttpStatusCode.OK, batchPdfResp.StatusCode);
        byte[] batchPdfBytes = await batchPdfResp.Content.ReadAsByteArrayAsync();
        Assert.True(batchPdfBytes.Length > 5_000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(batchPdfBytes, 0, 5));

        // 3. JSON Preview verifies SVG QR, UPI URI & zero plaintext Aadhaar leak
        HttpResponseMessage previewResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/properties/namuna8-extract/preview?wardNo=1",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);

        string previewJson = await previewResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("987654321012", previewJson, StringComparison.Ordinal);
        Assert.DoesNotContain("9822334455", previewJson, StringComparison.Ordinal);

        List<Namuna8ExtractPreviewClientDto>? previews = JsonSerializer.Deserialize<List<Namuna8ExtractPreviewClientDto>>(
            previewJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(previews);
        Assert.Equal(2, previews.Count);
        Assert.All(previews, item =>
        {
            Assert.Contains("<svg", item.VerifyQrSvg, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("upi://pay?pa=gpchincholi%40sbi", item.UpiPayUri ?? "", StringComparison.Ordinal);
            Assert.Equal("XXXX-XXXX-1012", item.PrimaryOwnerAadhaarMasked);
        });

        // 4. AuditLog recorded ExtractPrinted entries
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        int printAuditCount = await db.AuditLogs
            .AsNoTracking()
            .CountAsync(l => l.TenantId == tenantId && l.Action == "ExtractPrinted");
        Assert.True(printAuditCount >= 2);
    }

    [Fact]
    public async Task DigitalNameplates_BulkAndSingle_RenderValidPdf_WithVerifyAndUpiQrs()
    {
        (Guid tenantId, Guid gramsevakId) = await SeedPrintableTenantAsync(includeSignatures: false);
        using HttpClient client = factory.CreateClient();

        CitizenDto citizen = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(null, "2", "सुनिता देशमुख", "Sunita Deshmukh", "Female", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        PropertyDto prop = await CreateAssessedPropertyAsync(client, tenantId, gramsevakId, "2", "201", citizen.Id);

        HttpResponseMessage bulkPdfResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/properties/nameplates.pdf?wardNo=2&layout=bulk",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, bulkPdfResp.StatusCode);
        byte[] bulkBytes = await bulkPdfResp.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bulkBytes, 0, 5));
        Assert.True(bulkBytes.Length > 4_000);

        HttpResponseMessage singlePdfResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/nameplates.pdf?propertyId={prop.Id}&layout=single",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, singlePdfResp.StatusCode);
        byte[] singleBytes = await singlePdfResp.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(singleBytes, 0, 5));
        Assert.True(singleBytes.Length > 4_000);

        HttpResponseMessage previewResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/nameplates/preview?propertyId={prop.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        List<DigitalNameplateItemData>? items = await previewResp.Content.ReadFromJsonAsync<List<DigitalNameplateItemData>>();
        Assert.NotNull(items);
        Assert.Single(items);
        Assert.Contains("<svg", items[0].VerifyQrSvg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<svg", items[0].UpiQrSvg ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upi://pay?pa=gpchincholi%40sbi", items[0].UpiPayUri ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicVerifyExtract_ReturnsDpdpMinimizedPayload_RejectsTamperedTokens_AndIsolatesCrossTenantPrints()
    {
        (Guid tenantA, Guid gramsevakA) = await SeedPrintableTenantAsync(includeSignatures: false);
        (Guid tenantB, Guid gramsevakB) = await SeedPrintableTenantAsync(includeSignatures: false);
        using HttpClient client = factory.CreateClient();

        CitizenDto citizenA = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA,
            new CreateCitizenCommand(null, "4", "अजय भोसले", "Ajay Bhosale", "Male", "9911223344", "555566667777", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        PropertyDto propA = await CreateAssessedPropertyAsync(client, tenantA, gramsevakA, "4", "401", citizenA.Id);

        // Extract signed verification token via preview
        List<Namuna8ExtractPreviewClientDto> previews = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/namuna8-extract/preview?propertyId={propA.Id}",
            gramsevakA,
            ApplicationRole.Gramsevak,
            tenantA)))
            .Content.ReadFromJsonAsync<List<Namuna8ExtractPreviewClientDto>>())!;
        string token = previews[0].VerifyToken;

        // 1. Truly anonymous request (no Bearer, no X-Tenant-ID) verifies cleanly without exposing Aadhaar or mobile
        HttpResponseMessage publicResp = await client.GetAsync($"/api/v1/public/verify/extract/{Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, publicResp.StatusCode);

        string publicJson = await publicResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("555566667777", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("7777", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("9911223344", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("3344", publicJson, StringComparison.Ordinal);

        VerifiedExtractPublicDto? verified = JsonSerializer.Deserialize<VerifiedExtractPublicDto>(
            publicJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(verified);
        Assert.True(verified.IsValid);
        Assert.Equal("4", verified.WardNo);
        Assert.Equal("401", verified.PropertyNo);
        Assert.Equal("अजय भोसले", verified.PrimaryOwnerNameMr);
        Assert.True(verified.TotalAnnualTax > 0m);

        // 2. Tampered signature must fail with 400 Bad Request
        char lastChar = token[^1];
        char flipped = lastChar == 'A' ? 'B' : 'A';
        string tampered = $"{token[..^1]}{flipped}";
        HttpResponseMessage tamperedResp = await client.GetAsync($"/api/v1/public/verify/extract/{Uri.EscapeDataString(tampered)}");
        Assert.Equal(HttpStatusCode.BadRequest, tamperedResp.StatusCode);

        // 3. Cross-tenant print download attempt must return 404 NotFound
        HttpResponseMessage crossPrint = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{propA.Id}/namuna8-extract.pdf",
            gramsevakB,
            ApplicationRole.Gramsevak,
            tenantB));
        Assert.Equal(HttpStatusCode.NotFound, crossPrint.StatusCode);
    }

    private sealed record Namuna8ExtractPreviewClientDto(
        string VerifyToken,
        string VerifyQrSvg,
        string? PrimaryOwnerAadhaarMasked,
        string? UpiPayUri);
}

