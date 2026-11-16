using System.Text.Json;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Mutations;
using GPMH.Application.DTOs.Printing;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Mutations.Queries;

public sealed record GetMutationsQuery(
    string? Status = null,
    string? Search = null,
    string? WardNo = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedMutationsDto>;

public sealed record GetMutationByIdQuery(Guid Id) : IRequest<MutationDetailDto>;

public sealed record GetMutationRegisterPdfQuery(
    string? WardNo = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IRequest<GeneratedPdfFileDto>;

public sealed class MutationQueryHandlers(
    IMutationRepository mutationRepository,
    IStatutoryPdfRenderer pdfRenderer,
    IPrintDataRepository printDataRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetMutationsQuery, PagedMutationsDto>,
      IRequestHandler<GetMutationByIdQuery, MutationDetailDto>,
      IRequestHandler<GetMutationRegisterPdfQuery, GeneratedPdfFileDto>
{
    public async Task<PagedMutationsDto> Handle(GetMutationsQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        return await mutationRepository
            .SearchAsync(tenantId, request.Status, request.Search, request.WardNo, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MutationDetailDto> Handle(GetMutationByIdQuery request, CancellationToken cancellationToken)
    {
        return await mutationRepository
            .GetDetailByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PropertyMutation), request.Id);
    }

    public async Task<GeneratedPdfFileDto> Handle(GetMutationRegisterPdfQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        MutationRegisterPdfDocumentModel model = await mutationRepository
            .LoadRegisterPdfModelAsync(tenantId, request.WardNo, request.FromDate, request.ToDate, cancellationToken)
            .ConfigureAwait(false);

        byte[] pdfBytes = pdfRenderer.RenderMutationRegisterPdf(model);

        string scope = string.IsNullOrWhiteSpace(request.WardNo)
            ? "all"
            : new([.. request.WardNo.Trim().Where(c => char.IsLetterOrDigit(c) || c == '-')]);

        string details = JsonSerializer.Serialize(new
        {
            format = "MutationRegisterPdf",
            rowCount = model.Rows.Count,
            wardNo = request.WardNo,
            fromDate = request.FromDate,
            toDate = request.ToDate,
        });

        await printDataRepository
            .RecordPrintAuditAsync(tenantId, "MutationRegisterPrinted", $"ward:{scope}", details, cancellationToken)
            .ConfigureAwait(false);

        return new GeneratedPdfFileDto($"mutation-register-{scope}.pdf", pdfBytes);
    }
}

