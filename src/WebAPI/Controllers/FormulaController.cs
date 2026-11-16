using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Commands;
using GPMH.Application.Features.Formula.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.RequirePropertyRead)]
[Route("api/v1/formula")]
public sealed class FormulaController(ISender sender) : ControllerBase
{
    [HttpGet("workspace")]
    [ProducesResponseType(typeof(FormulaWorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FormulaWorkspaceDto>> GetWorkspace(CancellationToken cancellationToken)
    {
        FormulaWorkspaceDto workspace = await sender
            .Send(new GetFormulaSettingsWorkspaceQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(workspace);
    }

    [HttpPut("settings")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(FormulaSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormulaSettingDto>> SaveSettings(
        [FromBody] SaveFormulaSettingRequest request,
        CancellationToken cancellationToken)
    {
        SaveFormulaSettingCommand command = new(
            Id: request.Id,
            RatePeriodName: request.RatePeriodName,
            RateStartYear: request.RateStartYear,
            RateEndYear: request.RateEndYear,
            TaxDivisor: request.TaxDivisor,
            NoticeGraceDays: request.NoticeGraceDays,
            EarlyRebatePct: request.EarlyRebatePct,
            LatePenaltyPct: request.LatePenaltyPct,
            ApplyLightingHealthOnOpenPlot: request.ApplyLightingHealthOnOpenPlot,
            RoundArea: request.RoundArea,
            IsActive: request.IsActive,
            ResolutionNo: request.ResolutionNo,
            ResolutionDate: request.ResolutionDate,
            ResolutionReason: request.ResolutionReason,
            CreateNewPeriod: request.CreateNewPeriod);

        FormulaSettingDto saved = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(saved);
    }

    [HttpGet("construction-types")]
    [HttpGet("/api/Setting/GetAllConstructionTypeAsync")]
    [ProducesResponseType(typeof(IReadOnlyList<ConstructionTypeMasterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ConstructionTypeMasterDto>>> ListConstructionTypes(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ConstructionTypeMasterDto> items = await sender
            .Send(new GetConstructionTypesQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpPost("construction-types")]
    [HttpPost("/api/Setting/SaveConstructionTypeAsync")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(ConstructionTypeMasterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ConstructionTypeMasterDto>> SaveConstructionType(
        [FromBody] SaveConstructionTypeRequest request,
        CancellationToken cancellationToken)
    {
        SaveConstructionTypeCommand command = new(
            Id: request.Id,
            Code: request.Code,
            TypeName: request.TypeName,
            Category: request.Category,
            LandAnnualValueRate: request.LandAnnualValueRate,
            ConstructionReadyReckonerRate: request.ConstructionReadyReckonerRate,
            UsageWeightageFactor: request.UsageWeightageFactor,
            TaxRate: request.TaxRate,
            IsActive: request.IsActive,
            ResolutionNo: request.ResolutionNo,
            ResolutionDate: request.ResolutionDate,
            ChangeReason: request.ChangeReason);

        ConstructionTypeMasterDto saved = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(saved);
    }

    [HttpPut("construction-types/{id:guid}")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(ConstructionTypeMasterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConstructionTypeMasterDto>> UpdateConstructionType(
        Guid id,
        [FromBody] SaveConstructionTypeRequest request,
        CancellationToken cancellationToken)
    {
        SaveConstructionTypeCommand command = new(
            Id: id,
            Code: request.Code,
            TypeName: request.TypeName,
            Category: request.Category,
            LandAnnualValueRate: request.LandAnnualValueRate,
            ConstructionReadyReckonerRate: request.ConstructionReadyReckonerRate,
            UsageWeightageFactor: request.UsageWeightageFactor,
            TaxRate: request.TaxRate,
            IsActive: request.IsActive,
            ResolutionNo: request.ResolutionNo,
            ResolutionDate: request.ResolutionDate,
            ChangeReason: request.ChangeReason);

        ConstructionTypeMasterDto updated = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(updated);
    }

    [HttpGet("depreciation-slabs")]
    [ProducesResponseType(typeof(IReadOnlyList<DepreciationSlabDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DepreciationSlabDto>>> ListDepreciationSlabs(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DepreciationSlabDto> items = await sender
            .Send(new GetDepreciationSlabsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpPut("depreciation-slabs")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(IReadOnlyList<DepreciationSlabDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DepreciationSlabDto>>> SaveDepreciationSlabs(
        [FromBody] SaveDepreciationSlabsBatchRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DepreciationSlabDto> saved = await sender.Send(
            new SaveDepreciationSlabsBatchCommand(
                request.Slabs,
                request.ResolutionNo,
                request.ResolutionDate,
                request.ChangeReason),
            cancellationToken).ConfigureAwait(false);

        return Ok(saved);
    }

    [HttpGet("usage-weightages")]
    [ProducesResponseType(typeof(IReadOnlyList<UsageWeightageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UsageWeightageDto>>> ListUsageWeightages(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UsageWeightageDto> items = await sender
            .Send(new GetUsageWeightagesQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpPost("usage-weightages")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(UsageWeightageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UsageWeightageDto>> SaveUsageWeightage(
        [FromBody] SaveUsageWeightageRequest request,
        CancellationToken cancellationToken)
    {
        UsageWeightageDto saved = await sender.Send(
            new SaveUsageWeightageCommand(
                request.Id,
                request.Code,
                request.UsageName,
                request.Weightage,
                request.IsActive,
                request.ResolutionNo,
                request.ResolutionDate,
                request.ChangeReason),
            cancellationToken).ConfigureAwait(false);

        return Ok(saved);
    }

    [HttpPut("usage-weightages/{id:guid}")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(UsageWeightageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsageWeightageDto>> UpdateUsageWeightage(
        Guid id,
        [FromBody] SaveUsageWeightageRequest request,
        CancellationToken cancellationToken)
    {
        UsageWeightageDto updated = await sender.Send(
            new SaveUsageWeightageCommand(
                id,
                request.Code,
                request.UsageName,
                request.Weightage,
                request.IsActive,
                request.ResolutionNo,
                request.ResolutionDate,
                request.ChangeReason),
            cancellationToken).ConfigureAwait(false);

        return Ok(updated);
    }

    [HttpGet("area-slabs")]
    [ProducesResponseType(typeof(IReadOnlyList<AreaSlabTaxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AreaSlabTaxDto>>> ListAreaSlabs(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AreaSlabTaxDto> items = await sender
            .Send(new GetAreaSlabTaxesQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpPut("area-slabs")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(IReadOnlyList<AreaSlabTaxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AreaSlabTaxDto>>> SaveAreaSlabs(
        [FromBody] SaveAreaSlabTaxesBatchRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AreaSlabTaxDto> saved = await sender.Send(
            new SaveAreaSlabTaxesBatchCommand(
                request.Slabs,
                request.ResolutionNo,
                request.ResolutionDate,
                request.ChangeReason),
            cancellationToken).ConfigureAwait(false);

        return Ok(saved);
    }

    [HttpGet("water-taxes")]
    [ProducesResponseType(typeof(IReadOnlyList<WaterTaxMasterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<WaterTaxMasterDto>>> ListWaterTaxes(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<WaterTaxMasterDto> items = await sender
            .Send(new GetWaterTaxMastersQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpPost("water-taxes")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(WaterTaxMasterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WaterTaxMasterDto>> SaveWaterTax(
        [FromBody] SaveWaterTaxMasterRequest request,
        CancellationToken cancellationToken)
    {
        WaterTaxMasterDto saved = await sender.Send(
            new SaveWaterTaxMasterCommand(
                request.Id,
                request.Code,
                request.WaterTypeName,
                request.Amount,
                request.IsActive,
                request.ResolutionNo,
                request.ResolutionDate,
                request.ChangeReason),
            cancellationToken).ConfigureAwait(false);

        return Ok(saved);
    }

    [HttpPut("water-taxes/{id:guid}")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(WaterTaxMasterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WaterTaxMasterDto>> UpdateWaterTax(
        Guid id,
        [FromBody] SaveWaterTaxMasterRequest request,
        CancellationToken cancellationToken)
    {
        WaterTaxMasterDto updated = await sender.Send(
            new SaveWaterTaxMasterCommand(
                id,
                request.Code,
                request.WaterTypeName,
                request.Amount,
                request.IsActive,
                request.ResolutionNo,
                request.ResolutionDate,
                request.ChangeReason),
            cancellationToken).ConfigureAwait(false);

        return Ok(updated);
    }

    [HttpPost("reset-defaults")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(FormulaWorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FormulaWorkspaceDto>> ResetDefaults(
        [FromBody] ResetFormulaDefaultsRequest? request,
        CancellationToken cancellationToken)
    {
        ResetFormulaDefaultsCommand command = new(
            ResolutionNo: request?.ResolutionNo,
            ResolutionDate: request?.ResolutionDate,
            ResolutionReason: request?.ResolutionReason);

        FormulaWorkspaceDto workspace = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(workspace);
    }
}

public sealed record SaveFormulaSettingRequest(
    Guid? Id,
    string RatePeriodName,
    int RateStartYear,
    int RateEndYear,
    decimal TaxDivisor,
    int NoticeGraceDays,
    decimal EarlyRebatePct,
    decimal LatePenaltyPct,
    bool ApplyLightingHealthOnOpenPlot,
    bool RoundArea,
    bool IsActive,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ResolutionReason,
    bool CreateNewPeriod = false);

public sealed record SaveConstructionTypeRequest(
    Guid? Id,
    string Code,
    string TypeName,
    string Category,
    decimal LandAnnualValueRate,
    decimal ConstructionReadyReckonerRate,
    decimal UsageWeightageFactor,
    decimal TaxRate,
    bool IsActive = true,
    string? ResolutionNo = null,
    DateOnly? ResolutionDate = null,
    string? ChangeReason = null);

public sealed record SaveDepreciationSlabsBatchRequest(
    IReadOnlyList<DepreciationSlabBatchItem> Slabs,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ChangeReason);

public sealed record SaveUsageWeightageRequest(
    Guid? Id,
    string Code,
    string UsageName,
    decimal Weightage,
    bool IsActive,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ChangeReason);

public sealed record SaveAreaSlabTaxesBatchRequest(
    IReadOnlyList<AreaSlabTaxBatchItem> Slabs,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ChangeReason);

public sealed record SaveWaterTaxMasterRequest(
    Guid? Id,
    string Code,
    string WaterTypeName,
    decimal Amount,
    bool IsActive,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ChangeReason);

public sealed record ResetFormulaDefaultsRequest(
    string? ResolutionNo = null,
    DateOnly? ResolutionDate = null,
    string? ResolutionReason = null);
