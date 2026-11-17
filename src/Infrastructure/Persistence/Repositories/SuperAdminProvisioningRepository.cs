using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class SuperAdminProvisioningRepository(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDefaultFormulaSeeder formulaSeeder,
    IDateTimeProvider dateTimeProvider)
    : ISuperAdminProvisioningRepository
{
    public Task<IReadOnlyList<SuperAdminTenantSummaryDto>> ListTenantsAsync(CancellationToken cancellationToken) =>
        ListTenantsFilteredAsync(
            search: null,
            district: null,
            taluka: null,
            status: null,
            includeDeleted: false,
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<SuperAdminTenantSummaryDto>> ListTenantsFilteredAsync(
        string? search,
        string? district,
        string? taluka,
        string? status,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = dateTimeProvider.UtcNow;

        List<Tenant> tenants = await dbContext.Tenants
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!includeDeleted)
        {
            tenants = tenants.Where(t => !t.IsDeleted).ToList();
        }

        List<GramPanchayat> gps = await dbContext.GramPanchayats
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, GramPanchayat> gpByTenant = gps
            .GroupBy(g => g.TenantId)
            .ToDictionary(g => g.Key, g => g.First());

        var propsRaw = await dbContext.Properties
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Select(p => p.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, int> propsByTenant = propsRaw.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var citsRaw = await dbContext.Citizens
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(c => c.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, int> citsByTenant = citsRaw.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        List<Guid> activeYearIds = await dbContext.AssessmentYears
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(y => y.IsActive)
            .Select(y => y.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        HashSet<Guid> activeYearSet = [.. activeYearIds];

        var demandsRaw = await dbContext.PropertyDemands
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(d => new
            {
                d.TenantId,
                d.AssessmentYearId,
                d.ArrearsBalance,
                d.CurrentDemand,
                d.NoticeFee,
                d.WarrantFee,
                d.PenaltyAmount,
                d.RebateAmount,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> demandByTenant = demandsRaw
            .Where(d => activeYearSet.Count == 0 || activeYearSet.Contains(d.AssessmentYearId))
            .GroupBy(d => d.TenantId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(d => Math.Max(0m, d.ArrearsBalance + d.CurrentDemand + d.NoticeFee + d.WarrantFee + d.PenaltyAmount - d.RebateAmount)));

        var receiptsRaw = await dbContext.TaxReceipts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => !r.IsCancelled)
            .Select(r => new { r.TenantId, r.AssessmentYearId, r.TotalPaid })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> collectedByTenant = receiptsRaw
            .Where(r => activeYearSet.Count == 0 || activeYearSet.Contains(r.AssessmentYearId))
            .GroupBy(r => r.TenantId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalPaid));

        var adminMembers = await (
            from m in dbContext.TenantMemberships.AsNoTracking()
            join u in dbContext.Users.AsNoTracking() on m.UserId equals u.Id
            where m.IsActive && (m.Role == ApplicationRole.Gramsevak || m.Role == ApplicationRole.GP_Admin)
            select new { m.TenantId, m.Role, m.IsDefault, u.Username, u.Phone })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var adminByTenant = adminMembers
            .GroupBy(x => x.TenantId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.Role == ApplicationRole.Gramsevak ? 0 : 1)
                    .ThenByDescending(x => x.IsDefault)
                    .First());

        IEnumerable<SuperAdminTenantSummaryDto> mapped = tenants
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t =>
            {
                gpByTenant.TryGetValue(t.Id, out GramPanchayat? gp);
                adminByTenant.TryGetValue(t.Id, out var adm);
                return new SuperAdminTenantSummaryDto(
                    TenantId: t.Id,
                    TenantCode: t.TenantCode,
                    LgdCode: t.LgdCode,
                    PanchayatNameEn: t.NameEn,
                    PanchayatNameMr: t.NameMr,
                    DistrictName: gp?.DistrictName ?? string.Empty,
                    TehsilName: gp?.TehsilName ?? string.Empty,
                    Status: t.Status.ToString(),
                    SubscriptionExpiresAtUtc: t.SubscriptionExpiresAtUtc,
                    SetupProgressPct: gp?.SetupProgressPct ?? 0,
                    EffectiveStatus: t.GetEffectiveStatus(now),
                    DaysUntilExpiry: t.GetDaysUntilExpiry(now),
                    IsTrial: t.IsTrial,
                    IsDeleted: t.IsDeleted,
                    DeletedAtUtc: t.DeletedAtUtc,
                    AdminUsername: adm?.Username,
                    AdminPhone: adm?.Phone ?? gp?.GramsevakMobile,
                    TotalWards: gp?.TotalWards ?? 0,
                    PopulationCensus: gp?.PopulationCensus,
                    PropertiesCount: propsByTenant.GetValueOrDefault(t.Id, 0),
                    CitizensCount: citsByTenant.GetValueOrDefault(t.Id, 0),
                    TaxDemanded: demandByTenant.GetValueOrDefault(t.Id, 0m),
                    TaxCollected: collectedByTenant.GetValueOrDefault(t.Id, 0m),
                    ReferredByAgentId: t.ReferredByAgentId,
                    ReferralCodeUsed: t.ReferralCodeUsed);
            });

        if (!string.IsNullOrWhiteSpace(district))
        {
            string d = district.Trim();
            mapped = mapped.Where(t => t.DistrictName.Contains(d, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(taluka))
        {
            string tl = taluka.Trim();
            mapped = mapped.Where(t => t.TehsilName.Contains(tl, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            string st = status.Trim();
            mapped = st.Equals("TRASH", StringComparison.OrdinalIgnoreCase) || st.Equals("DELETED", StringComparison.OrdinalIgnoreCase)
                ? mapped.Where(t => t.IsDeleted)
                : mapped.Where(t =>
                    string.Equals(t.EffectiveStatus, st, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.Status, st, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim();
            mapped = mapped.Where(t =>
                t.LgdCode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.PanchayatNameEn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.PanchayatNameMr.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.DistrictName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.TehsilName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (t.AdminUsername is not null && t.AdminUsername.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (t.AdminPhone is not null && t.AdminPhone.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        return [.. mapped];
    }

    public async Task<SuperAdminDashboardStatsDto> GetStatewideStatsAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = dateTimeProvider.UtcNow;

        List<Tenant> allTenants = await dbContext.Tenants
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Tenant> liveTenants = [.. allTenants.Where(t => !t.IsDeleted)];
        HashSet<Guid> liveTenantIds = [.. liveTenants.Select(t => t.Id)];

        int trashCount = allTenants.Count(t => t.IsDeleted);
        int activeVillages = liveTenants.Count(t => t.IsLicenseOperational(now) && t.Status == TenantStatus.Active);
        int pendingActivation = liveTenants.Count(t => t.Status == TenantStatus.PendingApproval);
        int suspendedVillages = liveTenants.Count(t => t.Status == TenantStatus.Suspended);
        int expiredVillages = liveTenants.Count(t =>
            t.Status == TenantStatus.Expired ||
            (t.Status == TenantStatus.Active && t.SubscriptionExpiresAtUtc.HasValue && t.SubscriptionExpiresAtUtc.Value < now));

        DateTimeOffset cutoff30 = now.AddDays(30);
        int expiringIn30Days = liveTenants.Count(t =>
            t.SubscriptionExpiresAtUtc.HasValue &&
            t.SubscriptionExpiresAtUtc.Value >= now &&
            t.SubscriptionExpiresAtUtc.Value <= cutoff30);

        int pendingSubmissions = await dbContext.TenantPaymentSubmissions
            .AsNoTracking()
            .CountAsync(s => s.Status == TenantPaymentSubmission.StatusPending, cancellationToken)
            .ConfigureAwait(false);

        List<Guid> liveCitTenants = await dbContext.Citizens
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(c => c.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int totalCitizens = liveCitTenants.Count(id => liveTenantIds.Contains(id));

        List<Guid> livePropTenants = await dbContext.Properties
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Select(p => p.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int totalProperties = livePropTenants.Count(id => liveTenantIds.Contains(id));

        List<Guid> activeYearIds = await dbContext.AssessmentYears
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(y => y.IsActive)
            .Select(y => y.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        HashSet<Guid> activeYearSet = [.. activeYearIds];

        var demandsRaw = await dbContext.PropertyDemands
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(d => new
            {
                d.TenantId,
                d.AssessmentYearId,
                d.ArrearsBalance,
                d.CurrentDemand,
                d.NoticeFee,
                d.WarrantFee,
                d.PenaltyAmount,
                d.RebateAmount,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal demandedStatewide = demandsRaw
            .Where(d => liveTenantIds.Contains(d.TenantId) &&
                        (activeYearSet.Count == 0 || activeYearSet.Contains(d.AssessmentYearId)))
            .Sum(d => Math.Max(0m, d.ArrearsBalance + d.CurrentDemand + d.NoticeFee + d.WarrantFee + d.PenaltyAmount - d.RebateAmount));

        var receiptsRaw = await dbContext.TaxReceipts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => !r.IsCancelled)
            .Select(r => new { r.TenantId, r.AssessmentYearId, r.TotalPaid })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal collectedStatewide = receiptsRaw
            .Where(r => liveTenantIds.Contains(r.TenantId) &&
                        (activeYearSet.Count == 0 || activeYearSet.Contains(r.AssessmentYearId)))
            .Sum(r => r.TotalPaid);

        decimal collectionPct = demandedStatewide > 0m
            ? Math.Round((collectedStatewide / demandedStatewide) * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        List<decimal> revenueRows = await dbContext.TenantSubscriptionPayments
            .AsNoTracking()
            .Select(p => p.Amount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        decimal totalRevenue = revenueRows.Sum();

        return new SuperAdminDashboardStatsDto(
            TotalVillages: liveTenants.Count,
            ActiveVillages: activeVillages,
            PendingActivation: pendingActivation,
            ExpiredVillages: expiredVillages,
            SuspendedVillages: suspendedVillages,
            TrashCount: trashCount,
            PendingPaymentSubmissions: pendingSubmissions,
            ExpiringIn30Days: expiringIn30Days,
            TotalCitizens: totalCitizens,
            TotalProperties: totalProperties,
            TotalTaxDemandedStatewide: demandedStatewide,
            TotalTaxCollectedStatewide: collectedStatewide,
            CollectionPercentage: collectionPct,
            TotalPlatformRevenue: totalRevenue);
    }

    public async Task<SuperAdminTenantDetailDto?> GetTenantDetailAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        List<SuperAdminTenantSummaryDto> summaries = [.. await ListTenantsFilteredAsync(
            search: null,
            district: null,
            taluka: null,
            status: null,
            includeDeleted: true,
            cancellationToken: cancellationToken).ConfigureAwait(false)];

        SuperAdminTenantSummaryDto? summary = summaries.FirstOrDefault(s => s.TenantId == tenantId);
        if (summary is null)
        {
            return null;
        }

        GramPanchayat? gp = await dbContext.GramPanchayats
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        AssessmentYear? activeYear = await dbContext.AssessmentYears
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .OrderByDescending(a => a.StartYear)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        int assetsCount = await dbContext.ProtectedAssets
            .AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        int bankCount = await dbContext.BankAccounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(b => b.TenantId == tenantId && b.IsActive, cancellationToken)
            .ConfigureAwait(false);

        List<SuperAdminTenantUserDto> users = await (
            from m in dbContext.TenantMemberships.AsNoTracking()
            join u in dbContext.Users.AsNoTracking() on m.UserId equals u.Id
            where m.TenantId == tenantId
            select new SuperAdminTenantUserDto(
                u.Id,
                u.Username,
                u.FullNameEn,
                u.FullNameMr,
                u.Phone,
                u.Email,
                m.Role.ToString(),
                u.IsActive && m.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<TenantSubscriptionPayment> subPayments = await dbContext.TenantSubscriptionPayments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<TenantSubscriptionHistoryItemDto> paymentDtos = subPayments
            .OrderByDescending(p => p.PaymentDateUtc)
            .Select(p => new TenantSubscriptionHistoryItemDto(
                p.Id,
                p.PlanName,
                p.PaymentMode,
                p.Amount,
                p.GstAmount,
                p.DurationDays,
                p.PaymentReference,
                p.InvoiceNo,
                p.PaymentDateUtc,
                p.Notes))
            .ToList();

        IReadOnlyList<TenantPaymentSubmissionDto> submissions = await ListPaymentSubmissionsAsync(
            status: null,
            tenantId: tenantId,
            search: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        IReadOnlyList<SuperAdminAuditLogItemDto> recentLogs = await ListAuditLogsAsync(
            tenantId: tenantId,
            action: null,
            entityName: null,
            fromDate: null,
            toDate: null,
            search: null,
            limit: 15,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new SuperAdminTenantDetailDto(
            Summary: summary,
            DistrictCode: gp?.DistrictCode ?? string.Empty,
            TehsilCode: gp?.TehsilCode ?? string.Empty,
            Pincode: gp?.Pincode,
            CensusCode: gp?.CensusCode,
            OfficeAddress: gp?.OfficeAddress,
            SarpanchName: gp?.SarpanchName,
            SarpanchMobile: gp?.SarpanchMobile,
            GramsevakName: gp?.GramsevakName,
            GramsevakMobile: gp?.GramsevakMobile,
            PrimaryUpiId: gp?.UpiId,
            ActiveAssessmentYear: activeYear?.YearLabel,
            ProtectedAssetsCount: assetsCount,
            BankAccountsCount: bankCount,
            Users: users,
            SubscriptionPayments: paymentDtos,
            PaymentSubmissions: submissions,
            RecentAuditLogs: recentLogs);
    }

    public Task<Tenant?> FindTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public Task<Tenant?> FindActiveTenantForImpersonationAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, cancellationToken);

    public async Task<(User User, TenantMembership Membership)?> FindTenantAdminUserAsync(
        Guid tenantId,
        string? targetUsername,
        CancellationToken cancellationToken)
    {
        var query =
            from m in dbContext.TenantMemberships
            join u in dbContext.Users on m.UserId equals u.Id
            where m.TenantId == tenantId && m.IsActive
            select new { User = u, Membership = m };

        var rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(targetUsername))
        {
            string normalized = targetUsername.Trim();
            var match = rows.FirstOrDefault(r =>
                string.Equals(r.User.Username, normalized, StringComparison.OrdinalIgnoreCase));
            return match is null ? null : (match.User, match.Membership);
        }

        var defaultAdmin = rows
            .OrderBy(r => r.Membership.Role switch
            {
                ApplicationRole.Gramsevak => 0,
                ApplicationRole.GP_Admin => 1,
                _ => 2,
            })
            .ThenByDescending(r => r.Membership.IsDefault)
            .First();

        return (defaultAdmin.User, defaultAdmin.Membership);
    }

    public async Task RevokeAllUserRefreshTokensAsync(
        Guid userId,
        string reason,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        List<RefreshToken> active = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (RefreshToken token in active)
        {
            token.RevokedAtUtc = revokedAtUtc;
            token.RevokedReason = reason;
        }
    }

    public async Task<IReadOnlyList<TenantPaymentSubmissionDto>> ListPaymentSubmissionsAsync(
        string? status,
        Guid? tenantId,
        string? search,
        CancellationToken cancellationToken)
    {
        List<TenantPaymentSubmission> subs = await dbContext.TenantPaymentSubmissions
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenantId.HasValue)
        {
            subs = [.. subs.Where(s => s.TenantId == tenantId.Value)];
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            string st = status.Trim();
            subs = [.. subs.Where(s => string.Equals(s.Status, st, StringComparison.OrdinalIgnoreCase))];
        }

        Dictionary<Guid, Tenant> tenantsById = await dbContext.Tenants
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, cancellationToken)
            .ConfigureAwait(false);

        List<GramPanchayat> gps = await dbContext.GramPanchayats
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, string> districtByTenant = gps
            .GroupBy(g => g.TenantId)
            .ToDictionary(g => g.Key, g => g.First().DistrictName);

        HashSet<Guid> userIds = [.. subs.Select(s => s.SubmittedByUserId).Distinct()];
        Dictionary<Guid, string> usernames = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<TenantPaymentSubmissionDto> mapped = subs
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s =>
            {
                tenantsById.TryGetValue(s.TenantId, out Tenant? t);
                return new TenantPaymentSubmissionDto(
                    SubmissionId: s.Id,
                    TenantId: s.TenantId,
                    LgdCode: t?.LgdCode ?? string.Empty,
                    PanchayatNameEn: t?.NameEn ?? string.Empty,
                    PanchayatNameMr: t?.NameMr ?? string.Empty,
                    DistrictName: districtByTenant.GetValueOrDefault(s.TenantId, string.Empty),
                    SubmittedByUserId: s.SubmittedByUserId,
                    SubmittedByUsername: usernames.GetValueOrDefault(s.SubmittedByUserId, "-"),
                    PlanName: s.PlanName,
                    PaymentMode: s.PaymentMode,
                    Amount: s.Amount,
                    UtrReference: s.UtrReference,
                    PaymentDate: s.PaymentDate,
                    SlipAssetKey: s.SlipAssetKey,
                    Status: s.Status,
                    RejectionReason: s.RejectionReason,
                    ReviewedByUserId: s.ReviewedByUserId,
                    ReviewedAtUtc: s.ReviewedAtUtc,
                    ApprovedSubscriptionPaymentId: s.ApprovedSubscriptionPaymentId,
                    InvoiceNo: s.InvoiceNo,
                    Notes: s.Notes,
                    CreatedAtUtc: s.CreatedAtUtc);
            });

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim();
            mapped = mapped.Where(s =>
                s.UtrReference.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.LgdCode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.PanchayatNameEn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (s.InvoiceNo is not null && s.InvoiceNo.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        return [.. mapped];
    }

    public Task<TenantPaymentSubmission?> FindPaymentSubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken) =>
        dbContext.TenantPaymentSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

    public Task AddSubscriptionPaymentAsync(TenantSubscriptionPayment payment, CancellationToken cancellationToken) =>
        dbContext.TenantSubscriptionPayments.AddAsync(payment, cancellationToken).AsTask();

    public async Task<IReadOnlyList<SuperAdminAuditLogItemDto>> ListAuditLogsAsync(
        Guid? tenantId,
        string? action,
        string? entityName,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<AuditLog> query = dbContext.AuditLogs.AsNoTracking();

        if (tenantId.HasValue)
        {
            query = query.Where(l => l.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            string a = action.Trim();
            query = query.Where(l => l.Action == a);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            string e = entityName.Trim();
            query = query.Where(l => l.EntityName == e);
        }

        List<AuditLog> rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (fromDate.HasValue)
        {
            rows = [.. rows.Where(l => DateOnly.FromDateTime(l.TimestampUtc.UtcDateTime) >= fromDate.Value)];
        }

        if (toDate.HasValue)
        {
            rows = [.. rows.Where(l => DateOnly.FromDateTime(l.TimestampUtc.UtcDateTime) <= toDate.Value)];
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim();
            rows =
            [
                .. rows.Where(l =>
                    l.Action.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    l.EntityName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    l.EntityId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (l.ActorId is not null && l.ActorId.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (l.NewValuesJson is not null && l.NewValuesJson.Contains(q, StringComparison.OrdinalIgnoreCase))),
            ];
        }

        Dictionary<Guid, Tenant> tenantsById = await dbContext.Tenants
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, cancellationToken)
            .ConfigureAwait(false);

        int boundedLimit = Math.Clamp(limit, 1, 1000);

        return rows
            .OrderByDescending(l => l.TimestampUtc)
            .ThenByDescending(l => l.CreatedAtUtc)
            .Take(boundedLimit)
            .Select(l =>
            {
                Tenant? t = l.TenantId.HasValue ? tenantsById.GetValueOrDefault(l.TenantId.Value) : null;
                return new SuperAdminAuditLogItemDto(
                    Id: l.Id,
                    TenantId: l.TenantId,
                    TenantNameEn: t?.NameEn,
                    LgdCode: t?.LgdCode,
                    EntityName: l.EntityName,
                    EntityId: l.EntityId,
                    Action: l.Action,
                    ActorId: l.ActorId,
                    ActorRole: l.ActorRole,
                    OldValuesJson: l.OldValuesJson,
                    NewValuesJson: l.NewValuesJson,
                    IpAddress: l.IpAddress,
                    TimestampUtc: l.TimestampUtc);
            })
            .ToList();
    }

    public Task AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken) =>
        dbContext.AuditLogs.AddAsync(log, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        IDbContextTransaction? ownedTx = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            await work(cancellationToken).ConfigureAwait(false);

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

    public async Task PermanentDeleteTenantAsync(
        Tenant tenant,
        AuditLog deletionAuditLog,
        CancellationToken cancellationToken)
    {
        Guid tid = tenant.Id;

        await ExecuteInTransactionAsync(async ct =>
        {
            dbContext.TaxReceipts.RemoveRange(await dbContext.TaxReceipts.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.ReceiptCounters.RemoveRange(await dbContext.ReceiptCounters.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.PropertyDemandBills.RemoveRange(await dbContext.PropertyDemandBills.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.BillCounters.RemoveRange(await dbContext.BillCounters.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.PropertyDemands.RemoveRange(await dbContext.PropertyDemands.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.PropertyTaxCalcs.RemoveRange(await dbContext.PropertyTaxCalcs.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.PropertyOwners.RemoveRange(await dbContext.PropertyOwners.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.PropertyConstructions.RemoveRange(await dbContext.PropertyConstructions.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.PropertyMutations.RemoveRange(await dbContext.PropertyMutations.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.Properties.RemoveRange(await dbContext.Properties.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.Citizens.RemoveRange(await dbContext.Citizens.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.FormulaSettings.RemoveRange(await dbContext.FormulaSettings.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.ConstructionTypeMasters.RemoveRange(await dbContext.ConstructionTypeMasters.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.UsageWeightages.RemoveRange(await dbContext.UsageWeightages.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.DepreciationSlabs.RemoveRange(await dbContext.DepreciationSlabs.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.AreaSlabTaxes.RemoveRange(await dbContext.AreaSlabTaxes.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.WaterTaxMasters.RemoveRange(await dbContext.WaterTaxMasters.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.BankAccounts.RemoveRange(await dbContext.BankAccounts.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.AssessmentYears.RemoveRange(await dbContext.AssessmentYears.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.GramPanchayats.RemoveRange(await dbContext.GramPanchayats.IgnoreQueryFilters().Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.TenantPaymentSubmissions.RemoveRange(await dbContext.TenantPaymentSubmissions.Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.TenantSubscriptionPayments.RemoveRange(await dbContext.TenantSubscriptionPayments.Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.ProtectedAssets.RemoveRange(await dbContext.ProtectedAssets.Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.TenantMemberships.RemoveRange(await dbContext.TenantMemberships.Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));
            dbContext.TenantDatabaseMappings.RemoveRange(await dbContext.TenantDatabaseMappings.Where(x => x.TenantId == tid).ToListAsync(ct).ConfigureAwait(false));

            await dbContext.AuditLogs.AddAsync(deletionAuditLog, ct).ConfigureAwait(false);
            dbContext.Tenants.Remove(tenant);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsTenantLgdOrUsernameAsync(
        string lgdCode,
        string username,
        CancellationToken cancellationToken)
    {
        bool lgdExists = await dbContext.Tenants
            .AnyAsync(t => t.LgdCode == lgdCode, cancellationToken)
            .ConfigureAwait(false);

        if (lgdExists)
        {
            return true;
        }

        return await dbContext.Users
            .AnyAsync(u => u.Username == username, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ProvisionDirectTenantAsync(
        Tenant tenant,
        TenantDatabaseMapping mapping,
        GramPanchayat gp,
        AssessmentYear assessmentYear,
        User adminUser,
        TenantMembership membership,
        TenantSubscriptionPayment payment,
        CancellationToken cancellationToken)
    {
        Guid? previousTenant = tenantContext.CurrentTenantId;
        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            tenantContext.SetTenant(tenant.Id);

            await dbContext.Tenants.AddAsync(tenant, cancellationToken).ConfigureAwait(false);
            await dbContext.TenantDatabaseMappings.AddAsync(mapping, cancellationToken).ConfigureAwait(false);
            await dbContext.Users.AddAsync(adminUser, cancellationToken).ConfigureAwait(false);
            await dbContext.TenantMemberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);
            await dbContext.GramPanchayats.AddAsync(gp, cancellationToken).ConfigureAwait(false);
            await dbContext.AssessmentYears.AddAsync(assessmentYear, cancellationToken).ConfigureAwait(false);
            await dbContext.TenantSubscriptionPayments.AddAsync(payment, cancellationToken).ConfigureAwait(false);

            await formulaSeeder.SeedDefaultsForTenantAsync(tenant.Id, cancellationToken).ConfigureAwait(false);

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (previousTenant is Guid prev)
            {
                tenantContext.SetTenant(prev);
            }
            else
            {
                tenantContext.Clear();
            }
        }
    }
}
