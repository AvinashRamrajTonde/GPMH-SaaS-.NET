using System.Globalization;
using System.Text;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Tenancy;
using MediatR;

namespace GPMH.Application.Features.SuperAdmin.Queries;

public sealed record GetSuperAdminStatsQuery : IRequest<SuperAdminDashboardStatsDto>;

public sealed class GetSuperAdminStatsQueryHandler(ISuperAdminProvisioningRepository repository)
    : IRequestHandler<GetSuperAdminStatsQuery, SuperAdminDashboardStatsDto>
{
    public Task<SuperAdminDashboardStatsDto> Handle(
        GetSuperAdminStatsQuery request,
        CancellationToken cancellationToken) =>
        repository.GetStatewideStatsAsync(cancellationToken);
}

public sealed record GetSuperAdminTenantDetailQuery(Guid TenantId) : IRequest<SuperAdminTenantDetailDto>;

public sealed class GetSuperAdminTenantDetailQueryHandler(ISuperAdminProvisioningRepository repository)
    : IRequestHandler<GetSuperAdminTenantDetailQuery, SuperAdminTenantDetailDto>
{
    public async Task<SuperAdminTenantDetailDto> Handle(
        GetSuperAdminTenantDetailQuery request,
        CancellationToken cancellationToken) =>
        await repository.GetTenantDetailAsync(request.TenantId, cancellationToken).ConfigureAwait(false)
        ?? throw new NotFoundException(nameof(Tenant), request.TenantId);
}

public sealed record GetSuperAdminPaymentsQuery(
    string? Status = null,
    Guid? TenantId = null,
    string? Search = null) : IRequest<IReadOnlyList<TenantPaymentSubmissionDto>>;

public sealed class GetSuperAdminPaymentsQueryHandler(ISuperAdminProvisioningRepository repository)
    : IRequestHandler<GetSuperAdminPaymentsQuery, IReadOnlyList<TenantPaymentSubmissionDto>>
{
    public Task<IReadOnlyList<TenantPaymentSubmissionDto>> Handle(
        GetSuperAdminPaymentsQuery request,
        CancellationToken cancellationToken) =>
        repository.ListPaymentSubmissionsAsync(request.Status, request.TenantId, request.Search, cancellationToken);
}

public sealed record GetSuperAdminAuditLogsQuery(
    Guid? TenantId = null,
    string? Action = null,
    string? EntityName = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Search = null,
    int Limit = 100) : IRequest<IReadOnlyList<SuperAdminAuditLogItemDto>>;

public sealed class GetSuperAdminAuditLogsQueryHandler(ISuperAdminProvisioningRepository repository)
    : IRequestHandler<GetSuperAdminAuditLogsQuery, IReadOnlyList<SuperAdminAuditLogItemDto>>
{
    public Task<IReadOnlyList<SuperAdminAuditLogItemDto>> Handle(
        GetSuperAdminAuditLogsQuery request,
        CancellationToken cancellationToken) =>
        repository.ListAuditLogsAsync(
            request.TenantId,
            request.Action,
            request.EntityName,
            request.FromDate,
            request.ToDate,
            request.Search,
            request.Limit,
            cancellationToken);
}

public sealed record ExportSuperAdminAuditLogsCsvQuery(
    Guid? TenantId = null,
    string? Action = null,
    string? EntityName = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Search = null) : IRequest<GeneratedPdfFileDto>;

