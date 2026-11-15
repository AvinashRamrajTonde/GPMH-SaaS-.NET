using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auditing;
using GPMH.Application.DTOs.Auth;
using GPMH.Application.DTOs.Properties;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GPMH.IntegrationTests.Auditing;

public sealed class AuditTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    [Fact]
    public async Task PropertyLifecycle_RecordsChronologicalDiffs_SoftDeletesSafely_AndMasksSecrets()
    {
        Guid tenantId = Guid.NewGuid();
        Guid otherTenantId = Guid.NewGuid();
        const string secretPassword = "SuperSecret!Password99";

        string gramsevakUsername = $"audit.gs.{Guid.NewGuid():N}"[..24];
        string auditorUsername = $"audit.aud.{Guid.NewGuid():N}"[..24];
        string otherAuditorUsername = $"audit.oth.{Guid.NewGuid():N}"[..24];
        Guid gramsevakUserId;

        using (IServiceScope seedScope = factory.Services.CreateScope())
        {
            AppDbContext db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            IPasswordHasherService hasher = seedScope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

            db.Tenants.AddRange(
                new Tenant
                {
                    Id = tenantId,
                    TenantCode = $"audit-a-{tenantId:N}"[..22],
                    LgdCode = $"181{Random.Shared.Next(100, 999)}",
                    NameEn = "Audit Alpha GP",
                    NameMr = "ऑडिट अल्फा",
                    Status = TenantStatus.Active,
                },
                new Tenant
                {
                    Id = otherTenantId,
                    TenantCode = $"audit-b-{otherTenantId:N}"[..22],
                    LgdCode = $"182{Random.Shared.Next(100, 999)}",
                    NameEn = "Audit Beta GP",
                    NameMr = "ऑडिट बीटा",
                    Status = TenantStatus.Active,
                });

            User gsUser = new()
            {
                Username = gramsevakUsername,
                FullNameEn = "Gramsevak Auditor Test",
                FullNameMr = "ग्रामसेवक",
                PasswordHash = hasher.HashPassword(secretPassword),
            };
            gramsevakUserId = gsUser.Id;

            User auditorUser = new()
            {
                Username = auditorUsername,
                FullNameEn = "Auditor User",
                FullNameMr = "लेखापरीक्षक",
                PasswordHash = hasher.HashPassword("AuditorPass@1!"),
            };

            User otherAuditorUser = new()
            {
                Username = otherAuditorUsername,
                FullNameEn = "Other Auditor",
                FullNameMr = "इतर लेखापरीक्षक",
                PasswordHash = hasher.HashPassword("OtherPass@1!"),
            };

            db.Users.AddRange(gsUser, auditorUser, otherAuditorUser);
            db.TenantMemberships.AddRange(
                new TenantMembership { UserId = gsUser.Id, TenantId = tenantId, Role = ApplicationRole.Gramsevak, IsDefault = true },
                new TenantMembership { UserId = auditorUser.Id, TenantId = tenantId, Role = ApplicationRole.Auditor, IsDefault = true },
                new TenantMembership { UserId = otherAuditorUser.Id, TenantId = otherTenantId, Role = ApplicationRole.Auditor, IsDefault = true });

            await db.SaveChangesAsync();

            // Verify sensitive PasswordHash on User insert was masked cleanly in AuditLogs
            AuditLog? userInsertAudit = await db.AuditLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.EntityName == nameof(User) && l.EntityId == gramsevakUserId.ToString("D") && l.Action == "Added");

            Assert.NotNull(userInsertAudit);
            Assert.NotNull(userInsertAudit.NewValuesJson);
            Assert.Contains("\"[MASKED]\"", userInsertAudit.NewValuesJson, StringComparison.Ordinal);
            Assert.DoesNotContain(secretPassword, userInsertAudit.NewValuesJson, StringComparison.Ordinal);
            Assert.DoesNotContain(gsUser.PasswordHash, userInsertAudit.NewValuesJson, StringComparison.Ordinal);
        }

        HttpClient client = factory.CreateClient();
        string gsJwt = await LoginAsync(client, gramsevakUsername, secretPassword, tenantId);
        string auditorJwt = await LoginAsync(client, auditorUsername, "AuditorPass@1!", tenantId);
        string otherAuditorJwt = await LoginAsync(client, otherAuditorUsername, "OtherPass@1!", otherTenantId);

        // 1. POST Property -> Added
        using HttpRequestMessage createReq = new(HttpMethod.Post, "/api/v1/properties")
        {
            Content = JsonContent.Create(new
            {
                wardNo = "W-03",
                propertyNo = "301",
                builtUpAreaSqFt = 500m,
                openPlotAreaSqFt = 200m,
                waterConnectionCode = "SINGLE",
                isTaxExempt = false,
            }),
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gsJwt);
        createReq.Headers.Add("User-Agent", "GPMH-TestSuite/1.0");
        HttpResponseMessage createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        PropertyDto created = (await createResp.Content.ReadFromJsonAsync<PropertyDto>())!;

        // 2. PUT Property -> Modified
        using HttpRequestMessage updateReq = new(HttpMethod.Put, $"/api/v1/properties/{created.Id:D}")
        {
            Content = JsonContent.Create(new
            {
                builtUpAreaSqFt = 625m,
                openPlotAreaSqFt = 200m,
                waterConnectionCode = "DOUBLE",
                isTaxExempt = false,
            }),
        };
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gsJwt);
        updateReq.Headers.Add("User-Agent", "GPMH-TestSuite/1.0");
        HttpResponseMessage updateResp = await client.SendAsync(updateReq);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // 3. DELETE Property -> SoftDeleted (kept in DB, hidden from active queries)
        using HttpRequestMessage deleteReq = new(HttpMethod.Delete, $"/api/v1/properties/{created.Id:D}");
        deleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gsJwt);
        deleteReq.Headers.Add("User-Agent", "GPMH-TestSuite/1.0");
        HttpResponseMessage deleteResp = await client.SendAsync(deleteReq);
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Deleted property now returns 404 via API, while still present in raw table
        using HttpRequestMessage getDeletedReq = new(HttpMethod.Get, $"/api/v1/properties/{created.Id:D}");
        getDeletedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gsJwt);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(getDeletedReq)).StatusCode);

        using (IServiceScope dbScope = factory.Services.CreateScope())
        {
            AppDbContext db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Property? softDeletedRow = await db.Properties
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == created.Id);

            Assert.NotNull(softDeletedRow);
            Assert.True(softDeletedRow.IsDeleted);
            Assert.False(softDeletedRow.IsActive);
        }

        // 4. Auditor queries chronological audit trail for Property
        using HttpRequestMessage auditReq = new(HttpMethod.Get, $"/api/v1/audit/Property/{created.Id:D}");
        auditReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auditorJwt);
        HttpResponseMessage auditResp = await client.SendAsync(auditReq);
        Assert.Equal(HttpStatusCode.OK, auditResp.StatusCode);

        List<AuditLogDto>? trail = await auditResp.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        Assert.NotNull(trail);
        Assert.Equal(["Added", "Modified", "SoftDeleted"], trail.Select(t => t.Action).ToArray());

        AuditLogDto modifiedEntry = trail[1];
        Assert.Equal(gramsevakUserId.ToString("D"), modifiedEntry.ActorId);
        Assert.Equal(nameof(ApplicationRole.Gramsevak), modifiedEntry.ActorRole);
        Assert.Equal("GPMH-TestSuite/1.0", modifiedEntry.UserAgent);
        Assert.Contains("BuiltUpAreaSqFt", modifiedEntry.ChangedColumnsJson, StringComparison.Ordinal);
        Assert.Contains("500", modifiedEntry.OldValuesJson, StringComparison.Ordinal);
        Assert.Contains("625", modifiedEntry.NewValuesJson, StringComparison.Ordinal);

        // 5. Other tenant's Auditor sees empty trail (cannot read Tenant A's audit trail)
        using HttpRequestMessage crossAuditReq = new(HttpMethod.Get, $"/api/v1/audit/Property/{created.Id:D}");
        crossAuditReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherAuditorJwt);
        HttpResponseMessage crossAuditResp = await client.SendAsync(crossAuditReq);
        Assert.Equal(HttpStatusCode.OK, crossAuditResp.StatusCode);
        List<AuditLogDto>? otherTrail = await crossAuditResp.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        Assert.NotNull(otherTrail);
        Assert.Empty(otherTrail);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password, Guid tenantId)
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
}
