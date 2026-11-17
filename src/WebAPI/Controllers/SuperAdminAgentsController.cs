using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.Features.Agents.Commands;
using GPMH.Application.Features.Agents.Queries;
using GPMH.Domain.Enums;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/superadmin/agents")]
public sealed class SuperAdminAgentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminAgentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SuperAdminAgentSummaryDto>>> ListAgents(
        [FromQuery] string? district = null,
        [FromQuery] string? taluka = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SuperAdminAgentSummaryDto> agents = await sender
            .Send(new GetSuperAdminAgentsFilteredQuery(district, taluka, status, search), cancellationToken)
            .ConfigureAwait(false);

        return Ok(agents);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(SuperAdminAgentSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SuperAdminAgentSummaryDto>> CreateAgentManual(
        [FromBody] CreateAgentBySuperAdminCommand command,
        CancellationToken cancellationToken)
    {
        SuperAdminAgentSummaryDto created = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}/kyc")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(AgentProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentProfileDto>> DecideKyc(
        Guid id,
        [FromBody] UpdateAgentStatusRequest request,
        CancellationToken cancellationToken)
    {
        AgentProfileDto updated = await sender.Send(
            new UpdateAgentStatusCommand(
                id,
                request.Status,
                request.ApprovedNewGpRatePct,
                request.ApprovedRenewalRatePct,
                request.Reason),
            cancellationToken).ConfigureAwait(false);

        return Ok(updated);
    }

    [HttpPost("{agentId:guid}/status")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(AgentProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AgentProfileDto>> UpdateAgentStatusCompat(
        Guid agentId,
        [FromBody] UpdateAgentStatusRequest request,
        CancellationToken cancellationToken) =>
        DecideKyc(agentId, request, cancellationToken);

    [HttpGet("settlements")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [ProducesResponseType(typeof(SuperAdminAgentSettlementsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SuperAdminAgentSettlementsDto>> GetSettlements(CancellationToken cancellationToken)
    {
        SuperAdminAgentSettlementsDto hub = await sender
            .Send(new GetSuperAdminAgentSettlementsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(hub);
    }

    [HttpPost("{id:guid}/settle-payout")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(AgentPayoutItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AgentPayoutItemDto>> SettlePayout(
        Guid id,
        [FromBody] SettleAgentPayoutRequest request,
        CancellationToken cancellationToken)
    {
        AgentPayoutItemDto settled = await sender.Send(
            new SettleAgentPayoutCommand(
                AgentId: id,
                Amount: request.Amount,
                PaymentMode: request.PaymentMode,
                UtrReference: request.UtrReference,
                PaymentDate: request.PaymentDate,
                FulfillRequestedPayoutId: request.FulfillRequestedPayoutId,
                ReceiptAssetKey: request.ReceiptAssetKey,
                Remarks: request.Remarks),
            cancellationToken).ConfigureAwait(false);

        return Ok(settled);
    }

    [HttpGet("payouts/{id:guid}/voucher.pdf")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadPayoutVoucherPdf(Guid id, CancellationToken cancellationToken)
    {
        GeneratedPdfFileDto file = await sender
            .Send(new GetAgentPayoutVoucherPdfQuery(id, RestrictToCurrentAgent: false), cancellationToken)
            .ConfigureAwait(false);

        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }
}

public sealed record UpdateAgentStatusRequest(
    AgentKycStatus Status,
    decimal? ApprovedNewGpRatePct = null,
    decimal? ApprovedRenewalRatePct = null,
    string? Reason = null);

public sealed record SettleAgentPayoutRequest(
    decimal Amount,
    string PaymentMode,
    string UtrReference,
    DateOnly PaymentDate,
    Guid? FulfillRequestedPayoutId = null,
    string? ReceiptAssetKey = null,
    string? Remarks = null);

