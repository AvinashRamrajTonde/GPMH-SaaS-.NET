using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Properties.Commands;

public sealed record DeletePropertyCommand(Guid Id) : IRequest;

public sealed class DeletePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    ITenantContext tenantContext)
    : IRequestHandler<DeletePropertyCommand>
{
    public async Task Handle(DeletePropertyCommand request, CancellationToken cancellationToken)
    {
        if (!tenantContext.CurrentTenantId.HasValue)
        {
            throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        }

        Property property = await propertyRepository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Property), request.Id);

        property.IsDeleted = true;
        property.IsActive = false;

        await propertyRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

