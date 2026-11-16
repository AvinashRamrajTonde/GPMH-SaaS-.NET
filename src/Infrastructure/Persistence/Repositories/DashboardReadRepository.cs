using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class DashboardReadRepository(AppDbContext dbContext) : IDashboardReadRepository
{
    public async Task<DashboardSummaryDto> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        Tenant tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        GramPanchayat? gp = await dbContext.GramPanchayats
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        AssessmentYear? activeYear = await dbContext.AssessmentYears
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .OrderByDescending(a => a.StartYear)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        int totalProperties = await dbContext.Properties
            .AsNoTracking()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        int bankCount = await dbContext.BankAccounts
            .AsNoTracking()
            .CountAsync(b => b.IsActive, cancellationToken)
            .ConfigureAwait(false);

        bool hasFormula = await dbContext.FormulaSettings
            .AsNoTracking()
            .AnyAsync(f => f.IsActive, cancellationToken)
            .ConfigureAwait(false);

        List<DashboardActivityItemDto> rawActivities = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .Select(l => new DashboardActivityItemDto(
                l.Id,
                l.EntityName,
                l.EntityId,
                l.Action,
                l.ActorId,
                l.ActorRole,
                l.TimestampUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DashboardActivityItemDto> recentActivity = rawActivities
            .OrderByDescending(a => a.TimestampUtc)
            .Take(10)
            .ToList();

        bool step1Done = gp is not null && !string.IsNullOrWhiteSpace(gp.LgdCode);
        bool step2Done = gp is not null && !string.IsNullOrWhiteSpace(gp.GramsevakName) && !string.IsNullOrWhiteSpace(gp.SarpanchName);
        bool step3Done = activeYear is not null;
        bool step4Done = bankCount >= 1 && gp is not null && !string.IsNullOrWhiteSpace(gp.UpiId);
        bool step5Done = hasFormula;

        SetupChecklistItemDto[] checklist =
        [
            new(1, "geoProfile", step1Done, gp is not null ? $"LGD {gp.LgdCode} ({gp.DistrictName})" : null),
            new(2, "committee", step2Done, gp?.GramsevakName),
            new(3, "assessmentYear", step3Done, activeYear?.YearLabel),
            new(4, "bankQr", step4Done, gp?.UpiId),
            new(5, "formulaSeeds", step5Done, hasFormula ? "12 Construction / 9 Slabs" : null),
        ];

        int computedPct = (checklist.Count(s => s.IsCompleted) * 100) / checklist.Length;
        int setupProgress = Math.Max(gp?.SetupProgressPct ?? 0, computedPct);

        return new DashboardSummaryDto(
            TenantId: tenantId,
            PanchayatNameEn: gp?.PanchayatNameEn ?? tenant.NameEn,
            PanchayatNameMr: gp?.PanchayatNameMr ?? tenant.NameMr,
            LgdCode: gp?.LgdCode ?? tenant.LgdCode,
            ActiveAssessmentYear: activeYear?.YearLabel ?? "2026-27",
            SetupProgressPct: setupProgress,
            TotalProperties: totalProperties,
            CurrentFyDemand: 0m,
            TodayCollection: 0m,
            MonthCollection: 0m,
            OutstandingArrears: 0m,
            PendingMutations: 0,
            WaterAndSpecialCollection: 0m,
            TodayReceipts: 0,
            ChecklistSteps: checklist,
            RecentActivity: recentActivity);
    }

    public async Task<DashboardSearchResultDto> SearchTenantRecordsAsync(
        Guid tenantId,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        string normalized = query.Trim().ToLowerInvariant();
        int boundedLimit = Math.Clamp(limit, 1, 25);

        List<DashboardPropertyHitDto> properties = await dbContext.Properties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId &&
                        (p.WardNo.ToLower().Contains(normalized) ||
                         p.PropertyNo.ToLower().Contains(normalized)))
            .OrderBy(p => p.WardNo)
            .ThenBy(p => p.PropertyNo)
            .Take(boundedLimit)
            .Select(p => new DashboardPropertyHitDto(
                p.Id,
                p.WardNo,
                p.PropertyNo,
                p.TotalAreaSqFt,
                p.WaterConnectionCode))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DashboardCitizenHitDto> citizens = await dbContext.Citizens
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId &&
                        (c.CitizenCode.ToLower().Contains(normalized) ||
                         c.FullNameEn.ToLower().Contains(normalized) ||
                         c.FullNameMr.ToLower().Contains(normalized) ||
                         (c.MobileNo != null && c.MobileNo.Contains(normalized))))
            .OrderBy(c => c.CitizenCode)
            .Take(boundedLimit)
            .Select(c => new DashboardCitizenHitDto(
                c.Id,
                c.CitizenCode,
                c.WardNo,
                c.FullNameEn,
                c.FullNameMr,
                c.MobileNo,
                string.IsNullOrWhiteSpace(c.AadhaarLast4) ? null : $"XXXX-XXXX-{c.AadhaarLast4}"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DashboardSearchResultDto(properties, citizens);
    }
}

