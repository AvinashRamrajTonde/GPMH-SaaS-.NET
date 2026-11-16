using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auth;
using GPMH.Application.DTOs.Onboarding;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Onboarding;

public sealed class OnboardingWizardTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static readonly byte[] MinimalValidPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89,
    ];

    [Fact]
    public async Task CompleteOnboarding_StoresProvenanceAssetsBankAccountsAndSeedsFormulas()
    {
        Guid tenantId = Guid.NewGuid();
        Guid otherTenantId = Guid.NewGuid();
        string lgdCode = $"21{Random.Shared.Next(1000, 9999)}";
        string gsUsername = $"onb.gs.{Guid.NewGuid():N}"[..24];
        string otherUsername = $"onb.oth.{Guid.NewGuid():N}"[..24];
        const string pass = "Onboard@2026!";

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            IPasswordHasherService hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

            db.Tenants.AddRange(
                new Tenant
                {
                    Id = tenantId,
                    TenantCode = $"gp-{lgdCode}",
                    LgdCode = lgdCode,
                    NameEn = "Sonimoha GP",
                    NameMr = "सोनीमोहा ग्रा.पं.",
                    Status = TenantStatus.PendingApproval,
                },
                new Tenant
                {
                    Id = otherTenantId,
                    TenantCode = $"gp-other-{otherTenantId:N}"[..22],
                    LgdCode = $"22{Random.Shared.Next(1000, 9999)}",
                    NameEn = "Other GP",
                    NameMr = "इतर ग्रा.पं.",
                    Status = TenantStatus.Active,
                });

            User gsUser = new()
            {
                Username = gsUsername,
                FullNameEn = "Rajesh Deshmukh",
                FullNameMr = "राजेश देशमुख",
                PasswordHash = hasher.HashPassword(pass),
            };

            User otherUser = new()
            {
                Username = otherUsername,
                FullNameEn = "Other Clerk",
                FullNameMr = "इतर लिपिक",
                PasswordHash = hasher.HashPassword(pass),
            };

            db.Users.AddRange(gsUser, otherUser);
            db.TenantMemberships.AddRange(
                new TenantMembership { UserId = gsUser.Id, TenantId = tenantId, Role = ApplicationRole.Gramsevak, IsDefault = true },
                new TenantMembership { UserId = otherUser.Id, TenantId = otherTenantId, Role = ApplicationRole.Gramsevak, IsDefault = true });

            await db.SaveChangesAsync();
        }

        HttpClient client = factory.CreateClient();
        string jwt = await LoginAsync(client, gsUsername, pass, tenantId);
        string otherJwt = await LoginAsync(client, otherUsername, pass, otherTenantId);

        // 1. Upload private signature asset
        using MultipartFormDataContent form = new();
        form.Add(new StringContent("SIGNATURE"), "category");
        ByteArrayContent pngContent = new(MinimalValidPng);
        pngContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(pngContent, "file", "gramsevak_sign.png");

        using HttpRequestMessage uploadReq = new(HttpMethod.Post, "/api/v1/onboarding/upload-asset") { Content = form };
        uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        HttpResponseMessage uploadResp = await client.SendAsync(uploadReq);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        UploadedAssetDto uploaded = (await uploadResp.Content.ReadFromJsonAsync<UploadedAssetDto>())!;
        Assert.StartsWith("gp-", uploaded.AssetKey, StringComparison.Ordinal);

        // Cross-tenant download denied (403); owner tenant succeeds (200)
        using HttpRequestMessage crossDownload = new(HttpMethod.Get, $"/api/v1/onboarding/assets/{uploaded.AssetKey}");
        crossDownload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherJwt);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(crossDownload)).StatusCode);

        using HttpRequestMessage ownerDownload = new(HttpMethod.Get, $"/api/v1/onboarding/assets/{uploaded.AssetKey}");
        ownerDownload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        HttpResponseMessage ownerDownloadResp = await client.SendAsync(ownerDownload);
        Assert.Equal(HttpStatusCode.OK, ownerDownloadResp.StatusCode);
        Assert.Equal(MinimalValidPng, await ownerDownloadResp.Content.ReadAsByteArrayAsync());

        // 2. Complete Steps 1-5 onboarding wizard with provenance & 3 statutory banks
        using HttpRequestMessage completeReq = new(HttpMethod.Post, "/api/v1/onboarding/complete")
        {
            Content = JsonContent.Create(new
            {
                lgdCode,
                districtCode = "497",
                districtName = "Beed",
                tehsilCode = "4192",
                tehsilName = "Dharur",
                panchayatNameEn = "Sonimoha Gram Panchayat",
                panchayatNameMr = "सोनीमोहा ग्रामपंचायत",
                pincode = "431125",
                censusCode = "559812",
                officeAddress = "Near ZP School, Sonimoha",
                totalWards = 6,
                populationCensus = 3150,
                sarpanchName = "सौ. सुनीता पाटील",
                sarpanchMobile = "9822334411",
                upSarpanchName = "श्री. विकास शिंदे",
                gramsevakName = "राजेश देशमुख",
                gramsevakMobile = "9422881122",
                operatorName = "अमित वानखडे",
                operatorMobile = "9766554433",
                gramsevakSignatureKey = uploaded.AssetKey,
                assessmentYearLabel = "2026-27",
                assessmentStartYear = 2026,
                assessmentEndYear = 2027,
                lockAssessmentYear = true,
                initializeArrearsRollover = true,
                primaryUpiId = "sonimohagp@sbi",
                upiPayeeName = "Gram Panchayat Sonimoha",
                bankAccounts = new object[]
                {
                    new { fundType = "GRAM_NIDHI", bankName = "State Bank of India", branchName = "Dharur", accountNumber = "30112233445", ifscCode = "SBIN0001234", upiId = (string?)"sonimohagp@sbi", upiPayeeName = (string?)"GP Sonimoha", openingBalance = 50000m },
                    new { fundType = "XV_FINANCE", bankName = "Bank of Maharashtra", branchName = "Dharur", accountNumber = "60112233445", ifscCode = "MAHB0000412", upiId = (string?)null, upiPayeeName = (string?)null, openingBalance = 125000m },
                    new { fundType = "WATER_SUPPLY", bankName = "District Central Co-op", branchName = "Beed", accountNumber = "90112233445", ifscCode = "MSCB0012005", upiId = (string?)"sonimohawater@sbi", upiPayeeName = (string?)"GP Water Fund", openingBalance = 18500m },
                },
                seedDefaultFormulas = true,
                activateTenant = true,
                provenance = new
                {
                    isAutofilledFromLgd = true,
                    isEditedOnOnboarding = true,
                    editedFields = new[] { "pincode", "gramsevak_mobile" },
                    originalLgdSnapshotJson = """{"lgd_code":"171391","pincode":"431124"}""",
                },
            }),
        };
        completeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        HttpResponseMessage completeResp = await client.SendAsync(completeReq);
        Assert.Equal(HttpStatusCode.OK, completeResp.StatusCode);

        OnboardingStatusDto status = (await completeResp.Content.ReadFromJsonAsync<OnboardingStatusDto>())!;
        Assert.Equal("Active", status.TenantStatus);
        Assert.Equal(100, status.SetupProgressPct);
        Assert.True(status.FormulaSeeded);
        Assert.True(status.AssessmentYear.IsLocked);
        Assert.Equal(3, status.BankAccounts.Count);
        Assert.NotNull(status.Provenance);
        Assert.True(status.Provenance.IsEditedOnOnboarding);
        Assert.Contains("pincode", status.Provenance.EditedFields);

        // 3. Verify seeded statutory masters directly inside tenant context
        using IServiceScope verifyScope = factory.Services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        AppDbContext verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(1, await verifyDb.FormulaSettings.CountAsync());
        Assert.Equal(12, await verifyDb.ConstructionTypeMasters.CountAsync());
        Assert.Equal(3, await verifyDb.UsageWeightages.CountAsync());
        Assert.Equal(9, await verifyDb.DepreciationSlabs.CountAsync());
        Assert.Equal(3, await verifyDb.AreaSlabTaxes.CountAsync());
        Assert.Equal(3, await verifyDb.WaterTaxMasters.CountAsync());
    }

    [Fact]
    public async Task RegisterSelfServiceGp_CreatesActiveTenantAndLoginWorksImmediately()
    {
        string lgdCode = $"28{Random.Shared.Next(1000, 9999)}";
        string username = $"self.gp.{Guid.NewGuid():N}"[..22];
        HttpClient client = factory.CreateClient();

        HttpResponseMessage regResp = await client.PostAsJsonAsync("/api/v1/onboarding/register-gp", new
        {
            adminUsername = username,
            adminPassword = "SelfGp@2026!",
            adminEmail = $"{username}@gp.in",
            wizard = new
            {
                lgdCode,
                districtCode = "496",
                districtName = "Amravati",
                tehsilCode = "4100",
                tehsilName = "Anjangaon Surji",
                panchayatNameEn = "Self Onboarding Test GP",
                panchayatNameMr = "स्वयं नोंदणी चाचणी ग्रा.पं.",
                pincode = "444705",
                totalWards = 4,
                sarpanchName = "सरपंच चाचणी",
                gramsevakName = "ग्रामसेवक चाचणी",
                gramsevakMobile = "9876501234",
                assessmentYearLabel = "2026-27",
                assessmentStartYear = 2026,
                assessmentEndYear = 2027,
                lockAssessmentYear = true,
                initializeArrearsRollover = true,
                primaryUpiId = "selfgp@sbi",
                bankAccounts = Array.Empty<object>(),
                seedDefaultFormulas = true,
                activateTenant = true,
            },
        });

        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);
        OnboardingStatusDto created = (await regResp.Content.ReadFromJsonAsync<OnboardingStatusDto>())!;
        Assert.Equal("Active", created.TenantStatus);

        string jwt = await LoginAsync(client, username, "SelfGp@2026!", created.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(jwt));
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password, Guid tenantId)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password, tenantId });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        AuthResponseDto dto = (await resp.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        return dto.AccessToken;
    }
}

