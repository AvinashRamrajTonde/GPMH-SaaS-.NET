using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Tenant-scoped Gram Panchayat profile synchronized from the LGD directory with audited local overrides.
/// </summary>
public sealed class GramPanchayat : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string LgdCode { get; set; }
    public required string DistrictCode { get; set; }
    public required string DistrictName { get; set; }
    public required string TehsilCode { get; set; }
    public required string TehsilName { get; set; }
    public required string PanchayatNameEn { get; set; }
    public required string PanchayatNameMr { get; set; }
    public string? Pincode { get; set; }
    public int TotalWards { get; set; } = 1;
    public int? PopulationCensus { get; set; }
    public string? UpiId { get; set; }
    public string? UpiPayeeName { get; set; }
    public string? BankAccountLast4 { get; set; }
    public string? OverridesJson { get; set; }
}
