using GPMH.Application.DTOs.Onboarding;
using GPMH.Application.Features.Onboarding.Commands;
using GPMH.Application.Features.Onboarding.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Route("api/v1/onboarding")]
public sealed class OnboardingController(ISender sender) : ControllerBase
{
    [HttpGet("status")]
    [Authorize(Policy = AuthPolicies.RequireGpStaff)]
    [ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OnboardingStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        OnboardingStatusDto status = await sender
            .Send(new GetOnboardingStatusQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(status);
    }

    [HttpPost("complete")]
    [Authorize(Policy = AuthPolicies.RequireGpAdmin)]
    [ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OnboardingStatusDto>> CompleteOnboarding(
        [FromBody] CompleteOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        OnboardingStatusDto status = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(status);
    }

    [AllowAnonymous]
    [HttpPost("register-gp")]
    [ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OnboardingStatusDto>> RegisterSelfServiceGp(
        [FromBody] RegisterSelfServiceGpCommand command,
        CancellationToken cancellationToken)
    {
        OnboardingStatusDto created = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("upload-asset")]
    [Authorize(Policy = AuthPolicies.RequireGpStaff)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadedAssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UploadedAssetDto>> UploadAsset(
        [FromForm] string category,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest();
        }

        using MemoryStream ms = new();
        await file.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);

        UploadedAssetDto uploaded = await sender.Send(
            new UploadProtectedAssetCommand(category, file.FileName, file.ContentType, ms.ToArray()),
            cancellationToken).ConfigureAwait(false);

        return StatusCode(StatusCodes.Status201Created, uploaded);
    }

    [HttpGet("assets/{assetKey}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAsset(string assetKey, CancellationToken cancellationToken)
    {
        ProtectedAssetDownloadDto asset = await sender
            .Send(new GetProtectedAssetQuery(assetKey), cancellationToken)
            .ConfigureAwait(false);

        return File(asset.Content, asset.ContentType, asset.FileName);
    }
}

