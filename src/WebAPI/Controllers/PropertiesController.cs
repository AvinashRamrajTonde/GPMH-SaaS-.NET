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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PropertyDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        PropertyDto result = await sender
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
            id,
            request.BuiltUpAreaSqFt,
            request.OpenPlotAreaSqFt,
            request.BoundaryEast,
            request.BoundaryWest,
            request.BoundaryNorth,
            request.BoundarySouth,
            request.WaterConnectionCode,
            request.IsTaxExempt);

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
    bool IsTaxExempt);

