using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Printing;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Printing.Queries;

public sealed record GetNamuna8ExtractPdfQuery(
    Guid? PropertyId,
    string? WardNo,
    Guid? AssessmentYearId,
    IReadOnlyCollection<Guid>? PropertyIds,
    string FrontendBaseUrl) : IRequest<GeneratedPdfFileDto>;

public sealed record GetNamuna8ExtractPreviewQuery(
    Guid? PropertyId,
    string? WardNo,
    Guid? AssessmentYearId,
    IReadOnlyCollection<Guid>? PropertyIds,
    string FrontendBaseUrl) : IRequest<IReadOnlyList<Namuna8ExtractPdfDocumentModel>>;

public sealed record GetDigitalNameplatesPdfQuery(
    Guid? PropertyId,
    string? WardNo,
    IReadOnlyCollection<Guid>? PropertyIds,
    string LayoutMode,
    string FrontendBaseUrl) : IRequest<GeneratedPdfFileDto>;

public sealed record GetDigitalNameplatesPreviewQuery(
    Guid? PropertyId,
    string? WardNo,
    IReadOnlyCollection<Guid>? PropertyIds,
    string FrontendBaseUrl) : IRequest<IReadOnlyList<DigitalNameplateItemData>>;

public sealed record VerifyExtractTokenQuery(string Token) : IRequest<VerifiedExtractPublicDto>;

public sealed class VerifyExtractTokenQueryValidator : AbstractValidator<VerifyExtractTokenQuery>
{
    public VerifyExtractTokenQueryValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
    }
}

public sealed class PrintingQueryHandlers(
    IPrintDataRepository printDataRepository,
    IStatutoryPdfRenderer pdfRenderer,
    IExtractVerificationTokenService tokenService,
    ITenantContext tenantContext)
    : IRequestHandler<GetNamuna8ExtractPdfQuery, GeneratedPdfFileDto>,
      IRequestHandler<GetNamuna8ExtractPreviewQuery, IReadOnlyList<Namuna8ExtractPdfDocumentModel>>,
      IRequestHandler<GetDigitalNameplatesPdfQuery, GeneratedPdfFileDto>,
      IRequestHandler<GetDigitalNameplatesPreviewQuery, IReadOnlyList<DigitalNameplateItemData>>,
      IRequestHandler<VerifyExtractTokenQuery, VerifiedExtractPublicDto>
{
    public async Task<GeneratedPdfFileDto> Handle(
        GetNamuna8ExtractPdfQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        IReadOnlyList<Namuna8ExtractPdfDocumentModel> extracts = await printDataRepository
            .LoadNamuna8ExtractsAsync(
                tenantId,
                request.PropertyId,
                request.WardNo,
                request.AssessmentYearId,
                request.PropertyIds,
                request.FrontendBaseUrl,
                cancellationToken)
            .ConfigureAwait(false);

        if (extracts.Count == 0)
        {
            throw new NotFoundException(
                nameof(Property),
                request.PropertyId?.ToString() ?? request.WardNo ?? "batch");
        }

        byte[] pdfBytes = pdfRenderer.RenderNamuna8ExtractPdf(extracts);

        string safeScope = extracts.Count == 1
            ? $"{SanitizeSegment(extracts[0].WardNo)}-{SanitizeSegment(extracts[0].PropertyNo)}"
            : $"ward-{SanitizeSegment(string.IsNullOrWhiteSpace(request.WardNo) ? "all" : request.WardNo)}";

        string entityId = request.PropertyId?.ToString() ?? $"ward:{request.WardNo ?? "all"}";
        string details = JsonSerializer.Serialize(new
        {
            format = "Namuna8ExtractPdf",
            count = extracts.Count,
            wardNo = request.WardNo,
            assessmentYearId = request.AssessmentYearId,
            propertyIds = extracts.Select(e => e.PropertyId).Take(50).ToArray(),
        });

        await printDataRepository
            .RecordPrintAuditAsync(tenantId, "ExtractPrinted", entityId, details, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedPdfFileDto($"namuna8-{safeScope}.pdf", pdfBytes);
    }

    public async Task<IReadOnlyList<Namuna8ExtractPdfDocumentModel>> Handle(
        GetNamuna8ExtractPreviewQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        IReadOnlyList<Namuna8ExtractPdfDocumentModel> extracts = await printDataRepository
            .LoadNamuna8ExtractsAsync(
                tenantId,
                request.PropertyId,
                request.WardNo,
                request.AssessmentYearId,
                request.PropertyIds,
                request.FrontendBaseUrl,
                cancellationToken)
            .ConfigureAwait(false);

        if (request.PropertyId.HasValue && extracts.Count == 0)
        {
            throw new NotFoundException(nameof(Property), request.PropertyId.Value);
        }

        return extracts;
    }

    public async Task<GeneratedPdfFileDto> Handle(
        GetDigitalNameplatesPdfQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        IReadOnlyList<DigitalNameplateItemData> plates = await printDataRepository
            .LoadNameplatesAsync(
                tenantId,
                request.PropertyId,
                request.WardNo,
                request.PropertyIds,
                request.FrontendBaseUrl,
                cancellationToken)
            .ConfigureAwait(false);

        if (plates.Count == 0)
        {
            throw new NotFoundException(
                nameof(Property),
                request.PropertyId?.ToString() ?? request.WardNo ?? "nameplates");
        }

        string mode = string.Equals(request.LayoutMode, "single", StringComparison.OrdinalIgnoreCase)
            ? "single"
            : "bulk";

        byte[] pdfBytes = pdfRenderer.RenderDigitalNameplatesPdf(plates, mode);

        string safeScope = plates.Count == 1
            ? $"{SanitizeSegment(plates[0].WardNo)}-{SanitizeSegment(plates[0].PropertyNo)}"
            : $"ward-{SanitizeSegment(string.IsNullOrWhiteSpace(request.WardNo) ? "all" : request.WardNo)}";

        string entityId = request.PropertyId?.ToString() ?? $"ward:{request.WardNo ?? "all"}";
        string details = JsonSerializer.Serialize(new
        {
            format = "DigitalNameplatePdf",
            layoutMode = mode,
            count = plates.Count,
            wardNo = request.WardNo,
        });

        await printDataRepository
            .RecordPrintAuditAsync(tenantId, "ExtractPrinted", entityId, details, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedPdfFileDto($"nameplates-{safeScope}.pdf", pdfBytes);
    }

    public async Task<IReadOnlyList<DigitalNameplateItemData>> Handle(
        GetDigitalNameplatesPreviewQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        return await printDataRepository
            .LoadNameplatesAsync(
                tenantId,
                request.PropertyId,
                request.WardNo,
                request.PropertyIds,
                request.FrontendBaseUrl,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VerifiedExtractPublicDto> Handle(
        VerifyExtractTokenQuery request,
        CancellationToken cancellationToken)
    {
        if (!tokenService.TryValidateToken(request.Token, out ExtractVerifyPayloadDto? payload) || payload is null)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Token"] = ["Invalid or tampered Namuna 8 verification token."],
            });
        }

        VerifiedExtractPublicDto? verified = await printDataRepository
            .LoadVerifiedExtractPublicAsync(
                payload.TenantId,
                payload.PropertyId,
                payload.AssessmentYearId,
                request.Token,
                payload.IssuedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);

        return verified
            ?? throw new NotFoundException(nameof(Property), payload.PropertyId);
    }

    private static string SanitizeSegment(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "item";
        }

        char[] clean = [.. input.Trim().Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_')];
        string s = new string(clean).Trim('_');
        return string.IsNullOrEmpty(s) ? "item" : s;
    }
}

