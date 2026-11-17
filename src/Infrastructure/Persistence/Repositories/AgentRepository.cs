using System.Text.Json;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Agents;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GPMH.Infrastructure.Persistence.Repositories;

/// <summary>
/// Persistence and transaction coordinator for Field Agent KYC, referral attribution,
/// rate-snapshotted commission accrual, FIFO payout settlements, and voucher generation.
/// </summary>
public sealed class AgentRepository(
    AppDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : IAgentRepository
{
    public async Task<bool> ExistsDuplicateAsync(
        string usernameOrMobile,
        string aadhaarBlindIndex,
        string panBlindIndex,
        CancellationToken cancellationToken)
    {
        bool userExists = await dbContext.Users
            .AnyAsync(u => u.Username == usernameOrMobile, cancellationToken)
            .ConfigureAwait(false);

        if (userExists)
        {
            return true;
        }

        return await dbContext.FieldAgents
            .AnyAsync(
                a => a.MobileNo == usernameOrMobile ||
                     a.AadhaarBlindIndex == aadhaarBlindIndex ||
                     a.PanBlindIndex == panBlindIndex,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RegisterAgentAsync(
        User user,
        FieldAgent agent,
        IReadOnlyList<ProtectedAsset> kycAssets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(agent);

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await dbContext.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await dbContext.FieldAgents.AddAsync(agent, cancellationToken).ConfigureAwait(false);

        if (kycAssets.Count > 0)
        {
            await dbContext.ProtectedAssets.AddRangeAsync(kycAssets, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<FieldAgent?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.FieldAgents.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

    public Task<FieldAgent?> FindByIdAsync(Guid agentId, CancellationToken cancellationToken) =>
        dbContext.FieldAgents.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);

    public Task<FieldAgent?> FindByAgentCodeAsync(string agentCode, CancellationToken cancellationToken)
    {
        string normalized = agentCode.Trim().ToUpperInvariant();
        return dbContext.FieldAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgentCode.ToUpper() == normalized, cancellationToken);
    }

    public Task<User?> FindUserByLoginAsync(string loginIdentifier, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(
            u => u.Username == loginIdentifier || u.Email == loginIdentifier,
            cancellationToken);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<FieldAgent>> ListAllAsync(CancellationToken cancellationToken)
    {
        List<FieldAgent> agents = await dbContext.FieldAgents
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. agents.OrderByDescending(a => a.CreatedAtUtc)];
    }

    public async Task<AgentCommission?> TryAccrueCommissionForPaymentAsync(
        Tenant tenant,
        TenantSubscriptionPayment payment,
        Guid actorUserId,
        ApplicationRole? actorRole,
        string? ipAddress,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(payment);

        if (tenant.ReferredByAgentId is not Guid agentId || payment.Amount <= 0m)
        {
            return null;
        }

        bool alreadyAccrued = await dbContext.AgentCommissions
            .AsNoTracking()
            .AnyAsync(c => c.SubscriptionPaymentId == payment.Id, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyAccrued)
        {
            return null;
        }

        FieldAgent? agent = await dbContext.FieldAgents
            .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
            .ConfigureAwait(false);

        if (agent is null)
        {
            return null;
        }

        bool hasPriorPaid = await dbContext.TenantSubscriptionPayments
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenant.Id && p.Id != payment.Id && p.Amount > 0m, cancellationToken)
            .ConfigureAwait(false);

        string commissionType = hasPriorPaid
            ? AgentCommission.TypeRenewal
            : AgentCommission.TypeNewOnboarding;

        decimal ratePct = hasPriorPaid
            ? agent.ApprovedRenewalRatePct
            : agent.ApprovedNewGpRatePct;

        if (ratePct <= 0m)
        {
            return null;
        }

        decimal commissionAmount = Math.Round(payment.Amount * (ratePct / 100m), 2, MidpointRounding.AwayFromZero);
        if (commissionAmount <= 0m)
        {
            return null;
        }

        AgentCommission commission = new()
        {
            AgentId = agent.Id,
            TenantId = tenant.Id,
            SubscriptionPaymentId = payment.Id,
            CommissionType = commissionType,
            CollectedAmount = payment.Amount,
            RatePctSnapshot = ratePct,
            CommissionAmount = commissionAmount,
            SettledAmount = 0m,
            Status = AgentCommission.StatusAccrued,
            Notes = $"Auto-accrued from {payment.InvoiceNo ?? payment.PaymentReference ?? payment.PlanName}",
        };

        await dbContext.AgentCommissions.AddAsync(commission, cancellationToken).ConfigureAwait(false);
        await dbContext.AuditLogs.AddAsync(
            BuildAudit(
                tenantId: tenant.Id,
                actorUserId: actorUserId,
                actorRole: actorRole,
                entityName: nameof(AgentCommission),
                entityId: commission.Id.ToString("D"),
                action: "AgentCommissionAccrued",
                oldValues: null,
                newValues: new
                {
                    agent.AgentCode,
                    tenant.LgdCode,
                    CommissionType = commissionType,
                    CollectedAmount = payment.Amount,
                    RatePctSnapshot = ratePct,
                    CommissionAmount = commissionAmount,
                },
                utcNow: utcNow,
                ipAddress: ipAddress),
            cancellationToken).ConfigureAwait(false);

        return commission;
    }

    public async Task<AgentDashboardDto> GetAgentDashboardAsync(Guid agentId, CancellationToken cancellationToken)
    {
        FieldAgent agent = await dbContext.FieldAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), agentId);

        List<Tenant> referredTenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.ReferredByAgentId == agentId && !t.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> tenantIds = [.. referredTenants.Select(t => t.Id)];
        List<TenantSubscriptionPayment> payments = tenantIds.Count == 0
            ? []
            : await dbContext.TenantSubscriptionPayments
                .AsNoTracking()
                .Where(p => tenantIds.Contains(p.TenantId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        IReadOnlyList<AgentCommissionItemDto> commissionItems = await LoadCommissionDtosAsync(
            agentIdFilter: agentId,
            statusFilter: null,
            searchFilter: null,
            cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = dateTimeProvider.UtcNow;
        int activeCount = referredTenants.Count(t => t.IsLicenseOperational(now));
        int pendingCount = referredTenants.Count(t => t.Status == TenantStatus.PendingApproval);
        int upcoming90d = referredTenants.Count(t =>
        {
            int? days = t.GetDaysUntilExpiry(now);
            return days.HasValue && days.Value <= 90;
        });

        decimal totalSales = Math.Round(payments.Sum(p => p.Amount), 2, MidpointRounding.AwayFromZero);
        decimal totalEarned = Math.Round(
            commissionItems.Where(c => !string.Equals(c.CommissionType, AgentCommission.TypeClawback, StringComparison.OrdinalIgnoreCase) &&
                                       !string.Equals(c.Status, AgentCommission.StatusClawedBack, StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.CommissionAmount),
            2,
            MidpointRounding.AwayFromZero);
        decimal pendingPayable = Math.Round(commissionItems.Sum(c => c.RemainingPayable), 2, MidpointRounding.AwayFromZero);
        decimal paidCommission = Math.Round(
            commissionItems.Where(c => !string.Equals(c.CommissionType, AgentCommission.TypeClawback, StringComparison.OrdinalIgnoreCase) &&
                                       !string.Equals(c.Status, AgentCommission.StatusClawedBack, StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.SettledAmount),
            2,
            MidpointRounding.AwayFromZero);

        return new AgentDashboardDto(
            AgentCode: agent.AgentCode,
            FullName: agent.FullName,
            District: agent.District,
            ReferralLink: $"/onboarding?ref={Uri.EscapeDataString(agent.AgentCode)}",
            ApprovedNewGpRatePct: agent.ApprovedNewGpRatePct,
            ApprovedRenewalRatePct: agent.ApprovedRenewalRatePct,
            VillagesOnboarded: referredTenants.Count,
            ActiveGps: activeCount,
            PendingVerificationGps: pendingCount,
            TotalSalesValue: totalSales,
            TotalCommissionEarned: totalEarned,
            PendingPayableCommission: pendingPayable,
            PaidCommission: paidCommission,
            UpcomingRenewals90dCount: upcoming90d,
            RecentCommissions: commissionItems.Take(10).ToList());
    }

    public async Task<AgentVillagesResponseDto> GetAgentVillagesAsync(Guid agentId, CancellationToken cancellationToken)
    {
        FieldAgent agent = await dbContext.FieldAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), agentId);

        List<Tenant> tenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.ReferredByAgentId == agentId && !t.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> tenantIds = [.. tenants.Select(t => t.Id)];
        List<GramPanchayat> gps = tenantIds.Count == 0
            ? []
            : await dbContext.GramPanchayats
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(g => tenantIds.Contains(g.TenantId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        List<TenantSubscriptionPayment> payments = tenantIds.Count == 0
            ? []
            : await dbContext.TenantSubscriptionPayments
                .AsNoTracking()
                .Where(p => tenantIds.Contains(p.TenantId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        List<AgentCommission> commissions = await dbContext.AgentCommissions
            .AsNoTracking()
            .Where(c => c.AgentId == agentId &&
                        c.CommissionType != AgentCommission.TypeClawback &&
                        c.Status != AgentCommission.StatusClawedBack)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        PlatformPaymentAccount? plat = await dbContext.PlatformPaymentAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsActive, cancellationToken)
            .ConfigureAwait(false);
        decimal annualFee = plat?.AnnualFee ?? 6000.00m;
        decimal projectedRenewal = Math.Round(annualFee * (agent.ApprovedRenewalRatePct / 100m), 2, MidpointRounding.AwayFromZero);

        Dictionary<Guid, GramPanchayat> gpByTenant = gps.ToDictionary(g => g.TenantId);
        Dictionary<Guid, decimal> salesByTenant = payments
            .GroupBy(p => p.TenantId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero));
        Dictionary<Guid, decimal> commByTenant = commissions
            .GroupBy(c => c.TenantId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.CommissionAmount), 2, MidpointRounding.AwayFromZero));

        DateTimeOffset now = dateTimeProvider.UtcNow;

        List<AgentVillageItemDto> villages = tenants
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t =>
            {
                gpByTenant.TryGetValue(t.Id, out GramPanchayat? gp);
                salesByTenant.TryGetValue(t.Id, out decimal sales);
                commByTenant.TryGetValue(t.Id, out decimal earned);
                return new AgentVillageItemDto(
                    TenantId: t.Id,
                    LgdCode: t.LgdCode,
                    PanchayatNameEn: t.NameEn,
                    PanchayatNameMr: t.NameMr,
                    DistrictName: gp?.DistrictName ?? "-",
                    TehsilName: gp?.TehsilName ?? "-",
                    OnboardedAtUtc: t.CreatedAtUtc,
                    SubscriptionStatus: t.Status.ToString(),
                    EffectiveStatus: t.GetEffectiveStatus(now),
                    SubscriptionExpiresAtUtc: t.SubscriptionExpiresAtUtc,
                    DaysUntilExpiry: t.GetDaysUntilExpiry(now),
                    TotalSalesCollected: sales,
                    TotalCommissionEarned: earned,
                    ProjectedRenewalCommission: projectedRenewal);
            })
            .ToList();

        List<AgentVillageItemDto> renewals = villages
            .Where(v => v.DaysUntilExpiry.HasValue && v.DaysUntilExpiry.Value <= 90)
            .OrderBy(v => v.DaysUntilExpiry)
            .ToList();

        return new AgentVillagesResponseDto(villages, renewals);
    }

    public Task<IReadOnlyList<AgentCommissionItemDto>> ListAgentCommissionsAsync(
        Guid agentId,
        string? status,
        string? search,
        CancellationToken cancellationToken) =>
        LoadCommissionDtosAsync(agentId, status, search, cancellationToken);

    public async Task<IReadOnlyList<AgentPayoutItemDto>> ListAgentPayoutsAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        FieldAgent? agent = await dbContext.FieldAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
            .ConfigureAwait(false);

        List<AgentPayout> payouts = await dbContext.AgentPayouts
            .AsNoTracking()
            .Where(p => p.AgentId == agentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return payouts
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => MapPayoutDto(p, agent?.AgentCode, agent?.FullName))
            .ToList();
    }

    public async Task<decimal> GetAvailableRequestableCommissionAsync(Guid agentId, CancellationToken cancellationToken)
    {
        List<AgentCommission> rows = await dbContext.AgentCommissions
            .AsNoTracking()
            .Where(c => c.AgentId == agentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal unpaid = rows.Sum(c => c.GetRemainingPayable());

        List<AgentPayout> openRequests = await dbContext.AgentPayouts
            .AsNoTracking()
            .Where(p => p.AgentId == agentId && p.Status == AgentPayout.StatusRequested)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal alreadyRequested = openRequests.Sum(p => p.PayoutAmount);
        return Math.Max(0m, Math.Round(unpaid - alreadyRequested, 2, MidpointRounding.AwayFromZero));
    }

    public Task AddAgentPayoutAsync(AgentPayout payout, CancellationToken cancellationToken) =>
        dbContext.AgentPayouts.AddAsync(payout, cancellationToken).AsTask();

    public async Task<IReadOnlyList<SuperAdminAgentSummaryDto>> ListSuperAdminAgentDirectoryAsync(
        string? district,
        string? taluka,
        string? status,
        string? search,
        CancellationToken cancellationToken)
    {
        List<FieldAgent> agents = await dbContext.FieldAgents
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Tenant> tenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.ReferredByAgentId != null && !t.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<AgentCommission> commissions = await dbContext.AgentCommissions
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<AgentPayout> payouts = await dbContext.AgentPayouts
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = dateTimeProvider.UtcNow;
        IReadOnlyList<SuperAdminAgentSummaryDto> summaries = BuildSuperAdminAgentSummaries(
            agents, tenants, commissions, payouts, now);

        IEnumerable<SuperAdminAgentSummaryDto> filtered = summaries;

        if (!string.IsNullOrWhiteSpace(district) && !string.Equals(district, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            string d = district.Trim();
            filtered = filtered.Where(a => string.Equals(a.District, d, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(taluka) && !string.Equals(taluka, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            string tal = taluka.Trim();
            filtered = filtered.Where(a => a.TalukasCsv.Contains(tal, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            string st = status.Trim();
            filtered = filtered.Where(a => string.Equals(a.KycStatus.ToString(), st, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim();
            filtered = filtered.Where(a =>
                a.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.AgentCode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.MobileNo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.District.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.TalukasCsv.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return [.. filtered.OrderByDescending(a => a.CreatedAtUtc)];
    }

    public async Task<SuperAdminAgentSettlementsDto> GetSuperAdminSettlementsAsync(CancellationToken cancellationToken)
    {
        List<FieldAgent> agents = await dbContext.FieldAgents
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Tenant> tenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.ReferredByAgentId != null && !t.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<AgentCommission> commissions = await dbContext.AgentCommissions
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<AgentPayout> payouts = await dbContext.AgentPayouts
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = dateTimeProvider.UtcNow;
        IReadOnlyList<SuperAdminAgentSummaryDto> summaries = BuildSuperAdminAgentSummaries(
            agents, tenants, commissions, payouts, now)
            .OrderByDescending(s => s.PendingPayableBalance)
            .ThenByDescending(s => s.CreatedAtUtc)
            .ToList();

        IReadOnlyList<AgentCommissionItemDto> unsettled = await LoadCommissionDtosAsync(
            agentIdFilter: null,
            statusFilter: null,
            searchFilter: null,
            cancellationToken).ConfigureAwait(false);

        List<AgentCommissionItemDto> unsettledOnly = unsettled
            .Where(c => c.RemainingPayable > 0m)
            .OrderBy(c => c.CreatedAtUtc)
            .ToList();

        Dictionary<Guid, FieldAgent> agentById = agents.ToDictionary(a => a.Id);

        List<AgentPayoutItemDto> pendingRequests = payouts
            .Where(p => string.Equals(p.Status, AgentPayout.StatusRequested, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p =>
            {
                agentById.TryGetValue(p.AgentId, out FieldAgent? ag);
                return MapPayoutDto(p, ag?.AgentCode, ag?.FullName);
            })
            .ToList();

        List<AgentPayoutItemDto> disbursedHistory = payouts
            .Where(p => string.Equals(p.Status, AgentPayout.StatusDisbursed, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.PaidAtUtc ?? p.CreatedAtUtc)
            .Select(p =>
            {
                agentById.TryGetValue(p.AgentId, out FieldAgent? ag);
                return MapPayoutDto(p, ag?.AgentCode, ag?.FullName);
            })
            .ToList();

        decimal totalDisbursed = Math.Round(
            disbursedHistory.Sum(p => p.PayoutAmount),
            2,
            MidpointRounding.AwayFromZero);

        decimal pendingSettlement = Math.Round(
            summaries.Sum(s => s.PendingPayableBalance),
            2,
            MidpointRounding.AwayFromZero);

        return new SuperAdminAgentSettlementsDto(
            TotalAgents: agents.Count,
            PendingKycCount: agents.Count(a => a.KycStatus == AgentKycStatus.PendingApproval),
            ActiveAgentsCount: agents.Count(a => a.KycStatus == AgentKycStatus.Approved),
            TotalDisbursed: totalDisbursed,
            PendingSettlementAmount: pendingSettlement,
            AgentPayableSummaries: summaries,
            UnsettledCommissions: unsettledOnly,
            PendingWithdrawalRequests: pendingRequests,
            DisbursedHistory: disbursedHistory);
    }

    public async Task<AgentPayoutItemDto> SettleAgentPayoutAsync(
        Guid agentId,
        decimal amount,
        string paymentMode,
        string utrReference,
        DateOnly paymentDate,
        Guid? fulfillRequestedPayoutId,
        string? receiptAssetKey,
        string? remarks,
        Guid actorUserId,
        ApplicationRole? actorRole,
        string? ipAddress,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        decimal roundedAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (roundedAmount <= 0m)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Amount"] = ["Payout amount must be greater than zero."],
            });
        }

        string normalizedUtr = utrReference.Trim();
        string normalizedMode = paymentMode.Trim().ToUpperInvariant();

        AgentPayout? resultPayout = null;
        FieldAgent? targetAgent = null;

        IDbContextTransaction? ownedTx = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            targetAgent = await dbContext.FieldAgents
                .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException(nameof(FieldAgent), agentId);

            bool duplicateUtr = await dbContext.AgentPayouts
                .AsNoTracking()
                .AnyAsync(
                    p => p.Status == AgentPayout.StatusDisbursed &&
                         p.UtrReference.ToUpper() == normalizedUtr.ToUpper() &&
                         (!fulfillRequestedPayoutId.HasValue || p.Id != fulfillRequestedPayoutId.Value),
                    cancellationToken)
                .ConfigureAwait(false);

            if (duplicateUtr)
            {
                throw new DomainConflictException(
                    "DUPLICATE_PAYOUT_UTR",
                    $"UTR reference '{normalizedUtr}' has already been recorded on a disbursed payout.");
            }

            List<AgentCommission> candidateRows = await dbContext.AgentCommissions
                .Where(c => c.AgentId == agentId &&
                            c.CommissionType != AgentCommission.TypeClawback &&
                            c.Status != AgentCommission.StatusClawedBack)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<AgentCommission> unpaidFifo = candidateRows
                .Where(c => c.GetRemainingPayable() > 0m)
                .OrderBy(c => c.CreatedAtUtc)
                .ThenBy(c => c.Id)
                .ToList();

            decimal pendingPayable = Math.Round(
                unpaidFifo.Sum(c => c.GetRemainingPayable()),
                2,
                MidpointRounding.AwayFromZero);

            if (roundedAmount > pendingPayable)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Amount"] = [$"Settlement amount ₹{roundedAmount:0.00} exceeds pending payable commission ₹{pendingPayable:0.00}."],
                });
            }

            (int fyStart, string fyLabel) = ResolveFinancialYear(paymentDate);
            long seq = await AllocateNextVoucherSequenceAsync(fyLabel, cancellationToken).ConfigureAwait(false);
            string voucherNo = $"VCH-{fyStart}-{seq:D4}";

            if (fulfillRequestedPayoutId.HasValue)
            {
                resultPayout = await dbContext.AgentPayouts
                    .FirstOrDefaultAsync(p => p.Id == fulfillRequestedPayoutId.Value && p.AgentId == agentId, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException(nameof(AgentPayout), fulfillRequestedPayoutId.Value);

                if (!string.Equals(resultPayout.Status, AgentPayout.StatusRequested, StringComparison.OrdinalIgnoreCase))
                {
                    throw new DomainConflictException(
                        "PAYOUT_NOT_PENDING_REQUEST",
                        $"Payout '{resultPayout.VoucherNo}' is already '{resultPayout.Status}'.");
                }

                resultPayout.VoucherNo = voucherNo;
                resultPayout.VoucherSequence = seq;
                resultPayout.FinancialYear = fyLabel;
                resultPayout.PayoutAmount = roundedAmount;
                resultPayout.PaymentMode = normalizedMode;
                resultPayout.UtrReference = normalizedUtr;
                resultPayout.PaymentDate = paymentDate;
                resultPayout.Status = AgentPayout.StatusDisbursed;
                resultPayout.ReceiptAssetKey = string.IsNullOrWhiteSpace(receiptAssetKey) ? null : receiptAssetKey.Trim();
                resultPayout.Remarks = string.IsNullOrWhiteSpace(remarks) ? resultPayout.Remarks : remarks.Trim();
                resultPayout.SettledByUserId = actorUserId;
                resultPayout.PaidAtUtc = utcNow;
                resultPayout.UpdatedAtUtc = utcNow;
            }
            else
            {
                resultPayout = new AgentPayout
                {
                    AgentId = agentId,
                    VoucherNo = voucherNo,
                    VoucherSequence = seq,
                    FinancialYear = fyLabel,
                    PayoutAmount = roundedAmount,
                    PaymentMode = normalizedMode,
                    UtrReference = normalizedUtr,
                    PaymentDate = paymentDate,
                    Status = AgentPayout.StatusDisbursed,
                    ReceiptAssetKey = string.IsNullOrWhiteSpace(receiptAssetKey) ? null : receiptAssetKey.Trim(),
                    Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
                    SettledByUserId = actorUserId,
                    PaidAtUtc = utcNow,
                };
                await dbContext.AgentPayouts.AddAsync(resultPayout, cancellationToken).ConfigureAwait(false);
            }

            // Allocate settlement FIFO across oldest unpaid commission rows
            decimal remainingToAllocate = roundedAmount;
            List<Guid> allocatedIds = [];
            foreach (AgentCommission row in unpaidFifo)
            {
                if (remainingToAllocate <= 0m)
                {
                    break;
                }

                decimal rowRemaining = row.GetRemainingPayable();
                decimal apply = Math.Min(remainingToAllocate, rowRemaining);
                row.SettledAmount = Math.Round(row.SettledAmount + apply, 2, MidpointRounding.AwayFromZero);
                row.LatestPayoutId = resultPayout.Id;
                row.Status = row.SettledAmount >= row.CommissionAmount
                    ? AgentCommission.StatusSettled
                    : AgentCommission.StatusPartiallySettled;
                row.UpdatedAtUtc = utcNow;

                remainingToAllocate = Math.Round(remainingToAllocate - apply, 2, MidpointRounding.AwayFromZero);
                allocatedIds.Add(row.Id);
            }

            await dbContext.AuditLogs.AddAsync(
                BuildAudit(
                    tenantId: null,
                    actorUserId: actorUserId,
                    actorRole: actorRole,
                    entityName: nameof(AgentPayout),
                    entityId: resultPayout.Id.ToString("D"),
                    action: "AgentCommissionPayoutDisbursed",
                    oldValues: null,
                    newValues: new
                    {
                        VoucherNo = voucherNo,
                        targetAgent.AgentCode,
                        PayoutAmount = roundedAmount,
                        PaymentMode = normalizedMode,
                        UtrReference = normalizedUtr,
                        PaymentDate = paymentDate,
                        AllocatedCommissionIds = allocatedIds,
                    },
                    utcNow: utcNow,
                    ipAddress: ipAddress),
                cancellationToken).ConfigureAwait(false);

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

        return MapPayoutDto(resultPayout!, targetAgent!.AgentCode, targetAgent.FullName);
    }

    public async Task<AgentPayoutVoucherPdfModel?> BuildPayoutVoucherModelAsync(
        Guid payoutId,
        Guid? restrictToAgentId,
        CancellationToken cancellationToken)
    {
        AgentPayout? payout = await dbContext.AgentPayouts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == payoutId, cancellationToken)
            .ConfigureAwait(false);

        if (payout is null)
        {
            return null;
        }

        if (restrictToAgentId.HasValue && payout.AgentId != restrictToAgentId.Value)
        {
            throw new TenantAccessDeniedException("Cross-agent access to payout voucher is denied.");
        }

        if (!string.Equals(payout.Status, AgentPayout.StatusDisbursed, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainConflictException(
                "VOUCHER_NOT_DISBURSED",
                $"Payout voucher '{payout.VoucherNo}' cannot be downloaded while status is '{payout.Status}'.");
        }

        FieldAgent agent = await dbContext.FieldAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == payout.AgentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), payout.AgentId);

        List<AgentCommission> settledLines = await dbContext.AgentCommissions
            .AsNoTracking()
            .Where(c => c.LatestPayoutId == payout.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> tenantIds = [.. settledLines.Select(c => c.TenantId)];
        Dictionary<Guid, Tenant> tenantById = tenantIds.Count == 0
            ? []
            : await dbContext.Tenants
                .AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, cancellationToken)
                .ConfigureAwait(false);

        PlatformPaymentAccount? plat = await dbContext.PlatformPaymentAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.IsActive, cancellationToken)
            .ConfigureAwait(false);

        List<AgentPayoutVoucherLineDto> lines = settledLines
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c =>
            {
                tenantById.TryGetValue(c.TenantId, out Tenant? tenant);
                return new AgentPayoutVoucherLineDto(
                    LgdCode: tenant?.LgdCode ?? "-",
                    PanchayatNameEn: tenant?.NameEn ?? "-",
                    PanchayatNameMr: tenant?.NameMr ?? "-",
                    CommissionType: c.CommissionType,
                    CollectedAmount: c.CollectedAmount,
                    RatePctSnapshot: c.RatePctSnapshot,
                    CommissionAmount: c.CommissionAmount,
                    SettledAmount: c.SettledAmount);
            })
            .ToList();

        return new AgentPayoutVoucherPdfModel(
            VoucherNo: payout.VoucherNo,
            FinancialYear: payout.FinancialYear,
            PaymentDate: payout.PaymentDate,
            IssuedAtUtc: payout.PaidAtUtc ?? payout.CreatedAtUtc,
            Status: payout.Status,
            AgentCode: agent.AgentCode,
            AgentName: agent.FullName,
            AgentMobile: agent.MobileNo,
            AgentDistrict: agent.District,
            AgentBankName: agent.BankName,
            AgentBankBranch: agent.BankBranch,
            AgentAccountNumber: agent.AccountNumber,
            AgentIfscCode: agent.IfscCode,
            AgentUpiId: agent.UpiId,
            PayoutAmount: payout.PayoutAmount,
            AmountInWordsMr: MarathiNumberToWords.ToRupeesWords(payout.PayoutAmount),
            PaymentMode: payout.PaymentMode,
            UtrReference: payout.UtrReference,
            Remarks: payout.Remarks,
            PlatformAccountName: plat?.AccountName ?? "Maharashtra GP Digital Network",
            PlatformGstin: plat?.Gstin,
            PlatformSupportPhone: plat?.SupportPhone ?? "0721-2550101",
            PlatformSupportEmail: plat?.SupportEmail ?? "support@epanchayat-mh.gov.in",
            Lines: lines);
    }

    public Task AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken) =>
        dbContext.AuditLogs.AddAsync(log, cancellationToken).AsTask();

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
            token.UpdatedAtUtc = revokedAtUtc;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private async Task<long> AllocateNextVoucherSequenceAsync(string financialYear, CancellationToken cancellationToken)
    {
        DateTimeOffset now = dateTimeProvider.UtcNow;

        if (string.Equals(dbContext.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            const string sql = """
                INSERT INTO "voucher_counters" ("TenantId", "FinancialYear", "LastSequence", "UpdatedAtUtc")
                VALUES (@p0, @p1, 1, @p2)
                ON CONFLICT ("TenantId", "FinancialYear")
                DO UPDATE SET
                    "LastSequence" = "voucher_counters"."LastSequence" + 1,
                    "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
                RETURNING "LastSequence" AS "Value";
                """;

            return await dbContext.Database
                .SqlQueryRaw<long>(
                    sql,
                    new NpgsqlParameter("p0", VoucherCounter.GlobalTenantId),
                    new NpgsqlParameter("p1", financialYear),
                    new NpgsqlParameter("p2", now))
                .FirstAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        VoucherCounter? counter = await dbContext.VoucherCounters
            .FirstOrDefaultAsync(
                c => c.TenantId == VoucherCounter.GlobalTenantId && c.FinancialYear == financialYear,
                cancellationToken)
            .ConfigureAwait(false);

        long nextSeq;
        if (counter is null)
        {
            nextSeq = 1;
            dbContext.VoucherCounters.Add(new VoucherCounter
            {
                TenantId = VoucherCounter.GlobalTenantId,
                FinancialYear = financialYear,
                LastSequence = 1,
                UpdatedAtUtc = now,
            });
        }
        else
        {
            nextSeq = counter.LastSequence + 1;
            counter.LastSequence = nextSeq;
            counter.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return nextSeq;
    }

    private async Task<IReadOnlyList<AgentCommissionItemDto>> LoadCommissionDtosAsync(
        Guid? agentIdFilter,
        string? statusFilter,
        string? searchFilter,
        CancellationToken cancellationToken)
    {
        IQueryable<AgentCommission> query = dbContext.AgentCommissions.AsNoTracking();
        if (agentIdFilter.HasValue)
        {
            query = query.Where(c => c.AgentId == agentIdFilter.Value);
        }

        List<AgentCommission> rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return [];
        }

        HashSet<Guid> agentIds = [.. rows.Select(c => c.AgentId)];
        HashSet<Guid> tenantIds = [.. rows.Select(c => c.TenantId)];

        Dictionary<Guid, FieldAgent> agentById = await dbContext.FieldAgents
            .AsNoTracking()
            .Where(a => agentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, Tenant> tenantById = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<AgentCommissionItemDto> projected = rows
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c =>
            {
                agentById.TryGetValue(c.AgentId, out FieldAgent? ag);
                tenantById.TryGetValue(c.TenantId, out Tenant? tenant);
                return new AgentCommissionItemDto(
                    CommissionId: c.Id,
                    AgentId: c.AgentId,
                    AgentCode: ag?.AgentCode,
                    AgentName: ag?.FullName,
                    TenantId: c.TenantId,
                    LgdCode: tenant?.LgdCode ?? "-",
                    PanchayatNameEn: tenant?.NameEn ?? "-",
                    PanchayatNameMr: tenant?.NameMr ?? "-",
                    SubscriptionPaymentId: c.SubscriptionPaymentId,
                    CommissionType: c.CommissionType,
                    CollectedAmount: c.CollectedAmount,
                    RatePctSnapshot: c.RatePctSnapshot,
                    CommissionAmount: c.CommissionAmount,
                    SettledAmount: c.SettledAmount,
                    RemainingPayable: c.GetRemainingPayable(),
                    Status: c.Status,
                    LatestPayoutId: c.LatestPayoutId,
                    Notes: c.Notes,
                    CreatedAtUtc: c.CreatedAtUtc);
            });

        if (!string.IsNullOrWhiteSpace(statusFilter) && !string.Equals(statusFilter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            string st = statusFilter.Trim();
            projected = projected.Where(c => string.Equals(c.Status, st, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            string q = searchFilter.Trim();
            projected = projected.Where(c =>
                c.PanchayatNameEn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.PanchayatNameMr.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.LgdCode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.CommissionType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (c.AgentCode ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (c.AgentName ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return projected.ToList();
    }

    private static IReadOnlyList<SuperAdminAgentSummaryDto> BuildSuperAdminAgentSummaries(
        IReadOnlyList<FieldAgent> agents,
        IReadOnlyList<Tenant> tenants,
        IReadOnlyList<AgentCommission> commissions,
        IReadOnlyList<AgentPayout> payouts,
        DateTimeOffset now)
    {
        Dictionary<Guid, List<Tenant>> tenantsByAgent = tenants
            .Where(t => t.ReferredByAgentId.HasValue)
            .GroupBy(t => t.ReferredByAgentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<Guid, List<AgentCommission>> commByAgent = commissions
            .GroupBy(c => c.AgentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<Guid, List<AgentPayout>> reqByAgent = payouts
            .Where(p => string.Equals(p.Status, AgentPayout.StatusRequested, StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.AgentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return agents.Select(ag =>
        {
            tenantsByAgent.TryGetValue(ag.Id, out List<Tenant>? gps);
            commByAgent.TryGetValue(ag.Id, out List<AgentCommission>? agComms);
            reqByAgent.TryGetValue(ag.Id, out List<AgentPayout>? agReqs);

            decimal accrued = Math.Round(
                (agComms ?? []).Where(c => !string.Equals(c.CommissionType, AgentCommission.TypeClawback, StringComparison.OrdinalIgnoreCase) &&
                                           !string.Equals(c.Status, AgentCommission.StatusClawedBack, StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.CommissionAmount),
                2,
                MidpointRounding.AwayFromZero);

            decimal settled = Math.Round(
                (agComms ?? []).Where(c => !string.Equals(c.CommissionType, AgentCommission.TypeClawback, StringComparison.OrdinalIgnoreCase) &&
                                           !string.Equals(c.Status, AgentCommission.StatusClawedBack, StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.SettledAmount),
                2,
                MidpointRounding.AwayFromZero);

            decimal pending = Math.Round((agComms ?? []).Sum(c => c.GetRemainingPayable()), 2, MidpointRounding.AwayFromZero);

            return new SuperAdminAgentSummaryDto(
                AgentId: ag.Id,
                UserId: ag.UserId,
                AgentCode: ag.AgentCode,
                KycStatus: ag.KycStatus,
                StatusReason: ag.StatusReason,
                FullName: ag.FullName,
                MobileNo: ag.MobileNo,
                Email: ag.Email,
                District: ag.District,
                TalukasCsv: ag.TalukasCsv,
                AadhaarMasked: ag.AadhaarMasked,
                PanMasked: ag.PanMasked,
                RequestedCommissionRatePct: ag.RequestedCommissionRatePct,
                ApprovedNewGpRatePct: ag.ApprovedNewGpRatePct,
                ApprovedRenewalRatePct: ag.ApprovedRenewalRatePct,
                AadhaarDocumentKey: ag.AadhaarDocumentKey,
                PanDocumentKey: ag.PanDocumentKey,
                Address: ag.Address,
                VillageOrCity: ag.VillageOrCity,
                State: ag.State,
                BankName: ag.BankName,
                BankBranch: ag.BankBranch,
                AccountNumber: ag.AccountNumber,
                IfscCode: ag.IfscCode,
                UpiId: ag.UpiId,
                CreatedAtUtc: ag.CreatedAtUtc,
                ApprovedAtUtc: ag.ApprovedAtUtc,
                OnboardedGpCount: gps?.Count ?? 0,
                ActiveGpCount: gps?.Count(t => t.IsLicenseOperational(now)) ?? 0,
                TotalAccruedCommission: accrued,
                TotalSettledCommission: settled,
                PendingPayableBalance: pending,
                PendingWithdrawalRequestsCount: agReqs?.Count ?? 0);
        }).ToList();
    }

    private static AgentPayoutItemDto MapPayoutDto(AgentPayout p, string? agentCode, string? agentName) =>
        new(
            PayoutId: p.Id,
            AgentId: p.AgentId,
            AgentCode: agentCode,
            AgentName: agentName,
            VoucherNo: p.VoucherNo,
            VoucherSequence: p.VoucherSequence,
            FinancialYear: p.FinancialYear,
            PayoutAmount: p.PayoutAmount,
            PaymentMode: p.PaymentMode,
            UtrReference: p.UtrReference,
            PaymentDate: p.PaymentDate,
            Status: p.Status,
            ReceiptAssetKey: p.ReceiptAssetKey,
            Remarks: p.Remarks,
            RequestedAtUtc: p.RequestedAtUtc,
            PaidAtUtc: p.PaidAtUtc,
            CreatedAtUtc: p.CreatedAtUtc);

    private static (int StartYear, string FinancialYearLabel) ResolveFinancialYear(DateOnly date)
    {
        int startYear = date.Month >= 4 ? date.Year : date.Year - 1;
        return (startYear, $"{startYear}-{(startYear + 1) % 100:D2}");
    }

    private static AuditLog BuildAudit(
        Guid? tenantId,
        Guid actorUserId,
        ApplicationRole? actorRole,
        string entityName,
        string entityId,
        string action,
        object? oldValues,
        object? newValues,
        DateTimeOffset utcNow,
        string? ipAddress) =>
        new()
        {
            TenantId = tenantId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ActorId = actorUserId.ToString("D"),
            ActorRole = actorRole?.ToString(),
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = ipAddress,
            TimestampUtc = utcNow,
        };
}
