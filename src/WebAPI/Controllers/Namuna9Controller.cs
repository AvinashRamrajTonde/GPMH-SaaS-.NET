using GPMH.Application.DTOs.Namuna9;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.Features.Namuna9.Commands;
using GPMH.Application.Features.Namuna9.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

public sealed record PublishDraftDemandsRequest(
    Guid? AssessmentYearId = null,
    IReadOnlyList<Guid>? PropertyIds = null);

public sealed record BatchAdjustNamuna9FeesRequest(
    Guid AssessmentYearId,
    IReadOnlyList<Namuna9FeeAdjustmentItem> Adjustments,
    string? Remarks = null);

public sealed record ApplyEarlyRebateBatchRequest(
    Guid AssessmentYearId,
    DateOnly ApplicationDate,
    DateOnly? CutoffDate = null,
    IReadOnlyList<Guid>? PropertyIds = null);

public sealed record ExecuteFyRolloverRequest(
    Guid SourceAssessmentYearId,
    string TargetYearLabel,
    bool CarryForwardCurrentTaxCalc = true);

[ApiController]
[Authorize(Policy = AuthPolicies.RequirePropertyRead)]
[Route("api/v1/namuna-9")]
public sealed class Namuna9Controller(ISender sender) : ControllerBase
{
    [HttpGet("register")]
    [ProducesResponseType(typeof(Namuna9WorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Namuna9WorkspaceDto>> GetRegister(
        [FromQuery] Guid? yearId = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] string? search = null,
        [FromQuery] string format = "C",
        [FromQuery] bool pendingOnly = false,
        CancellationToken cancellationToken = default)
    {
        Namuna9WorkspaceDto result = await sender
            .Send(new GetNamuna9RegisterQuery(yearId, wardNo, search, format, pendingOnly), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost("publish")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(Namuna9PublishResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Namuna9PublishResultDto>> Publish(
        [FromBody] PublishDraftDemandsRequest request,
        CancellationToken cancellationToken)
    {
        Namuna9PublishResultDto result = await sender
            .Send(new PublishDraftDemandsCommand(request.AssessmentYearId, request.PropertyIds), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPut("adjustments")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(Namuna9AdjustResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Namuna9AdjustResultDto>> AdjustFees(
        [FromBody] BatchAdjustNamuna9FeesRequest request,
        CancellationToken cancellationToken)
    {
        Namuna9AdjustResultDto result = await sender
            .Send(
                new BatchAdjustNamuna9FeesCommand(request.AssessmentYearId, request.Adjustments, request.Remarks),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost("apply-rebate")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(Namuna9RebateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Namuna9RebateResultDto>> ApplyEarlyRebate(
        [FromBody] ApplyEarlyRebateBatchRequest request,
        CancellationToken cancellationToken)
    {
        Namuna9RebateResultDto result = await sender
            .Send(
                new ApplyEarlyRebateBatchCommand(
                    request.AssessmentYearId,
                    request.ApplicationDate,
                    request.CutoffDate,
                    request.PropertyIds),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("register.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadRegisterPdf(
        [FromQuery] Guid? yearId = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] string? search = null,
        [FromQuery] string format = "C",
        [FromQuery] string part = "Both",
        [FromQuery] string style = "color",
        [FromQuery] int leftMarginMm = 10,
        [FromQuery] bool pendingOnly = false,
        CancellationToken cancellationToken = default)
    {
        GeneratedPdfFileDto file = await sender
            .Send(
                new GetNamuna9RegisterPdfQuery(
                    yearId,
                    wardNo,
                    search,
                    format,
                    part,
                    style,
                    leftMarginMm,
                    pendingOnly),
                cancellationToken)
            .ConfigureAwait(false);

        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }

    [HttpGet("export.csv")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] Guid? yearId = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] string? search = null,
        [FromQuery] string format = "C",
        [FromQuery] bool pendingOnly = false,
        CancellationToken cancellationToken = default)
    {
        Namuna9CsvFileDto file = await sender
            .Send(new GetNamuna9CsvQuery(yearId, wardNo, search, format, pendingOnly), cancellationToken)
            .ConfigureAwait(false);

        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    [HttpPost("rollover")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(Namuna9RolloverResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Namuna9RolloverResultDto>> Rollover(
        [FromBody] ExecuteFyRolloverRequest request,
        CancellationToken cancellationToken)
    {
        Namuna9RolloverResultDto result = await sender
            .Send(
                new ExecuteFyRolloverCommand(
                    request.SourceAssessmentYearId,
                    request.TargetYearLabel,
                    request.CarryForwardCurrentTaxCalc),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }
}

