using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Individual floor or structure assessment block attached to a Namuna 8 property.
/// </summary>
public sealed class PropertyConstruction : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PropertyId { get; set; }
    public int FloorNo { get; set; }
    public required string FloorLabel { get; set; }
    public required string ConstructionTypeCode { get; set; }
    public required string UsageCode { get; set; }
    public decimal LengthFt { get; set; }
    public decimal WidthFt { get; set; }
    public decimal AreaSqFt { get; set; }
    public decimal AreaSqM { get; set; }
    public int ConstructionStartYear { get; set; }
    public bool IsActive { get; set; } = true;
}