public sealed class ExportSuperAdminAuditLogsCsvQueryHandler(ISuperAdminProvisioningRepository repository)
    : IRequestHandler<ExportSuperAdminAuditLogsCsvQuery, GeneratedPdfFileDto>
{
    public async Task<GeneratedPdfFileDto> Handle(
        ExportSuperAdminAuditLogsCsvQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SuperAdminAuditLogItemDto> items = await repository
            .ListAuditLogsAsync(
                request.TenantId,
                request.Action,
                request.EntityName,
                request.FromDate,
                request.ToDate,
                request.Search,
                limit: 1000,
                cancellationToken)
            .ConfigureAwait(false);

        StringBuilder sb = new();
        sb.AppendLine("TimestampUtc,TenantLgdCode,TenantNameEn,Action,EntityName,EntityId,ActorId,ActorRole,IpAddress,NewValuesJson");

        foreach (SuperAdminAuditLogItemDto item in items)
        {
            sb.Append(CsvEscape(item.TimestampUtc.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(CsvEscape(item.LgdCode)).Append(',')
                .Append(CsvEscape(item.TenantNameEn)).Append(',')
                .Append(CsvEscape(item.Action)).Append(',')
                .Append(CsvEscape(item.EntityName)).Append(',')
                .Append(CsvEscape(item.EntityId)).Append(',')
                .Append(CsvEscape(item.ActorId)).Append(',')
                .Append(CsvEscape(item.ActorRole)).Append(',')
                .Append(CsvEscape(item.IpAddress)).Append(',')
                .Append(CsvEscape(item.NewValuesJson))
                .AppendLine();
        }

        byte[] bom = Encoding.UTF8.GetPreamble();
        byte[] body = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] csv = new byte[bom.Length + body.Length];
        bom.CopyTo(csv, 0);
        body.CopyTo(csv, bom.Length);

        return new GeneratedPdfFileDto($"SuperAdmin_AuditLogs_{DateTime.UtcNow:yyyyMMdd}.csv", csv);
    }

    private static string CsvEscape(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "\"\"";
        }

        string safe = raw.TrimStart();
        if (safe.Length > 0 && (safe[0] is '=' or '+' or '-' or '@'))
        {
            safe = "'" + safe;
        }

        return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}

public sealed record GetPlatformPaymentAccountQuery : IRequest<PlatformPaymentAccountDto>;

public sealed class GetPlatformPaymentAccountQueryHandler(IPlatformBillingRepository billingRepository)
    : IRequestHandler<GetPlatformPaymentAccountQuery, PlatformPaymentAccountDto>
{
    public async Task<PlatformPaymentAccountDto> Handle(
        GetPlatformPaymentAccountQuery request,
        CancellationToken cancellationToken)
    {
        PlatformPaymentAccount acc = await billingRepository
            .GetOrCreatePlatformAccountAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PlatformPaymentAccountDto(
            acc.Id,
            acc.AccountName,
            acc.BankName,
            acc.BranchName,
            acc.AccountNumber,
            acc.IfscCode,
            acc.UpiId,
            acc.AnnualFee,
            acc.GstPercent,
            acc.Gstin,
            acc.SupportPhone,
            acc.SupportEmail,
            acc.IsActive,
            acc.UpdatedAtUtc ?? acc.CreatedAtUtc);
    }
}

public sealed record GetSuperAdminPaymentInvoicePdfQuery(Guid PaymentOrSubmissionId) : IRequest<GeneratedPdfFileDto>;

public sealed class GetSuperAdminPaymentInvoicePdfQueryHandler(
    IPlatformBillingRepository billingRepository,
    IStatutoryPdfRenderer pdfRenderer)
    : IRequestHandler<GetSuperAdminPaymentInvoicePdfQuery, GeneratedPdfFileDto>
{
    public async Task<GeneratedPdfFileDto> Handle(
        GetSuperAdminPaymentInvoicePdfQuery request,
        CancellationToken cancellationToken)
    {
        GstSubscriptionInvoicePdfModel model = await billingRepository
            .BuildGstInvoiceModelAsync(request.PaymentOrSubmissionId, restrictToTenantId: null, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(TenantSubscriptionPayment), request.PaymentOrSubmissionId);

        byte[] bytes = pdfRenderer.RenderSubscriptionGstInvoicePdf(model);
        string safeInv = model.InvoiceNo.Replace('/', '-').Replace(' ', '_');
        return new GeneratedPdfFileDto($"Invoice_{safeInv}.pdf", bytes);
    }
}

