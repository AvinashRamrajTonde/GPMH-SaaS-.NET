namespace GPMH.Application.DTOs.Mutations;

public sealed record MutationOwnerSnapshotItem(
    Guid CitizenId,
    string CitizenCode,
    string FullNameMr,
    string FullNameEn,
    string? AadhaarMasked,
    decimal OwnershipSharePct,
    bool IsPrimaryOwner);

public sealed record MutationSupportingAssetDto(
    string AssetKey,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record MutationListItemDto(
    Guid Id,
    Guid PropertyId,
    string WardNo,
    string PropertyNo,
    string ApplicationNo,
    string MutationType,
    string PreviousOwnersSummaryMr,
    string PreviousOwnersSummaryEn,
    string ProposedOwnersSummaryMr,
    string ProposedOwnersSummaryEn,
    string? DocumentReferenceNo,
    DateOnly? DocumentDate,
    string Status,
    DateTimeOffset NoticeIssuedAtUtc,
    DateTimeOffset NoticeEndsAtUtc,
    int DaysRemaining,
    bool IsNoticeWindowElapsed,
    bool HasObjection,
    string? ResolutionNo,
    DateOnly? ResolutionDate,
    DateTimeOffset CreatedAtUtc);

public sealed record PagedMutationsDto(
    IReadOnlyList<MutationListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record MutationDetailDto(
    Guid Id,
    Guid TenantId,
    Guid PropertyId,
    string WardNo,
    string PropertyNo,
    string? StreetOrLaneName,
    string? PropertyDescription,
    decimal TotalAreaSqFt,
    string ApplicationNo,
    string MutationType,
    IReadOnlyList<MutationOwnerSnapshotItem> PreviousOwners,
    IReadOnlyList<MutationOwnerSnapshotItem> ProposedOwners,
    string? DocumentReferenceNo,
    DateOnly? DocumentDate,
    string? Remarks,
    IReadOnlyList<MutationSupportingAssetDto> SupportingAssets,
    string Status,
    DateTimeOffset NoticeIssuedAtUtc,
    DateTimeOffset NoticeEndsAtUtc,
    int DaysRemaining,
    bool IsNoticeWindowElapsed,
    bool HasObjection,
    string? ObjectionSummary,
    DateTimeOffset? ObjectionRecordedAtUtc,
    string? ResolutionNo,
    DateOnly? ResolutionDate,
    string? RejectionReason,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record MutationRegisterPdfRow(
    string ApplicationNo,
    string PropertyNoDisplay,
    string EntryDateDisplay,
    string MutationTypeLabelMr,
    string? DocumentReferenceNo,
    string PropertyDescriptionMr,
    string PreviousOwnersMr,
    string ProposedOwnersMr,
    string BriefSummaryMr,
    string ResolutionDisplay);

public sealed record MutationRegisterPdfDocumentModel(
    Guid TenantId,
    string PanchayatNameMr,
    string VillageNameMr,
    string TehsilName,
    string DistrictName,
    string LgdCode,
    string AssessmentYearLabel,
    string? WardFilterLabel,
    IReadOnlyList<MutationRegisterPdfRow> Rows,
    byte[]? GramsevakSignatureBytes = null);

