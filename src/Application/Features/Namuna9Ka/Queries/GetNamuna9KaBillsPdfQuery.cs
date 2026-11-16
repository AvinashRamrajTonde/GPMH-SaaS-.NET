using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9Ka;
using GPMH.Application.DTOs.Printing;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Namuna9Ka.Queries;

public sealed record GetNamuna9KaBillsPdfQuery(
    Guid? AssessmentYearId = null,
    string? WardNo = null,
    string? Search = null,
    IReadOnlyCollection<Guid>? PropertyIds = null,
    string LayoutMode = "2in1_landscape",
    string SplitMode = "counterfoil") : IRequest<GeneratedPdfFileDto>;

public sealed class GetNamuna9KaBillsPdfQueryValidator : AbstractValidator<GetNamuna9KaBillsPdfQuery>
{
    public GetNamuna9KaBillsPdfQueryValidator()
    {
        RuleFor(x => x.LayoutMode)
            .Must(m => NormalizeLayout(m) is "2in1_landscape" or "a5")
            .WithMessage("LayoutMode must be '2in1_landscape' or 'a5'.");

        RuleFor(x => x.SplitMode)
            .Must(s => NormalizeSplit(s) is "counterfoil" or "batch2in1")
            .WithMessage("SplitMode must be 'counterfoil' or 'batch2in1'.");

        RuleFor(x => x.WardNo).MaximumLength(40);
        RuleFor(x => x.Search).MaximumLength(100);
    }

    internal static string NormalizeLayout(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? "2in1_landscape" : raw.Trim().ToLowerInvariant();

    internal static string NormalizeSplit(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? "counterfoil" : raw.Trim().ToLowerInvariant();
}

public sealed class GetNamuna9KaBillsPdfQueryHandler(
    INamuna9KaRepository repository,
    IPrintDataRepository printDataRepository,
    IStatutoryPdfRenderer pdfRenderer,
    ITenantContext tenantContext)
    : IRequestHandler<GetNamuna9KaBillsPdfQuery, GeneratedPdfFileDto>
{
    public async Task<GeneratedPdfFileDto> Handle(
        GetNamuna9KaBillsPdfQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        Namuna9KaWorkspaceDto workspace = await repository
            .LoadWorkspaceAsync(
                tenantId,
                request.AssessmentYearId,
                request.WardNo,
                request.Search,
                pendingOnly: false,
                request.PropertyIds,
                cancellationToken)
            .ConfigureAwait(false);

        if (workspace.Items.Count == 0)
        {
            throw new NotFoundException(
                nameof(PropertyDemandBill),
                request.WardNo ?? request.AssessmentYearId?.ToString() ?? "batch");
        }

        string layout = GetNamuna9KaBillsPdfQueryValidator.NormalizeLayout(request.LayoutMode);
        string split = GetNamuna9KaBillsPdfQueryValidator.NormalizeSplit(request.SplitMode);

        Namuna9KaPdfHeaderModel header = await repository
            .LoadPdfHeaderAsync(tenantId, workspace.SelectedYear.YearLabel, cancellationToken)
            .ConfigureAwait(false);

        Namuna9KaPdfDocumentModel docModel = new(
            Header: header,
            Bills: workspace.Items,
            LayoutMode: layout,
            SplitMode: split,
            NoticeGraceDays: workspace.NoticeGraceDays,
            EarlyRebatePct: workspace.EarlyRebatePct,
            UpiPayeeName: workspace.ConfiguredPayeeName);

        byte[] pdfBytes = pdfRenderer.RenderNamuna9KaBillsPdf(docModel);

        string detailsJson = JsonSerializer.Serialize(new
        {
            yearLabel = workspace.SelectedYear.YearLabel,
            layoutMode = layout,
            splitMode = split,
            wardNo = request.WardNo,
            billCount = workspace.Items.Count,
            issuedCount = workspace.Items.Count(b => b.BillId.HasValue),
        });

        await printDataRepository
            .RecordPrintAuditAsync(
                tenantId,
                "Namuna9KaBillsPrinted",
                $"year:{workspace.SelectedYear.YearLabel}",
                detailsJson,
                cancellationToken)
            .ConfigureAwait(false);

        string safeYear = new([.. workspace.SelectedYear.YearLabel.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')]);
        string fileName = $"namuna9ka-bills-{safeYear}-{layout}.pdf";
        return new GeneratedPdfFileDto(fileName, pdfBytes);
    }
}

