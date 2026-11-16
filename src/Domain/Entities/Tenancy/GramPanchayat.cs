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
    public string? CensusCode { get; set; }
    public string? OfficeAddress { get; set; }
    public int TotalWards { get; set; } = 1;
    public int? PopulationCensus { get; set; }
    public string? SarpanchName { get; set; }
    public string? SarpanchMobile { get; set; }
    public string? UpSarpanchName { get; set; }
    public string? GramsevakName { get; set; }
    public string? GramsevakMobile { get; set; }
    public string? OperatorName { get; set; }
    public string? OperatorMobile { get; set; }
    public string? GramsevakSignatureKey { get; set; }
    public string? SarpanchSignatureKey { get; set; }
    public string? OperatorSignatureKey { get; set; }
    public string? OfficialStampKey { get; set; }
    public bool IsArrearsRolloverInitialized { get; set; }
    public int SetupProgressPct { get; set; }
    public string? UpiId { get; set; }
    public string? UpiPayeeName { get; set; }
    public string? BankAccountLast4 { get; set; }
    public string? OverridesJson { get; set; }
    public string? ProvenanceJson { get; set; }
}
