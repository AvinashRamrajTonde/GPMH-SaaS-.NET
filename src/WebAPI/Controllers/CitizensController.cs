using GPMH.Application.DTOs.Citizens;
using GPMH.Application.Features.Citizens.Commands;
using GPMH.Application.Features.Citizens.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.RequirePropertyRead)]
[Route("api/v1/citizens")]
public sealed class CitizensController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedCitizensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedCitizensDto>> List(
        [FromQuery] string? search,
        [FromQuery] string? wardNo,
        [FromQuery] string? exemptionCategory,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        PagedCitizensDto result = await sender
            .Send(new GetCitizensQuery(search, wardNo, exemptionCategory, page, pageSize), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CitizenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CitizenDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        CitizenDto result = await sender
            .Send(new GetCitizenByIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.RequirePropertyWrite)]
    [ProducesResponseType(typeof(CitizenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CitizenDto>> Create(
        [FromBody] CreateCitizenCommand command,
        CancellationToken cancellationToken)
    {
        CitizenDto created = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.RequirePropertyWrite)]
    [ProducesResponseType(typeof(CitizenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CitizenDto>> Update(
        Guid id,
        [FromBody] UpdateCitizenRequest request,
        CancellationToken cancellationToken)
    {
        UpdateCitizenCommand command = new(
            Id: id,
            WardNo: request.WardNo,
            FullNameMr: request.FullNameMr,
            FullNameEn: request.FullNameEn,
            Gender: request.Gender,
            MobileNo: request.MobileNo,
            AadhaarNumber: request.AadhaarNumber,
            SocialCategory: request.SocialCategory,
            ExemptionCategory: request.ExemptionCategory,
            IsActive: request.IsActive);

        CitizenDto updated = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(updated);
    }
}

public sealed record UpdateCitizenRequest(
    string? WardNo,
    string FullNameMr,
    string FullNameEn,
    string Gender,
    string? MobileNo = null,
    string? AadhaarNumber = null,
    string SocialCategory = "General",
    string ExemptionCategory = "NotExempted",
    bool IsActive = true);

