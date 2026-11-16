using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9Ka;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;
using GPMH.Infrastructure.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GPMH.Infrastructure.Persistence.Repositories;

/// <summary>Tenant-scoped EF Core repository for Namuna 9-Ka demand bill generation and QuestPDF loading.</summary>
public sealed class Namuna9KaRepository(
    AppDbContext dbContext,
    IPiiProtectionService piiProtectionService,
    IDateTimeProvider dateTimeProvider) : INamuna9KaRepository
{
    public async Task<Namuna9KaWorkspaceDto> LoadWorkspaceAsync(
        Guid tenantId,
        Guid? assessmentYearId,
        string? wardNo,
        string? search,
        bool pendingOnly,
        IReadOnlyCollection<Guid>? propertyIds,
        CancellationToken cancellationToken)
    {
        Tenant tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        List<AssessmentYear> years = [.. (await dbContext.AssessmentYears
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .OrderByDescending(y => y.StartYear)
            .ThenBy(y => y.YearLabel, StringComparer.Ordinal)];

        AssessmentYear selectedYear = ResolveYear(years, assessmentYearId);

        FormulaSetting? formula = (await dbContext.FormulaSettings
            .AsNoTracking()
            .Where(f => f.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .OrderByDescending(f => f.RateStartYear)
            .FirstOrDefault();

        GramPanchayat? gp = await dbContext.GramPanchayats
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        List<BankAccount> banks = await dbContext.BankAccounts
            .AsNoTracking()
            .Where(b => b.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        ResolvedUpiProfile upi = StatutoryPrintAssetHelper.ResolvePreferredUpi(banks, gp, tenant.NameEn);

        IQueryable<PropertyDemand> demandQuery = dbContext.PropertyDemands
            .AsNoTracking()
            .Where(d => d.AssessmentYearId == selectedYear.Id);

        if (propertyIds is { Count: > 0 })
        {
            demandQuery = demandQuery.Where(d => propertyIds.Contains(d.PropertyId));
        }

        List<PropertyDemand> demands = await demandQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

        int noticeGraceDays = formula?.NoticeGraceDays > 0
            ? formula.NoticeGraceDays
            : Namuna9RebateAndPenaltyCalculator.DefaultNoticeGraceDays;
        decimal earlyRebatePct = formula?.EarlyRebatePct ?? Namuna9RebateAndPenaltyCalculator.DefaultEarlyRebatePct;
        DateOnly defaultCutoff = Namuna9RebateAndPenaltyCalculator.DefaultRebateCutoff(selectedYear.StartYear);

        if (demands.Count == 0)
        {
            return new Namuna9KaWorkspaceDto(
                SelectedYear: ToYearDto(selectedYear),
                AssessmentYears: [.. years.Select(ToYearDto)],
                NoticeGraceDays: noticeGraceDays,
                EarlyRebatePct: earlyRebatePct,
                DefaultRebateCutoffDate: defaultCutoff,
                HasUpiConfigured: !string.IsNullOrWhiteSpace(upi.UpiId),
                ConfiguredUpiId: upi.UpiId,
                ConfiguredPayeeName: upi.PayeeName,
                TotalProperties: 0,
                BillsIssuedCount: 0,
                PendingGenerationCount: 0,
                TotalNetPayable: 0m,
                Items: []);
        }

        List<Guid> propIds = [.. demands.Select(d => d.PropertyId).Distinct()];

        List<Property> props = await dbContext.Properties
            .AsNoTracking()
            .Where(p => propIds.Contains(p.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, Property> propsById = props.ToDictionary(p => p.Id);

        var ownerRows = await (
            from po in dbContext.PropertyOwners.AsNoTracking()
            join c in dbContext.Citizens.AsNoTracking() on po.CitizenId equals c.Id
            where po.IsActive && propIds.Contains(po.PropertyId)
            select new
            {
                po.PropertyId,
                po.IsPrimaryOwner,
                po.OwnershipSharePct,
                c.FullNameMr,
                c.FullNameEn,
                c.AadhaarLast4,
                c.MobileNo,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var primaryOwnerByProp = ownerRows
            .GroupBy(o => o.PropertyId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsPrimaryOwner)
                    .ThenByDescending(x => x.OwnershipSharePct)
                    .First());

        Dictionary<Guid, PropertyTaxCalc> calcsByProp = await dbContext.PropertyTaxCalcs
            .AsNoTracking()
            .Where(c => c.AssessmentYearId == selectedYear.Id && propIds.Contains(c.PropertyId))
            .ToDictionaryAsync(c => c.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        List<PropertyDemandBill> activeBillsRaw = await dbContext.PropertyDemandBills
            .AsNoTracking()
            .Where(b => b.AssessmentYearId == selectedYear.Id &&
                        b.Status != PropertyDemandBill.StatusSuperseded &&
                        propIds.Contains(b.PropertyId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, PropertyDemandBill> activeBillsByProp = activeBillsRaw
            .GroupBy(b => b.PropertyId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(b => b.BillSequence).First());

        string? normalizedWard = string.IsNullOrWhiteSpace(wardNo) ? null : wardNo.Trim();
        string? normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        List<Namuna9KaBillItemDto> items = [];

        foreach (PropertyDemand demand in demands)
        {
            if (!propsById.TryGetValue(demand.PropertyId, out Property? prop))
            {
                continue;
            }

            if (normalizedWard is not null &&
                !string.Equals(prop.WardNo, normalizedWard, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (pendingOnly && demand.PendingBalance <= 0m)
            {
                continue;
            }

            primaryOwnerByProp.TryGetValue(prop.Id, out var owner);
            calcsByProp.TryGetValue(prop.Id, out PropertyTaxCalc? taxCalc);
            activeBillsByProp.TryGetValue(prop.Id, out PropertyDemandBill? activeBill);

            if (normalizedSearch is not null)
            {
                bool match = prop.WardNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    prop.PropertyNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (owner?.FullNameMr.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (owner?.FullNameEn.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (activeBill?.BillNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false);
                if (!match)
                {
                    continue;
                }
            }

            DemandHeadBreakdown currentArrears = DemandHeadBreakdown.FromJson(demand.ArrearsByHeadJson);
            DemandHeadBreakdown currentCurrent = ResolveCurrentHeads(demand, taxCalc, prop);
            decimal demandNoticePenalty = demand.NoticeFee + demand.WarrantFee + demand.PenaltyAmount;

            BillHeadSnapshot? snap = activeBill is not null
                ? BillHeadSnapshot.FromJson(activeBill.HeadBreakdownJson)
                : null;

            bool isStale = activeBill is not null && (snap is not null
                ? !snap.MatchesAmounts(
                    currentArrears,
                    currentCurrent,
                    demand.NoticeFee,
                    demand.WarrantFee,
                    demand.PenaltyAmount,
                    demand.RebateAmount,
                    demand.PendingBalance)
                : activeBill.ArrearsAmount != demand.ArrearsBalance ||
                  activeBill.CurrentAmount != demand.CurrentDemand ||
                  activeBill.NoticeAndPenaltyAmount != demandNoticePenalty ||
                  activeBill.RebateAmount != demand.RebateAmount ||
                  activeBill.NetPayableAmount != demand.PendingBalance);

            DemandHeadBreakdown displayArrears = snap?.Arrears ?? currentArrears;
            DemandHeadBreakdown displayCurrent = snap?.Current ?? currentCurrent;
            decimal displayNoticeFee = snap?.NoticeFee ?? demand.NoticeFee;
            decimal displayWarrantFee = snap?.WarrantFee ?? demand.WarrantFee;
            decimal displayPenalty = snap?.PenaltyAmount ?? demand.PenaltyAmount;
            decimal displayRebate = activeBill?.RebateAmount ?? demand.RebateAmount;
            decimal displayPaid = snap?.PaidAmount ?? demand.PaidAmount;
            decimal displayArrearsTotal = activeBill?.ArrearsAmount ?? demand.ArrearsBalance;
            decimal displayCurrentTotal = activeBill?.CurrentAmount ?? demand.CurrentDemand;
            decimal displayNoticeAndPenaltyTotal = activeBill?.NoticeAndPenaltyAmount ?? demandNoticePenalty;
            decimal displayNetPayable = activeBill?.NetPayableAmount ?? demand.PendingBalance;

            Namuna9KaHeadRowDto headRow = new(
                BuildingArrears: displayArrears.HouseTax,
                BuildingCurrent: displayCurrent.HouseTax,
                LightingArrears: displayArrears.LightingTax,
                LightingCurrent: displayCurrent.LightingTax,
                HealthArrears: displayArrears.HealthTax,
                HealthCurrent: displayCurrent.HealthTax,
                SanitationArrears: displayArrears.SanitationTax,
                SanitationCurrent: displayCurrent.SanitationTax,
                ToiletArrears: displayArrears.ToiletTax,
                ToiletCurrent: displayCurrent.ToiletTax,
                GeneralWaterArrears: displayArrears.GeneralWaterTax,
                GeneralWaterCurrent: displayCurrent.GeneralWaterTax,
                SpecialWaterArrears: displayArrears.SpecialWaterTax,
                SpecialWaterCurrent: displayCurrent.SpecialWaterTax,
                OtherArrears: displayArrears.OtherTax,
                OtherCurrent: displayCurrent.OtherTax,
                CarriedFeesAndPenalty: displayArrears.FeesAndPenalty,
                NoticeFee: displayNoticeFee,
                WarrantFee: displayWarrantFee,
                PenaltyAmount: displayPenalty,
                RebateAmount: displayRebate,
                PaidAmount: displayPaid,
                TotalArrears: displayArrearsTotal,
                TotalCurrent: displayCurrentTotal,
                TotalPayable: displayNetPayable);

            decimal generalTotal =
                displayArrears.GeneralTaxTotal + displayCurrent.GeneralTaxTotal;
            decimal waterTotal =
                displayArrears.WaterTotal + displayCurrent.WaterTotal;

            string? maskedAadhaar = !string.IsNullOrWhiteSpace(owner?.AadhaarLast4)
                ? $"XXXX-XXXX-{owner.AadhaarLast4}"
                : null;
            string? maskedMobile = !string.IsNullOrWhiteSpace(owner?.MobileNo) && owner.MobileNo.Length >= 4
                ? $"XXXXXX{owner.MobileNo[^4..]}"
                : null;

            string occupier = !string.IsNullOrWhiteSpace(prop.OccupierName)
                ? prop.OccupierName
                : (!string.IsNullOrWhiteSpace(owner?.FullNameMr) ? "स्वतः (Self)" : "-");

            string? previewUpi = activeBill is not null
                ? activeBill.UpiPayUri
                : (displayNetPayable > 0m
                    ? BuildBillUpiPayUri(upi.UpiId, upi.PayeeName, displayNetPayable, "PREVIEW", prop.WardNo, prop.PropertyNo)
                    : null);

            items.Add(new Namuna9KaBillItemDto(
                DemandId: demand.Id,
                BillId: activeBill?.Id,
                PropertyId: prop.Id,
                WardNo: prop.WardNo,
                PropertyNo: prop.PropertyNo,
                PrimaryOwnerNameMr: owner?.FullNameMr,
                PrimaryOwnerNameEn: owner?.FullNameEn,
                PrimaryOwnerAadhaarMasked: maskedAadhaar,
                PrimaryOwnerMobileMasked: maskedMobile,
                OccupierName: occupier,
                WaterConnectionCode: prop.WaterConnectionCode,
                IsTaxExempt: prop.IsTaxExempt,
                BillNo: activeBill?.BillNo,
                BillSequence: activeBill?.BillSequence,
                IssueDate: activeBill?.IssueDate,
                DueDate: activeBill?.DueDate,
                RebateCutoffDate: activeBill?.RebateCutoffDate ?? demand.RebateCutoffDate,
                BillStatus: activeBill?.Status ?? "PendingGeneration",
                DemandStatus: demand.Status,
                ArrearsAmount: displayArrearsTotal,
                CurrentAmount: displayCurrentTotal,
                GeneralDemandTotal: generalTotal,
                WaterDemandTotal: waterTotal,
                NoticeAndPenaltyAmount: displayNoticeAndPenaltyTotal,
                RebateAmount: displayRebate,
                PaidAmount: displayPaid,
                NetPayableAmount: displayNetPayable,
                AmountInWordsMr: MarathiNumberToWords.ToRupeesWords(displayNetPayable),
                UpiIdUsed: activeBill?.UpiIdUsed ?? upi.UpiId,
                UpiPayUri: previewUpi,
                IsStaleBill: isStale,
                Heads: headRow));
        }

        List<Namuna9KaBillItemDto> ordered =
        [
            .. items
                .OrderBy(i => i.WardNo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.PropertyNo, StringComparer.OrdinalIgnoreCase),
        ];

        return new Namuna9KaWorkspaceDto(
            SelectedYear: ToYearDto(selectedYear),
            AssessmentYears: [.. years.Select(ToYearDto)],
            NoticeGraceDays: noticeGraceDays,
            EarlyRebatePct: earlyRebatePct,
            DefaultRebateCutoffDate: defaultCutoff,
            HasUpiConfigured: !string.IsNullOrWhiteSpace(upi.UpiId),
            ConfiguredUpiId: upi.UpiId,
            ConfiguredPayeeName: upi.PayeeName,
            TotalProperties: ordered.Count,
            BillsIssuedCount: ordered.Count(i => i.BillId.HasValue),
            PendingGenerationCount: ordered.Count(i => !i.BillId.HasValue),
            TotalNetPayable: ordered.Sum(i => i.NetPayableAmount),
            Items: ordered);
    }

    public async Task<Namuna9KaBillGenContext> LoadGenerationContextAsync(
        Guid tenantId,
        Guid? assessmentYearId,
        string? wardNo,
        IReadOnlyCollection<Guid>? propertyIds,
        CancellationToken cancellationToken)
    {
        Tenant tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        List<AssessmentYear> years = await dbContext.AssessmentYears
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        AssessmentYear year = ResolveYear(years, assessmentYearId);

        FormulaSetting? formula = (await dbContext.FormulaSettings
            .AsNoTracking()
            .Where(f => f.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .OrderByDescending(f => f.RateStartYear)
            .FirstOrDefault();

        GramPanchayat? gp = await dbContext.GramPanchayats
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        List<BankAccount> banks = await dbContext.BankAccounts
            .AsNoTracking()
            .Where(b => b.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        ResolvedUpiProfile upi = StatutoryPrintAssetHelper.ResolvePreferredUpi(banks, gp, tenant.NameEn);

        IQueryable<Property> propQuery = dbContext.Properties.AsNoTracking();
        if (propertyIds is { Count: > 0 })
        {
            propQuery = propQuery.Where(p => propertyIds.Contains(p.Id));
        }

        List<Property> props = await propQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(wardNo))
        {
            string normalizedWard = wardNo.Trim();
            props = [.. props.Where(p => string.Equals(p.WardNo, normalizedWard, StringComparison.OrdinalIgnoreCase))];
        }

        props =
        [
            .. props
                .OrderBy(p => p.WardNo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.PropertyNo, StringComparer.OrdinalIgnoreCase),
        ];

        List<Guid> pids = [.. props.Select(p => p.Id)];

        Dictionary<Guid, PropertyDemand> demands = pids.Count == 0
            ? []
            : await dbContext.PropertyDemands
                .AsNoTracking()
                .Where(d => d.AssessmentYearId == year.Id && pids.Contains(d.PropertyId))
                .ToDictionaryAsync(d => d.PropertyId, cancellationToken)
                .ConfigureAwait(false);

        Dictionary<Guid, PropertyTaxCalc> calcs = pids.Count == 0
            ? []
            : await dbContext.PropertyTaxCalcs
                .AsNoTracking()
                .Where(c => c.AssessmentYearId == year.Id && pids.Contains(c.PropertyId))
                .ToDictionaryAsync(c => c.PropertyId, cancellationToken)
                .ConfigureAwait(false);

        List<PropertyDemandBill> activeBillsList = pids.Count == 0
            ? []
            : await dbContext.PropertyDemandBills
                .Where(b => b.AssessmentYearId == year.Id &&
                            b.Status != PropertyDemandBill.StatusSuperseded &&
                            pids.Contains(b.PropertyId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        Dictionary<Guid, PropertyDemandBill> activeBills = activeBillsList
            .GroupBy(b => b.PropertyId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(b => b.BillSequence).First());

        return new Namuna9KaBillGenContext(
            Year: year,
            Formula: formula,
            UpiId: upi.UpiId,
            PayeeName: upi.PayeeName,
            Properties: props,
            DemandsByProperty: demands,
            TaxCalcsByProperty: calcs,
            ActiveBillsByProperty: activeBills);
    }

    public async Task<Namuna9KaPdfHeaderModel> LoadPdfHeaderAsync(
        Guid tenantId,
        string assessmentYearLabel,
        CancellationToken cancellationToken)
    {
        Tenant tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        GramPanchayat? gp = await dbContext.GramPanchayats
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        ProtectedSignatoryAssets sigs = await StatutoryPrintAssetHelper
            .LoadSignatoryAssetsAsync(dbContext, piiProtectionService, tenantId, gp, cancellationToken)
            .ConfigureAwait(false);

        return new Namuna9KaPdfHeaderModel(
            TenantId: tenantId,
            LgdCode: gp?.LgdCode ?? tenant.LgdCode,
            PanchayatNameMr: gp?.PanchayatNameMr ?? tenant.NameMr,
            PanchayatNameEn: gp?.PanchayatNameEn ?? tenant.NameEn,
            VillageNameMr: gp?.PanchayatNameMr ?? tenant.NameMr,
            TehsilName: gp?.TehsilName ?? "-",
            DistrictName: gp?.DistrictName ?? "-",
            Pincode: gp?.Pincode,
            AssessmentYearLabel: assessmentYearLabel,
            GramsevakName: gp?.GramsevakName,
            SarpanchName: gp?.SarpanchName,
            OperatorName: gp?.OperatorName,
            GramsevakSignatureBytes: sigs.GramsevakSignatureBytes,
            SarpanchSignatureBytes: sigs.SarpanchSignatureBytes,
            OfficialStampBytes: sigs.OfficialStampBytes);
    }

    public async Task<long> AllocateBillSequenceRangeAsync(
        Guid tenantId,
        string financialYear,
        int count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return 0;
        }

        string fy = financialYear.Trim();
        DateTimeOffset now = dateTimeProvider.UtcNow;

        if (string.Equals(dbContext.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            const string sql = """
                INSERT INTO "bill_counters" ("TenantId", "FinancialYear", "LastSequence", "UpdatedAtUtc")
                VALUES (@p0, @p1, @p2, @p3)
                ON CONFLICT ("TenantId", "FinancialYear")
                DO UPDATE SET
                    "LastSequence" = "bill_counters"."LastSequence" + EXCLUDED."LastSequence",
                    "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
                RETURNING "LastSequence" AS "Value";
                """;

            long endSeq = await dbContext.Database
                .SqlQueryRaw<long>(
                    sql,
                    new NpgsqlParameter("p0", tenantId),
                    new NpgsqlParameter("p1", fy),
                    new NpgsqlParameter("p2", (long)count),
                    new NpgsqlParameter("p3", now))
                .FirstAsync(cancellationToken)
                .ConfigureAwait(false);

            return endSeq - count + 1;
        }

        BillCounter? counter = await dbContext.BillCounters
            .FirstOrDefaultAsync(c => c.FinancialYear == fy, cancellationToken)
            .ConfigureAwait(false);

        long firstSeq;
        if (counter is null)
        {
            firstSeq = 1;
            dbContext.BillCounters.Add(new BillCounter
            {
                TenantId = tenantId,
                FinancialYear = fy,
                LastSequence = count,
                UpdatedAtUtc = now,
            });
        }
        else
        {
            firstSeq = counter.LastSequence + 1;
            counter.LastSequence += count;
            counter.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return firstSeq;
    }

    public Task AddBillsAsync(IEnumerable<PropertyDemandBill> bills, CancellationToken cancellationToken) =>
        dbContext.PropertyDemandBills.AddRangeAsync(bills, cancellationToken);

    public Task AddAuditLogsAsync(IEnumerable<AuditLog> logs, CancellationToken cancellationToken) =>
        dbContext.AuditLogs.AddRangeAsync(logs, cancellationToken);

    public string? BuildBillUpiPayUri(
        string? upiId,
        string? payeeName,
        decimal amount,
        string billNo,
        string wardNo,
        string propertyNo) =>
        StatutoryPrintAssetHelper.BuildUpiPayUri(
            upiId,
            payeeName,
            amount,
            transactionNote: $"TaxBill-{wardNo}-{propertyNo}",
            transactionRef: billNo,
            twoDecimalPlaces: true);

    public string FormatMarathiRupeesInWords(decimal amount) =>
        MarathiNumberToWords.ToRupeesWords(amount);

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

    private static AssessmentYear ResolveYear(IReadOnlyList<AssessmentYear> years, Guid? yearId)
    {
        AssessmentYear? resolved = yearId.HasValue
            ? years.FirstOrDefault(y => y.Id == yearId.Value)
            : years.OrderByDescending(y => y.IsActive).ThenByDescending(y => y.StartYear).FirstOrDefault();

        return resolved ?? throw new NotFoundException(
            yearId.HasValue
                ? $"Assessment year '{yearId}' was not found."
                : "No active assessment year is configured for this Gram Panchayat.");
    }

    private static Namuna9KaYearSummaryDto ToYearDto(AssessmentYear y) =>
        new(y.Id, y.YearLabel, y.StartYear, y.EndYear, y.IsActive, y.IsLocked);

    private static DemandHeadBreakdown ResolveCurrentHeads(PropertyDemand demand, PropertyTaxCalc? calc, Property property)
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
}
