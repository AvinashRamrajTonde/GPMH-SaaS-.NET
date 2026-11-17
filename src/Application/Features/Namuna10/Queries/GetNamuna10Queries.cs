using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Application.DTOs.Namuna10;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Domain.Services;
using MediatR;

namespace GPMH.Application.Features.Namuna10.Queries;

public sealed record GetNamuna10RegisterQuery(
    Guid? AssessmentYearId = null,
    string? WardNo = null,
    string? Search = null,
    string? StatusFilter = null) : IRequest<Namuna10RegisterWorkspaceDto>;

public sealed record GetNamuna10ReceiptsQuery(
    Guid? AssessmentYearId = null,
    Guid? PropertyId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    PaymentMode? Mode = null,
    string? WardNo = null,
    string? Search = null,
    bool IncludeCancelled = true) : IRequest<Namuna10ReceiptsPageDto>;

public sealed record GetNamuna10ReceiptByIdQuery(Guid ReceiptId) : IRequest<TaxReceiptDto>;

public sealed class GetNamuna10RegisterQueryHandler(INamuna10Repository repository)
    : IRequestHandler<GetNamuna10RegisterQuery, Namuna10RegisterWorkspaceDto>
{
    public async Task<Namuna10RegisterWorkspaceDto> Handle(
        GetNamuna10RegisterQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AssessmentYear> years = await repository.ListYearsAsync(cancellationToken).ConfigureAwait(false);
        AssessmentYear selectedYear = await Namuna10Support
            .ResolveYearAsync(repository, request.AssessmentYearId, cancellationToken)
            .ConfigureAwait(false);

        FormulaSetting? formula = await repository.GetActiveFormulaSettingAsync(cancellationToken).ConfigureAwait(false);
        int noticeGraceDays = formula?.NoticeGraceDays ?? 15;
        decimal earlyRebatePct = formula?.EarlyRebatePct ?? Namuna9RebateAndPenaltyCalculator.DefaultEarlyRebatePct;
        DateOnly defaultCutoff = Namuna9RebateAndPenaltyCalculator.DefaultRebateCutoff(selectedYear.StartYear);

        IReadOnlyList<Namuna10DemandContext> contexts = await repository
            .LoadRegisterContextsAsync(selectedYear.Id, request.WardNo, request.Search, cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<Namuna10RegisterRowDto> mapped = contexts.Select(Namuna10Support.ToRegisterRowDto);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
        {
            string filter = request.StatusFilter.Trim();
            mapped = filter.ToLowerInvariant() switch
            {
                "pending" or "unpaid" => mapped.Where(r => r.RemainingBalance > 0m && r.PaidAmount == 0m),
                "partial" or "partiallypaid" => mapped.Where(r => r.RemainingBalance > 0m && r.PaidAmount > 0m),
                "paid" or "settled" => mapped.Where(r => r.RemainingBalance <= 0m),
                "due" or "pendingonly" => mapped.Where(r => r.RemainingBalance > 0m),
                _ => mapped.Where(r => string.Equals(r.DemandStatus, filter, StringComparison.OrdinalIgnoreCase)),
            };
        }

        List<Namuna10RegisterRowDto> rows = [.. mapped];

        Namuna10KpisDto kpis = new(
            TotalProperties: rows.Count,
            TotalDemand: rows.Sum(r => r.NetDemand),
            TotalCollected: rows.Sum(r => r.PaidAmount),
            TotalBalance: rows.Sum(r => r.RemainingBalance),
            PartiallyPaidCount: rows.Count(r => r.RemainingBalance > 0m && r.PaidAmount > 0m),
            FullyPaidCount: rows.Count(r => r.RemainingBalance <= 0m));

        List<Namuna9YearDto> yearDtos = [.. years.Select(Namuna10Support.ToYearDto)];

        return new Namuna10RegisterWorkspaceDto(
            SelectedYear: Namuna10Support.ToYearDto(selectedYear),
            AssessmentYears: yearDtos,
            NoticeGraceDays: noticeGraceDays,
            EarlyRebatePct: earlyRebatePct,
            DefaultRebateCutoffDate: defaultCutoff,
            Kpis: kpis,
            Items: rows);
    }
}

public sealed class GetNamuna10ReceiptsQueryHandler(INamuna10Repository repository)
    : IRequestHandler<GetNamuna10ReceiptsQuery, Namuna10ReceiptsPageDto>
{
    public async Task<Namuna10ReceiptsPageDto> Handle(
        GetNamuna10ReceiptsQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AssessmentYear> years = await repository.ListYearsAsync(cancellationToken).ConfigureAwait(false);
        AssessmentYear selectedYear = await Namuna10Support
            .ResolveYearAsync(repository, request.AssessmentYearId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Namuna10ReceiptContext> contexts = await repository
            .ListReceiptsAsync(
                selectedYear.Id,
                request.PropertyId,
                request.FromDate,
                request.ToDate,
                request.Mode,
                request.WardNo,
                request.Search,
                request.IncludeCancelled,
                cancellationToken)
            .ConfigureAwait(false);

        List<TaxReceiptDto> items =
        [
            .. contexts.Select(c => Namuna10Support.ToReceiptDto(c, repository)),
        ];

        decimal activeCollected = items
            .Where(i => !i.IsCancelled)
            .Sum(i => i.TotalPaid);
        int cancelledCount = items.Count(i => i.IsCancelled);

        return new Namuna10ReceiptsPageDto(
            SelectedYear: Namuna10Support.ToYearDto(selectedYear),
            AssessmentYears: [.. years.Select(Namuna10Support.ToYearDto)],
            TotalCount: items.Count,
            TotalCollectedAmount: activeCollected,
            CancelledCount: cancelledCount,
            Items: items);
    }
}

public sealed class GetNamuna10ReceiptByIdQueryHandler(INamuna10Repository repository)
    : IRequestHandler<GetNamuna10ReceiptByIdQuery, TaxReceiptDto>
{
    public async Task<TaxReceiptDto> Handle(
        GetNamuna10ReceiptByIdQuery request,
        CancellationToken cancellationToken)
    {
        Namuna10ReceiptContext ctx = await repository
            .GetReceiptByIdAsync(request.ReceiptId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(TaxReceipt), request.ReceiptId);

        return Namuna10Support.ToReceiptDto(ctx, repository);
    }
}

