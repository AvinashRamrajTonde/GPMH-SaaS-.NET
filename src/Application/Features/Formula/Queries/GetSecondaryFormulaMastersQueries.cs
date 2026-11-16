using GPMH.Application.DTOs.Formula;
using MediatR;

namespace GPMH.Application.Features.Formula.Queries;

public sealed record GetDepreciationSlabsQuery : IRequest<IReadOnlyList<DepreciationSlabDto>>;

public sealed record GetUsageWeightagesQuery : IRequest<IReadOnlyList<UsageWeightageDto>>;

public sealed record GetAreaSlabTaxesQuery : IRequest<IReadOnlyList<AreaSlabTaxDto>>;

public sealed record GetWaterTaxMastersQuery : IRequest<IReadOnlyList<WaterTaxMasterDto>>;

public sealed class SecondaryFormulaMasterQueriesHandler(ISender sender)
    : IRequestHandler<GetDepreciationSlabsQuery, IReadOnlyList<DepreciationSlabDto>>,
      IRequestHandler<GetUsageWeightagesQuery, IReadOnlyList<UsageWeightageDto>>,
      IRequestHandler<GetAreaSlabTaxesQuery, IReadOnlyList<AreaSlabTaxDto>>,
      IRequestHandler<GetWaterTaxMastersQuery, IReadOnlyList<WaterTaxMasterDto>>
{
    public async Task<IReadOnlyList<DepreciationSlabDto>> Handle(
        GetDepreciationSlabsQuery request,
        CancellationToken cancellationToken)
    {
        FormulaWorkspaceDto ws = await sender.Send(new GetFormulaSettingsWorkspaceQuery(), cancellationToken).ConfigureAwait(false);
        return ws.DepreciationSlabs ?? [];
    }

    public async Task<IReadOnlyList<UsageWeightageDto>> Handle(
        GetUsageWeightagesQuery request,
        CancellationToken cancellationToken)
    {
        FormulaWorkspaceDto ws = await sender.Send(new GetFormulaSettingsWorkspaceQuery(), cancellationToken).ConfigureAwait(false);
        return ws.UsageWeightages ?? [];
    }

    public async Task<IReadOnlyList<AreaSlabTaxDto>> Handle(
        GetAreaSlabTaxesQuery request,
        CancellationToken cancellationToken)
    {
        FormulaWorkspaceDto ws = await sender.Send(new GetFormulaSettingsWorkspaceQuery(), cancellationToken).ConfigureAwait(false);
        return ws.AreaSlabTaxes ?? [];
    }

    public async Task<IReadOnlyList<WaterTaxMasterDto>> Handle(
        GetWaterTaxMastersQuery request,
        CancellationToken cancellationToken)
    {
        FormulaWorkspaceDto ws = await sender.Send(new GetFormulaSettingsWorkspaceQuery(), cancellationToken).ConfigureAwait(false);
        return ws.WaterTaxes ?? [];
    }
}

