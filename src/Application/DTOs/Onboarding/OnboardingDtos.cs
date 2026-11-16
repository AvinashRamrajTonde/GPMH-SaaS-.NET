namespace GPMH.Application.DTOs.Onboarding;

public sealed record LgdProvenanceDto(
    bool IsAutofilledFromLgd,
    bool IsEditedOnOnboarding,
    IReadOnlyList<string> EditedFields,
    string? OriginalLgdSnapshotJson);

public sealed record BankAccountDto(
    string FundType,
    string BankName,
    string BranchName,
    string AccountNumber,
    string IfscCode,
    string? UpiId,
    string? UpiPayeeName,
    string? QrImageKey,
    decimal OpeningBalance);

public sealed record ExecutiveCommitteeDto(
    string? SarpanchName,
    string? SarpanchMobile,
    string? UpSarpanchName,
    string? GramsevakName,
    string? GramsevakMobile,
    string? OperatorName,
    string? OperatorMobile,
    string? GramsevakSignatureKey,
    string? SarpanchSignatureKey,
    string? OperatorSignatureKey,
    string? OfficialStampKey);

public sealed record AssessmentYearSetupDto(
    string YearLabel,
    int StartYear,
    int EndYear,
    bool IsLocked,
    bool IsArrearsRolloverInitialized);

public sealed record OnboardingStatusDto(
    Guid TenantId,
    string TenantStatus,
    int SetupProgressPct,
    string LgdCode,
    string DistrictCode,
    string DistrictName,
    string TehsilCode,
    string TehsilName,
    string PanchayatNameEn,
    string PanchayatNameMr,
    string? Pincode,
    string? CensusCode,
    string? OfficeAddress,
    int TotalWards,
    int? PopulationCensus,
    ExecutiveCommitteeDto Committee,
    AssessmentYearSetupDto AssessmentYear,
    IReadOnlyList<BankAccountDto> BankAccounts,
    bool FormulaSeeded,
    LgdProvenanceDto? Provenance);

public sealed record UploadedAssetDto(
    string AssetKey,
    string Category,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record ProtectedAssetDownloadDto(
    string FileName,
    string ContentType,
    byte[] Content);

