namespace GPMH.Application.DTOs.Properties;

public sealed record PropertyDto(
    Guid Id,
    Guid TenantId,
    Guid? ParentPropertyId,
    string WardNo,
    string PropertyNo,
    decimal BuiltUpAreaSqFt,
    decimal OpenPlotAreaSqFt,
    decimal TotalAreaSqFt,
    string? BoundaryEast,
    string? BoundaryWest,
    string? BoundaryNorth,
    string? BoundarySouth,
    string WaterConnectionCode,
    bool IsTaxExempt,
    bool IsActive);

