using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Application.Features.Agents.Commands;
using GPMH.Application.Features.Agents.Queries;
using GPMH.Application.Features.SuperAdmin.Commands;
using GPMH.Application.Features.SuperAdmin.Queries;
using GPMH.Domain.Enums;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
[Route("api/v1/superadmin")]
public sealed class SuperAdminTenantsController(ISender sender) : ControllerBase
{
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminTenantSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SuperAdminTenantSummaryDto>>> ListTenants(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SuperAdminTenantSummaryDto> tenants = await sender
            .Send(new GetSuperAdminTenantsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(tenants);
    }

    [HttpPost("tenants/provision")]
    [ProducesResponseType(typeof(ProvisionedTenantResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProvisionedTenantResultDto>> ProvisionTenantDirect(
        [FromBody] ProvisionTenantDirectCommand command,
        CancellationToken cancellationToken)
    {
        ProvisionedTenantResultDto provisioned = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, provisioned);
    }

    [HttpGet("agents")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgentProfileDto>>> ListAgents(CancellationToken cancellationToken)
    {
        IReadOnlyList<AgentProfileDto> agents = await sender
            .Send(new GetSuperAdminAgentsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(agents);
    }

    [HttpPost("agents/{agentId:guid}/status")]
    [ProducesResponseType(typeof(AgentProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentProfileDto>> UpdateAgentStatus(
        Guid agentId,
        [FromBody] UpdateAgentStatusRequest request,
        CancellationToken cancellationToken)
    {
        AgentProfileDto updated = await sender.Send(
            new UpdateAgentStatusCommand(
                agentId,
                request.Status,
                request.ApprovedNewGpRatePct,
                request.ApprovedRenewalRatePct,
                request.Reason),
            cancellationToken).ConfigureAwait(false);

        return Ok(updated);
    }
}

public sealed record UpdateAgentStatusRequest(
    AgentKycStatus Status,
    decimal? ApprovedNewGpRatePct = null,
    decimal? ApprovedRenewalRatePct = null,
    string? Reason = null);

