using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Tax.Queries;
using GPMH.Domain.Services;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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

    [HttpPost("calculate")]
    [Authorize(Policy = AuthPolicies.RequirePropertyRead)]
    [ProducesResponseType(typeof(CalculatedPropertyTaxDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalculatedPropertyTaxDto>> CalculateFromMasters(
        [FromBody] CalculatePropertyTaxFromMastersQuery query,
        CancellationToken cancellationToken)
    {
        CalculatedPropertyTaxDto result = await sender
            .Send(query, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }
}
