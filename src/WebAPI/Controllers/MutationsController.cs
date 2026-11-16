using GPMH.Application.DTOs.Mutations;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.Features.Mutations.Commands;
using GPMH.Application.Features.Mutations.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.RequirePropertyRead)]
[Route("api/v1/mutations")]
public sealed class MutationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedMutationsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedMutationsDto>> List(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        PagedMutationsDto result = await sender
            .Send(new GetMutationsQuery(status, search, wardNo, page, pageSize), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MutationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MutationDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        MutationDetailDto result = await sender
            .Send(new GetMutationByIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.RequirePropertyWrite)]
    [ProducesResponseType(typeof(MutationDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MutationDetailDto>> Initiate(
        [FromBody] CreateMutationCommand command,
        CancellationToken cancellationToken)
    {
        MutationDetailDto created = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/objection")]
    [Authorize(Policy = AuthPolicies.RequirePropertyWrite)]
    [ProducesResponseType(typeof(MutationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MutationDetailDto>> RecordObjection(
        Guid id,
        [FromBody] RecordMutationObjectionRequest request,
        CancellationToken cancellationToken)
    {
        MutationDetailDto updated = await sender
            .Send(new RecordMutationObjectionCommand(id, request.ObjectionSummary), cancellationToken)
            .ConfigureAwait(false);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(MutationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MutationDetailDto>> Approve(
        Guid id,
        [FromBody] ApproveMutationRequest request,
        CancellationToken cancellationToken)
    {
        MutationDetailDto updated = await sender.Send(
            new ApproveMutationCommand(id, request.ResolutionNo, request.ResolutionDate, request.ObjectionResolvedNotes),
            cancellationToken).ConfigureAwait(false);

        return Ok(updated);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(MutationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MutationDetailDto>> Reject(
        Guid id,
        [FromBody] RejectMutationRequest request,
        CancellationToken cancellationToken)
    {
        MutationDetailDto updated = await sender.Send(
            new RejectMutationCommand(id, request.RejectionReason, request.ResolutionNo, request.ResolutionDate),
            cancellationToken).ConfigureAwait(false);

        return Ok(updated);
    }

    [HttpGet("register.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadRegisterPdf(
        [FromQuery] string? wardNo = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        GeneratedPdfFileDto file = await sender
            .Send(new GetMutationRegisterPdfQuery(wardNo, fromDate, toDate), cancellationToken)
            .ConfigureAwait(false);

        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }
}

public sealed record RecordMutationObjectionRequest(string ObjectionSummary);

public sealed record ApproveMutationRequest(
    string ResolutionNo,
    DateOnly ResolutionDate,
    string? ObjectionResolvedNotes = null);

public sealed record RejectMutationRequest(
    string RejectionReason,
    string ResolutionNo,
    DateOnly ResolutionDate);

