using System.Globalization;
using System.Text.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Mutations;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Infrastructure.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class MutationRepository(
    AppDbContext dbContext,
    IPiiProtectionService piiProtectionService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IMutationRepository
{
    private static readonly JsonSerializerOptions JsonWebOptions = new(JsonSerializerDefaults.Web);

    public async Task<PagedMutationsDto> SearchAsync(
        Guid tenantId,
        string? status,
        string? search,
        string? wardNo,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from m in dbContext.PropertyMutations.AsNoTracking()
            join p in dbContext.Properties.AsNoTracking() on m.PropertyId equals p.Id
            select new { m, p.WardNo, p.PropertyNo };

        if (!string.IsNullOrWhiteSpace(status))
        {
            string st = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.m.Status.ToLower() == st);
        }

        if (!string.IsNullOrWhiteSpace(wardNo))
        {
            string w = wardNo.Trim().ToLowerInvariant();
            query = query.Where(x => x.WardNo.ToLower() == w);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.m.ApplicationNo.ToLower().Contains(q) ||
                x.WardNo.ToLower().Contains(q) ||
                x.PropertyNo.ToLower().Contains(q) ||
                (x.m.DocumentReferenceNo != null && x.m.DocumentReferenceNo.ToLower().Contains(q)) ||
                (x.m.ResolutionNo != null && x.m.ResolutionNo.ToLower().Contains(q)) ||
                x.m.PreviousOwnersSnapshotJson.ToLower().Contains(q) ||
                x.m.ProposedOwnersJson.ToLower().Contains(q));
        }

        var rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        List<MutationListItemDto> ordered =
        [
            .. rows
                .OrderByDescending(x => x.m.NoticeIssuedAtUtc)
                .ThenByDescending(x => x.m.ApplicationNo, StringComparer.Ordinal)
                .Select(x =>
                {
                    List<MutationOwnerSnapshotItem> prev = ParseOwners(x.m.PreviousOwnersSnapshotJson);
                    List<MutationOwnerSnapshotItem> prop = ParseOwners(x.m.ProposedOwnersJson);
                    int daysRemaining = ComputeDaysRemaining(x.m.NoticeEndsAtUtc, utcNow);
                    return new MutationListItemDto(
                        Id: x.m.Id,
                        PropertyId: x.m.PropertyId,
                        WardNo: x.WardNo,
                        PropertyNo: x.PropertyNo,
                        ApplicationNo: x.m.ApplicationNo,
                        MutationType: x.m.MutationType,
                        PreviousOwnersSummaryMr: SummarizeOwnersMr(prev),
                        PreviousOwnersSummaryEn: SummarizeOwnersEn(prev),
                        ProposedOwnersSummaryMr: SummarizeOwnersMr(prop),
                        ProposedOwnersSummaryEn: SummarizeOwnersEn(prop),
                        DocumentReferenceNo: x.m.DocumentReferenceNo,
                        DocumentDate: x.m.DocumentDate,
                        Status: x.m.Status,
                        NoticeIssuedAtUtc: x.m.NoticeIssuedAtUtc,
                        NoticeEndsAtUtc: x.m.NoticeEndsAtUtc,
                        DaysRemaining: daysRemaining,
                        IsNoticeWindowElapsed: utcNow >= x.m.NoticeEndsAtUtc,
                        HasObjection: x.m.HasObjection,
                        ResolutionNo: x.m.ResolutionNo,
                        ResolutionDate: x.m.ResolutionDate,
                        CreatedAtUtc: x.m.CreatedAtUtc);
                }),
        ];

        int totalCount = ordered.Count;
        List<MutationListItemDto> pageItems = [.. ordered.Skip((page - 1) * pageSize).Take(pageSize)];
        return new PagedMutationsDto(pageItems, totalCount, page, pageSize);
    }

    public async Task<MutationDetailDto?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (
            from mut in dbContext.PropertyMutations.AsNoTracking()
            join prop in dbContext.Properties.AsNoTracking() on mut.PropertyId equals prop.Id
            where mut.Id == id
            select new
            {
                Mutation = mut,
                prop.WardNo,
                prop.PropertyNo,
                prop.StreetOrLaneName,
                prop.PropertyDescription,
                prop.TotalAreaSqFt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        PropertyMutation m = row.Mutation;
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;

        return new MutationDetailDto(
            Id: m.Id,
            TenantId: m.TenantId,
            PropertyId: m.PropertyId,
            WardNo: row.WardNo,
            PropertyNo: row.PropertyNo,
            StreetOrLaneName: row.StreetOrLaneName,
            PropertyDescription: row.PropertyDescription,
            TotalAreaSqFt: row.TotalAreaSqFt,
            ApplicationNo: m.ApplicationNo,
            MutationType: m.MutationType,
            PreviousOwners: ParseOwners(m.PreviousOwnersSnapshotJson),
            ProposedOwners: ParseOwners(m.ProposedOwnersJson),
            DocumentReferenceNo: m.DocumentReferenceNo,
            DocumentDate: m.DocumentDate,
            Remarks: m.Remarks,
            SupportingAssets: ParseAssets(m.SupportingAssetIdsJson),
            Status: m.Status,
            NoticeIssuedAtUtc: m.NoticeIssuedAtUtc,
            NoticeEndsAtUtc: m.NoticeEndsAtUtc,
            DaysRemaining: ComputeDaysRemaining(m.NoticeEndsAtUtc, utcNow),
            IsNoticeWindowElapsed: utcNow >= m.NoticeEndsAtUtc,
            HasObjection: m.HasObjection,
            ObjectionSummary: m.ObjectionSummary,
            ObjectionRecordedAtUtc: m.ObjectionRecordedAtUtc,
            ResolutionNo: m.ResolutionNo,
            ResolutionDate: m.ResolutionDate,
            RejectionReason: m.RejectionReason,
            ApprovedAtUtc: m.ApprovedAtUtc,
            CreatedAtUtc: m.CreatedAtUtc);
    }

    public Task<PropertyMutation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.PropertyMutations.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<string> GenerateNextApplicationNoAsync(int startYear, CancellationToken cancellationToken)
    {
        string prefix = $"FR-{startYear}-";
        List<string> existing = await dbContext.PropertyMutations
            .AsNoTracking()
            .Where(m => m.ApplicationNo.StartsWith(prefix))
            .Select(m => m.ApplicationNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int maxSeq = 0;
        foreach (string appNo in existing)
        {
            if (int.TryParse(appNo[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int num) && num > maxSeq)
            {
                maxSeq = num;
            }
        }

        return $"{prefix}{(maxSeq + 1):D4}";
    }

    public async Task<IReadOnlyList<MutationOwnerSnapshotItem>> LoadCurrentPropertyOwnersAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        var raw = await (
            from po in dbContext.PropertyOwners.AsNoTracking()
            join c in dbContext.Citizens.AsNoTracking() on po.CitizenId equals c.Id
            where po.PropertyId == propertyId && po.IsActive
            select new
            {
                c.Id,
                c.CitizenCode,
                c.FullNameMr,
                c.FullNameEn,
                c.AadhaarLast4,
                po.OwnershipSharePct,
                po.IsPrimaryOwner,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. raw
                .OrderByDescending(x => x.IsPrimaryOwner)
                .ThenByDescending(x => x.OwnershipSharePct)
                .Select(x => new MutationOwnerSnapshotItem(
                    CitizenId: x.Id,
                    CitizenCode: x.CitizenCode,
                    FullNameMr: x.FullNameMr,
                    FullNameEn: x.FullNameEn,
                    AadhaarMasked: string.IsNullOrWhiteSpace(x.AadhaarLast4) ? null : $"XXXX-XXXX-{x.AadhaarLast4}",
                    OwnershipSharePct: x.OwnershipSharePct,
                    IsPrimaryOwner: x.IsPrimaryOwner)),
        ];
    }

    public async Task<IReadOnlyList<MutationSupportingAssetDto>> ValidateTenantAssetKeysAsync(
        Guid tenantId,
        IReadOnlyCollection<string> assetKeys,
        CancellationToken cancellationToken)
    {
        List<string> normalizedKeys = [.. assetKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct(StringComparer.Ordinal)];
        if (normalizedKeys.Count == 0)
        {
            return [];
        }

        return await dbContext.ProtectedAssets
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && normalizedKeys.Contains(a.AssetKey))
            .Select(a => new MutationSupportingAssetDto(a.AssetKey, a.FileName, a.ContentType, a.SizeBytes))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(PropertyMutation mutation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await dbContext.PropertyMutations.AddAsync(mutation, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task ApproveMutationAtomicallyAsync(
        PropertyMutation mutation,
        IReadOnlyList<PropertyOwner> replacementOwners,
        string auditDetailsJson,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? ownedTx = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            List<PropertyOwner> oldOwners = await dbContext.PropertyOwners
                .Where(po => po.PropertyId == mutation.PropertyId && po.IsActive)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (PropertyOwner old in oldOwners)
            {
                old.IsActive = false;
            }

            if (replacementOwners.Count > 0)
            {
                await dbContext.PropertyOwners.AddRangeAsync(replacementOwners, cancellationToken).ConfigureAwait(false);
            }

            await dbContext.AuditLogs.AddAsync(new AuditLog
            {
                TenantId = mutation.TenantId,
                EntityName = nameof(PropertyMutation),
                EntityId = mutation.Id.ToString(),
                Action = "MutationApproved",
                ActorId = currentUserService.UserId?.ToString(),
                ActorRole = currentUserService.Role?.ToString(),
                CorrelationId = currentUserService.CorrelationId,
                IpAddress = currentUserService.IpAddress,
                UserAgent = currentUserService.UserAgent,
                OldValuesJson = mutation.PreviousOwnersSnapshotJson,
                NewValuesJson = auditDetailsJson,
                TimestampUtc = dateTimeProvider.UtcNow,
            }, cancellationToken).ConfigureAwait(false);

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (ownedTx is not null)
            {
                await ownedTx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ownedTx is not null)
            {
                await ownedTx.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<MutationRegisterPdfDocumentModel> LoadRegisterPdfModelAsync(
        Guid tenantId,
        string? wardNo,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        Tenant tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {tenantId} was not found.");

        GramPanchayat? gp = await dbContext.GramPanchayats
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        AssessmentYear? year = await dbContext.AssessmentYears
            .AsNoTracking()
            .Where(y => y.TenantId == tenantId)
            .OrderByDescending(y => y.IsActive)
            .ThenByDescending(y => y.StartYear)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var query =
            from m in dbContext.PropertyMutations.AsNoTracking()
            join p in dbContext.Properties.AsNoTracking() on m.PropertyId equals p.Id
            select new { m, p.WardNo, p.PropertyNo, p.PropertyDescription };

        if (!string.IsNullOrWhiteSpace(wardNo))
        {
            string w = wardNo.Trim().ToLowerInvariant();
            query = query.Where(x => x.WardNo.ToLower() == w);
        }

        var raw = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        List<MutationRegisterPdfRow> rows = [];
        foreach (var item in raw.OrderBy(x => x.m.ApplicationNo, StringComparer.Ordinal))
        {
            DateOnly entryDate = item.m.DocumentDate ?? DateOnly.FromDateTime(item.m.NoticeIssuedAtUtc.LocalDateTime);
            if (fromDate.HasValue && entryDate < fromDate.Value)
            {
                continue;
            }
            if (toDate.HasValue && entryDate > toDate.Value)
            {
                continue;
            }

            List<MutationOwnerSnapshotItem> prev = ParseOwners(item.m.PreviousOwnersSnapshotJson);
            List<MutationOwnerSnapshotItem> prop = ParseOwners(item.m.ProposedOwnersJson);
            string typeMr = FormatMutationTypeMr(item.m.MutationType);
            string docNoPart = string.IsNullOrWhiteSpace(item.m.DocumentReferenceNo) ? "-" : item.m.DocumentReferenceNo;
            string brief = $"{typeMr} | क्र. {docNoPart} | दि. {entryDate:dd/MM/yyyy}"
                + (string.IsNullOrWhiteSpace(item.m.Remarks) ? string.Empty : $" — {item.m.Remarks}");

            string resolutionText = !string.IsNullOrWhiteSpace(item.m.ResolutionNo)
                ? $"ठराव क्र. {item.m.ResolutionNo} / {item.m.ResolutionDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "-"}"
                : FormatStatusMr(item.m.Status);

            rows.Add(new MutationRegisterPdfRow(
                ApplicationNo: item.m.ApplicationNo,
                PropertyNoDisplay: $"{item.WardNo}/{item.PropertyNo}",
                EntryDateDisplay: entryDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                MutationTypeLabelMr: typeMr,
                DocumentReferenceNo: item.m.DocumentReferenceNo,
                PropertyDescriptionMr: string.IsNullOrWhiteSpace(item.PropertyDescription) ? "-" : item.PropertyDescription,
                PreviousOwnersMr: SummarizeOwnersMr(prev),
                ProposedOwnersMr: SummarizeOwnersMr(prop),
                BriefSummaryMr: brief,
                ResolutionDisplay: resolutionText));
        }

        byte[]? gsSig = await StatutoryPrintAssetHelper
            .TryLoadProtectedImageAsync(dbContext, piiProtectionService, tenantId, gp?.GramsevakSignatureKey, cancellationToken)
            .ConfigureAwait(false);

        return new MutationRegisterPdfDocumentModel(
            TenantId: tenantId,
            PanchayatNameMr: gp?.PanchayatNameMr ?? tenant.NameMr,
            VillageNameMr: gp?.PanchayatNameMr ?? tenant.NameMr,
            TehsilName: gp?.TehsilName ?? "-",
            DistrictName: gp?.DistrictName ?? "-",
            LgdCode: gp?.LgdCode ?? tenant.LgdCode,
            AssessmentYearLabel: year?.YearLabel ?? $"{DateTime.UtcNow.Year}-{(DateTime.UtcNow.Year + 1) % 100:00}",
            WardFilterLabel: string.IsNullOrWhiteSpace(wardNo) ? null : wardNo.Trim(),
            Rows: rows,
            GramsevakSignatureBytes: gsSig);
    }

    private static List<MutationOwnerSnapshotItem> ParseOwners(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<MutationOwnerSnapshotItem>>(json, JsonWebOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<MutationSupportingAssetDto> ParseAssets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<MutationSupportingAssetDto>>(json, JsonWebOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string SummarizeOwnersMr(IReadOnlyList<MutationOwnerSnapshotItem> owners) =>
        owners.Count == 0
            ? "-"
            : string.Join(", ", owners.Select(o =>
                owners.Count > 1 ? $"{o.FullNameMr} ({o.OwnershipSharePct:0.##}%)" : o.FullNameMr));

    private static string SummarizeOwnersEn(IReadOnlyList<MutationOwnerSnapshotItem> owners) =>
        owners.Count == 0
            ? "-"
            : string.Join(", ", owners.Select(o => o.FullNameEn));

    private static int ComputeDaysRemaining(DateTimeOffset endsAtUtc, DateTimeOffset utcNow) =>
        utcNow >= endsAtUtc ? 0 : Math.Max(1, (int)Math.Ceiling((endsAtUtc - utcNow).TotalDays));

    private static string FormatMutationTypeMr(string mutationType) => mutationType switch
    {
        "Heirship" => "वारस नोंद",
        "SaleDeed" => "खरेदीखत",
        "GiftDeed" => "बक्षीसपत्र",
        "ReleaseDeed" => "हक्कसोडपत्र",
        "CourtDecree" => "कोर्ट हुकूम",
        "Partition" => "वाटपपत्र (Partition)",
        _ => mutationType,
    };

    private static string FormatStatusMr(string status) => status switch
    {
        "NoticePublished" => "१५-दिवस जाहीर नोटीस सुरू",
        "ObjectionRecorded" => "आक्षेप नोंदवला (प्रलंबित)",
        "Approved" => "मंजूर",
        "Rejected" => "नामंजूर",
        _ => status,
    };
}
