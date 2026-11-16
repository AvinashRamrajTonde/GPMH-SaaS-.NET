using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Partitions;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Enums;
using GPMH.Domain.Services;
using MediatR;

namespace GPMH.Application.Features.Partitions.Commands;

public sealed record PartitionChildRequestItem(
    string ChildPropertyNo,
    IReadOnlyList<PropertyOwnerRequestItem> Owners,
    decimal BuiltUpAreaSqFt,
    decimal OpenPlotAreaSqFt,
    IReadOnlyList<PropertyFloorRequestItem>? Floors = null,
    string? StreetOrLaneName = null,
    string? CitySurveyOrGutNo = null,
    string? OccupierName = null,
    string? PropertyDescription = null,
    string? BoundaryEast = null,
    string? BoundaryWest = null,
    string? BoundaryNorth = null,
    string? BoundarySouth = null,
    string? WaterConnectionCode = null,
    decimal OtherTax = 0m,
    bool IsTaxExempt = false);

public sealed record ExecutePropertyPartitionCommand(
    Guid ParentPropertyId,
    string ResolutionNo,
    DateOnly ResolutionDate,
    IReadOnlyList<PartitionChildRequestItem> ChildPartitions,
    string? PartitionReason = null,
    string? DocumentReferenceNo = null,
    DateOnly? DocumentDate = null,
    string? Remarks = null,
    IReadOnlyList<string>? SupportingAssetKeys = null) : IRequest<ExecutePartitionResultDto>;

public sealed class PartitionChildRequestItemValidator : AbstractValidator<PartitionChildRequestItem>
{
    public PartitionChildRequestItemValidator()
    {
        RuleFor(x => x.ChildPropertyNo)
            .NotEmpty()
            .MaximumLength(32)
            .Must(v => !string.IsNullOrWhiteSpace(v) && !v.Contains("#SPLIT-", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Child property number must be valid and non-empty (max 32 chars).");

        RuleFor(x => x.BuiltUpAreaSqFt).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.OpenPlotAreaSqFt).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.OtherTax).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Owners)
            .NotNull()
            .NotEmpty()
            .WithMessage("Each child partition must assign at least one owner.");

        RuleFor(x => x)
            .Must(x => (x.Floors is { Count: > 0 } || x.BuiltUpAreaSqFt + x.OpenPlotAreaSqFt > 0m))
            .WithMessage("Each child partition must have positive total area (built-up or open plot).");
    }
}

public sealed class ExecutePropertyPartitionCommandValidator : AbstractValidator<ExecutePropertyPartitionCommand>
{
    public ExecutePropertyPartitionCommandValidator()
    {
        RuleFor(x => x.ParentPropertyId).NotEmpty();
        RuleFor(x => x.ResolutionNo).NotEmpty().MaximumLength(80);
        RuleFor(x => x.DocumentReferenceNo).MaximumLength(120);
        RuleFor(x => x.Remarks).MaximumLength(600);
        RuleFor(x => x.ChildPartitions)
            .NotNull()
            .Must(c => c.Count >= 2)
            .WithMessage("MINIMUM_TWO_PARTITIONS_REQUIRED: A property split must produce at least two child partitions.");

        RuleForEach(x => x.ChildPartitions).SetValidator(new PartitionChildRequestItemValidator());
    }
}

