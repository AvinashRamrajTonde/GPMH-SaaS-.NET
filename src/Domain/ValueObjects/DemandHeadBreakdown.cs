using System.Text.Json;

namespace GPMH.Domain.ValueObjects;

/// <summary>
/// Namuna 9 per-tax-head amounts, aligned 1:1 with <c>TaxHeadBalances</c> used by the Namuna 10 FIFO allocator
/// (water is split into General/Special for the register). <see cref="FeesAndPenalty"/> carries unpaid
/// notice/warrant/penalty (and any unclassified legacy balance) forward across financial years.
/// </summary>
public sealed record DemandHeadBreakdown(
    decimal HouseTax = 0m,
    decimal LightingTax = 0m,
    decimal HealthTax = 0m,
    decimal SanitationTax = 0m,
    decimal ToiletTax = 0m,
    decimal GeneralWaterTax = 0m,
    decimal SpecialWaterTax = 0m,
    decimal OtherTax = 0m,
    decimal FeesAndPenalty = 0m)
{
    public static readonly DemandHeadBreakdown Empty = new();

    /// <summary>House + Lighting + Health + Sanitation + Toilet + Other (no water).</summary>
    public decimal GeneralTaxTotal => HouseTax + LightingTax + HealthTax + SanitationTax + ToiletTax + OtherTax;

    public decimal WaterTotal => GeneralWaterTax + SpecialWaterTax;

    public decimal Total => GeneralTaxTotal + WaterTotal + FeesAndPenalty;

    public string ToJson() => DemandHeadJson.Serialize(this);

    public static DemandHeadBreakdown FromJson(string? json) =>
        DemandHeadJson.Deserialize<DemandHeadBreakdown>(json) ?? Empty;
}

/// <summary>Recovered amounts per head plus statutory charges paid and rebate granted (consumed by Namuna 10).</summary>
public sealed record DemandCollectedBreakdown(
    decimal HouseTax = 0m,
    decimal LightingTax = 0m,
    decimal HealthTax = 0m,
    decimal SanitationTax = 0m,
    decimal ToiletTax = 0m,
    decimal GeneralWaterTax = 0m,
    decimal SpecialWaterTax = 0m,
    decimal OtherTax = 0m,
    decimal NoticeFeePaid = 0m,
    decimal WarrantFeePaid = 0m,
    decimal PenaltyPaid = 0m,
    decimal RebateGranted = 0m)
{
    public static readonly DemandCollectedBreakdown Empty = new();

    public DemandHeadBreakdown Heads => new(
        HouseTax, LightingTax, HealthTax, SanitationTax, ToiletTax, GeneralWaterTax, SpecialWaterTax, OtherTax);

    public decimal FeesPaid => NoticeFeePaid + WarrantFeePaid + PenaltyPaid;

    public decimal Total => Heads.Total + FeesPaid;

    public string ToJson() => DemandHeadJson.Serialize(this);

    public static DemandCollectedBreakdown FromJson(string? json) =>
        DemandHeadJson.Deserialize<DemandCollectedBreakdown>(json) ?? Empty;
}

internal static class DemandHeadJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

