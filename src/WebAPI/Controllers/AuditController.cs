using GPMH.Application.DTOs.Auditing;
using GPMH.Application.Features.Auditing.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.RequireAuditorOrAdmin)]
[Route("api/v1/audit")]
public sealed class AuditController(ISender sender) : ControllerBase
{
    [HttpGet("{entityName}/{entityId}")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> GetEntityTrail(
        string entityName,
        string entityId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AuditLogDto> result = await sender
            .Send(new GetEntityAuditTrailQuery(entityName, entityId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }
}

