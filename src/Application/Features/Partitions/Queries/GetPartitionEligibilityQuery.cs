using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Partitions;
using GPMH.Domain.Services;
using MediatR;

namespace GPMH.Application.Features.Partitions.Queries;

public sealed record GetPartitionEligibilityQuery(Guid PropertyId) : IRequest<PartitionEligibilityDto>;

public sealed class GetPartitionEligibilityQueryHandler(
    IPartitionRepository partitionRepository)
    : IRequestHandler<GetPartitionEligibilityQuery, PartitionEligibilityDto>
{
    public async Task<PartitionEligibilityDto> Handle(
        GetPartitionEligibilityQuery request,
        CancellationToken cancellationToken)
    {
        PartitionContextData context = await partitionRepository
            .LoadPartitionContextAsync(request.PropertyId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Property '{request.PropertyId}' was not found.");

        decimal pendingDues = context.Demands.Sum(d => d.PendingBalance);
        decimal arrears = context.Demands.Sum(d => d.ArrearsBalance);
        decimal currentDemand = context.Demands.Sum(d => d.CurrentDemand);
        decimal paid = context.Demands.Sum(d => d.PaidAmount);

        bool isActive = context.Property.IsActive &&
            string.Equals(context.Property.Status, "Active", StringComparison.OrdinalIgnoreCase);

        bool zeroDuesPassed = isActive && pendingDues == 0m;
        string? blockReason = null;
        if (!isActive)
        {
            blockReason = $"Property '{context.Property.WardNo}/{context.Property.PropertyNo}' has status '{context.Property.Status}' and is not active for partitioning.";
        }
        else if (pendingDues != 0m)
        {
            blockReason = $"ZERO_DUES_GATE_FAILED: Property has pending tax dues of ₹{pendingDues:N2}. All dues must be zero before partitioning.";
        }

        decimal totalSqFt = context.Property.TotalAreaSqFt > 0m
            ? context.Property.TotalAreaSqFt
            : context.Property.BuiltUpAreaSqFt + context.Property.OpenPlotAreaSqFt;

        decimal totalSqM = Math.Round(
            totalSqFt * CapitalValueTaxCalculator.SqFtToSqM,
            4,
            MidpointRounding.AwayFromZero);

        IReadOnlyList<PartitionFloorItemDto> floors =
        [
            .. context.CurrentFloors.Select(f => new PartitionFloorItemDto(
                f.FloorNo,
                f.FloorLabel,
                f.ConstructionTypeCode,
                f.UsageCode,
                f.LengthFt,
                f.WidthFt,
                f.AreaSqFt,
                f.AreaSqM,
                f.ConstructionStartYear)),
        ];

        string baseNo = context.Property.PropertyNo.Trim();
        IReadOnlyList<string> suggestions = [$"{baseNo}/1", $"{baseNo}/2"];

        return new PartitionEligibilityDto(
            PropertyId: context.Property.Id,
            WardNo: context.Property.WardNo,
            PropertyNo: context.Property.PropertyNo,
            Status: context.Property.Status,
            IsActive: context.Property.IsActive,
            StreetOrLaneName: context.Property.StreetOrLaneName,
            CitySurveyOrGutNo: context.Property.CitySurveyOrGutNo,
            BuiltUpAreaSqFt: context.Property.BuiltUpAreaSqFt,
            OpenPlotAreaSqFt: context.Property.OpenPlotAreaSqFt,
            TotalAreaSqFt: totalSqFt,
            TotalAreaSqM: totalSqM,
            PendingDues: pendingDues,
            ArrearsBalance: arrears,
            CurrentDemand: currentDemand,
            PaidAmount: paid,
            ZeroDuesGatePassed: zeroDuesPassed,
            BlockReason: blockReason,
            WaterConnectionCode: context.Property.WaterConnectionCode,
            IsTaxExempt: context.Property.IsTaxExempt,
            OtherTax: context.Property.OtherTax,
            CurrentOwners: context.CurrentOwners,
            CurrentFloors: floors,
            SuggestedChildPropertyNos: suggestions);
    }
}

