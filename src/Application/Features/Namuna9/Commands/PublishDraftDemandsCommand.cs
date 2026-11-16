using System.Text.Json;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Domain.Services;
using MediatR;

namespace GPMH.Application.Features.Namuna9.Commands;

public sealed record PublishDraftDemandsCommand(
    Guid? AssessmentYearId = null,
    IReadOnlyList<Guid>? PropertyIds = null) : IRequest<Namuna9PublishResultDto>;

public sealed class PublishDraftDemandsCommandHandler(
    INamuna9Repository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<PublishDraftDemandsCommand, Namuna9PublishResultDto>
{
    public async Task<Namuna9PublishResultDto> Handle(
        PublishDraftDemandsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.SuperAdmin or ApplicationRole.Gramsevak or ApplicationRole.GP_Admin))
        {
            throw new TenantAccessDeniedException(
                "Only Gramsevak, GP Admin, or Super Admin can publish Namuna 9 demand registers.");
        }

        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");

        AssessmentYear year = await Namuna9Support
            .ResolveYearAsync(repository, request.AssessmentYearId, cancellationToken)
            .ConfigureAwait(false);
        Namuna9Support.EnsureNotLocked(year);

        IReadOnlyList<Namuna9DemandContext> contexts = await repository
            .LoadContextsAsync(year.Id, null, null, request.PropertyIds, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = dateTimeProvider.UtcNow;
        DateOnly defaultCutoff = Namuna9RebateAndPenaltyCalculator.DefaultRebateCutoff(year.StartYear);

        int published = 0;
        int skipped = 0;
        List<Guid> publishedPropertyIds = [];

        foreach (Namuna9DemandContext ctx in contexts)
        {
            PropertyDemand d = ctx.Demand;
            if (!string.Equals(d.Status, PropertyDemand.StatusDraft, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            d.Status = d.PendingBalance <= 0m && d.PaidAmount > 0m
                ? PropertyDemand.StatusSettled
                : PropertyDemand.StatusPublished;
            d.PublishedAtUtc = now;
            d.RebateCutoffDate ??= defaultCutoff;
            d.EarlyRebateEligible = d.CurrentDemand > 0m && !ctx.Property.IsTaxExempt;
            d.RecalculatePendingBalance();

            publishedPropertyIds.Add(ctx.Property.Id);
            published++;
        }

        if (published > 0)
        {
            await repository.AddAuditLogsAsync(
                [
                    Namuna9Support.CreateAudit(
                        currentUserService,
                        dateTimeProvider,
                        tenantId,
                        nameof(PropertyDemand),
                        year.Id.ToString(),
                        "Namuna9DemandsPublished",
                        null,
                        JsonSerializer.Serialize(new
                        {
                            year.YearLabel,
                            PublishedCount = published,
                            PropertyIds = publishedPropertyIds,
                        })),
                ],
                cancellationToken).ConfigureAwait(false);

            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new Namuna9PublishResultDto(year.YearLabel, published, skipped);
    }
}

