using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;

namespace GPMH.Application.Features.Namuna9;

internal static class Namuna9Support
{
    public static Namuna9YearDto ToDto(AssessmentYear y) =>
        new(y.Id, y.YearLabel, y.StartYear, y.EndYear, y.IsActive, y.IsLocked);

    public static async Task<AssessmentYear> ResolveYearAsync(
        INamuna9Repository repository,
        Guid? yearId,
        CancellationToken cancellationToken)
    {
        return await repository.GetYearAsync(yearId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(
                yearId.HasValue
                    ? $"Assessment year '{yearId}' was not found."
                    : "No active assessment year is configured for this Gram Panchayat.");
    }

    public static void EnsureNotLocked(AssessmentYear year)
    {
        if (year.IsLocked)
        {
            throw new DomainConflictException(
                "ASSESSMENT_YEAR_LOCKED",
                $"Assessment year {year.YearLabel} is locked after financial-year rollover and cannot be modified.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["assessmentYearId"] = year.Id,
                    ["yearLabel"] = year.YearLabel,
                });
        }
    }

    public static DemandHeadBreakdown ResolveCurrentHeads(PropertyDemand demand, PropertyTaxCalc? calc, Property property)
    {
        DemandHeadBreakdown current = DemandHeadBreakdown.FromJson(demand.CurrentByHeadJson);
        if (current.Total > 0m || demand.CurrentDemand <= 0m)
        {
            return current;
        }

        return calc is not null
            ? DemandHeadMapper.FromTaxHeads(
                calc.BuildingTax, calc.OpenLandTax, calc.LightingTax, calc.HealthTax, calc.SanitationTax,
                calc.ToiletTax, calc.WaterTax, calc.OtherTax, property.WaterConnectionCode)
            : new DemandHeadBreakdown(OtherTax: demand.CurrentDemand);
    }

    public static Namuna9DemandRowDto ToRow(Namuna9DemandContext ctx)
    {
        PropertyDemand d = ctx.Demand;
        DemandHeadBreakdown arrears = DemandHeadBreakdown.FromJson(d.ArrearsByHeadJson);
        DemandHeadBreakdown current = ResolveCurrentHeads(d, ctx.TaxCalc, ctx.Property);
        DemandCollectedBreakdown collected = DemandCollectedBreakdown.FromJson(d.CollectedByHeadJson);

        DemandHeadBreakdown balance = DemandLedgerCalculator.ComputeRemaining(
            arrears, current, collected, d.NoticeFee, d.WarrantFee, d.PenaltyAmount, d.RebateAmount, d.PendingBalance);

        static Namuna9HeadAmountsDto H(decimal a, decimal c) => new(a, c, a + c);

        return new Namuna9DemandRowDto(
            DemandId: d.Id,
            PropertyId: ctx.Property.Id,
            WardNo: ctx.Property.WardNo,
            PropertyNo: ctx.Property.PropertyNo,
            PrimaryOwnerNameMr: ctx.OwnerNameMr,
            PrimaryOwnerNameEn: ctx.OwnerNameEn,
            WaterConnectionCode: ctx.Property.WaterConnectionCode,
            IsTaxExempt: ctx.Property.IsTaxExempt,
            HouseTax: H(arrears.HouseTax, current.HouseTax),
            LightingTax: H(arrears.LightingTax, current.LightingTax),
            HealthTax: H(arrears.HealthTax, current.HealthTax),
            SanitationTax: H(arrears.SanitationTax, current.SanitationTax),
            ToiletTax: H(arrears.ToiletTax, current.ToiletTax),
            GeneralWaterTax: H(arrears.GeneralWaterTax, current.GeneralWaterTax),
            SpecialWaterTax: H(arrears.SpecialWaterTax, current.SpecialWaterTax),
            OtherTax: H(arrears.OtherTax, current.OtherTax),
            CarriedFeesAndPenalty: arrears.FeesAndPenalty,
            NoticeFee: d.NoticeFee,
            WarrantFee: d.WarrantFee,
            PenaltyAmount: d.PenaltyAmount,
            RebateAmount: d.RebateAmount,
            TotalArrears: d.ArrearsBalance,
            TotalCurrent: d.CurrentDemand,
            NetDemand: d.TotalNetDemand,
            CollectedByHead: collected,
            BalanceByHead: balance,
            PaidAmount: d.PaidAmount,
            PendingBalance: d.PendingBalance,
            Status: d.Status,
            EarlyRebateApplied: d.EarlyRebateApplied,
            RebateCutoffDate: d.RebateCutoffDate,
            Remarks: d.DemandRemarks,
            PublishedAtUtc: d.PublishedAtUtc);
    }

    public static Namuna9FormulaRatesDto ToRates(FormulaSetting? f, int startYear) =>
        new(
            RatePeriodName: f?.RatePeriodName ?? string.Empty,
            TaxDivisor: f?.TaxDivisor ?? 1000m,
            NoticeGraceDays: f?.NoticeGraceDays ?? Namuna9RebateAndPenaltyCalculator.DefaultNoticeGraceDays,
            EarlyRebatePct: f?.EarlyRebatePct ?? Namuna9RebateAndPenaltyCalculator.DefaultEarlyRebatePct,
            LatePenaltyPct: f?.LatePenaltyPct ?? Namuna9RebateAndPenaltyCalculator.DefaultLatePenaltyPct,
            DefaultRebateCutoffDate: Namuna9RebateAndPenaltyCalculator.DefaultRebateCutoff(startYear));

    public static AuditLog CreateAudit(
        ICurrentUserService user,
        IDateTimeProvider clock,
        Guid tenantId,
        string entityName,
        string entityId,
        string action,
        string? oldJson,
        string? newJson) =>
        new()
        {
            TenantId = tenantId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ActorId = user.UserId?.ToString(),
            ActorRole = user.Role?.ToString(),
            CorrelationId = user.CorrelationId,
            IpAddress = user.IpAddress,
            UserAgent = user.UserAgent,
            OldValuesJson = oldJson,
            NewValuesJson = newJson,
            TimestampUtc = clock.UtcNow,
        };

    public static void ApplyStatus(PropertyDemand d)
    {
        if (string.Equals(d.Status, PropertyDemand.StatusDraft, StringComparison.Ordinal))
        {
            return;
        }

        d.Status = d.PendingBalance <= 0m
            ? PropertyDemand.StatusSettled
            : d.PaidAmount > 0m
                ? PropertyDemand.StatusPartiallyPaid
                : PropertyDemand.StatusPublished;
    }
}

