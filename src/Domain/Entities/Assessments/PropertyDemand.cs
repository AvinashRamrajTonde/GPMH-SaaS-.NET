using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Tenant-scoped annual tax demand ledger linked to a property and assessment year.
/// Created as DraftAssessment upon Namuna 8 save and promoted to DemandPublished in Namuna 9.
/// </summary>
public sealed class PropertyDemand : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid AssessmentYearId { get; set; }
    public decimal ArrearsBalance { get; set; }
    public decimal CurrentDemand { get; set; }
    public decimal RebateAmount { get; set; }
    public decimal PenaltyAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingBalance { get; set; }
    public string Status { get; set; } = "DraftAssessment";
}

