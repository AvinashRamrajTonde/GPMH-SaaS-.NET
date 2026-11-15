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
}

