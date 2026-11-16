using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;

namespace GPMH.Application.Common.Interfaces;

public sealed record Namuna10DemandContext(
    PropertyDemand Demand,
    Property Property,
    PropertyTaxCalc? TaxCalc,
    string? OwnerNameMr,
    string? OwnerNameEn,
    string? OwnerAadhaarMasked,
    string? OwnerMobileMasked,
    PropertyDemandBill? ActiveBill,
    TaxReceipt? LastReceipt);

public sealed record Namuna10CollectionContext(
    AssessmentYear Year,
    FormulaSetting? Formula,
    Property Property,
    PropertyDemand Demand,
    PropertyTaxCalc? TaxCalc,
    PropertyDemandBill? ActiveBill,
    string? DefaultOwnerNameMr,
    string? DefaultOwnerNameEn,
    string? OwnerAadhaarMasked,
    string? OwnerMobileMasked);

public sealed record Namuna10ReceiptContext(
    TaxReceipt Receipt,
    AssessmentYear Year,
    Property Property,
    PropertyDemand Demand,
    PropertyDemandBill? Bill,
    string? OwnerNameMr,
    string? OwnerNameEn,
    string? OwnerAadhaarMasked,
    string? OwnerMobileMasked);

/// <summary>Tenant-scoped repository for Namuna 10 collection register, receipts, and FIFO ledger updates.</summary>
public interface INamuna10Repository
{
    Task<IReadOnlyList<AssessmentYear>> ListYearsAsync(CancellationToken cancellationToken);

    Task<AssessmentYear?> GetYearAsync(Guid? yearId, CancellationToken cancellationToken);

    Task<FormulaSetting?> GetActiveFormulaSettingAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Namuna10DemandContext>> LoadRegisterContextsAsync(
        Guid assessmentYearId,
        string? wardNo,
        string? search,
        CancellationToken cancellationToken);

    Task<Namuna10ReceiptContext?> FindReceiptByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken);

    Task<Namuna10CollectionContext?> LoadCollectionContextAsync(
        Guid propertyId,
        Guid assessmentYearId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Namuna10ReceiptContext>> ListReceiptsAsync(
        Guid? assessmentYearId,
        Guid? propertyId,
        DateOnly? fromDate,
        DateOnly? toDate,
        PaymentMode? mode,
        string? wardNo,
        string? search,
        bool includeCancelled,
        CancellationToken cancellationToken);

    Task<Namuna10ReceiptContext?> GetReceiptByIdAsync(
        Guid receiptId,
        CancellationToken cancellationToken);

    Task<long> AllocateNextReceiptSequenceAsync(
        Guid tenantId,
        string financialYear,
        CancellationToken cancellationToken);

    Task AddReceiptAsync(TaxReceipt receipt, CancellationToken cancellationToken);

    Task AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken);

    string FormatMarathiRupeesInWords(decimal amount);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}

