using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.Auth;
using GPMH.Application.DTOs.Onboarding;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Application.Features.Agents.Commands;
using GPMH.Application.Features.Onboarding.Commands;
using GPMH.Application.Features.Onboarding.Subscription;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Agents;

public sealed class AgentCommissionAndPayoutIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static readonly byte[] MinimalPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private async Task<(string SuperAdminUser, string SupportAdminUser, string Password)> SeedAdminsAsync()
    {
        string suffix = Guid.NewGuid().ToString("N")[..6];
        string saUser = $"sa.c28.{suffix}";
        string supUser = $"sup.c28.{suffix}";
        const string pass = "AdminAgents@2026!";

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IPasswordHasherService hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        db.Users.AddRange(
            new User
            {
                Username = saUser,
                FullNameEn = "SuperAdmin Commissions",
                FullNameMr = "सुपरॲडमिन",
                PasswordHash = hasher.HashPassword(pass),
                IsSuperAdmin = true,
                IsActive = true,
            },
            new User
            {
                Username = supUser,
                FullNameEn = "Support Admin Reviewer",
                FullNameMr = "सपोर्ट प्रशासक",
                PasswordHash = hasher.HashPassword(pass),
                IsSupportAdmin = true,
                IsActive = true,
            });

        await db.SaveChangesAsync();
        return (saUser, supUser, pass);
    }

    private static HttpRequestMessage AuthReq(HttpMethod method, string uri, string jwt, object? payload = null)
    {
        HttpRequestMessage req = new(method, uri);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        if (payload is not null)
        {
            req.Content = JsonContent.Create(payload);
        }
        return req;
    }

    private static string UniqueMobile() => $"8{Random.Shared.Next(100000000, 999999999)}";
    private static string UniqueAadhaar(string last4 = "1234") => $"77{Random.Shared.Next(100000, 999999)}{last4}";
    private static string UniquePan() =>
        $"A{(char)('A' + Random.Shared.Next(0, 26))}{(char)('A' + Random.Shared.Next(0, 26))}DE{Random.Shared.Next(1000, 9999)}F";

    [Fact]
    public async Task ReferralValidation_And_ProtectedAssetInspection_EnforcesKycApprovalAndSupportReadOnly()
    {
        (string saUser, string supUser, string pass) = await SeedAdminsAsync();
        HttpClient client = factory.CreateClient();

        // 1. Self-register Pending Agent with Aadhaar document -> KycStatus = PendingApproval
        string pendingMobile = UniqueMobile();
        string pendingAadhaar = UniqueAadhaar("4321");
        string pendingPan = UniquePan();
        const string agentPass = "AgentSecret@2026!";

        using MultipartFormDataContent form = new();
        form.Add(new StringContent("Pending Test Agent"), "fullName");
        form.Add(new StringContent(pendingMobile), "mobileNo");
        form.Add(new StringContent($"pending.{pendingMobile}@gpmh.in"), "email");
        form.Add(new StringContent(agentPass), "password");
        form.Add(new StringContent("Camp Road, Amravati"), "address");
        form.Add(new StringContent("Amravati"), "villageOrCity");
        form.Add(new StringContent("Amravati"), "district");
        form.Add(new StringContent("Amravati, Morshi"), "talukasCsv");
        form.Add(new StringContent(pendingAadhaar), "aadhaarNumber");
        form.Add(new StringContent(pendingPan), "panNumber");
        ByteArrayContent png = new(MinimalPng);
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(png, "aadhaarDocument", "aadhaar.png");

        HttpResponseMessage regRes = await client.PostAsync("/api/v1/agents/register", form);
        Assert.Equal(HttpStatusCode.Created, regRes.StatusCode);
        AgentRegistrationResultDto pendingAgent = (await regRes.Content.ReadFromJsonAsync<AgentRegistrationResultDto>())!;

        // 2. Public validate-referral rejects Pending agent cleanly (Valid = false, zero PII)
        ReferralValidationDto pendingRef = (await client.GetFromJsonAsync<ReferralValidationDto>(
            $"/api/v1/agents/validate-referral/{pendingAgent.AgentCode}"))!;
        Assert.False(pendingRef.Valid);
        Assert.Null(pendingRef.FullName);
        Assert.Null(pendingRef.District);

        // Self-service GP onboarding using pending agent referral code MUST fail 400
        string lgdBad = $"61{Random.Shared.Next(1000, 9999)}";
        HttpResponseMessage badRefGpRes = await client.PostAsJsonAsync("/api/v1/onboarding/register-gp", new
        {
            adminUsername = $"gs.bad.{lgdBad}",
            adminPassword = pass,
            adminEmail = $"bad.{lgdBad}@gov.in",
            referralCode = pendingAgent.AgentCode,
            wizard = new
            {
                lgdCode = lgdBad,
                districtCode = "496",
                districtName = "Amravati",
                tehsilCode = "4102",
                tehsilName = "Morshi",
                panchayatNameEn = "Invalid Ref GP",
                panchayatNameMr = "अवैध रेफरल",
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
        Assert.Equal(HttpStatusCode.BadRequest, badRefGpRes.StatusCode);

        // 3. SuperAdmin & Support_Admin login
        string saJwt = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = saUser, password = pass }))
            .Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;
        string supJwt = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = supUser, password = pass }))
            .Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;

        // Fetch KYC doc key via SuperAdmin directory + verify Support_Admin can inspect /api/v1/assets/{key}
        IReadOnlyList<SuperAdminAgentSummaryDto> dir = (await (await client.SendAsync(
            AuthReq(HttpMethod.Get, $"/api/v1/superadmin/agents?search={pendingAgent.AgentCode}", supJwt)))
            .Content.ReadFromJsonAsync<IReadOnlyList<SuperAdminAgentSummaryDto>>())!;
        SuperAdminAgentSummaryDto listed = Assert.Single(dir);
        Assert.NotNull(listed.AadhaarDocumentKey);
        Assert.Equal("XXXX-XXXX-4321", listed.AadhaarMasked);

        HttpResponseMessage supAssetRes = await client.SendAsync(AuthReq(
            HttpMethod.Get, $"/api/v1/assets/{listed.AadhaarDocumentKey}", supJwt));
        Assert.Equal(HttpStatusCode.OK, supAssetRes.StatusCode);

        // 4. Support_Admin cannot approve KYC (403); SuperAdmin approves via PUT /kyc
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthReq(
            HttpMethod.Put, $"/api/v1/superadmin/agents/{pendingAgent.AgentId}/kyc", supJwt,
            new { status = AgentKycStatus.Approved, approvedNewGpRatePct = 15m, approvedRenewalRatePct = 10m }))).StatusCode);

        HttpResponseMessage kycApproveRes = await client.SendAsync(AuthReq(
            HttpMethod.Put, $"/api/v1/superadmin/agents/{pendingAgent.AgentId}/kyc", saJwt,
            new { status = AgentKycStatus.Approved, approvedNewGpRatePct = 15m, approvedRenewalRatePct = 10m, reason = "Verified KYC" }));
        Assert.Equal(HttpStatusCode.OK, kycApproveRes.StatusCode);

        // 5. Public validate-referral now returns Valid = true with masked-free minimal info
        ReferralValidationDto approvedRef = (await client.GetFromJsonAsync<ReferralValidationDto>(
            $"/api/v1/agents/validate-referral/{pendingAgent.AgentCode}"))!;
        Assert.True(approvedRef.Valid);
        Assert.Equal("Pending Test Agent", approvedRef.FullName);
        Assert.Equal("Amravati", approvedRef.District);

        // 6. Approved agent logs in and directly registers a GP via POST /api/v1/agents/register-gp
        string agentJwt = (await (await client.PostAsJsonAsync("/api/v1/agents/login", new { identifier = pendingMobile, password = agentPass }))
            .Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;

        string lgdGood = $"62{Random.Shared.Next(1000, 9999)}";
        HttpResponseMessage agentGpRes = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/agents/register-gp",
            agentJwt,
            new
            {
                adminUsername = $"gs.ag.{lgdGood}",
                adminPassword = pass,
                adminEmail = $"ag.{lgdGood}@gov.in",
                wizard = new
                {
                    lgdCode = lgdGood,
                    districtCode = "496",
                    districtName = "Amravati",
                    tehsilCode = "4102",
                    tehsilName = "Morshi",
                    panchayatNameEn = "Agent Assisted GP",
                    panchayatNameMr = "प्रतिनिधी ग्रामपंचायत",
                    totalWards = 4,
                    assessmentYearLabel = "2026-27",
                    assessmentStartYear = 2026,
                    assessmentEndYear = 2027,
                    lockAssessmentYear = false,
                    initializeArrearsRollover = false,
                    bankAccounts = Array.Empty<object>(),
                    seedDefaultFormulas = true,
                    activateTenant = false,
                },
            }));
        Assert.Equal(HttpStatusCode.Created, agentGpRes.StatusCode);

        AgentVillagesResponseDto villages = (await (await client.SendAsync(AuthReq(HttpMethod.Get, "/api/v1/agents/villages", agentJwt)))
            .Content.ReadFromJsonAsync<AgentVillagesResponseDto>())!;
        Assert.Contains(villages.Villages, v => v.LgdCode == lgdGood);

        // 7. Direct SuperAdmin provisioning with ReferralCode automatically accrues NEW_ONBOARDING commission!
        string lgdDirect = $"64{Random.Shared.Next(1000, 9999)}";
        HttpResponseMessage directProvRes = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/superadmin/tenants/provision",
            saJwt,
            new
            {
                lgdCode = lgdDirect,
                panchayatNameEn = "Direct Referred GP",
                panchayatNameMr = "थेट रेफरल ग्रा.पं.",
                districtName = "Amravati",
                tehsilName = "Morshi",
                adminFullName = "Direct Gramsevak",
                adminUsername = $"gs.dir.{lgdDirect}",
                adminEmail = $"dir.{lgdDirect}@gov.in",
                adminPassword = pass,
                paymentAmount = 6000m,
                durationDays = 365,
                referralCode = pendingAgent.AgentCode,
            }));
        Assert.Equal(HttpStatusCode.Created, directProvRes.StatusCode);

        IReadOnlyList<AgentCommissionItemDto> directComms = (await (await client.SendAsync(
            AuthReq(HttpMethod.Get, "/api/v1/agents/commissions", agentJwt)))
            .Content.ReadFromJsonAsync<IReadOnlyList<AgentCommissionItemDto>>())!;
        Assert.Contains(directComms, c =>
            c.LgdCode == lgdDirect &&
            c.CommissionType == "NEW_ONBOARDING" &&
            c.RatePctSnapshot == 15m &&
            c.CommissionAmount == 900m);
    }

    [Fact]
    public async Task AutoCommissionAccrual_PartialAndFullSettlement_VoucherPdf_And_DpdpMasking_WorkEndToEnd()
    {
        (string saUser, _, string pass) = await SeedAdminsAsync();
        HttpClient client = factory.CreateClient();

        string saJwt = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = saUser, password = pass }))
            .Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;

        // 1. SuperAdmin creates Auto-Approved Agent with 15% new GP & 10% renewal commission rates
        string mobile = UniqueMobile();
        string rawAadhaar = UniqueAadhaar("1234");
        string rawPan = UniquePan();
        const string initialPass = "ManualAgent@2026!";

        HttpResponseMessage createAgentRes = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/superadmin/agents",
            saJwt,
            new CreateAgentBySuperAdminCommand(
                FullName: "Rameshwar Patil",
                MobileNo: mobile,
                Email: $"rameshwar.{mobile}@gpmh.in",
                Password: initialPass,
                Address: "Main Road, Warud",
                VillageOrCity: "Warud",
                District: "Amravati",
                TalukasCsv: "Warud,Morshi",
                IdProofType: "AADHAAR",
                AadhaarNumber: rawAadhaar,
                PanNumber: rawPan,
                BankName: "State Bank of India",
                BankBranch: "Warud",
                AccountNumber: "30998877665",
                IfscCode: "SBIN0001234",
                UpiId: "rameshwar@sbi",
                ApprovedNewGpRatePct: 15m,
                ApprovedRenewalRatePct: 10m,
                AutoApprove: true)));

        Assert.Equal(HttpStatusCode.Created, createAgentRes.StatusCode);
        string createRawJson = await createAgentRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain(rawAadhaar, createRawJson, StringComparison.Ordinal);
        SuperAdminAgentSummaryDto createdAgent = (await createAgentRes.Content.ReadFromJsonAsync<SuperAdminAgentSummaryDto>())!;
        Assert.Equal(AgentKycStatus.Approved, createdAgent.KycStatus);
        Assert.Equal("XXXX-XXXX-1234", createdAgent.AadhaarMasked);

        // Agent logs in -> verifies KPI dashboard initializes at zero + Aadhaar masked
        HttpResponseMessage loginRes = await client.PostAsJsonAsync("/api/v1/agents/login", new { identifier = mobile, password = initialPass });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        AuthResponseDto agentAuth = (await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        string agentJwt = agentAuth.AccessToken;

        string meRaw = await (await client.SendAsync(AuthReq(HttpMethod.Get, "/api/v1/agents/me", agentJwt))).Content.ReadAsStringAsync();
        Assert.DoesNotContain(rawAadhaar, meRaw, StringComparison.Ordinal);

        // 2. Register GP via ReferralCode -> Gramsevak pays ₹6,000 -> SuperAdmin approves -> ₹900 (15%) NEW_ONBOARDING commission accrues!
        string lgdGp = $"63{Random.Shared.Next(1000, 9999)}";
        string gsUsername = $"gs.c28.{lgdGp}";
        HttpResponseMessage regGp = await client.PostAsJsonAsync("/api/v1/onboarding/register-gp", new
        {
            adminUsername = gsUsername,
            adminPassword = pass,
            adminEmail = $"{gsUsername}@gov.in",
            referralCode = createdAgent.AgentCode,
            wizard = new
            {
                lgdCode = lgdGp,
                districtCode = "496",
                districtName = "Amravati",
                tehsilCode = "4103",
                tehsilName = "Warud",
                panchayatNameEn = "Warud Demo GP",
                panchayatNameMr = "वरुड ग्रामपंचायत",
                totalWards = 4,
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
        Assert.Equal(HttpStatusCode.Created, regGp.StatusCode);

        string gsJwt = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = gsUsername, password = pass }))
            .Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;

        // First payment: ₹6,000 -> SuperAdmin approves -> accrues 15% = ₹900 NEW_ONBOARDING
        TenantPaymentSubmissionDto firstSub = (await (await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/onboarding/subscription-payments",
            gsJwt,
            new SubmitTenantPaymentCommand("Standard", "UPI", 6000m, $"UTR-1-{lgdGp}", DateOnly.FromDateTime(DateTime.UtcNow)))))
            .Content.ReadFromJsonAsync<TenantPaymentSubmissionDto>())!;

        HttpResponseMessage approve1Res = await client.SendAsync(AuthReq(
            HttpMethod.Post, $"/api/v1/superadmin/payments/{firstSub.SubmissionId}/approve", saJwt,
            new { durationDays = 365, notes = "Initial activation" }));
        Assert.Equal(HttpStatusCode.OK, approve1Res.StatusCode);

        AgentDashboardDto dashAfterFirst = (await (await client.SendAsync(AuthReq(HttpMethod.Get, "/api/v1/agents/dashboard", agentJwt)))
            .Content.ReadFromJsonAsync<AgentDashboardDto>())!;
        Assert.Equal(1, dashAfterFirst.VillagesOnboarded);
        Assert.Equal(1, dashAfterFirst.ActiveGps);
        Assert.Equal(6000m, dashAfterFirst.TotalSalesValue);
        Assert.Equal(900m, dashAfterFirst.TotalCommissionEarned);
        Assert.Equal(900m, dashAfterFirst.PendingPayableCommission);
        Assert.Equal(0m, dashAfterFirst.PaidCommission);

        // Second payment (Renewal): ₹6,000 -> SuperAdmin approves -> accrues 10% = ₹600 RENEWAL!
        TenantPaymentSubmissionDto renewalSub = (await (await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/onboarding/subscription-payments",
            gsJwt,
            new SubmitTenantPaymentCommand("Renewal", "NEFT", 6000m, $"UTR-2-{lgdGp}", DateOnly.FromDateTime(DateTime.UtcNow)))))
            .Content.ReadFromJsonAsync<TenantPaymentSubmissionDto>())!;

        HttpResponseMessage approve2Res = await client.SendAsync(AuthReq(
            HttpMethod.Post, $"/api/v1/superadmin/payments/{renewalSub.SubmissionId}/approve", saJwt,
            new { durationDays = 365, notes = "Annual renewal" }));
        Assert.Equal(HttpStatusCode.OK, approve2Res.StatusCode);

        IReadOnlyList<AgentCommissionItemDto> ledger = (await (await client.SendAsync(
            AuthReq(HttpMethod.Get, "/api/v1/agents/commissions", agentJwt)))
            .Content.ReadFromJsonAsync<IReadOnlyList<AgentCommissionItemDto>>())!;
        Assert.Equal(2, ledger.Count);
        Assert.Contains(ledger, c => c.CommissionType == "NEW_ONBOARDING" && c.RatePctSnapshot == 15m && c.CommissionAmount == 900m);
        Assert.Contains(ledger, c => c.CommissionType == "RENEWAL" && c.RatePctSnapshot == 10m && c.CommissionAmount == 600m);

        // 3. Agent requests ₹500 withdrawal -> SuperAdmin settles ₹500 partial -> remaining ₹1,000 -> settles ₹1,000 -> overpayment rejected (400)
        HttpResponseMessage reqPayoutRes = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            "/api/v1/agents/payouts/request",
            agentJwt,
            new RequestAgentPayoutCommand(500m, "UPI", "Need partial payout")));
        Assert.Equal(HttpStatusCode.Created, reqPayoutRes.StatusCode);
        AgentPayoutItemDto requestedPayout = (await reqPayoutRes.Content.ReadFromJsonAsync<AgentPayoutItemDto>())!;
        Assert.Equal("Requested", requestedPayout.Status);

        // Requested voucher cannot be printed until Disbursed (409)
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(AuthReq(
            HttpMethod.Get, $"/api/v1/agents/payouts/{requestedPayout.PayoutId}/voucher.pdf", agentJwt))).StatusCode);

        // Settlement hub exposes 5 KPIs + PendingWithdrawalRequests + no plaintext Aadhaar
        HttpResponseMessage hubRes = await client.SendAsync(AuthReq(HttpMethod.Get, "/api/v1/superadmin/agents/settlements", saJwt));
        Assert.Equal(HttpStatusCode.OK, hubRes.StatusCode);
        string hubRaw = await hubRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain(rawAadhaar, hubRaw, StringComparison.Ordinal);
        SuperAdminAgentSettlementsDto hub = (await hubRes.Content.ReadFromJsonAsync<SuperAdminAgentSettlementsDto>())!;
        Assert.True(hub.TotalAgents >= 1);
        Assert.Contains(hub.PendingWithdrawalRequests, p => p.PayoutId == requestedPayout.PayoutId);

        DateOnly payDate = new(2026, 11, 17);

        // Partial disbursal of ₹500 (fulfilling request)
        HttpResponseMessage settle1Res = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            $"/api/v1/superadmin/agents/{createdAgent.AgentId}/settle-payout",
            saJwt,
            new
            {
                amount = 500m,
                paymentMode = "UPI",
                utrReference = $"UTR-VCH1-{mobile}",
                paymentDate = payDate,
                fulfillRequestedPayoutId = requestedPayout.PayoutId,
                remarks = "First partial disbursement",
            }));
        Assert.Equal(HttpStatusCode.OK, settle1Res.StatusCode);
        AgentPayoutItemDto payout1 = (await settle1Res.Content.ReadFromJsonAsync<AgentPayoutItemDto>())!;
        Assert.Equal("Disbursed", payout1.Status);
        Assert.StartsWith("VCH-2026-", payout1.VoucherNo, StringComparison.Ordinal);
        Assert.Equal(500m, payout1.PayoutAmount);

        // Check QuestPDF Voucher downloads cleanly for BOTH Agent and SuperAdmin
        HttpResponseMessage agPdfRes = await client.SendAsync(AuthReq(
            HttpMethod.Get, $"/api/v1/agents/payouts/{payout1.PayoutId}/voucher.pdf", agentJwt));
        Assert.Equal(HttpStatusCode.OK, agPdfRes.StatusCode);
        Assert.Equal("application/pdf", agPdfRes.Content.Headers.ContentType?.MediaType);
        byte[] pdfBytes = await agPdfRes.Content.ReadAsByteArrayAsync();
        Assert.True(pdfBytes.Length > 1000);
        Assert.Equal("%PDF-"u8.ToArray(), pdfBytes[..5]);

        HttpResponseMessage saPdfRes = await client.SendAsync(AuthReq(
            HttpMethod.Get, $"/api/v1/superadmin/agents/payouts/{payout1.PayoutId}/voucher.pdf", saJwt));
        Assert.Equal(HttpStatusCode.OK, saPdfRes.StatusCode);

        // Check remaining balance is ₹1,000 (900 - 500 + 600 = 1000)
        AgentDashboardDto dashAfterPartial = (await (await client.SendAsync(AuthReq(HttpMethod.Get, "/api/v1/agents/dashboard", agentJwt)))
            .Content.ReadFromJsonAsync<AgentDashboardDto>())!;
        Assert.Equal(1000m, dashAfterPartial.PendingPayableCommission);
        Assert.Equal(500m, dashAfterPartial.PaidCommission);

        // Second settlement of remaining ₹1,000 clears balance to ₹0 & increments VoucherSequence!
        HttpResponseMessage settle2Res = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            $"/api/v1/superadmin/agents/{createdAgent.AgentId}/settle-payout",
            saJwt,
            new
            {
                amount = 1000m,
                paymentMode = "NEFT_RTGS",
                utrReference = $"UTR-VCH2-{mobile}",
                paymentDate = payDate,
                remarks = "Final settlement",
            }));
        Assert.Equal(HttpStatusCode.OK, settle2Res.StatusCode);
        AgentPayoutItemDto payout2 = (await settle2Res.Content.ReadFromJsonAsync<AgentPayoutItemDto>())!;
        Assert.Equal(payout1.VoucherSequence + 1, payout2.VoucherSequence);

        // Overpayment > ₹0 fails cleanly with 400
        HttpResponseMessage overpayRes = await client.SendAsync(AuthReq(
            HttpMethod.Post,
            $"/api/v1/superadmin/agents/{createdAgent.AgentId}/settle-payout",
            saJwt,
            new
            {
                amount = 100m,
                paymentMode = "UPI",
                utrReference = $"UTR-OVER-{mobile}",
                paymentDate = payDate,
            }));
        Assert.Equal(HttpStatusCode.BadRequest, overpayRes.StatusCode);

        // 4. Agent profile update + password change + refresh token revocation
        HttpResponseMessage profRes = await client.SendAsync(AuthReq(
            HttpMethod.Put,
            "/api/v1/agents/profile",
            agentJwt,
            new UpdateAgentProfileCommand(
                $"rameshwar.updated.{mobile}@gpmh.in",
                "Station Road, Warud",
                "Warud",
                "Amravati",
                "Warud,Morshi,Chandur Bazar",
                "Bank of Maharashtra",
                "Warud",
                "60112233445",
                "MAHB0000123",
                "updated.agent@okicici")));
        Assert.Equal(HttpStatusCode.OK, profRes.StatusCode);
        AgentProfileDto updatedProf = (await profRes.Content.ReadFromJsonAsync<AgentProfileDto>())!;
        Assert.Equal("updated.agent@okicici", updatedProf.UpiId);

        const string newPass = "UpdatedAgent@2026!";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(AuthReq(
            HttpMethod.Post, "/api/v1/agents/change-password", agentJwt,
            new ChangeAgentPasswordCommand("WrongCurrent@1", newPass)))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(AuthReq(
            HttpMethod.Post, "/api/v1/agents/change-password", agentJwt,
            new ChangeAgentPasswordCommand(initialPass, newPass)))).StatusCode);

        // Stale refresh token revoked; old password invalid; new password works!
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = agentAuth.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/v1/agents/login", new { identifier = mobile, password = initialPass })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/v1/agents/login", new { identifier = mobile, password = newPass })).StatusCode);

        // Confirm AuditLogs never leaked raw Aadhaar
        using IServiceScope dbScope = factory.Services.CreateScope();
        AppDbContext db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(l => l.EntityId == createdAgent.AgentId.ToString("D") || l.Action.Contains("Agent"))
            .ToListAsync();
        Assert.All(logs, l => Assert.DoesNotContain(rawAadhaar, l.NewValuesJson ?? string.Empty, StringComparison.Ordinal));
    }
}
