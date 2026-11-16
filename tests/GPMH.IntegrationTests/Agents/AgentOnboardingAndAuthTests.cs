using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.Auth;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Agents;

public sealed class AgentOnboardingAndAuthTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static readonly byte[] MinimalPdf = "%PDF-1.4\n1 0 obj\n<<>>\nendobj\n"u8.ToArray();
    private static readonly byte[] MinimalPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task AgentLifecycle_EncryptsKyc_GatesPendingLogin_AndGrantsFieldAgentJwtUponApproval()
    {
        string saUsername = $"sa.agent.{Guid.NewGuid():N}"[..22];
        const string saPass = "SuperAgent@2026!";

        using (IServiceScope seedScope = factory.Services.CreateScope())
        {
            AppDbContext db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            IPasswordHasherService hasher = scopeHasher(seedScope);
            db.Users.Add(new User
            {
                Username = saUsername,
                FullNameEn = "SuperAdmin Reviewer",
                FullNameMr = "सुपरॲडमिन",
                PasswordHash = hasher.HashPassword(saPass),
                IsSuperAdmin = true,
            });
            await db.SaveChangesAsync();

            static IPasswordHasherService scopeHasher(IServiceScope s) => s.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        }

        HttpClient client = factory.CreateClient();
        string mobile = $"9{Random.Shared.Next(700000000, 999999999)}";
        string aadhaar = $"4321{Random.Shared.Next(1000, 9999)}7890";
        string pan = $"AB{Random.Shared.Next(100, 999) switch { var n => "CDE" }}{Random.Shared.Next(1000, 9999)}F";
        // Guarantee strictly valid PAN format [A-Z]{5}[0-9]{4}[A-Z]
        pan = $"A{(char)('A' + Random.Shared.Next(0, 26))}CDE{Random.Shared.Next(1000, 9999)}F";
        const string agentPass = "AgentPass@2026!";

        // 1. Register 13-field Agent + Aadhaar PNG + PAN PDF KYC uploads
        using MultipartFormDataContent form = new();
        form.Add(new StringContent("Mahesh Vitthalrao Deshmukh"), "fullName");
        form.Add(new StringContent(mobile), "mobileNo");
        form.Add(new StringContent($"mahesh.{mobile}@agency.in"), "email");
        form.Add(new StringContent(agentPass), "password");
        form.Add(new StringContent("Plot 12, Camp Road, Amravati"), "address");
        form.Add(new StringContent("Achalpur"), "villageOrCity");
        form.Add(new StringContent("Amravati"), "district");
        form.Add(new StringContent("Achalpur, Anjangaon Surji, Morshi"), "talukasCsv");
        form.Add(new StringContent("AADHAAR"), "idProofType");
        form.Add(new StringContent(aadhaar), "aadhaarNumber");
        form.Add(new StringContent(pan), "panNumber");
        form.Add(new StringContent("State Bank of India"), "bankName");
        form.Add(new StringContent("Amravati Main"), "bankBranch");
        form.Add(new StringContent("30992817261"), "accountNumber");
        form.Add(new StringContent("SBIN0000301"), "ifscCode");
        form.Add(new StringContent("mahesh.agent@sbi"), "upiId");
        form.Add(new StringContent("25"), "requestedCommissionRatePct");

        ByteArrayContent pngContent = new(MinimalPng);
        pngContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(pngContent, "aadhaarDocument", "aadhaar.png");

        ByteArrayContent pdfContent = new(MinimalPdf);
        pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdfContent, "panDocument", "pan.pdf");

        HttpResponseMessage regResp = await client.PostAsync("/api/v1/agents/register", form);
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        AgentRegistrationResultDto registered = (await regResp.Content.ReadFromJsonAsync<AgentRegistrationResultDto>())!;
        Assert.Equal(AgentKycStatus.PendingApproval, registered.KycStatus);
        Assert.Equal("XXXX-XXXX-7890", registered.AadhaarMasked);
        Assert.StartsWith("AGT-MH-", registered.AgentCode, StringComparison.Ordinal);

        // Verify duplicate PAN/Aadhaar/mobile rejected with 400
        using MultipartFormDataContent dupForm = new();
        dupForm.Add(new StringContent("Dup Agent"), "fullName");
        dupForm.Add(new StringContent(mobile), "mobileNo");
        dupForm.Add(new StringContent("dup@agency.in"), "email");
        dupForm.Add(new StringContent(agentPass), "password");
        dupForm.Add(new StringContent("Addr"), "address");
        dupForm.Add(new StringContent("City"), "villageOrCity");
        dupForm.Add(new StringContent("Beed"), "district");
        dupForm.Add(new StringContent("Dharur"), "talukasCsv");
        dupForm.Add(new StringContent(aadhaar), "aadhaarNumber");
        dupForm.Add(new StringContent(pan), "panNumber");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/agents/register", dupForm)).StatusCode);

        // Verify AuditLogs & FieldAgent never leak plaintext Aadhaar
        using (IServiceScope dbScope = factory.Services.CreateScope())
        {
            AppDbContext db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.FieldAgents.AsNoTracking().FirstAsync(a => a.Id == registered.AgentId);
            Assert.NotNull(stored.AadhaarEncrypted);
            Assert.DoesNotContain(aadhaar, Encoding.UTF8.GetString(stored.AadhaarEncrypted), StringComparison.Ordinal);

            var audits = await db.AuditLogs.AsNoTracking().Where(l => l.EntityId == registered.AgentId.ToString("D")).ToListAsync();
            Assert.All(audits, l => Assert.DoesNotContain(aadhaar, l.NewValuesJson ?? string.Empty, StringComparison.Ordinal));
        }

        // 2. Pending agent login MUST return 403 ProblemDetails status gate with helpline metadata
        HttpResponseMessage gatedResp = await client.PostAsJsonAsync("/api/v1/agents/login", new
        {
            identifier = mobile,
            password = agentPass,
        });
        Assert.Equal(HttpStatusCode.Forbidden, gatedResp.StatusCode);
        ProblemDetails gatedProblem = (await gatedResp.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal("PendingApproval", gatedProblem.Extensions["kycStatus"]?.ToString());
        Assert.Equal("0721-2550101", gatedProblem.Extensions["helplinePhone"]?.ToString());

        // 3. SuperAdmin approves Agent KYC with 20% new GP / 12% renewal commission
        HttpResponseMessage saLoginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = saUsername, password = saPass });
        string saJwt = (await saLoginResp.Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;

        using HttpRequestMessage approveReq = new(HttpMethod.Post, $"/api/v1/superadmin/agents/{registered.AgentId:D}/status")
        {
            Content = JsonContent.Create(new
            {
                status = AgentKycStatus.Approved,
                approvedNewGpRatePct = 20m,
                approvedRenewalRatePct = 12m,
                reason = "KYC verified via Aadhaar & PAN.",
            }),
        };
        approveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", saJwt);
        HttpResponseMessage approveResp = await client.SendAsync(approveReq);
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        // 4. Approved agent logs in -> receives Field_Agent JWT with agent_id claim
        HttpResponseMessage agentLoginResp = await client.PostAsJsonAsync("/api/v1/agents/login", new
        {
            identifier = mobile,
            password = agentPass,
        });
        Assert.Equal(HttpStatusCode.OK, agentLoginResp.StatusCode);
        AuthResponseDto agentAuth = (await agentLoginResp.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        Assert.Equal(ApplicationRole.Field_Agent, agentAuth.User.Role);

        JwtSecurityToken parsedJwt = new JwtSecurityTokenHandler().ReadJwtToken(agentAuth.AccessToken);
        Assert.Contains(parsedJwt.Claims, c => c.Type == "agent_id" && c.Value == registered.AgentId.ToString("D"));

        // 5. Agent accesses /api/v1/agents/me (200 OK) while blocked from GP /api/v1/properties (403 Forbidden)
        using HttpRequestMessage meReq = new(HttpMethod.Get, "/api/v1/agents/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentAuth.AccessToken);
        HttpResponseMessage meResp = await client.SendAsync(meReq);
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        AgentProfileDto me = (await meResp.Content.ReadFromJsonAsync<AgentProfileDto>())!;
        Assert.Equal(20m, me.ApprovedNewGpRatePct);
        Assert.Equal("XXXX-XXXX-7890", me.AadhaarMasked);

        using HttpRequestMessage gpForbiddenReq = new(HttpMethod.Get, "/api/v1/properties");
        gpForbiddenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentAuth.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(gpForbiddenReq)).StatusCode);
    }
}

