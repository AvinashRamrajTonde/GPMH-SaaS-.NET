using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Statutory Gram Panchayat fund bank account (GRAM_NIDHI, XV_FINANCE, WATER_SUPPLY) with dynamic UPI config.
/// </summary>
public sealed class BankAccount : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string FundType { get; set; }
    public required string BankName { get; set; }
    public required string BranchName { get; set; }
    public required string AccountNumber { get; set; }
    public required string IfscCode { get; set; }
    public string? UpiId { get; set; }
    public string? UpiPayeeName { get; set; }
    public string? QrImageKey { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

