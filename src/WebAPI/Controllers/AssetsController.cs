using GPMH.Application.DTOs.Onboarding;
using GPMH.Application.Features.Onboarding.Commands;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Route("api/v1/assets")]
public sealed class AssetsController(ISender sender) : ControllerBase
{
    [HttpPost("upload")]
    [Authorize(Policy = AuthPolicies.RequirePropertyWrite)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadedAssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UploadedAssetDto>> Upload(
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

    [HttpGet("{assetKey}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string assetKey, CancellationToken cancellationToken)
    {
        ProtectedAssetDownloadDto asset = await sender
            .Send(new GetProtectedAssetQuery(assetKey), cancellationToken)
            .ConfigureAwait(false);

        return File(asset.Content, asset.ContentType, asset.FileName);
    }
}

