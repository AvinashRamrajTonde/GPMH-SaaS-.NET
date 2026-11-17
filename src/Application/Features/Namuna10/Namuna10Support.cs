using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Application.DTOs.Namuna10;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;

namespace GPMH.Application.Features.Namuna10;

internal static class Namuna10Support
{
    public static Namuna9YearDto ToYearDto(AssessmentYear y) =>
        new(y.Id, y.YearLabel, y.StartYear, y.EndYear, y.IsActive, y.IsLocked);

    public static async Task<AssessmentYear> ResolveYearAsync(
        INamuna10Repository repository,
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

    public static Namuna10DueBreakdown ComputeRemainingDues(PropertyDemand d, PropertyTaxCalc? calc, Property p)
    {
        DemandHeadBreakdown arrears = DemandHeadBreakdown.FromJson(d.ArrearsByHeadJson);
        DemandHeadBreakdown current = ResolveCurrentHeads(d, calc, p);
        DemandCollectedBreakdown collected = DemandCollectedBreakdown.FromJson(d.CollectedByHeadJson);

        return Namuna10FifoBridge.ComputeRemainingDues(
            arrears: arrears,
            current: current,
            collected: collected,
            noticeFee: d.NoticeFee,
            warrantFee: d.WarrantFee,
            penaltyAmount: d.PenaltyAmount,
            rebateAmount: d.RebateAmount,
            pendingBalance: d.PendingBalance);
    }

    public static TaxHeadDuesDto ToHeadDuesDto(DemandHeadBreakdown b) =>
        new(
            HouseTax: b.HouseTax,
            LightingTax: b.LightingTax,
            HealthTax: b.HealthTax,
            SanitationTax: b.SanitationTax,
            ToiletTax: b.ToiletTax,
            GeneralWaterTax: b.GeneralWaterTax,
            SpecialWaterTax: b.SpecialWaterTax,
            OtherTax: b.OtherTax,
            GeneralTaxTotal: b.GeneralTaxTotal,
            WaterTotal: b.WaterTotal,
            Total: b.GeneralTaxTotal + b.WaterTotal);

    public static Namuna10RegisterRowDto ToRegisterRowDto(Namuna10DemandContext ctx)
    {
        PropertyDemand d = ctx.Demand;
        Namuna10DueBreakdown due = ComputeRemainingDues(d, ctx.TaxCalc, ctx.Property);

        Namuna10PropertySummaryReceiptDto? lastReceipt = ctx.LastReceipt is null
            ? null
            : new Namuna10PropertySummaryReceiptDto(
                ReceiptId: ctx.LastReceipt.Id,
                ReceiptNo: ctx.LastReceipt.ReceiptNo,
                BookNo: ctx.LastReceipt.BookNo,
                ReceiptDate: ctx.LastReceipt.ReceiptDate,
                PaymentMode: ctx.LastReceipt.PaymentMode,
                TotalPaid: ctx.LastReceipt.TotalPaid,
                IsCancelled: ctx.LastReceipt.IsCancelled);

        return new Namuna10RegisterRowDto(
            DemandId: d.Id,
            PropertyId: ctx.Property.Id,
            WardNo: ctx.Property.WardNo,
            PropertyNo: ctx.Property.PropertyNo,
            PrimaryOwnerNameMr: ctx.OwnerNameMr,
            PrimaryOwnerNameEn: ctx.OwnerNameEn,
            PrimaryOwnerAadhaarMasked: ctx.OwnerAadhaarMasked,
            PrimaryOwnerMobileMasked: ctx.OwnerMobileMasked,
            WaterConnectionCode: ctx.Property.WaterConnectionCode,
            IsTaxExempt: ctx.Property.IsTaxExempt,
            DemandStatus: d.Status,
            ActiveBillNo: ctx.ActiveBill?.BillNo,
            ActiveBillId: ctx.ActiveBill?.Id,
            ArrearsDue: ToHeadDuesDto(due.ArrearsDue),
            CurrentDue: ToHeadDuesDto(due.CurrentDue),
            NoticeFeeDue: due.NoticeFeeDue,
            WarrantFeeDue: due.WarrantFeeDue,
            PenaltyDue: due.PenaltyDue,
            CarriedFeesDue: due.CarriedFeesDue,
            RebateAmount: d.RebateAmount,
            EarlyRebateEligible: d.EarlyRebateEligible,
            EarlyRebateApplied: d.EarlyRebateApplied,
            RebateCutoffDate: d.RebateCutoffDate,
            NetDemand: d.TotalNetDemand,
            PaidAmount: d.PaidAmount,
            RemainingBalance: d.PendingBalance,
            LastReceipt: lastReceipt);
    }

    public static TaxReceiptDto ToReceiptDto(
        Namuna10ReceiptContext ctx,
        INamuna10Repository repository,
        bool idempotentReplay = false)
    {
        TaxReceipt r = ctx.Receipt;
        return new TaxReceiptDto(
            ReceiptId: r.Id,
            TenantId: r.TenantId,
            PropertyId: r.PropertyId,
            PropertyDemandId: r.PropertyDemandId,
            AssessmentYearId: r.AssessmentYearId,
            YearLabel: ctx.Year.YearLabel,
            OperationId: r.OperationId,
            DemandBillId: r.DemandBillId,
            BillNo: ctx.Bill?.BillNo,
            WardNo: ctx.Property.WardNo,
            PropertyNo: ctx.Property.PropertyNo,
            PrimaryOwnerNameMr: ctx.OwnerNameMr,
            PrimaryOwnerNameEn: ctx.OwnerNameEn,
            PrimaryOwnerAadhaarMasked: ctx.OwnerAadhaarMasked,
            PrimaryOwnerMobileMasked: ctx.OwnerMobileMasked,
            BookNo: r.BookNo,
            ReceiptNo: r.ReceiptNo,
            ReceiptSequence: r.ReceiptSequence,
            ReceiptDate: r.ReceiptDate,
            CollectedAtUtc: r.CollectedAtUtc,
            PaymentMode: r.PaymentMode,
            PayerName: r.PayerName,
            ReferenceNo: r.ReferenceNo,
            BankName: r.BankName,
            ChequeDate: r.ChequeDate,
            Remarks: r.Remarks,
            TotalPaid: r.TotalPaid,
            DiscountGiven: r.DiscountGiven,
            ExcessCredit: r.ExcessCredit,
            RemainingBalanceAfterPayment: ctx.Demand.PendingBalance,
            HeadAllocation: TaxReceiptHeadAllocation.FromJson(r.HeadAllocationJson),
            AmountInWordsMr: repository.FormatMarathiRupeesInWords(r.TotalPaid),
            IsCancelled: r.IsCancelled,
            CancelReason: r.CancelReason,
            CancelledAtUtc: r.CancelledAtUtc,
            IdempotentReplay: idempotentReplay);
    }

    public static AuditLog CreateAudit(
        Guid tenantId,
        Guid actorId,
        string actorRole,
        string entityName,
        string entityId,
        string action,
        string? oldJson,
        string? newJson,
        DateTimeOffset utcNow) =>
        new()
        {
            TenantId = tenantId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ActorId = actorId.ToString("D"),
            ActorRole = actorRole,
            OldValuesJson = oldJson,
            NewValuesJson = newJson,
            TimestampUtc = utcNow,
        };
}

