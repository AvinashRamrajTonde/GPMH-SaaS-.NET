using GPMH.Domain.Entities.Agents;
using GPMH.Infrastructure.Printing;
using Xunit;

namespace GPMH.UnitTests;

public sealed class AgentCommissionTests
{
    [Fact]
    public void GetRemainingPayable_TracksPartialSettlementAndClawbackAccurately()
    {
        AgentCommission commission = new()
        {
            AgentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CommissionType = AgentCommission.TypeNewOnboarding,
            CollectedAmount = 6000m,
            RatePctSnapshot = 15m,
            CommissionAmount = 900m,
            SettledAmount = 0m,
            Status = AgentCommission.StatusAccrued,
        };

        Assert.Equal(900m, commission.GetRemainingPayable());

        commission.SettledAmount = 500m;
        commission.Status = AgentCommission.StatusPartiallySettled;
        Assert.Equal(400m, commission.GetRemainingPayable());

        commission.SettledAmount = 900m;
        commission.Status = AgentCommission.StatusSettled;
        Assert.Equal(0m, commission.GetRemainingPayable());

        AgentCommission clawback = new()
        {
            AgentId = commission.AgentId,
            TenantId = commission.TenantId,
            CommissionType = AgentCommission.TypeClawback,
            CollectedAmount = 0m,
            RatePctSnapshot = 15m,
            CommissionAmount = 900m,
            SettledAmount = 0m,
            Status = AgentCommission.StatusClawedBack,
        };

        Assert.Equal(0m, clawback.GetRemainingPayable());
    }

    [Fact]
    public void VoucherAmountWords_ConvertsPartialAndFullAmountsInMarathi()
    {
        Assert.Equal("पाचशे रुपये फक्त", MarathiNumberToWords.ToRupeesWords(500m));
        Assert.Equal("नऊशे रुपये फक्त", MarathiNumberToWords.ToRupeesWords(900m));
    }
}

