using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Printing;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Namuna10.Queries;

public sealed record VerifyReceiptTokenQuery(string Token) : IRequest<VerifiedReceiptPublicDto>;

public sealed class VerifyReceiptTokenQueryHandler(
    IExtractVerificationTokenService verificationTokenService,
    INamuna10Repository repository)
    : IRequestHandler<VerifyReceiptTokenQuery, VerifiedReceiptPublicDto>
{
    public async Task<VerifiedReceiptPublicDto> Handle(
        VerifyReceiptTokenQuery request,
        CancellationToken cancellationToken)
    {
        if (!verificationTokenService.TryValidateReceiptToken(request.Token, out ReceiptVerifyPayloadDto? payload) ||
            payload is null)
        {
            throw new DomainValidationException(
                "Invalid or tampered Namuna 10 receipt verification token.");
        }

        return await repository
            .LoadVerifiedReceiptPublicAsync(
                payload.TenantId,
                payload.ReceiptId,
                request.Token.Trim(),
                payload.IssuedAtUtc,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(TaxReceipt), payload.ReceiptId);
    }
}

