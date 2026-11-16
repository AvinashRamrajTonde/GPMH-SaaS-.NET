using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Mutations;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Mutations.Commands;

public sealed record CreateMutationCommand(
    Guid PropertyId,
    string MutationType,
    IReadOnlyList<PropertyOwnerRequestItem> ProposedOwners,
    string? DocumentReferenceNo = null,
    DateOnly? DocumentDate = null,
    string? Remarks = null,
    IReadOnlyList<string>? SupportingAssetKeys = null) : IRequest<MutationDetailDto>;

public sealed record RecordMutationObjectionCommand(
    Guid MutationId,
    string ObjectionSummary) : IRequest<MutationDetailDto>;

public sealed record ApproveMutationCommand(
    Guid MutationId,
    string ResolutionNo,
    DateOnly ResolutionDate,
    string? ObjectionResolvedNotes = null) : IRequest<MutationDetailDto>;

public sealed record RejectMutationCommand(
    Guid MutationId,
    string RejectionReason,
    string ResolutionNo,
    DateOnly ResolutionDate) : IRequest<MutationDetailDto>;

internal static class MutationValidationRules
{
    public static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Heirship",
        "SaleDeed",
        "GiftDeed",
        "ReleaseDeed",
        "CourtDecree",
        "Partition",
    };

    public static string NormalizeMutationType(string raw) => raw.Trim() switch
    {
        var s when s.Equals("Heirship", StringComparison.OrdinalIgnoreCase) => "Heirship",
        var s when s.Equals("SaleDeed", StringComparison.OrdinalIgnoreCase) => "SaleDeed",
        var s when s.Equals("GiftDeed", StringComparison.OrdinalIgnoreCase) => "GiftDeed",
        var s when s.Equals("ReleaseDeed", StringComparison.OrdinalIgnoreCase) => "ReleaseDeed",
        var s when s.Equals("CourtDecree", StringComparison.OrdinalIgnoreCase) => "CourtDecree",
        var s when s.Equals("Partition", StringComparison.OrdinalIgnoreCase) => "Partition",
        _ => raw.Trim(),
    };
}

public sealed class CreateMutationCommandValidator : AbstractValidator<CreateMutationCommand>
{
    public CreateMutationCommandValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.MutationType)
            .NotEmpty()
            .Must(t => t is not null && MutationValidationRules.AllowedTypes.Contains(t.Trim()))
            .WithMessage("MutationType must be Heirship, SaleDeed, GiftDeed, ReleaseDeed, CourtDecree, or Partition.");
        RuleFor(x => x.ProposedOwners)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one new owner must be specified.");
        RuleFor(x => x.DocumentReferenceNo).MaximumLength(120);
        RuleFor(x => x.Remarks).MaximumLength(600);
        RuleForEach(x => x.ProposedOwners).ChildRules(owner =>
        {
            owner.RuleFor(o => o.CitizenId).NotEmpty();
            owner.RuleFor(o => o.OwnershipSharePct).GreaterThan(0m).LessThanOrEqualTo(100m);
        });
    }
}

public sealed class RecordMutationObjectionCommandValidator : AbstractValidator<RecordMutationObjectionCommand>
{
    public RecordMutationObjectionCommandValidator()
    {
        RuleFor(x => x.MutationId).NotEmpty();
        RuleFor(x => x.ObjectionSummary).NotEmpty().MaximumLength(600);
    }
}

public sealed class ApproveMutationCommandValidator : AbstractValidator<ApproveMutationCommand>
{
    public ApproveMutationCommandValidator()
    {
        RuleFor(x => x.MutationId).NotEmpty();
        RuleFor(x => x.ResolutionNo).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ResolutionDate).NotEmpty();
        RuleFor(x => x.ObjectionResolvedNotes).MaximumLength(500);
    }
}

public sealed class RejectMutationCommandValidator : AbstractValidator<RejectMutationCommand>
{
    public RejectMutationCommandValidator()
    {
        RuleFor(x => x.MutationId).NotEmpty();
        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ResolutionNo).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ResolutionDate).NotEmpty();
    }
}

