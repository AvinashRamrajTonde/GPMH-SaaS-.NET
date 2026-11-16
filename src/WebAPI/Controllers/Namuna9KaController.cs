using GPMH.Application.DTOs.Namuna9Ka;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.Features.Namuna9Ka.Commands;
using GPMH.Application.Features.Namuna9Ka.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

public sealed record GenerateNamuna9KaBillsRequest(
    Guid? AssessmentYearId = null,
    string? WardNo = null,
    IReadOnlyList<Guid>? PropertyIds = null,
    DateOnly? IssueDate = null,
    bool RegenerateIfChanged = false);

[ApiController]
[Authorize(Policy = AuthPolicies.RequirePropertyRead)]
[Route("api/v1/namuna-9ka")]
public sealed class Namuna9KaController(ISender sender) : ControllerBase
{
    [HttpGet("bills")]
    [ProducesResponseType(typeof(Namuna9KaWorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Namuna9KaWorkspaceDto>> GetBills(
        [FromQuery] Guid? yearId = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] string? search = null,
        [FromQuery] bool pendingOnly = false,
        [FromQuery] string[]? propertyIds = null,
        CancellationToken cancellationToken = default)
    {
        Namuna9KaWorkspaceDto result = await sender
            .Send(
                new GetNamuna9KaBillsQuery(yearId, wardNo, search, pendingOnly, QueryParamHelper.ParseGuidList(propertyIds)),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost("bills/generate")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(Namuna9KaGenerateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Namuna9KaGenerateResultDto>> GenerateBills(
        [FromBody] GenerateNamuna9KaBillsRequest request,
        CancellationToken cancellationToken)
    {
        Namuna9KaGenerateResultDto result = await sender
            .Send(
                new GenerateDemandBillsBatchCommand(
                    request.AssessmentYearId,
                    request.WardNo,
                    request.PropertyIds,
                    request.IssueDate,
                    request.RegenerateIfChanged),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("bills.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadBillsPdf(
        [FromQuery] Guid? yearId = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] string? search = null,
        [FromQuery] string[]? propertyIds = null,
        [FromQuery] string layout = "2in1_landscape",
        [FromQuery] string splitMode = "counterfoil",
        CancellationToken cancellationToken = default)
    {
        GeneratedPdfFileDto file = await sender
            .Send(
                new GetNamuna9KaBillsPdfQuery(
                    yearId,
                    wardNo,
                    search,
                    QueryParamHelper.ParseGuidList(propertyIds),
                    layout,
                    splitMode),
                cancellationToken)
            .ConfigureAwait(false);

        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }
}
