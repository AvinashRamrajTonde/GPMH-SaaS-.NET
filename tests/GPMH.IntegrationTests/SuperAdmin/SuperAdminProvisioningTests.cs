using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auth;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.SuperAdmin;

public sealed class SuperAdminProvisioningTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    [Fact]
    public async Task DirectProvisioning_EnforcesSuperAdminRbac_ActivatesTenant_AndEnablesImmediateLogin()
    {
        string saUsername = $"super.admin.{Guid.NewGuid():N}"[..24];
        string gsUsername = $"non.sa.{Guid.NewGuid():N}"[..22];
        const string pass = "Super@2026!";

        Guid helperTenantId = Guid.NewGuid();
        Guid superAdminId;

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            IPasswordHasherService hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

            User superAdmin = new()
            {
                Username = saUsername,
                FullNameEn = "State SuperAdmin",
                FullNameMr = "सुपरॲडमिन",
                PasswordHash = hasher.HashPassword(pass),
                IsSuperAdmin = true,
            };
            superAdminId = superAdmin.Id;

            db.Tenants.Add(new Tenant
            {
                Id = helperTenantId,
                TenantCode = $"gp-helper-{helperTenantId:N}"[..22],
                LgdCode = $"41{Random.Shared.Next(1000, 9999)}",
                NameEn = "Helper GP",
                NameMr = "हेल्पर",
                Status = TenantStatus.Active,
            });

            User regularUser = new()
            {
                Username = gsUsername,
                FullNameEn = "Regular GS",
                FullNameMr = "ग्रामसेवक",
                PasswordHash = hasher.HashPassword(pass),
                IsSuperAdmin = false,
            };

            db.Users.AddRange(superAdmin, regularUser);
            db.TenantMemberships.Add(new TenantMembership
            {
                UserId = regularUser.Id,
                TenantId = helperTenantId,
                Role = ApplicationRole.Gramsevak,
                IsDefault = true,
            });
            await db.SaveChangesAsync();
        }

        HttpClient client = factory.CreateClient();

        // 1. Gramsevak cannot access SuperAdmin endpoint -> 403 Forbidden
        HttpResponseMessage gsLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = gsUsername, password = pass });
        string gsJwt = (await gsLogin.Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;

        using HttpRequestMessage deniedReq = new(HttpMethod.Get, "/api/v1/superadmin/tenants");
        deniedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gsJwt);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(deniedReq)).StatusCode);

        // 2. SuperAdmin provisions a brand-new GP directly
        HttpResponseMessage saLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = saUsername, password = pass });
        Assert.Equal(HttpStatusCode.OK, saLogin.StatusCode);
        string saJwt = (await saLogin.Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;

        string newLgd = $"55{Random.Shared.Next(1000, 9999)}";
        string newAdminUser = $"98{Random.Shared.Next(10000000, 99999999)}";

        using HttpRequestMessage provisionReq = new(HttpMethod.Post, "/api/v1/superadmin/tenants/provision")
        {
            Content = JsonContent.Create(new
            {
                lgdCode = newLgd,
                panchayatNameEn = "Gram Panchayat Warud Khurd",
                panchayatNameMr = "ग्रामपंचायत वरुड खुर्द",
                districtCode = "496",
                districtName = "Amravati",
                tehsilCode = "4101",
                tehsilName = "Warud",
                pincode = "444705",
                censusCode = "530120",
                officeAddress = "Main Bazar Chowk, Warud Khurd",
                adminFullName = "राजेश वानखडे",
                adminUsername = newAdminUser,
                adminEmail = $"warud.{newLgd}@gov.in",
                adminPassword = "Maha@12345",
                planName = "Enterprise",
                paymentMode = "NEFT_RTGS",
                paymentAmount = 6000m,
                durationDays = 365,
                paymentReference = $"UTR-{newLgd}-99",
                totalWards = 6,
                populationCensus = 4200,
            }),
        };
        provisionReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", saJwt);

        HttpResponseMessage provisionResp = await client.SendAsync(provisionReq);
        Assert.Equal(HttpStatusCode.Created, provisionResp.StatusCode);

        ProvisionedTenantResultDto result = (await provisionResp.Content.ReadFromJsonAsync<ProvisionedTenantResultDto>())!;
        Assert.Equal("Active", result.Status);
        Assert.Equal(newLgd, result.LgdCode);
        Assert.Equal(6000m, result.AmountPaid);
        Assert.True(result.FormulaSeeded);

        // 3. Verify newly provisioned Gramsevak logs in immediately & reads seeded dashboard
        HttpResponseMessage newLoginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = newAdminUser,
            password = "Maha@12345",
        });
        Assert.Equal(HttpStatusCode.OK, newLoginResp.StatusCode);
        AuthResponseDto newAuth = (await newLoginResp.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        Assert.Equal(result.TenantId, newAuth.User.ActiveTenantId);

        using HttpRequestMessage dashReq = new(HttpMethod.Get, "/api/v1/dashboard/summary");
        dashReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAuth.AccessToken);
        HttpResponseMessage dashResp = await client.SendAsync(dashReq);
        Assert.Equal(HttpStatusCode.OK, dashResp.StatusCode);

        DashboardSummaryDto summary = (await dashResp.Content.ReadFromJsonAsync<DashboardSummaryDto>())!;
        Assert.Equal(newLgd, summary.LgdCode);

        // 4. Verify AuditLog recorded SuperAdmin actor and SubscriptionPayment row exists
        using IServiceScope verifyScope = factory.Services.CreateScope();
        AppDbContext verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await verifyDb.TenantSubscriptionPayments.AnyAsync(p => p.TenantId == result.TenantId && p.PaymentReference == $"UTR-{newLgd}-99"));
        Assert.True(await verifyDb.AuditLogs.AnyAsync(a => a.TenantId == result.TenantId && a.ActorId == superAdminId.ToString("D") && a.EntityName == nameof(Tenant)));
    }
}