public sealed class MutationCommandHandlers(
    IMutationRepository mutationRepository,
    IPropertyRepository propertyRepository,
    ICitizenRepository citizenRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateMutationCommand, MutationDetailDto>,
      IRequestHandler<RecordMutationObjectionCommand, MutationDetailDto>,
      IRequestHandler<ApproveMutationCommand, MutationDetailDto>,
      IRequestHandler<RejectMutationCommand, MutationDetailDto>
{
    private static readonly JsonSerializerOptions JsonWebOptions = new(JsonSerializerDefaults.Web);

    public async Task<MutationDetailDto> Handle(CreateMutationCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        Property property = await propertyRepository
            .GetByIdAsync(request.PropertyId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Property), request.PropertyId);

        if (!property.IsActive || string.Equals(property.Status, "Partitioned", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainConflictException(
                "PROPERTY_INACTIVE",
                $"Property '{property.WardNo}/{property.PropertyNo}' is inactive or already partitioned.");
        }

        decimal shareSum = request.ProposedOwners.Sum(o => o.OwnershipSharePct);
        if (Math.Abs(shareSum - 100.00m) > 0.01m)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.ProposedOwners)] = [$"Proposed ownership percentages must equal 100.00% (currently {shareSum:F2}%)."],
            });
        }

        if (!request.ProposedOwners.Any(o => o.IsPrimaryOwner))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.ProposedOwners)] = ["Exactly one primary new owner must be designated."],
            });
        }

        HashSet<Guid> distinctCitizenIds = [.. request.ProposedOwners.Select(o => o.CitizenId)];
        if (distinctCitizenIds.Count != request.ProposedOwners.Count)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.ProposedOwners)] = ["Duplicate citizen entries are not allowed in proposed owners."],
            });
        }

        IReadOnlyDictionary<Guid, Citizen> citizensById = await citizenRepository
            .GetByIdsAsync(distinctCitizenIds, cancellationToken)
            .ConfigureAwait(false);

        if (citizensById.Count != distinctCitizenIds.Count)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.ProposedOwners)] = ["One or more proposed new owner citizens were not found in this Gram Panchayat."],
            });
        }

        IReadOnlyList<MutationSupportingAssetDto> verifiedAssets = request.SupportingAssetKeys is { Count: > 0 }
            ? await mutationRepository.ValidateTenantAssetKeysAsync(tenantId, request.SupportingAssetKeys, cancellationToken).ConfigureAwait(false)
            : [];

        if (request.SupportingAssetKeys is { Count: > 0 } && verifiedAssets.Count != request.SupportingAssetKeys.Distinct(StringComparer.Ordinal).Count())
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.SupportingAssetKeys)] = ["One or more uploaded document keys were invalid or outside this tenant."],
            });
        }

        IReadOnlyList<MutationOwnerSnapshotItem> previousOwners = await mutationRepository
            .LoadCurrentPropertyOwnersAsync(property.Id, cancellationToken)
            .ConfigureAwait(false);

        List<MutationOwnerSnapshotItem> proposedSnapshots = [.. request.ProposedOwners.Select(item =>
        {
            Citizen c = citizensById[item.CitizenId];
            return new MutationOwnerSnapshotItem(
                CitizenId: c.Id,
                CitizenCode: c.CitizenCode,
                FullNameMr: c.FullNameMr,
                FullNameEn: c.FullNameEn,
                AadhaarMasked: string.IsNullOrWhiteSpace(c.AadhaarLast4) ? null : $"XXXX-XXXX-{c.AadhaarLast4}",
                OwnershipSharePct: Math.Round(item.OwnershipSharePct, 2, MidpointRounding.AwayFromZero),
                IsPrimaryOwner: item.IsPrimaryOwner);
        })];

        AssessmentYear? activeYear = await propertyRepository
            .GetActiveAssessmentYearAsync(cancellationToken)
            .ConfigureAwait(false);
        int yearNum = activeYear?.StartYear ?? dateTimeProvider.UtcNow.Year;
        string appNo = await mutationRepository
            .GenerateNextApplicationNoAsync(yearNum, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset issuedUtc = dateTimeProvider.UtcNow;
        PropertyMutation mutation = new()
        {
            TenantId = tenantId,
            PropertyId = property.Id,
            ApplicationNo = appNo,
            MutationType = MutationValidationRules.NormalizeMutationType(request.MutationType),
            PreviousOwnersSnapshotJson = JsonSerializer.Serialize(previousOwners, JsonWebOptions),
            ProposedOwnersJson = JsonSerializer.Serialize(proposedSnapshots, JsonWebOptions),
            DocumentReferenceNo = string.IsNullOrWhiteSpace(request.DocumentReferenceNo) ? null : request.DocumentReferenceNo.Trim(),
            DocumentDate = request.DocumentDate,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
            SupportingAssetIdsJson = verifiedAssets.Count > 0 ? JsonSerializer.Serialize(verifiedAssets, JsonWebOptions) : null,
            NoticeIssuedAtUtc = issuedUtc,
            NoticeEndsAtUtc = issuedUtc.AddDays(15),
            HasObjection = false,
            Status = "NoticePublished",
        };

        await mutationRepository.AddAsync(mutation, cancellationToken).ConfigureAwait(false);
        await mutationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await mutationRepository
            .GetDetailByIdAsync(mutation.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PropertyMutation), mutation.Id);
    }

    public async Task<MutationDetailDto> Handle(RecordMutationObjectionCommand request, CancellationToken cancellationToken)
    {
        PropertyMutation mutation = await mutationRepository
            .GetByIdAsync(request.MutationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PropertyMutation), request.MutationId);

        if (mutation.IsFinalized())
        {
            throw new DomainConflictException(
                "MUTATION_FINALIZED",
                $"Mutation '{mutation.ApplicationNo}' is already {mutation.Status} and cannot receive objections.");
        }

        mutation.HasObjection = true;
        mutation.ObjectionSummary = request.ObjectionSummary.Trim();
        mutation.ObjectionRecordedByUserId = currentUserService.UserId;
        mutation.ObjectionRecordedAtUtc = dateTimeProvider.UtcNow;
        mutation.Status = "ObjectionRecorded";

        await mutationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await mutationRepository
            .GetDetailByIdAsync(mutation.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PropertyMutation), mutation.Id);
    }

    public async Task<MutationDetailDto> Handle(ApproveMutationCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.Gramsevak or ApplicationRole.GP_Admin or ApplicationRole.SuperAdmin))
        {
            throw new TenantAccessDeniedException("Only Gramsevak, GP_Admin, or SuperAdmin may approve property mutations.");
        }

        PropertyMutation mutation = await mutationRepository
            .GetByIdAsync(request.MutationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PropertyMutation), request.MutationId);

        if (mutation.IsFinalized())
        {
            throw new DomainConflictException(
                "MUTATION_FINALIZED",
                $"Mutation '{mutation.ApplicationNo}' is already {mutation.Status}.");
        }

        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        if (!mutation.IsNoticeWindowElapsed(utcNow))
        {
            int daysRemaining = Math.Max(1, (int)Math.Ceiling((mutation.NoticeEndsAtUtc - utcNow).TotalDays));
            throw new DomainConflictException(
                "NOTICE_PERIOD_ACTIVE",
                $"15-day statutory public notice period for {mutation.ApplicationNo} ends on {mutation.NoticeEndsAtUtc:dd/MM/yyyy} ({daysRemaining} day(s) remaining).",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["noticeEndsAtUtc"] = mutation.NoticeEndsAtUtc,
                    ["daysRemaining"] = daysRemaining,
                });
        }

        if (mutation.HasObjection && string.IsNullOrWhiteSpace(request.ObjectionResolvedNotes))
        {
            throw new DomainConflictException(
                "OBJECTION_UNRESOLVED",
                "Mutation carries an active citizen objection; supply ObjectionResolvedNotes describing the Gram Panchayat resolution decision.");
        }

        List<MutationOwnerSnapshotItem> proposed = JsonSerializer.Deserialize<List<MutationOwnerSnapshotItem>>(
            mutation.ProposedOwnersJson,
            JsonWebOptions) ?? [];

        if (proposed.Count == 0)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(mutation.ProposedOwnersJson)] = ["Mutation contains no proposed owners."],
            });
        }

        // Verify all proposed citizens remain present in tenant register
        HashSet<Guid> cidSet = [.. proposed.Select(p => p.CitizenId)];
        IReadOnlyDictionary<Guid, Citizen> existing = await citizenRepository
            .GetByIdsAsync(cidSet, cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count != cidSet.Count)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["ProposedOwners"] = ["One or more proposed owner citizen records are missing."],
            });
        }

        List<PropertyOwner> replacementOwners = [.. proposed.Select(p => new PropertyOwner
        {
            TenantId = mutation.TenantId,
            PropertyId = mutation.PropertyId,
            CitizenId = p.CitizenId,
            OwnershipSharePct = p.OwnershipSharePct,
            IsPrimaryOwner = p.IsPrimaryOwner,
            IsActive = true,
        })];

        mutation.Status = "Approved";
        mutation.ResolutionNo = request.ResolutionNo.Trim();
        mutation.ResolutionDate = request.ResolutionDate;
        mutation.ApprovedByUserId = currentUserService.UserId;
        mutation.ApprovedAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(request.ObjectionResolvedNotes))
        {
            string clearance = $"[आक्षेप निकाली: {request.ObjectionResolvedNotes.Trim()}]";
            mutation.Remarks = string.IsNullOrWhiteSpace(mutation.Remarks)
                ? clearance
                : $"{mutation.Remarks} {clearance}";
        }

        string auditDetails = JsonSerializer.Serialize(new
        {
            applicationNo = mutation.ApplicationNo,
            mutationType = mutation.MutationType,
            resolutionNo = mutation.ResolutionNo,
            resolutionDate = mutation.ResolutionDate,
            previousOwnersJson = mutation.PreviousOwnersSnapshotJson,
            newOwnersCount = replacementOwners.Count,
            objectionCleared = mutation.HasObjection,
        }, JsonWebOptions);

        await mutationRepository
            .ApproveMutationAtomicallyAsync(mutation, replacementOwners, auditDetails, cancellationToken)
            .ConfigureAwait(false);

        return await mutationRepository
            .GetDetailByIdAsync(mutation.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PropertyMutation), mutation.Id);
    }

    public async Task<MutationDetailDto> Handle(RejectMutationCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.Gramsevak or ApplicationRole.GP_Admin or ApplicationRole.SuperAdmin))
        {
            throw new TenantAccessDeniedException("Only Gramsevak, GP_Admin, or SuperAdmin may reject property mutations.");
        }

        PropertyMutation mutation = await mutationRepository
            .GetByIdAsync(request.MutationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PropertyMutation), request.MutationId);

        if (mutation.IsFinalized())
        {
            throw new DomainConflictException(
                "MUTATION_FINALIZED",
                $"Mutation '{mutation.ApplicationNo}' is already {mutation.Status}.");
        }

        mutation.Status = "Rejected";
        mutation.RejectionReason = request.RejectionReason.Trim();
        mutation.ResolutionNo = request.ResolutionNo.Trim();
        mutation.ResolutionDate = request.ResolutionDate;
        mutation.ApprovedByUserId = currentUserService.UserId;
        mutation.ApprovedAtUtc = dateTimeProvider.UtcNow;

        await mutationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await mutationRepository
            .GetDetailByIdAsync(mutation.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PropertyMutation), mutation.Id);
    }
}

