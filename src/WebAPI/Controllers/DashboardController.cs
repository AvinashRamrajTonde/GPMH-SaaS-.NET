using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.Features.Dashboard.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.RequirePropertyRead)]
[Route("api/v1/dashboard")]
public sealed class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        DashboardSummaryDto summary = await sender
            .Send(new GetDashboardSummaryQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(summary);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(DashboardSearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardSearchResultDto>> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        DashboardSearchResultDto result = await sender
            .Send(new SearchDashboardQuery(q, limit), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }
}

