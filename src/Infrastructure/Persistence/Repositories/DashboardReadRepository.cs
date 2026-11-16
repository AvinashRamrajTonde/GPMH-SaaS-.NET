using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class DashboardReadRepository(
    AppDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : IDashboardReadRepository
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

        decimal currentFyDemand = 0m;
        decimal outstandingArrears = 0m;
        if (activeYear is not null)
        {
            var demands = await dbContext.PropertyDemands
                .AsNoTracking()
                .Where(d => d.AssessmentYearId == activeYear.Id)
                .Select(d => new { d.CurrentDemand, d.ArrearsBalance, d.PendingBalance })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            currentFyDemand = demands.Sum(d => d.CurrentDemand);
            outstandingArrears = demands.Sum(d => Math.Min(d.ArrearsBalance, d.PendingBalance));
        }

        int pendingMutations = await dbContext.PropertyMutations
            .AsNoTracking()
            .CountAsync(m => m.Status == "NoticePublished" || m.Status == "ObjectionRecorded", cancellationToken)
            .ConfigureAwait(false);

        DateOnly todayUtc = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var activeReceipts = await dbContext.TaxReceipts
            .AsNoTracking()
            .Where(r => !r.IsCancelled)
            .Select(r => new
            {
                r.AssessmentYearId,
                r.ReceiptDate,
                r.TotalPaid,
                r.HeadAllocationJson,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal todayCollection = activeReceipts
            .Where(r => r.ReceiptDate == todayUtc)
            .Sum(r => r.TotalPaid);

        int todayReceipts = activeReceipts.Count(r => r.ReceiptDate == todayUtc);

        decimal monthCollection = activeReceipts
            .Where(r => r.ReceiptDate.Year == todayUtc.Year && r.ReceiptDate.Month == todayUtc.Month)
            .Sum(r => r.TotalPaid);

        decimal waterAndSpecialCollection = (activeYear is not null
                ? activeReceipts.Where(r => r.AssessmentYearId == activeYear.Id)
                : activeReceipts)
            .Sum(r => TaxReceiptHeadAllocation.FromJson(r.HeadAllocationJson).TotalWaterAllocated);

        return new DashboardSummaryDto(
            TenantId: tenantId,
            PanchayatNameEn: gp?.PanchayatNameEn ?? tenant.NameEn,
            PanchayatNameMr: gp?.PanchayatNameMr ?? tenant.NameMr,
            LgdCode: gp?.LgdCode ?? tenant.LgdCode,
            ActiveAssessmentYear: activeYear?.YearLabel ?? "2026-27",
            SetupProgressPct: setupProgress,
            TotalProperties: totalProperties,
            CurrentFyDemand: currentFyDemand,
            TodayCollection: todayCollection,
            MonthCollection: monthCollection,
            OutstandingArrears: outstandingArrears,
            PendingMutations: pendingMutations,
            WaterAndSpecialCollection: waterAndSpecialCollection,
            TodayReceipts: todayReceipts,
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