public sealed class ExecutePropertyPartitionCommandHandler(
    IPartitionRepository partitionRepository,
    IPropertyRepository propertyRepository,
    ICitizenRepository citizenRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ISender sender)
    : IRequestHandler<ExecutePropertyPartitionCommand, ExecutePartitionResultDto>
{
    public async Task<ExecutePartitionResultDto> Handle(
        ExecutePropertyPartitionCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.SuperAdmin or ApplicationRole.Gramsevak or ApplicationRole.GP_Admin))
        {
            throw new TenantAccessDeniedException(
                "Only Gramsevak, GP Admin, or Super Admin can approve and execute a property partition.");
        }

        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");

        PartitionContextData context = await partitionRepository
            .LoadPartitionContextAsync(request.ParentPropertyId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Property '{request.ParentPropertyId}' was not found.");

        Property parent = context.Property;
        if (!parent.IsActive || !string.Equals(parent.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainConflictException(
                "PROPERTY_NOT_ACTIVE",
                $"Property '{parent.WardNo}/{parent.PropertyNo}' has status '{parent.Status}' and cannot be partitioned again.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["propertyId"] = parent.Id,
                    ["status"] = parent.Status,
                });
        }

        List<string> supportingKeys = NormalizeAssetKeys(request.SupportingAssetKeys);
        if (supportingKeys.Count > 0)
        {
            IReadOnlySet<string> existingAssets = await partitionRepository
                .GetExistingProtectedAssetKeysAsync(tenantId, supportingKeys, cancellationToken)
                .ConfigureAwait(false);

            List<string> missingAssets = [.. supportingKeys.Where(k => !existingAssets.Contains(k))];
            if (missingAssets.Count > 0)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["SupportingAssetKeys"] = [$"Unknown supporting asset key(s): {string.Join(", ", missingAssets)}"],
                });
            }
        }

        decimal parentPendingDues = context.Demands.Sum(d => d.PendingBalance);
        decimal parentTotalSqFt = parent.TotalAreaSqFt > 0m
            ? parent.TotalAreaSqFt
            : parent.BuiltUpAreaSqFt + parent.OpenPlotAreaSqFt;

        decimal parentAreaSqM = Math.Round(
            parentTotalSqFt * CapitalValueTaxCalculator.SqFtToSqM,
            4,
            MidpointRounding.AwayFromZero);

        List<ChildPartitionArea> validatorChildren = [];
        List<string> candidatePropertyNos = [];
        Dictionary<int, (IReadOnlyList<PropertyFloorRequestItem>? Floors, decimal BuiltUpSqFt, decimal OpenPlotSqFt)> resolvedChildAreas = [];

        for (int i = 0; i < request.ChildPartitions.Count; i++)
        {
            PartitionChildRequestItem childReq = request.ChildPartitions[i];
            string cleanNo = childReq.ChildPropertyNo.Trim();
            candidatePropertyNos.Add(cleanNo);

            (IReadOnlyList<PropertyFloorRequestItem>? childFloors, decimal builtUpSqFt) = ResolveChildFloors(
                parent,
                context.CurrentFloors,
                childReq,
                request.ResolutionDate.Year);

            decimal openPlotSqFt = Math.Round(childReq.OpenPlotAreaSqFt, 4, MidpointRounding.AwayFromZero);
            decimal childTotalSqFt = builtUpSqFt + openPlotSqFt;
            decimal childAreaSqM = Math.Round(
                childTotalSqFt * CapitalValueTaxCalculator.SqFtToSqM,
                4,
                MidpointRounding.AwayFromZero);

            validatorChildren.Add(new ChildPartitionArea(cleanNo, childAreaSqM));
            resolvedChildAreas[i] = (childFloors, builtUpSqFt, openPlotSqFt);
        }

        PartitionValidationResult validation = PropertyPartitionValidator.Validate(
            parentPendingDues,
            parentAreaSqM,
            validatorChildren);

        if (!validation.IsValid)
        {
            string? zeroDuesError = validation.Errors.FirstOrDefault(e =>
                e.StartsWith("ZERO_DUES_GATE_FAILED", StringComparison.Ordinal));
            if (zeroDuesError is not null)
            {
                throw new DomainConflictException(
                    "ZERO_DUES_GATE_FAILED",
                    zeroDuesError,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["propertyId"] = parent.Id,
                        ["pendingDues"] = parentPendingDues,
                        ["errors"] = validation.Errors,
                    });
            }

            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["ChildPartitions"] = [.. validation.Errors],
            });
        }

        IReadOnlyList<string> existingConflicts = await partitionRepository
            .FindConflictingPropertyNosAsync(parent.WardNo, candidatePropertyNos, parent.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existingConflicts.Count > 0)
        {
            throw new DomainConflictException(
                "CHILD_PROPERTY_NO_CONFLICT",
                $"Ward '{parent.WardNo}' already contains property number(s): {string.Join(", ", existingConflicts)}.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["wardNo"] = parent.WardNo,
                    ["conflictingPropertyNos"] = existingConflicts,
                });
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        string originalParentNo = parent.PropertyNo;
        string archivedParentNo = BuildArchivedPropertyNo(originalParentNo, parent.Id);

        List<Property> childProperties = [];
        List<PropertyOwner> allChildOwners = [];
        List<PropertyConstruction> allChildConstructions = [];
        List<PropertyTaxCalc> allChildTaxCalcs = [];
        List<PropertyDemand> allChildDemands = [];
        List<PartitionChildResultDto> childResults = [];

        for (int i = 0; i < request.ChildPartitions.Count; i++)
        {
            PartitionChildRequestItem item = request.ChildPartitions[i];
            (IReadOnlyList<PropertyFloorRequestItem>? childFloors, decimal builtUpSqFt, decimal openPlotSqFt) = resolvedChildAreas[i];

            string waterCode = string.IsNullOrWhiteSpace(item.WaterConnectionCode)
                ? parent.WaterConnectionCode
                : item.WaterConnectionCode.Trim().ToUpperInvariant();

            Property child = new()
            {
                TenantId = tenantId,
                ParentPropertyId = parent.Id,
                WardNo = parent.WardNo,
                PropertyNo = item.ChildPropertyNo.Trim(),
                StreetOrLaneName = string.IsNullOrWhiteSpace(item.StreetOrLaneName)
                    ? parent.StreetOrLaneName
                    : item.StreetOrLaneName.Trim(),
                CitySurveyOrGutNo = string.IsNullOrWhiteSpace(item.CitySurveyOrGutNo)
                    ? parent.CitySurveyOrGutNo
                    : item.CitySurveyOrGutNo.Trim(),
                OccupierName = string.IsNullOrWhiteSpace(item.OccupierName)
                    ? null
                    : item.OccupierName.Trim(),
                PropertyDescription = string.IsNullOrWhiteSpace(item.PropertyDescription)
                    ? parent.PropertyDescription
                    : item.PropertyDescription.Trim(),
                BuiltUpAreaSqFt = builtUpSqFt,
                OpenPlotAreaSqFt = openPlotSqFt,
                TotalAreaSqFt = builtUpSqFt + openPlotSqFt,
                BoundaryEast = string.IsNullOrWhiteSpace(item.BoundaryEast) ? parent.BoundaryEast : item.BoundaryEast.Trim(),
                BoundaryWest = string.IsNullOrWhiteSpace(item.BoundaryWest) ? parent.BoundaryWest : item.BoundaryWest.Trim(),
                BoundaryNorth = string.IsNullOrWhiteSpace(item.BoundaryNorth) ? parent.BoundaryNorth : item.BoundaryNorth.Trim(),
                BoundarySouth = string.IsNullOrWhiteSpace(item.BoundarySouth) ? parent.BoundarySouth : item.BoundarySouth.Trim(),
                WaterConnectionCode = waterCode,
                OtherTax = item.OtherTax,
                IsTaxExempt = item.IsTaxExempt,
                Status = "Active",
                IsActive = true,
            };

            (
                IReadOnlyList<PropertyOwner>? owners,
                IReadOnlyList<PropertyConstruction>? constructions,
                PropertyTaxCalc? taxCalc,
                PropertyDemand? draftDemand,
                decimal computedBuiltUpSqFt,
                string? primaryOwnerNameMr,
                string? primaryOwnerNameEn) = await PropertyAssessmentHelper.PrepareAssessmentAsync(
                    tenantId,
                    child,
                    item.Owners,
                    childFloors,
                    calculateTax: true,
                    openPlotConstructionCode: null,
                    propertyRepository,
                    citizenRepository,
                    sender,
                    cancellationToken).ConfigureAwait(false);

            if (childFloors is { Count: > 0 })
            {
                child.BuiltUpAreaSqFt = computedBuiltUpSqFt;
                child.TotalAreaSqFt = computedBuiltUpSqFt + child.OpenPlotAreaSqFt;
            }

            if (string.IsNullOrWhiteSpace(child.OccupierName) && !string.IsNullOrWhiteSpace(primaryOwnerNameMr))
            {
                child.OccupierName = primaryOwnerNameMr;
            }

            childProperties.Add(child);
            if (owners is { Count: > 0 })
            {
                allChildOwners.AddRange(owners);
            }

            if (constructions is { Count: > 0 })
            {
                allChildConstructions.AddRange(constructions);
            }

            if (taxCalc is not null)
            {
                allChildTaxCalcs.Add(taxCalc);
            }

            if (draftDemand is not null)
            {
                allChildDemands.Add(draftDemand);
            }

            decimal childAreaSqM = Math.Round(
                child.TotalAreaSqFt * CapitalValueTaxCalculator.SqFtToSqM,
                4,
                MidpointRounding.AwayFromZero);

            childResults.Add(new PartitionChildResultDto(
                PropertyId: child.Id,
                WardNo: child.WardNo,
                PropertyNo: child.PropertyNo,
                BuiltUpAreaSqFt: child.BuiltUpAreaSqFt,
                OpenPlotAreaSqFt: child.OpenPlotAreaSqFt,
                TotalAreaSqFt: child.TotalAreaSqFt,
                AreaSqM: childAreaSqM,
                TotalAnnualTax: taxCalc?.TotalTax ?? 0m,
                DraftDemandBalance: draftDemand?.PendingBalance ?? 0m,
                PrimaryOwnerNameMr: primaryOwnerNameMr ?? string.Empty,
                PrimaryOwnerNameEn: primaryOwnerNameEn ?? string.Empty));
        }

        string appNo = $"PRT-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        string combinedRemarks = string.IsNullOrWhiteSpace(request.PartitionReason)
            ? (request.Remarks?.Trim() ?? string.Empty)
            : string.IsNullOrWhiteSpace(request.Remarks)
                ? request.PartitionReason.Trim()
                : $"{request.PartitionReason.Trim()} | {request.Remarks.Trim()}";

        PropertyMutation partitionMutation = new()
        {
            TenantId = tenantId,
            PropertyId = parent.Id,
            ApplicationNo = appNo,
            MutationType = "Partition",
            PreviousOwnersSnapshotJson = JsonSerializer.Serialize(context.CurrentOwners),
            ProposedOwnersJson = JsonSerializer.Serialize(childResults),
            DocumentReferenceNo = string.IsNullOrWhiteSpace(request.DocumentReferenceNo) ? null : request.DocumentReferenceNo.Trim(),
            DocumentDate = request.DocumentDate,
            Remarks = string.IsNullOrWhiteSpace(combinedRemarks) ? null : combinedRemarks[..Math.Min(combinedRemarks.Length, 600)],
            SupportingAssetIdsJson = supportingKeys.Count == 0 ? null : JsonSerializer.Serialize(supportingKeys),
            NoticeIssuedAtUtc = now,
            NoticeEndsAtUtc = now,
            HasObjection = false,
            ResolutionNo = request.ResolutionNo.Trim(),
            ResolutionDate = request.ResolutionDate,
            Status = "Approved",
            ApprovedByUserId = currentUserService.UserId,
            ApprovedAtUtc = now,
        };

        string auditOldJson = JsonSerializer.Serialize(new
        {
            ParentPropertyId = parent.Id,
            WardNo = parent.WardNo,
            PropertyNo = originalParentNo,
            TotalAreaSqFt = parentTotalSqFt,
            AreaSqM = parentAreaSqM,
            PendingDues = parentPendingDues,
            Owners = context.CurrentOwners,
        });

        string auditNewJson = JsonSerializer.Serialize(new
        {
            ArchivedParentPropertyNo = archivedParentNo,
            ResolutionNo = partitionMutation.ResolutionNo,
            ResolutionDate = partitionMutation.ResolutionDate,
            AreaValidation = new
            {
                validation.ParentAreaSqm,
                validation.TotalChildAreaSqm,
                validation.AreaDeltaSqm,
                validation.AllowedToleranceSqm,
            },
            Children = childResults,
        });

        await partitionRepository.ExecutePartitionAtomicallyAsync(
            parent,
            archivedParentNo,
            childProperties,
            allChildOwners,
            allChildConstructions,
            allChildTaxCalcs,
            allChildDemands,
            partitionMutation,
            auditOldJson,
            auditNewJson,
            cancellationToken).ConfigureAwait(false);

        return new ExecutePartitionResultDto(
            ParentPropertyId: parent.Id,
            OriginalParentPropertyNo: originalParentNo,
            ArchivedParentPropertyNo: archivedParentNo,
            PartitionMutationId: partitionMutation.Id,
            ApplicationNo: partitionMutation.ApplicationNo,
            ParentAreaSqM: validation.ParentAreaSqm,
            TotalChildAreaSqM: validation.TotalChildAreaSqm,
            AreaDeltaSqM: validation.AreaDeltaSqm,
            AllowedToleranceSqM: validation.AllowedToleranceSqm,
            ChildProperties: childResults);
    }

    private static (IReadOnlyList<PropertyFloorRequestItem>? Floors, decimal BuiltUpSqFt) ResolveChildFloors(
        Property parent,
        IReadOnlyList<PropertyConstruction> parentFloors,
        PartitionChildRequestItem childReq,
        int fallbackYear)
    {
        if (childReq.Floors is { Count: > 0 })
        {
            decimal sumSqFt = 0m;
            foreach (PropertyFloorRequestItem floor in childReq.Floors)
            {
                decimal fArea = floor.LengthFt > 0m && floor.WidthFt > 0m
                    ? floor.LengthFt * floor.WidthFt
                    : (floor.AreaSqFt ?? 0m);
                sumSqFt += fArea;
            }

            return (childReq.Floors, Math.Round(sumSqFt, 4, MidpointRounding.AwayFromZero));
        }

        decimal targetBuiltUp = Math.Round(childReq.BuiltUpAreaSqFt, 4, MidpointRounding.AwayFromZero);
        if (targetBuiltUp <= 0m)
        {
            return (null, 0m);
        }

        if (parentFloors.Count > 0)
        {
            decimal parentBuiltUp = parentFloors.Sum(f => f.AreaSqFt);
            if (parentBuiltUp > 0m)
            {
                decimal ratio = targetBuiltUp / parentBuiltUp;
                List<PropertyFloorRequestItem> scaled = [];
                decimal allocatedSoFar = 0m;

                for (int idx = 0; idx < parentFloors.Count; idx++)
                {
                    PropertyConstruction src = parentFloors[idx];
                    decimal partArea = idx == parentFloors.Count - 1
                        ? Math.Round(targetBuiltUp - allocatedSoFar, 4, MidpointRounding.AwayFromZero)
                        : Math.Round(src.AreaSqFt * ratio, 4, MidpointRounding.AwayFromZero);

                    if (partArea <= 0m)
                    {
                        partArea = 0.01m;
                    }

                    allocatedSoFar += partArea;
                    scaled.Add(new PropertyFloorRequestItem(
                        FloorNo: src.FloorNo,
                        FloorLabel: src.FloorLabel,
                        ConstructionTypeCode: src.ConstructionTypeCode,
                        UsageCode: src.UsageCode,
                        LengthFt: 0m,
                        WidthFt: 0m,
                        AreaSqFt: partArea,
                        ConstructionStartYear: src.ConstructionStartYear));
                }

                return (scaled, targetBuiltUp);
            }
        }

        List<PropertyFloorRequestItem> defaultFloor =
        [
            new PropertyFloorRequestItem(
                FloorNo: 0,
                FloorLabel: "तळमजला",
                ConstructionTypeCode: "RCC_PUCCA",
                UsageCode: "RESIDENTIAL",
                LengthFt: 0m,
                WidthFt: 0m,
                AreaSqFt: targetBuiltUp,
                ConstructionStartYear: Math.Clamp(fallbackYear - 1, 1900, 2100)),
        ];

        return (defaultFloor, targetBuiltUp);
    }

    private static string BuildArchivedPropertyNo(string originalPropertyNo, Guid parentId)
    {
        string suffix = $"#SPLIT-{parentId.ToString("N")[..8]}";
        int maxBaseLen = 40 - suffix.Length;
        string trimmed = originalPropertyNo.Trim();
        string basePart = trimmed.Length > maxBaseLen ? trimmed[..maxBaseLen] : trimmed;
        return $"{basePart}{suffix}";
    }

    private static List<string> NormalizeAssetKeys(IReadOnlyList<string>? keys)
    {
        if (keys is null || keys.Count == 0)
        {
            return [];
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        List<string> clean = [];
        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string trimmed = key.Trim();
            if (seen.Add(trimmed))
            {
                clean.Add(trimmed);
            }
        }

        return clean;
    }
}
