using GPMH.Application.Features.Tax.Queries;
using GPMH.Domain.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Route("api/v1/tax")]
public sealed class TaxPreviewController(ISender sender) : ControllerBase
{
    [HttpPost("preview")]
    [ProducesResponseType(typeof(PropertyTaxCalculationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PropertyTaxCalculationResult>> PreviewCapitalTax(
        [FromBody] PropertyTaxCalculationInput input,
        CancellationToken cancellationToken)
    {
        PropertyTaxCalculationResult result = await sender
            .Send(new PreviewCapitalTaxQuery(input), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }
}

