using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Tenant-scoped Namuna 8 assessable property with partition lineage (ParentPropertyId).
/// </summary>
public sealed class Property : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? ParentPropertyId { get; set; }
    public required string WardNo { get; set; }
    public required string PropertyNo { get; set; }
    public decimal BuiltUpAreaSqFt { get; set; }
    public decimal OpenPlotAreaSqFt { get; set; }
    public decimal TotalAreaSqFt { get; set; }
    public string? BoundaryEast { get; set; }
    public string? BoundaryWest { get; set; }
    public string? BoundaryNorth { get; set; }
    public string? BoundarySouth { get; set; }
    public string WaterConnectionCode { get; set; } = "NONE";
    public bool IsTaxExempt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
}

