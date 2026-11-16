using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.Mutations;
using GPMH.Application.DTOs.Onboarding;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Citizens.Commands;
using GPMH.Application.Features.Mutations.Commands;
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

namespace GPMH.IntegrationTests.Mutations;

public sealed class MutationIntegrationTests(GpmhApiFactory factory)
    : IClassFixture<GpmhApiFactory>
{
    private static int _lgdSequence = 600000;

    private async Task<(Guid TenantId, Guid GramsevakId, Guid OperatorId)> SeedMutationTenantAsync()
    {
        Guid tenantId = Guid.NewGuid();
        Guid gramsevakId = Guid.NewGuid();
        Guid operatorId = Guid.NewGuid();
        string lgdCode = Interlocked.Increment(ref _lgdSequence).ToString();

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        IDefaultFormulaSeeder seeder = scope.ServiceProvider.GetRequiredService<IDefaultFormulaSeeder>();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantCode = $"gp-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = $"Mutation GP {lgdCode}",
            NameMr = $"फेरफार ग्रा.पं. {lgdCode}",
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

        db.GramPanchayats.Add(new GramPanchayat
        {
            TenantId = tenantId,
            LgdCode = lgdCode,
            DistrictCode = "503",
            DistrictName = "अमरावती",
            TehsilCode = "4010",
            TehsilName = "अंजनगाव सुर्जी",
            PanchayatNameEn = $"Mutation Village {lgdCode}",
            PanchayatNameMr = $"चिंचोली {lgdCode}",
            GramsevakName = "श्री. जी. एस. मोरे",
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

        await seeder.SeedDefaultsForTenantAsync(tenantId, CancellationToken.None);
        await db.SaveChangesAsync();

        return (tenantId, gramsevakId, operatorId);
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
    public async Task MutationLifecycle_15DayNoticeGate_Objection_ResolutionApproval_ProtectedAsset_AndPdfRegister()
    {
        (Guid tenantId, Guid gramsevakId, Guid operatorId) = await SeedMutationTenantAsync();
        (Guid otherTenantId, Guid otherUserId, _) = await SeedMutationTenantAsync();
        using HttpClient client = factory.CreateClient();

        // Seed citizens & initial property owned by Old Owner (100%)
        CitizenDto oldOwner = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand("CIT-OLD", "1", "अमोल देशमुख", "Amol Deshmukh", "Male", "9800000001", "101020203030", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        CitizenDto newHeir1 = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand("CIT-NEW1", "1", "ऋषिकेश सावरकर", "Rishikesh Savarkar", "Male", "9800000002", "404050506060", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        CitizenDto newHeir2 = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand("CIT-NEW2", "1", "अनिता सावरकर", "Anita Savarkar", "Female", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        PropertyDto property = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: "1",
                PropertyNo: "55",
                BuiltUpAreaSqFt: 300m,
                OpenPlotAreaSqFt: 100m,
                Owners: [new(oldOwner.Id, 100m, true)],
                Floors: [new(0, "तळमजला", "RCC_PUCCA", "RESIDENTIAL", 20m, 15m, null, 2021)],
                CalculateTax: true))))
            .Content.ReadFromJsonAsync<PropertyDto>())!;

        // 1. Upload private Sale Deed PDF to ProtectedAsset Vault (/api/v1/assets/upload)
        byte[] samplePdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF");
        using MultipartFormDataContent multipart = new();
        multipart.Add(new StringContent("MUTATION_DOC"), "category");
        ByteArrayContent pdfContent = new(samplePdfBytes);
        pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipart.Add(pdfContent, "file", "kharedikhat.pdf");

        HttpRequestMessage uploadReq = CreateTenantRequest(HttpMethod.Post, "/api/v1/assets/upload", operatorId, ApplicationRole.GP_Operator, tenantId);
        uploadReq.Content = multipart;
        HttpResponseMessage uploadResp = await client.SendAsync(uploadReq);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);
        UploadedAssetDto uploadedAsset = (await uploadResp.Content.ReadFromJsonAsync<UploadedAssetDto>())!;

        // Cross-tenant access to ProtectedAsset must fail with 403 Forbidden
        HttpResponseMessage crossAsset = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/assets/{uploadedAsset.AssetKey}",
            otherUserId,
            ApplicationRole.Gramsevak,
            otherTenantId));
        Assert.Equal(HttpStatusCode.Forbidden, crossAsset.StatusCode);

        // 2. Initiate Mutation by GP_Operator (60% + 40% proposed new owners)
        HttpResponseMessage createResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/mutations",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new CreateMutationCommand(
                PropertyId: property.Id,
                MutationType: "SaleDeed",
                ProposedOwners:
                [
                    new(newHeir1.Id, 60m, true),
                    new(newHeir2.Id, 40m, false),
                ],
                DocumentReferenceNo: "DEED-2026-09",
                DocumentDate: new DateOnly(2026, 11, 2),
                Remarks: "नोंदणीकृत खरेदीखत क्र. ९ अन्वये हस्तांतरण",
                SupportingAssetKeys: [uploadedAsset.AssetKey])));

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        MutationDetailDto created = (await createResp.Content.ReadFromJsonAsync<MutationDetailDto>())!;
        Assert.StartsWith("FR-2026-", created.ApplicationNo, StringComparison.Ordinal);
        Assert.Equal("NoticePublished", created.Status);
        Assert.False(created.IsNoticeWindowElapsed);
        Assert.True(created.DaysRemaining >= 14);
        Assert.Single(created.PreviousOwners);
        Assert.Equal("अमोल देशमुख", created.PreviousOwners[0].FullNameMr);
        Assert.Single(created.SupportingAssets);

        // Dashboard pending mutations should show 1
        DashboardSummaryDto dash = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/dashboard/summary",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<DashboardSummaryDto>())!;
        Assert.Equal(1, dash.PendingMutations);

        // 3. Premature approval inside 15-day notice period must return 409 Conflict
        HttpResponseMessage prematureResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/mutations/{created.Id}/approve",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new ApproveMutationRequest("ठराव क्र. ९/१", new DateOnly(2026, 11, 17))));
        Assert.Equal(HttpStatusCode.Conflict, prematureResp.StatusCode);

        // GP_Operator cannot approve even after notice period (RBAC)
        HttpResponseMessage opApproveResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/mutations/{created.Id}/approve",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new ApproveMutationRequest("ठराव क्र. ९/१", new DateOnly(2026, 11, 17))));
        Assert.Equal(HttpStatusCode.Forbidden, opApproveResp.StatusCode);

        // 4. Record citizen objection during notice period
        HttpResponseMessage objResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/mutations/{created.Id}/objection",
            operatorId,
            ApplicationRole.GP_Operator,
            tenantId,
            new RecordMutationObjectionRequest("सहहिस्सेदार संमती कागदपत्र तपासणी प्रलंबित")));
        Assert.Equal(HttpStatusCode.OK, objResp.StatusCode);
        MutationDetailDto afterObj = (await objResp.Content.ReadFromJsonAsync<MutationDetailDto>())!;
        Assert.Equal("ObjectionRecorded", afterObj.Status);
        Assert.True(afterObj.HasObjection);

        // Simulate 15-day statutory notice elapsing in DB (no backdoor API bypass!)
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
            PropertyMutation? mutEntity = await db.PropertyMutations.FirstOrDefaultAsync(m => m.Id == created.Id);
            Assert.NotNull(mutEntity);
            mutEntity.NoticeIssuedAtUtc = DateTimeOffset.UtcNow.AddDays(-16);
            mutEntity.NoticeEndsAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        // 5. Approval with unresolved objection without clearance notes must fail with 409 Conflict
        HttpResponseMessage unresolvedResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/mutations/{created.Id}/approve",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new ApproveMutationRequest("ठराव क्र. ९/१", new DateOnly(2026, 12, 5))));
        Assert.Equal(HttpStatusCode.Conflict, unresolvedResp.StatusCode);

        // 6. Approve with GP Resolution + objection clearance -> atomically transfers ownership
        HttpResponseMessage approveResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/mutations/{created.Id}/approve",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new ApproveMutationRequest(
                "ठराव क्र. ९/१",
                new DateOnly(2026, 12, 5),
                "सर्व वारस व मूळ मालकाची प्रत्यक्ष संमती तपासली व आक्षेप निकाली काढला.")));
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        MutationDetailDto approved = (await approveResp.Content.ReadFromJsonAsync<MutationDetailDto>())!;
        Assert.Equal("Approved", approved.Status);
        Assert.Equal("ठराव क्र. ९/१", approved.ResolutionNo);

        // Property 360 now reflects the 2 new owners cleanly while preserving historical owner snapshot
        PropertyDetailDto propAfter = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{property.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<PropertyDetailDto>())!;
        Assert.Equal(2, propAfter.Owners.Count);
        Assert.Equal("ऋषिकेश सावरकर", propAfter.Owners.First(o => o.IsPrimaryOwner).FullNameMr);

        // Historical inactive PropertyOwner row & MutationApproved AuditLog exist in DB
        using (IServiceScope verifyScope = factory.Services.CreateScope())
        {
            AppDbContext db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            verifyScope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
            List<PropertyOwner> allOwners = await db.PropertyOwners
                .AsNoTracking()
                .Where(po => po.PropertyId == property.Id)
                .ToListAsync();
            Assert.Equal(3, allOwners.Count);
            Assert.Single(allOwners, po => !po.IsActive && po.CitizenId == oldOwner.Id);
            Assert.Equal(2, allOwners.Count(po => po.IsActive));

            bool hasAudit = await db.AuditLogs
                .AsNoTracking()
                .AnyAsync(l => l.TenantId == tenantId && l.EntityName == nameof(PropertyMutation) && l.Action == "MutationApproved");
            Assert.True(hasAudit);
        }

        // 7. Mutation Register QuestPDF A4 Landscape export works cleanly
        HttpResponseMessage regPdfResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/mutations/register.pdf?wardNo=1",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId));
        Assert.Equal(HttpStatusCode.OK, regPdfResp.StatusCode);
        Assert.Equal("application/pdf", regPdfResp.Content.Headers.ContentType?.MediaType);
        byte[] pdfBytes = await regPdfResp.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdfBytes, 0, 5));
        Assert.True(pdfBytes.Length > 4_000);
    }

    [Fact]
    public async Task MutationRejection_PreservesOriginalOwner_AndFailsOnNon100Share()
    {
        (Guid tenantId, Guid gramsevakId, _) = await SeedMutationTenantAsync();
        using HttpClient client = factory.CreateClient();

        CitizenDto c1 = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(null, "2", "प्रमोद कुलकर्णी", "Pramod Kulkarni", "Male", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        CitizenDto c2 = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/citizens",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateCitizenCommand(null, "2", "सुहास कुलकर्णी", "Suhas Kulkarni", "Male", DpdpConsentAccepted: true))))
            .Content.ReadFromJsonAsync<CitizenDto>())!;

        PropertyDto prop = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/properties",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreatePropertyCommand(
                WardNo: "2",
                PropertyNo: "90",
                BuiltUpAreaSqFt: 200m,
                OpenPlotAreaSqFt: 50m,
                Owners: [new(c1.Id, 100m, true)]))))
            .Content.ReadFromJsonAsync<PropertyDto>())!;

        // Proposed owners share != 100% -> 400 Bad Request
        HttpResponseMessage badShare = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/mutations",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateMutationCommand(prop.Id, "GiftDeed", [new(c2.Id, 80m, true)])));
        Assert.Equal(HttpStatusCode.BadRequest, badShare.StatusCode);

        // Initiate valid mutation then reject via GP Resolution
        MutationDetailDto initiated = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/mutations",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new CreateMutationCommand(prop.Id, "GiftDeed", [new(c2.Id, 100m, true)]))))
            .Content.ReadFromJsonAsync<MutationDetailDto>())!;

        HttpResponseMessage rejectResp = await client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            $"/api/v1/mutations/{initiated.Id}/reject",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId,
            new RejectMutationRequest("बक्षीसपत्र नोंदणीकृत नाही.", "ठराव क्र. १०/४", new DateOnly(2026, 11, 18))));
        Assert.Equal(HttpStatusCode.OK, rejectResp.StatusCode);

        MutationDetailDto rejected = (await rejectResp.Content.ReadFromJsonAsync<MutationDetailDto>())!;
        Assert.Equal("Rejected", rejected.Status);

        PropertyDetailDto propDetail = (await (await client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            $"/api/v1/properties/{prop.Id}",
            gramsevakId,
            ApplicationRole.Gramsevak,
            tenantId))).Content.ReadFromJsonAsync<PropertyDetailDto>())!;
        Assert.Single(propDetail.Owners);
        Assert.Equal(c1.Id, propDetail.Owners[0].CitizenId);
    }
}

