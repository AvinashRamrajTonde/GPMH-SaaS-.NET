using System.Text.Json;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna10;
using GPMH.Application.DTOs.Printing;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Namuna10.Queries;

public sealed record GetNamuna10DualReceiptPdfQuery(
    Guid ReceiptId,
    string FrontendBaseUrl) : IRequest<GeneratedPdfFileDto>;

public sealed class GetNamuna10DualReceiptPdfQueryHandler(
    INamuna10Repository repository,
    IExtractVerificationTokenService verificationTokenService,
    IStatutoryPdfRenderer pdfRenderer,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetNamuna10DualReceiptPdfQuery, GeneratedPdfFileDto>
{
    public async Task<GeneratedPdfFileDto> Handle(
        GetNamuna10DualReceiptPdfQuery request,
        CancellationToken cancellationToken)
    {
        string normalizedBaseUrl = string.IsNullOrWhiteSpace(request.FrontendBaseUrl)
            ? string.Empty
            : request.FrontendBaseUrl.TrimEnd('/');
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;

        Namuna10DualReceiptPdfModel model = await repository
            .LoadReceiptPdfModelAsync(
                request.ReceiptId,
                (tenantId, receiptId) =>
                {
                    string token = verificationTokenService.GenerateReceiptToken(
                        new ReceiptVerifyPayloadDto(tenantId, receiptId, utcNow));
                    string escaped = Uri.EscapeDataString(token);
                    return string.IsNullOrEmpty(normalizedBaseUrl)
                        ? $"/verify/receipt/{escaped}"
                        : $"{normalizedBaseUrl}/verify/receipt/{escaped}";
                },
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(TaxReceipt), request.ReceiptId);

        byte[] pdfBytes = pdfRenderer.RenderNamuna10DualReceiptPdf(model);

        if (currentUserService.UserId is Guid actorId)
        {
            await repository.AddAuditLogAsync(
                Namuna10Support.CreateAudit(
                    model.TenantId,
                    actorId,
                    currentUserService.Role?.ToString() ?? string.Empty,
                    nameof(TaxReceipt),
                    model.ReceiptId.ToString("D"),
                    "Namuna10ReceiptPrinted",
                    oldJson: null,
                    newJson: JsonSerializer.Serialize(new
                    {
                        ReceiptNo = model.ReceiptNo,
                        IsCancelled = model.IsCancelled,
                        TotalPaid = model.TotalPaid,
                    }),
                    utcNow),
                cancellationToken).ConfigureAwait(false);

            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        string safeReceiptNo = model.ReceiptNo.Replace('/', '-').Replace(' ', '_');
        return new GeneratedPdfFileDto(
            FileName: $"Namuna10_Receipt_{safeReceiptNo}.pdf",
            Content: pdfBytes);
    }
}

