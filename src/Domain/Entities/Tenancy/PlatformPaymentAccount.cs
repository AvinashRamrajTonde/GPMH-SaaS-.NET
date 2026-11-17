using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Platform-wide payment collection configuration (UPI ID, bank details, annual fee, and GST settings).
/// </summary>
public sealed class PlatformPaymentAccount : BaseEntity, IGlobalEntity
{
    public static readonly Guid DefaultAccountId = new("11111111-1111-1111-1111-111111116000");

    public required string AccountName { get; set; }
    public required string BankName { get; set; }
    public required string BranchName { get; set; }
    public required string AccountNumber { get; set; }
    public required string IfscCode { get; set; }
    public required string UpiId { get; set; }
    public decimal AnnualFee { get; set; } = 6000.00m;
    public decimal GstPercent { get; set; } = 18.00m;
    public string? Gstin { get; set; }
    public string? SupportPhone { get; set; }
    public string? SupportEmail { get; set; }
    public bool IsActive { get; set; } = true;
}

