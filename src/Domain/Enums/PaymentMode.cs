namespace GPMH.Domain.Enums;

/// <summary>
/// Supported treasury collection channels for Namuna 10 and Namuna 11 receipts.
/// </summary>
public enum PaymentMode
{
    Cash = 1,
    Upi = 2,
    Cheque = 3,
    NeftRtgs = 4,
    OnlineGateway = 5,
}

