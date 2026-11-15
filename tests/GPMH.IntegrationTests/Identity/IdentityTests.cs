using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auth;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Identity;

public sealed class IdentityTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    [Fact]
    public async Task WebLogin_RefreshRotation_AndFamilyReuseDetection_EnforcesAdr006()
    {
        (Guid tenantId, User user, string password) = await SeedUserWithRoleAsync(
            "gramsevak.sonimoha",
            "Pass@171391!",
            ApplicationRole.Gramsevak);

        HttpClient client = factory.CreateClient();

        // 1. Wrong password -> 401
        HttpResponseMessage badLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = "WrongPassword!",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        // 2. Web login sets HttpOnly; Secure; SameSite=Strict cookie and omits refreshToken from JSON
        HttpResponseMessage loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password,
            tenantId,
        });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        AuthResponseDto? loginBody = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(loginBody);
        Assert.Null(loginBody.RefreshToken);
        Assert.NotEmpty(loginBody.AccessToken);
        Assert.Equal(ApplicationRole.Gramsevak, loginBody.User.Role);
        Assert.Equal(tenantId, loginBody.User.ActiveTenantId);

        JwtSecurityToken parsedJwt = new JwtSecurityTokenHandler().ReadJwtToken(loginBody.AccessToken);
        Assert.Equal("gpmh-api", parsedJwt.Issuer);
        Assert.Contains(parsedJwt.Claims, c => c.Type == "tenant_id" && c.Value == tenantId.ToString("D"));

        string initialCookieValue = ExtractRefreshCookie(loginResp, out string rawSetCookieHeader);
        Assert.Contains("httponly", rawSetCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", rawSetCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", rawSetCookieHeader, StringComparison.OrdinalIgnoreCase);

        // 3. Real JWT authenticates GET /api/v1/auth/me without TestAuth headers
        using HttpRequestMessage meReq = new(HttpMethod.Get, "/api/v1/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);
        HttpResponseMessage meResp = await client.SendAsync(meReq);
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        AuthenticatedUserDto? meDto = await meResp.Content.ReadFromJsonAsync<AuthenticatedUserDto>();
        Assert.NotNull(meDto);
        Assert.Equal(user.Id, meDto.UserId);
        Assert.Equal(tenantId, meDto.ActiveTenantId);

        // 4. Web refresh via Cookie header rotates token
        using HttpRequestMessage refreshReq = new(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshReq.Headers.Add("Cookie", $"gpmh_refresh_token={initialCookieValue}");
        HttpResponseMessage refreshResp = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);

        AuthResponseDto? refreshBody = await refreshResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(refreshBody);
        Assert.Null(refreshBody.RefreshToken);
        string rotatedCookieValue = ExtractRefreshCookie(refreshResp, out _);
        Assert.NotEqual(initialCookieValue, rotatedCookieValue);

        // 5. Security Incident: Replay ALREADY-ROTATED initialCookieValue -> 401 AND revokes whole family
        using HttpRequestMessage replayReq = new(HttpMethod.Post, "/api/v1/auth/refresh");
        replayReq.Headers.Add("Cookie", $"gpmh_refresh_token={initialCookieValue}");
        HttpResponseMessage replayResp = await client.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResp.StatusCode);

        // 6. Legitimate rotatedCookieValue in the same family MUST now also fail with 401
        using HttpRequestMessage poisonedFamilyReq = new(HttpMethod.Post, "/api/v1/auth/refresh");
        poisonedFamilyReq.Headers.Add("Cookie", $"gpmh_refresh_token={rotatedCookieValue}");
        HttpResponseMessage poisonedFamilyResp = await client.SendAsync(poisonedFamilyReq);
        Assert.Equal(HttpStatusCode.Unauthorized, poisonedFamilyResp.StatusCode);

        // 7. Verify AuditLogs recorded RefreshTokenReuseDetected incident for both compromised attempts
        using IServiceScope verifyScope = factory.Services.CreateScope();
        AppDbContext db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<AuditLog> securityEvents = await db.AuditLogs
            .AsNoTracking()
            .Where(l => l.ActorId == user.Id.ToString("D") && l.Action == "RefreshTokenReuseDetected")
            .ToListAsync();
        Assert.Equal(2, securityEvents.Count);
    }

    [Fact]
    public async Task MobileLogin_AndLogout_DeliversBodyTokenAndRevokesSession()
    {
        (Guid tenantId, User user, string password) = await SeedUserWithRoleAsync(
            "surveyor.sonimoha",
            "Survey@2026!",
            ApplicationRole.Surveyor);

        HttpClient client = factory.CreateClient();

        using HttpRequestMessage loginReq = new(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { username = user.Username, password, tenantId }),
        };
        loginReq.Headers.Add("X-Client-Type", "mobile");

        HttpResponseMessage loginResp = await client.SendAsync(loginReq);
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        Assert.False(loginResp.Headers.Contains("Set-Cookie"));

        AuthResponseDto? loginBody = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(loginBody);
        Assert.False(string.IsNullOrWhiteSpace(loginBody.RefreshToken));

        // Rotate via JSON body
        using HttpRequestMessage refreshReq = new(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { refreshToken = loginBody.RefreshToken, clientType = "mobile" }),
        };
        HttpResponseMessage refreshResp = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);

        AuthResponseDto? rotatedBody = await refreshResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(rotatedBody);
        Assert.False(string.IsNullOrWhiteSpace(rotatedBody.RefreshToken));
        Assert.NotEqual(loginBody.RefreshToken, rotatedBody.RefreshToken);

        // Logout revokes active session
        HttpResponseMessage logoutResp = await client.PostAsJsonAsync("/api/v1/auth/logout", new
        {
            refreshToken = rotatedBody.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        // Refresh after logout MUST fail with 401
        HttpResponseMessage postLogoutRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = rotatedBody.RefreshToken,
            clientType = "mobile",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, postLogoutRefresh.StatusCode);
    }

    [Fact]
    public async Task CanonicalRbacPolicies_Enforce401And403AcrossRoles()
    {
        (Guid tenantId, User citizenUser, string citizenPass) = await SeedUserWithRoleAsync(
            "citizen.sonimoha",
            "Citizen@2026!",
            ApplicationRole.Citizen);

        (Guid _, User auditorUser, string auditorPass) = await SeedUserWithRoleAsync(
            "auditor.sonimoha",
            "Auditor@2026!",
            ApplicationRole.Auditor,
            existingTenantId: tenantId);

        (Guid _, User gramsevakUser, string gramsevakPass) = await SeedUserWithRoleAsync(
            "clerk.sonimoha",
            "Clerk@2026!",
            ApplicationRole.Gramsevak,
            existingTenantId: tenantId);

        HttpClient client = factory.CreateClient();

        // 1. Unauthenticated /me -> 401
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        // 2. Citizen cannot list or create properties -> 403
        string citizenJwt = await LoginAndGetJwtAsync(client, citizenUser.Username, citizenPass, tenantId);
        using HttpRequestMessage citizenRead = new(HttpMethod.Get, "/api/v1/properties");
        citizenRead.Headers.Authorization = new AuthenticationHeaderValue("Bearer", citizenJwt);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(citizenRead)).StatusCode);

        // 3. Auditor can read properties (200) but cannot create properties (403)
        string auditorJwt = await LoginAndGetJwtAsync(client, auditorUser.Username, auditorPass, tenantId);
        using HttpRequestMessage auditorRead = new(HttpMethod.Get, "/api/v1/properties");
        auditorRead.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auditorJwt);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(auditorRead)).StatusCode);

        using HttpRequestMessage auditorWrite = new(HttpMethod.Post, "/api/v1/properties")
        {
            Content = JsonContent.Create(new
            {
                wardNo = "W-09",
                propertyNo = "901",
                builtUpAreaSqFt = 500m,
                openPlotAreaSqFt = 100m,
            }),
        };
        auditorWrite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auditorJwt);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(auditorWrite)).StatusCode);

        // 4. Gramsevak can create properties (201)
        string gramsevakJwt = await LoginAndGetJwtAsync(client, gramsevakUser.Username, gramsevakPass, tenantId);
        using HttpRequestMessage gramsevakWrite = new(HttpMethod.Post, "/api/v1/properties")
        {
            Content = JsonContent.Create(new
            {
                wardNo = "W-09",
                propertyNo = "902",
                builtUpAreaSqFt = 500m,
                openPlotAreaSqFt = 100m,
            }),
        };
        gramsevakWrite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gramsevakJwt);
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(gramsevakWrite)).StatusCode);
    }

    private async Task<string> LoginAndGetJwtAsync(HttpClient client, string username, string password, Guid tenantId)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username,
            password,
            tenantId,
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        AuthResponseDto? dto = await resp.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(dto);
        return dto.AccessToken;
    }

    private async Task<(Guid TenantId, User User, string RawPassword)> SeedUserWithRoleAsync(
        string usernamePrefix,
        string rawPassword,
        ApplicationRole role,
        Guid? existingTenantId = null)
    {
        Guid tenantId = existingTenantId ?? Guid.NewGuid();
        string uniqueUsername = $"{usernamePrefix}.{Guid.NewGuid():N}"[..28];

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IPasswordHasherService hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        if (!existingTenantId.HasValue)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                TenantCode = $"gp-{tenantId:N}"[..20],
                LgdCode = $"19{Random.Shared.Next(1000, 9999)}",
                NameEn = "Sonimoha Gram Panchayat",
                NameMr = "सोनीमोहा ग्रामपंचायत",
                Status = TenantStatus.Active,
            });
        }

        User user = new()
        {
            Username = uniqueUsername,
            Email = $"{uniqueUsername}@grampanchayat.in",
            FullNameEn = $"Test {role}",
            FullNameMr = $"चाचणी {role}",
            PasswordHash = hasher.HashPassword(rawPassword),
            IsSuperAdmin = role == ApplicationRole.SuperAdmin,
            IsActive = true,
        };
        db.Users.Add(user);

        db.TenantMemberships.Add(new TenantMembership
        {
            UserId = user.Id,
            TenantId = tenantId,
            Role = role,
            IsDefault = true,
            IsActive = true,
        });

        await db.SaveChangesAsync();
        return (tenantId, user, rawPassword);
    }

    private static string ExtractRefreshCookie(HttpResponseMessage response, out string fullHeader)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values));
        string? refreshHeader = values.FirstOrDefault(v => v.StartsWith("gpmh_refresh_token=", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(refreshHeader);
        fullHeader = refreshHeader;

        int eqIdx = refreshHeader.IndexOf('=', StringComparison.Ordinal);
        int semiIdx = refreshHeader.IndexOf(';', eqIdx + 1);
        string raw = semiIdx > eqIdx ? refreshHeader[(eqIdx + 1)..semiIdx] : refreshHeader[(eqIdx + 1)..];
        return Uri.UnescapeDataString(raw);
    }
}

