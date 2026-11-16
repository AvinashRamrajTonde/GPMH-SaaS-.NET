using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Application.Features.Properties.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.RequirePropertyRead)]
[Route("api/v1/properties")]
public sealed class PropertiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PropertyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PropertyDto>>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<PropertyDto> result = await sender
            .Send(new GetPropertiesQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("hub-summary")]
    [ProducesResponseType(typeof(PropertyHubKpisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PropertyHubKpisDto>> GetHubSummary(CancellationToken cancellationToken)
    {
        PropertyHubKpisDto result = await sender
            .Send(new GetPropertyHubKpisQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PropertyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PropertyDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        PropertyDetailDto result = await sender
            .Send(new GetPropertyByIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.RequirePropertyWrite)]
    [ProducesResponseType(typeof(PropertyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PropertyDto>> Create(
        [FromBody] CreatePropertyCommand command,
        CancellationToken cancellationToken)
    {
        PropertyDto created = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.RequirePropertyWrite)]
    [ProducesResponseType(typeof(PropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PropertyDto>> Update(
        Guid id,
        [FromBody] UpdatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        UpdatePropertyCommand command = new(
            Id: id,
            BuiltUpAreaSqFt: request.BuiltUpAreaSqFt,
            OpenPlotAreaSqFt: request.OpenPlotAreaSqFt,
            BoundaryEast: request.BoundaryEast,
            BoundaryWest: request.BoundaryWest,
            BoundaryNorth: request.BoundaryNorth,
            BoundarySouth: request.BoundarySouth,
            WaterConnectionCode: request.WaterConnectionCode,
            IsTaxExempt: request.IsTaxExempt,
            StreetOrLaneName: request.StreetOrLaneName,
            CitySurveyOrGutNo: request.CitySurveyOrGutNo,
            OccupierName: request.OccupierName,
            PropertyDescription: request.PropertyDescription,
            OtherTax: request.OtherTax,
            OpenPlotConstructionCode: request.OpenPlotConstructionCode,
            Owners: request.Owners,
            Floors: request.Floors,
            CalculateTax: request.CalculateTax);

        PropertyDto updated = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.RequirePropertyWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePropertyCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}

public sealed record UpdatePropertyRequest(
    decimal BuiltUpAreaSqFt,
    decimal OpenPlotAreaSqFt,
    string? BoundaryEast,
    string? BoundaryWest,
    string? BoundaryNorth,
    string? BoundarySouth,
    string WaterConnectionCode,
    bool IsTaxExempt,
    string? StreetOrLaneName = null,
    string? CitySurveyOrGutNo = null,
    string? OccupierName = null,
    string? PropertyDescription = null,
    decimal OtherTax = 0m,
    string? OpenPlotConstructionCode = null,
    IReadOnlyList<PropertyOwnerRequestItem>? Owners = null,
    IReadOnlyList<PropertyFloorRequestItem>? Floors = null,
    bool CalculateTax = false);
