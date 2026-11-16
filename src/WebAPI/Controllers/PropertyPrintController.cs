using GPMH.Application.DTOs.Printing;
using GPMH.Application.Features.Printing.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.RequirePropertyRead)]
[Route("api/v1/properties")]
public sealed class PropertyPrintController(ISender sender, IConfiguration configuration) : ControllerBase
{
    [HttpGet("{id:guid}/namuna8-extract.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadSingleNamuna8ExtractPdf(
        Guid id,
        [FromQuery] Guid? assessmentYearId = null,
        CancellationToken cancellationToken = default)
    {
        GeneratedPdfFileDto file = await sender.Send(
            new GetNamuna8ExtractPdfQuery(
                PropertyId: id,
                WardNo: null,
                AssessmentYearId: assessmentYearId,
                PropertyIds: null,
                FrontendBaseUrl: ResolveFrontendBaseUrl()),
            cancellationToken).ConfigureAwait(false);

        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }

    [HttpGet("namuna8-extract/batch.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadBatchNamuna8ExtractPdf(
        [FromQuery] string? wardNo = null,
        [FromQuery] Guid? assessmentYearId = null,
        [FromQuery] string[]? propertyIds = null,
        CancellationToken cancellationToken = default)
    {
        GeneratedPdfFileDto file = await sender.Send(
            new GetNamuna8ExtractPdfQuery(
                PropertyId: null,
                WardNo: wardNo,
                AssessmentYearId: assessmentYearId,
                PropertyIds: QueryParamHelper.ParseGuidList(propertyIds),
                FrontendBaseUrl: ResolveFrontendBaseUrl()),
            cancellationToken).ConfigureAwait(false);

        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }

    [HttpGet("namuna8-extract/preview")]
    [ProducesResponseType(typeof(IReadOnlyList<Namuna8ExtractPdfDocumentModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Namuna8ExtractPdfDocumentModel>>> PreviewNamuna8Extracts(
        [FromQuery] Guid? propertyId = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] Guid? assessmentYearId = null,
        [FromQuery] string[]? propertyIds = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Namuna8ExtractPdfDocumentModel> extracts = await sender.Send(
            new GetNamuna8ExtractPreviewQuery(
                PropertyId: propertyId,
                WardNo: wardNo,
                AssessmentYearId: assessmentYearId,
                PropertyIds: QueryParamHelper.ParseGuidList(propertyIds),
                FrontendBaseUrl: ResolveFrontendBaseUrl()),
            cancellationToken).ConfigureAwait(false);

        return Ok(extracts);
    }

    [HttpGet("nameplates.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadNameplatesPdf(
        [FromQuery] Guid? propertyId = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] string[]? propertyIds = null,
        [FromQuery] string layout = "bulk",
        CancellationToken cancellationToken = default)
    {
        GeneratedPdfFileDto file = await sender.Send(
            new GetDigitalNameplatesPdfQuery(
                PropertyId: propertyId,
                WardNo: wardNo,
                PropertyIds: QueryParamHelper.ParseGuidList(propertyIds),
                LayoutMode: layout,
                FrontendBaseUrl: ResolveFrontendBaseUrl()),
            cancellationToken).ConfigureAwait(false);

        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }

    [HttpGet("nameplates/preview")]
    [ProducesResponseType(typeof(IReadOnlyList<DigitalNameplateItemData>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DigitalNameplateItemData>>> PreviewNameplates(
        [FromQuery] Guid? propertyId = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] string[]? propertyIds = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DigitalNameplateItemData> plates = await sender.Send(
            new GetDigitalNameplatesPreviewQuery(
                PropertyId: propertyId,
                WardNo: wardNo,
                PropertyIds: QueryParamHelper.ParseGuidList(propertyIds),
                FrontendBaseUrl: ResolveFrontendBaseUrl()),
            cancellationToken).ConfigureAwait(false);

        return Ok(plates);
    }

    private string ResolveFrontendBaseUrl()
    {
        string? origin = Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(origin, UriKind.Absolute, out Uri? originUri))
        {
            return $"{originUri.Scheme}://{originUri.Authority}";
        }

        string? configured = configuration["Frontend:BaseUrl"] ?? configuration["App:FrontendBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        return $"{Request.Scheme}://{Request.Host}";
    }
}

