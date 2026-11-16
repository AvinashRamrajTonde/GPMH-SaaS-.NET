using GPMH.Application.DTOs.Namuna9Ka;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;

namespace GPMH.Application.Common.Interfaces;

public sealed record Namuna9KaBillGenContext(
    AssessmentYear Year,
    FormulaSetting? Formula,
    string? UpiId,
    string PayeeName,
    IReadOnlyList<Property> Properties,
    IReadOnlyDictionary<Guid, PropertyDemand> DemandsByProperty,
    IReadOnlyDictionary<Guid, PropertyTaxCalc> TaxCalcsByProperty,
    IReadOnlyDictionary<Guid, PropertyDemandBill> ActiveBillsByProperty);

/// <summary>Tenant-scoped repository for Namuna 9-Ka demand bill generation and QuestPDF data loading.</summary>
public interface INamuna9KaRepository
{
    Task<Namuna9KaWorkspaceDto> LoadWorkspaceAsync(
        Guid tenantId,
        Guid? assessmentYearId,
        string? wardNo,
        string? search,
        bool pendingOnly,
        IReadOnlyCollection<Guid>? propertyIds,
        CancellationToken cancellationToken);

    Task<Namuna9KaBillGenContext> LoadGenerationContextAsync(
        Guid tenantId,
        Guid? assessmentYearId,
        string? wardNo,
        IReadOnlyCollection<Guid>? propertyIds,
        CancellationToken cancellationToken);

    Task<Namuna9KaPdfHeaderModel> LoadPdfHeaderAsync(
        Guid tenantId,
        string assessmentYearLabel,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically reserves <paramref name="count"/> monotonic sequence numbers for
    /// (<paramref name="tenantId"/>, <paramref name="financialYear"/>) and returns the FIRST allocated sequence.
    /// </summary>
    Task<long> AllocateBillSequenceRangeAsync(
        Guid tenantId,
        string financialYear,
        int count,
        CancellationToken cancellationToken);

    Task AddBillsAsync(IEnumerable<PropertyDemandBill> bills, CancellationToken cancellationToken);

    Task AddAuditLogsAsync(IEnumerable<AuditLog> logs, CancellationToken cancellationToken);

    string? BuildBillUpiPayUri(
        string? upiId,
        string? payeeName,
        decimal amount,
        string billNo,
        string wardNo,
        string propertyNo);

    string FormatMarathiRupeesInWords(decimal amount);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}

