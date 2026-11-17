using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auth;
using GPMH.Application.DTOs.Citizens;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.Onboarding;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Application.Features.Citizens.Commands;
using GPMH.Application.Features.Onboarding.Subscription;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using GPMH.WebAPI.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.SuperAdmin;

public sealed class SuperAdminPlatformAndBillingTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static int _seq = 940000;

    private static readonly byte[] MinimalValidPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89,
    ];

    private async Task<(string SuperAdminUser, string SupportAdminUser, string AgentCode)> SeedPlatformActorsAsync()
    {
        string suffix = Interlocked.Increment(ref _seq).ToString();
        string saUsername = $"sa.plat.{suffix}";
        string supUsername = $"sup.plat.{suffix}";
        string agentUsername = $"agt.ref.{suffix}";
        string agentCode = $"AGT{suffix}";
        const string pass = "Platform@2026!";

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IPasswordHasherService hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        IPiiProtectionService pii = scope.ServiceProvider.GetRequiredService<IPiiProtectionService>();

        User superAdmin = new()
        {
            Username = saUsername,
            FullNameEn = "State SuperAdmin",
            FullNameMr = "सुपरॲडमिन",
            PasswordHash = hasher.HashPassword(pass),
            IsSuperAdmin = true,
            IsActive = true,
        };

        User supportAdmin = new()
        {
            Username = supUsername,
            FullNameEn = "State Support Engineer",
            FullNameMr = "तांत्रिक सहाय्य अधिकारी",
            PasswordHash = hasher.HashPassword(pass),
            IsSupportAdmin = true,
            IsActive = true,
        };

        User agentUser = new()
        {
            Username = agentUsername,
            FullNameEn = "Approved Onboarding Agent",
            FullNameMr = "मंजूर प्रतिनिधी",
            PasswordHash = hasher.HashPassword(pass),
            IsActive = true,
        };

        FieldAgent agent = new()
        {
            UserId = agentUser.Id,
            AgentCode = agentCode,
            KycStatus = AgentKycStatus.Approved,
            FullName = "Approved Onboarding Agent",
            MobileNo = $"99{suffix}12"[..10],
            Email = $"{agentUsername}@gpmh.in",
            IdProofType = "AADHAAR_PAN",
            PanMasked = "ABCDE1234F",
            AadhaarMasked = "XXXX-XXXX-8899",
            AadhaarBlindIndex = pii.ComputeBlindIndex($"111122228899-{suffix}"),
            PanBlindIndex = pii.ComputeBlindIndex($"ABCDE{suffix}F"[..10]),
            Address = "Station Road",
            VillageOrCity = "Amravati",
            District = "Amravati",
            TalukasCsv = "Amravati,Warud",
            ApprovedNewGpRatePct = 10m,
            ApprovedRenewalRatePct = 5m,
            ApprovedAtUtc = DateTimeOffset.UtcNow,
        };

        db.Users.AddRange(superAdmin, supportAdmin, agentUser);
        db.FieldAgents.Add(agent);
        await db.SaveChangesAsync();

        return (saUsername, supUsername, agentCode);
    }

    private static HttpRequestMessage AuthReq(
        HttpMethod method,
        string url,
        string bearerToken,
        object? jsonBody = null,
        Guid? tenantId = null)
    {
        HttpRequestMessage req = new(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        if (tenantId.HasValue)
        {
            req.Headers.Add("X-Tenant-ID", tenantId.Value.ToString("D"));
        }
        if (jsonBody is not null)
        {
            req.Content = JsonContent.Create(jsonBody);
        }
        return req;
    }

    [Fact]
    public async Task EndToEnd_StatewideKpis_SupportImpersonation_SubscriptionApproval_LicenseEnforcement_And_TenantLifecycle()
    {
        const string pass = "Platform@2026!";
        (string saUser, string supUser, string agentCode) = await SeedPlatformActorsAsync();
        using HttpClient client = factory.CreateClient();

        AuthResponseDto saAuth = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = saUser, password = pass })).Content.ReadFromJsonAsync<AuthResponseDto>())!;
        AuthResponseDto supAuth = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = supUser, password = pass })).Content.ReadFromJsonAsync<AuthResponseDto>())!;
        Assert.Equal(ApplicationRole.Support_Admin, supAuth.User.Role);

        // 1. Provision GP Alpha directly + add 1 property + publish demand + collect Namuna 10 receipt
        string lgdAlpha = $"71{Random.Shared.Next(1000, 9999)}";
        string alphaAdmin = $"gs.alpha.{lgdAlpha}";
        HttpResponseMessage provResp = await client.SendAsync(AuthReq(HttpMethod.Post, "/api/v1/superadmin/tenants/provision", saAuth.AccessToken, new
        {
            lgdCode = lgdAlpha,
            panchayatNameEn = "Alpha SuperAdmin GP",
            panchayatNameMr = "अल्फा ग्रा.पं.",
            districtCode = "496",
            districtName = "Amravati",
            tehsilCode = "4101",
            tehsilName = "Warud",
            adminFullName = "Alpha Gramsevak",
            adminUsername = alphaAdmin,
            adminEmail = $"{alphaAdmin}@gov.in",
            adminPassword = pass,
            paymentAmount = 6000m,
            durationDays = 365,
            paymentReference = $"DIRECT-{lgdAlpha}",
        }));
        Assert.Equal(HttpStatusCode.Created, provResp.StatusCode);
        ProvisionedTenantResultDto alphaProv = (await provResp.Content.ReadFromJsonAsync<ProvisionedTenantResultDto>())!;

        AuthResponseDto alphaAuth = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = alphaAdmin, password = pass })).Content.ReadFromJsonAsync<AuthResponseDto>())!;

        CitizenDto alphaCit = (await (await client.SendAsync(AuthReq(HttpMethod.Post, "/api/v1/citizens", alphaAuth.AccessToken,
            new CreateCitizenCommand($"CIT-{lgdAlpha}", "1", "अजय देशमुख", "Ajay Deshmukh", "Male", "9823011223", "777788889999", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;
        PropertyDto alphaProp = (await (await client.SendAsync(AuthReq(HttpMethod.Post, "/api/v1/properties", alphaAuth.AccessToken,
            new CreatePropertyCommand(
                WardNo: "1",
                PropertyNo: "10",
                BuiltUpAreaSqFt: 225m,
                OpenPlotAreaSqFt: 0m,
                BoundaryEast: "E",
                BoundaryWest: "W",
                BoundaryNorth: "N",
                BoundarySouth: "S",
                WaterConnectionCode: "NONE",
                IsTaxExempt: false,
                Owners: [new PropertyOwnerRequestItem(alphaCit.Id, 100m, true)],
                Floors: [new PropertyFloorRequestItem(0, "GF", "HUT_MUD", "RESIDENTIAL", 15m, 15m, null, 2021)],
                CalculateTax: true))))
            .Content.ReadFromJsonAsync<PropertyDto>())!;

        // 2. Register GP Beta with activateTenant=false + ReferralCode -> starts PendingApproval!
        string lgdBeta = $"72{Random.Shared.Next(1000, 9999)}";
        string betaAdmin = $"gs.beta.{lgdBeta}";
        HttpResponseMessage betaReg = await client.PostAsJsonAsync("/api/v1/onboarding/register-gp", new
        {
            adminUsername = betaAdmin,
            adminPassword = pass,
            adminEmail = $"{betaAdmin}@gov.in",
            referralCode = agentCode,
            wizard = new
            {
                lgdCode = lgdBeta,
                districtCode = "496",
                districtName = "Amravati",
                tehsilCode = "4102",
                tehsilName = "Morshi",
                panchayatNameEn = "Beta Pending GP",
                panchayatNameMr = "बीटा ग्रा.पं.",
                totalWards = 3,
                assessmentYearLabel = "2026-27",
                assessmentStartYear = 2026,
                assessmentEndYear = 2027,
                lockAssessmentYear = false,
                initializeArrearsRollover = false,
                bankAccounts = Array.Empty<object>(),
                seedDefaultFormulas = true,
                activateTenant = false,
            },
        });
        Assert.Equal(HttpStatusCode.Created, betaReg.StatusCode);
        OnboardingStatusDto betaCreated = (await betaReg.Content.ReadFromJsonAsync<OnboardingStatusDto>())!;
        Assert.Equal("PendingApproval", betaCreated.TenantStatus);

        AuthResponseDto betaAuth = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = betaAdmin, password = pass })).Content.ReadFromJsonAsync<AuthResponseDto>())!;

        // 3. TenantLicenseMiddleware blocks operational write on PendingApproval GP Beta (402 TENANT_LICENSE_INACTIVE)
        HttpResponseMessage blockedPropRes = await client.SendAsync(AuthReq(HttpMethod.Post, "/api/v1/citizens", betaAuth.AccessToken,
            new CreateCitizenCommand($"CIT-{lgdBeta}", "1", "रवी काळे", "Ravi Kale", "Male", "9823011444", "666655554444", DpdpConsentAccepted: true)));
        Assert.Equal(HttpStatusCode.PaymentRequired, blockedPropRes.StatusCode);
        JsonElement blockedJson = await blockedPropRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENANT_LICENSE_INACTIVE", blockedJson.GetProperty("errorCode").GetString());

        // GP Beta CAN read subscription-status & upload payment slip & submit UTR
        GpSubscriptionStatusDto betaSubStatus = (await (await client.SendAsync(
            AuthReq(HttpMethod.Get, "/api/v1/onboarding/subscription-status", betaAuth.AccessToken)))
            .Content.ReadFromJsonAsync<GpSubscriptionStatusDto>())!;
        Assert.Equal(6000m, betaSubStatus.PlatformPaymentAccount.AnnualFee);
        Assert.Equal("epanchayatraj@sbi", betaSubStatus.PlatformPaymentAccount.UpiId);

        using MultipartFormDataContent slipForm = new();
        slipForm.Add(new StringContent("PAYMENT_SLIP"), "category");
        ByteArrayContent pngPart = new(MinimalValidPng);
        pngPart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        slipForm.Add(pngPart, "file", "utr_slip.png");
        HttpResponseMessage slipUploadResp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/onboarding/upload-asset")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", betaAuth.AccessToken) },
            Content = slipForm,
        });
        Assert.Equal(HttpStatusCode.Created, slipUploadResp.StatusCode);
        UploadedAssetDto uploadedSlip = (await slipUploadResp.Content.ReadFromJsonAsync<UploadedAssetDto>())!;

        string utr1 = $"UTR-{lgdBeta}-6000";
        HttpResponseMessage subRes = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/onboarding/subscription-payments",
            betaAuth.AccessToken,
            new SubmitTenantPaymentCommand("Standard", "UPI", 6000m, utr1, DateOnly.FromDateTime(DateTime.UtcNow), uploadedSlip.AssetKey, "First payment")));
        Assert.Equal(HttpStatusCode.Created, subRes.StatusCode);
        TenantPaymentSubmissionDto submitted = (await subRes.Content.ReadFromJsonAsync<TenantPaymentSubmissionDto>())!;
        Assert.Equal(TenantPaymentSubmission.StatusPending, submitted.Status);

        // Duplicate UTR blocked with 409 Conflict
        HttpResponseMessage dupRes = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/onboarding/subscription-payments",
            betaAuth.AccessToken,
            new SubmitTenantPaymentCommand("Standard", "UPI", 6000m, utr1, DateOnly.FromDateTime(DateTime.UtcNow))));
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);

        // 4. Support_Admin RBAC: allowed to read stats/tenants/payments + impersonate, DENIED from writes/approvals (403)
        HttpResponseMessage supStatsRes = await client.SendAsync(AuthReq(HttpMethod.Get, "/api/v1/superadmin/stats", supAuth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, supStatsRes.StatusCode);
        SuperAdminDashboardStatsDto stats = (await supStatsRes.Content.ReadFromJsonAsync<SuperAdminDashboardStatsDto>())!;
        Assert.True(stats.TotalVillages >= 2);
        Assert.True(stats.ActiveVillages >= 1);
        Assert.True(stats.PendingActivation >= 1);
        Assert.True(stats.PendingPaymentSubmissions >= 1);
        Assert.True(stats.TotalProperties >= 1);
        Assert.True(stats.TotalTaxDemandedStatewide >= alphaProp.TotalAnnualTax);
        Assert.True(stats.TotalPlatformRevenue >= 6000m);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthReq(
            HttpMethod.Post, $"/api/v1/superadmin/payments/{submitted.SubmissionId}/approve", supAuth.AccessToken, new { durationDays = 365 }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthReq(
            HttpMethod.Put, "/api/v1/superadmin/platform-settings", supAuth.AccessToken, new { }))).StatusCode);

        // Short impersonation reason rejected (400)
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(AuthReq(
            HttpMethod.Post, $"/api/v1/superadmin/tenants/{alphaProv.TenantId}/impersonate", supAuth.AccessToken,
            new StartImpersonationRequest("short")))).StatusCode);

        // Valid 15-min read-only impersonation token by Support_Admin targeting GP Alpha
        HttpResponseMessage impRes = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            $"/api/v1/superadmin/tenants/{alphaProv.TenantId}/impersonate",
            supAuth.AccessToken,
            new StartImpersonationRequest("Diagnosing Ward 1 property tax calculation and extract sync")));
        Assert.Equal(HttpStatusCode.OK, impRes.StatusCode);
        SupportImpersonationResultDto impDto = (await impRes.Content.ReadFromJsonAsync<SupportImpersonationResultDto>())!;
        Assert.True(impDto.ReadOnly);
        Assert.Equal(900, impDto.ExpiresInSeconds);

        JwtSecurityToken parsedImp = new JwtSecurityTokenHandler().ReadJwtToken(impDto.AccessToken);
        Assert.Equal("true", parsedImp.Claims.First(c => c.Type == "support_impersonation").Value);
        Assert.Equal(alphaProv.TenantId.ToString("D"), parsedImp.Claims.First(c => c.Type == "tenant_id").Value);

        // Impersonation token CAN read GP Alpha properties, CANNOT switch tenant, CANNOT write (403 IMPERSONATION_READ_ONLY)!
        HttpResponseMessage impGetProps = await client.SendAsync(AuthReq(HttpMethod.Get, "/api/v1/properties", impDto.AccessToken));
        Assert.Equal(HttpStatusCode.OK, impGetProps.StatusCode);
        List<PropertyDto>? impProps = await impGetProps.Content.ReadFromJsonAsync<List<PropertyDto>>();
        Assert.Single(impProps!);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            AuthReq(HttpMethod.Get, "/api/v1/properties", impDto.AccessToken, tenantId: betaCreated.TenantId))).StatusCode);

        HttpResponseMessage impBlockedWrite = await client.SendAsync(AuthReq(
            HttpMethod.Post, "/api/v1/properties", impDto.AccessToken, new { wardNo = "1" }));
        Assert.Equal(HttpStatusCode.Forbidden, impBlockedWrite.StatusCode);
        JsonElement impErrJson = await impBlockedWrite.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IMPERSONATION_READ_ONLY", impErrJson.GetProperty("errorCode").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(
            AuthReq(HttpMethod.Post, "/api/v1/superadmin/impersonate/exit", impDto.AccessToken))).StatusCode);

        // Regular GP Gramsevak cannot access impersonate/exit -> 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            AuthReq(HttpMethod.Post, "/api/v1/superadmin/impersonate/exit", alphaAuth.AccessToken))).StatusCode);

        // Reject workflow + allow clean resubmission with the same UTR after rejection
        string rejUtr = $"UTR-REJ-{lgdBeta}";
        TenantPaymentSubmissionDto badSub = (await (await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/onboarding/subscription-payments",
            betaAuth.AccessToken,
            new SubmitTenantPaymentCommand("Standard", "NEFT", 6000m, rejUtr, DateOnly.FromDateTime(DateTime.UtcNow)))))
            .Content.ReadFromJsonAsync<TenantPaymentSubmissionDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(AuthReq(
            HttpMethod.Post,
            $"/api/v1/superadmin/payments/{badSub.SubmissionId}/reject",
            saAuth.AccessToken,
            new RejectTenantPaymentRequest("Blurry slip screenshot; please re-upload")))).StatusCode);

        HttpResponseMessage resubAfterReject = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/onboarding/subscription-payments",
            betaAuth.AccessToken,
            new SubmitTenantPaymentCommand("Standard", "NEFT", 6000m, rejUtr, DateOnly.FromDateTime(DateTime.UtcNow))));
        Assert.Equal(HttpStatusCode.Created, resubAfterReject.StatusCode);

        // 5. SuperAdmin approves GP Beta UTR submission -> Activates tenant (+365d), emits INV GST PDF, unlocks writes!
        HttpResponseMessage appRes = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            $"/api/v1/superadmin/payments/{submitted.SubmissionId}/approve",
            saAuth.AccessToken,
            new ApproveTenantPaymentRequest(365, "Verified SBI UTR")));
        Assert.Equal(HttpStatusCode.OK, appRes.StatusCode);
        TenantPaymentSubmissionDto approved = (await appRes.Content.ReadFromJsonAsync<TenantPaymentSubmissionDto>())!;
        Assert.Equal(TenantPaymentSubmission.StatusApproved, approved.Status);
        Assert.StartsWith("INV-", approved.InvoiceNo!, StringComparison.Ordinal);

        // GST Invoice PDF works for BOTH SuperAdmin and owning GP Beta!
        HttpResponseMessage saInvPdf = await client.SendAsync(AuthReq(
            HttpMethod.Get, $"/api/v1/superadmin/payments/{submitted.SubmissionId}/invoice.pdf", saAuth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, saInvPdf.StatusCode);
        byte[] invBytes = await saInvPdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(invBytes, 0, 5));
        Assert.True(invBytes.Length > 5000);

        HttpResponseMessage gpInvPdf = await client.SendAsync(AuthReq(
            HttpMethod.Get, $"/api/v1/onboarding/subscription-payments/{submitted.SubmissionId}/invoice.pdf", betaAuth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, gpInvPdf.StatusCode);

        // GP Beta operational write now succeeds!
        HttpResponseMessage betaCitNow = await client.SendAsync(AuthReq(HttpMethod.Post, "/api/v1/citizens", betaAuth.AccessToken,
            new CreateCitizenCommand($"CIT-{lgdBeta}-OK", "1", "रवी काळे", "Ravi Kale", "Male", "9823011444", "666655554444", DpdpConsentAccepted: true)));
        Assert.Equal(HttpStatusCode.Created, betaCitNow.StatusCode);

        // 6. Grant 14-day Trial on Alpha -> Dashboard 30-day expiry banner activates! Then Suspend -> 403 TENANT_LICENSE_INACTIVE!
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(AuthReq(
            HttpMethod.Post,
            $"/api/v1/superadmin/tenants/{alphaProv.TenantId}/status",
            saAuth.AccessToken,
            new UpdateTenantStatusRequest("GrantTrial", TrialDays: 14, Reason: "Short extension")))).StatusCode);

        DashboardSummaryDto alphaDash = (await (await client.SendAsync(
            AuthReq(HttpMethod.Get, "/api/v1/dashboard/summary", alphaAuth.AccessToken)))
            .Content.ReadFromJsonAsync<DashboardSummaryDto>())!;
        Assert.True(alphaDash.ShowExpiryWarning30Days);
        Assert.InRange(alphaDash.DaysUntilExpiry!.Value, 13, 15);
        Assert.Equal("epanchayatraj@sbi", alphaDash.PlatformPaymentInfo?.UpiId);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(AuthReq(
            HttpMethod.Post,
            $"/api/v1/superadmin/tenants/{alphaProv.TenantId}/status",
            saAuth.AccessToken,
            new UpdateTenantStatusRequest("Suspend", Reason: "Test suspension")))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthReq(
            HttpMethod.Post, "/api/v1/citizens", alphaAuth.AccessToken,
            new CreateCitizenCommand($"CIT-{lgdAlpha}-BLK", "1", "बंद", "Blocked", "Male", "9823000999", null, DpdpConsentAccepted: true)))).StatusCode);

        // 7. Reset Admin Password, Tenant 360 Detail, Platform Settings, Soft Delete -> Restore -> Permanent Delete
        const string newPass = "NewPass@2026!";
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(AuthReq(
            HttpMethod.Post,
            $"/api/v1/superadmin/tenants/{betaCreated.TenantId}/reset-admin-password",
            saAuth.AccessToken,
            new ResetTenantAdminPasswordRequest(newPass)))).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = betaAdmin, password = pass })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = betaAdmin, password = newPass })).StatusCode);

        HttpResponseMessage detailRes = await client.SendAsync(AuthReq(
            HttpMethod.Get, $"/api/v1/superadmin/tenants/{betaCreated.TenantId}", saAuth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        SuperAdminTenantDetailDto detail = (await detailRes.Content.ReadFromJsonAsync<SuperAdminTenantDetailDto>())!;
        Assert.Equal(agentCode, detail.Summary.ReferralCodeUsed);
        Assert.NotEmpty(detail.Users);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(AuthReq(
            HttpMethod.Put, "/api/v1/superadmin/platform-settings", saAuth.AccessToken,
            new
            {
                accountName = "Maha e-Panchayat Updated",
                bankName = "State Bank of India",
                branchName = "Mumbai",
                accountNumber = "39876543210",
                ifscCode = "SBIN0001234",
                upiId = "updatedgp@sbi",
                annualFee = 6500m,
                gstPercent = 18m,
                gstin = "27AABCM1234F1Z5",
                supportPhone = "0721-2550101",
                supportEmail = "support@epanchayat-mh.gov.in",
            }))).StatusCode);

        // Soft Delete Beta -> hidden by default, shown with includeDeleted=true -> Restore -> Soft Delete -> Permanent Delete
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(
            AuthReq(HttpMethod.Delete, $"/api/v1/superadmin/tenants/{betaCreated.TenantId}", saAuth.AccessToken))).StatusCode);

        List<SuperAdminTenantSummaryDto> liveList = (await (await client.SendAsync(
            AuthReq(HttpMethod.Get, "/api/v1/superadmin/tenants", saAuth.AccessToken))).Content.ReadFromJsonAsync<List<SuperAdminTenantSummaryDto>>())!;
        Assert.DoesNotContain(liveList, t => t.TenantId == betaCreated.TenantId);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            AuthReq(HttpMethod.Post, $"/api/v1/superadmin/tenants/{betaCreated.TenantId}/restore", saAuth.AccessToken))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(
            AuthReq(HttpMethod.Delete, $"/api/v1/superadmin/tenants/{betaCreated.TenantId}", saAuth.AccessToken))).StatusCode);

        // Wrong LGD rejected; matching LGD purges permanently
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(AuthReq(
            HttpMethod.Delete, $"/api/v1/superadmin/tenants/{betaCreated.TenantId}/permanent?confirmLgdCode=000000", saAuth.AccessToken))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(AuthReq(
            HttpMethod.Delete, $"/api/v1/superadmin/tenants/{betaCreated.TenantId}/permanent?confirmLgdCode={lgdBeta}", saAuth.AccessToken))).StatusCode);

        // Audit log filter + CSV export verify SupportImpersonationStarted + SubscriptionPaymentApproved + TenantPermanentlyDeleted
        HttpResponseMessage csvRes = await client.SendAsync(AuthReq(
            HttpMethod.Get, "/api/v1/superadmin/audit-logs/export.csv", saAuth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, csvRes.StatusCode);
        string csvText = await csvRes.Content.ReadAsStringAsync();
        Assert.Contains("SupportImpersonationStarted", csvText);
        Assert.Contains("SubscriptionPaymentApproved", csvText);
        Assert.Contains("TenantPermanentlyDeleted", csvText);
    }
}

