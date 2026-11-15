using GPMH.Application.DTOs.Lgd;
using GPMH.Application.Features.Lgd.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Route("api/v1/lgd")]
public sealed class LgdController(ISender sender) : ControllerBase
{
    [HttpGet("districts")]
    [ProducesResponseType(typeof(IReadOnlyList<LgdDistrictDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LgdDistrictDto>>> GetDistricts(CancellationToken cancellationToken)
    {
        IReadOnlyList<LgdDistrictDto> result = await sender
            .Send(new GetDistrictsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("tehsils")]
    [ProducesResponseType(typeof(IReadOnlyList<LgdTehsilDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<LgdTehsilDto>>> GetTehsils(
        [FromQuery] string district,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LgdTehsilDto> result = await sender
            .Send(new GetTehsilsQuery(district), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("panchayats")]
    [ProducesResponseType(typeof(IReadOnlyList<LgdPanchayatSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<LgdPanchayatSummaryDto>>> GetPanchayats(
        [FromQuery] string district,
        [FromQuery] string tehsil,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LgdPanchayatSummaryDto> result = await sender
            .Send(new GetPanchayatsQuery(district, tehsil), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("panchayat/{lgdCode}")]
    [ProducesResponseType(typeof(LgdPanchayatDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LgdPanchayatDetailDto>> GetPanchayatByCode(
        string lgdCode,
        CancellationToken cancellationToken)
    {
        LgdPanchayatDetailDto result = await sender
            .Send(new LookupPanchayatQuery(lgdCode), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<LgdPanchayatSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<LgdPanchayatSummaryDto>>> SearchPanchayats(
        [FromQuery] string q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LgdPanchayatSummaryDto> result = await sender
            .Send(new SearchPanchayatsQuery(q, limit), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(LgdStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LgdStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        LgdStatsDto result = await sender
            .Send(new GetLgdStatsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }
}

