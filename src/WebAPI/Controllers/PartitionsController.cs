using GPMH.Application.DTOs.Partitions;
using GPMH.Application.Features.Partitions.Commands;
using GPMH.Application.Features.Partitions.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

public sealed record ExecutePropertyPartitionRequest(
    string ResolutionNo,
    DateOnly ResolutionDate,
    IReadOnlyList<PartitionChildRequestItem> ChildPartitions,
    string? PartitionReason = null,
    string? DocumentReferenceNo = null,
    DateOnly? DocumentDate = null,
    string? Remarks = null,
    IReadOnlyList<string>? SupportingAssetKeys = null);

[ApiController]
[Authorize]
[Route("api/v1/properties")]
public sealed class PartitionsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}/partition-eligibility")]
    [Authorize(Policy = AuthPolicies.RequirePropertyRead)]
    [ProducesResponseType(typeof(PartitionEligibilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEligibility(Guid id, CancellationToken cancellationToken)
    {
        PartitionEligibilityDto result = await sender
            .Send(new GetPartitionEligibilityQuery(id), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("{id:guid}/partition")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(ExecutePartitionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExecutePartition(
        Guid id,
        [FromBody] ExecutePropertyPartitionRequest request,
        CancellationToken cancellationToken)
    {
        ExecutePartitionResultDto result = await sender.Send(
            new ExecutePropertyPartitionCommand(
                ParentPropertyId: id,
                ResolutionNo: request.ResolutionNo,
                ResolutionDate: request.ResolutionDate,
                ChildPartitions: request.ChildPartitions,
                PartitionReason: request.PartitionReason,
                DocumentReferenceNo: request.DocumentReferenceNo,
                DocumentDate: request.DocumentDate,
                Remarks: request.Remarks,
                SupportingAssetKeys: request.SupportingAssetKeys),
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }
}
