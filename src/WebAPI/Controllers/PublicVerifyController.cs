using GPMH.Application.DTOs.Printing;
using GPMH.Application.Features.Printing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/public/verify")]
public sealed class PublicVerifyController(ISender sender) : ControllerBase
{
    [HttpGet("extract/{token}")]
    [ProducesResponseType(typeof(VerifiedExtractPublicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerifiedExtractPublicDto>> VerifyExtract(
        string token,
        CancellationToken cancellationToken)
    {
        VerifiedExtractPublicDto result = await sender
            .Send(new VerifyExtractTokenQuery(token), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }
}

