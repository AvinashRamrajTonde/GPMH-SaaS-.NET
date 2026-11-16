using System.Text.Json;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Onboarding;
using GPMH.Domain.Entities.Tenancy;
using MediatR;

namespace GPMH.Application.Features.Onboarding.Queries;

public sealed record GetOnboardingStatusQuery : IRequest<OnboardingStatusDto>;

public sealed class GetOnboardingStatusQueryHandler(
    IOnboardingRepository onboardingRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetOnboardingStatusQuery, OnboardingStatusDto>
{
    public async Task<OnboardingStatusDto> Handle(GetOnboardingStatusQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        Tenant tenant = await onboardingRepository.GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        GramPanchayat gp = await onboardingRepository.GetGramPanchayatAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? new GramPanchayat
            {
                TenantId = tenantId,
                LgdCode = tenant.LgdCode,
                DistrictCode = string.Empty,
                DistrictName = string.Empty,
                TehsilCode = string.Empty,
                TehsilName = string.Empty,
                PanchayatNameEn = tenant.NameEn,
                PanchayatNameMr = tenant.NameMr,
            };

        AssessmentYear? year = await onboardingRepository.GetActiveAssessmentYearAsync(tenantId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<BankAccount> banks = await onboardingRepository.GetBankAccountsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        bool formulaSeeded = await onboardingRepository.HasFormulaSettingsAsync(tenantId, cancellationToken).ConfigureAwait(false);

        LgdProvenanceDto? provenance = null;
        if (!string.IsNullOrWhiteSpace(gp.ProvenanceJson))
        {
            provenance = JsonSerializer.Deserialize<LgdProvenanceDto>(gp.ProvenanceJson);
        }

        return new OnboardingStatusDto(
            TenantId: tenant.Id,
            TenantStatus: tenant.Status.ToString(),
            SetupProgressPct: gp.SetupProgressPct,
            LgdCode: gp.LgdCode,
            DistrictCode: gp.DistrictCode,
            DistrictName: gp.DistrictName,
            TehsilCode: gp.TehsilCode,
            TehsilName: gp.TehsilName,
            PanchayatNameEn: gp.PanchayatNameEn,
            PanchayatNameMr: gp.PanchayatNameMr,
            Pincode: gp.Pincode,
            CensusCode: gp.CensusCode,
            OfficeAddress: gp.OfficeAddress,
            TotalWards: gp.TotalWards,
            PopulationCensus: gp.PopulationCensus,
            Committee: new ExecutiveCommitteeDto(
                gp.SarpanchName,
                gp.SarpanchMobile,
                gp.UpSarpanchName,
                gp.GramsevakName,
                gp.GramsevakMobile,
                gp.OperatorName,
                gp.OperatorMobile,
                gp.GramsevakSignatureKey,
                gp.SarpanchSignatureKey,
                gp.OperatorSignatureKey,
                gp.OfficialStampKey),
            AssessmentYear: new AssessmentYearSetupDto(
                year?.YearLabel ?? "2026-27",
                year?.StartYear ?? 2026,
                year?.EndYear ?? 2027,
                year?.IsLocked ?? false,
                gp.IsArrearsRolloverInitialized),
            BankAccounts: banks.Select(b => new BankAccountDto(
                b.FundType,
                b.BankName,
                b.BranchName,
                b.AccountNumber,
                b.IfscCode,
                b.UpiId,
                b.UpiPayeeName,
                b.QrImageKey,
                b.OpeningBalance)).ToList(),
            FormulaSeeded: formulaSeeded,
            Provenance: provenance);
    }
}

