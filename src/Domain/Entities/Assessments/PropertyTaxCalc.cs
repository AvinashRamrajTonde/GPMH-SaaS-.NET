using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Immutable per-assessment-year capital value tax calculation snapshot for a Namuna 8 property.
/// </summary>
public sealed class PropertyTaxCalc : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid AssessmentYearId { get; set; }
    public decimal BuildingTax { get; set; }
    public decimal OpenLandTax { get; set; }
    public decimal LightingTax { get; set; }
    public decimal HealthTax { get; set; }
    public decimal SanitationTax { get; set; }
    public decimal ToiletTax { get; set; }
    public decimal WaterTax { get; set; }
    public decimal OtherTax { get; set; }
    public decimal TotalTax { get; set; }
    public required string CalculationSnapshotJson { get; set; }
}

