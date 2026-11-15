using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auditing;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Auditing.Queries;

public sealed record GetEntityAuditTrailQuery(
    string EntityName,
    string EntityId) : IRequest<IReadOnlyList<AuditLogDto>>;

public sealed class GetEntityAuditTrailQueryValidator : AbstractValidator<GetEntityAuditTrailQuery>
{
    public GetEntityAuditTrailQueryValidator()
    {
        RuleFor(x => x.EntityName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.EntityId).NotEmpty().MaximumLength(120);
    }
}

public sealed class GetEntityAuditTrailQueryHandler(
    IAuditLogReader auditLogReader,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : IRequestHandler<GetEntityAuditTrailQuery, IReadOnlyList<AuditLogDto>>
{
    public async Task<IReadOnlyList<AuditLogDto>> Handle(
        GetEntityAuditTrailQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
        {
            throw new AuthenticationFailedException("Authenticated context is required.");
        }

        Guid? scopedTenantId = currentUserService.Role == ApplicationRole.SuperAdmin
            ? tenantContext.CurrentTenantId
            : tenantContext.CurrentTenantId
                ?? throw new TenantAccessDeniedException("Valid tenant context is required to query tenant audit logs.");

        IReadOnlyList<AuditLog> entries = await auditLogReader
            .GetEntityTrailAsync(
                request.EntityName.Trim(),
                request.EntityId.Trim(),
                scopedTenantId,
                cancellationToken)
            .ConfigureAwait(false);

        return entries
            .Select(l => new AuditLogDto(
                l.Id,
                l.TenantId,
                l.EntityName,
                l.EntityId,
                l.Action,
                l.ActorId,
                l.ActorRole,
                l.CorrelationId,
                l.IpAddress,
                l.UserAgent,
                l.OldValuesJson,
                l.NewValuesJson,
                l.ChangedColumnsJson,
                l.TimestampUtc))
            .ToList();
    }
}

