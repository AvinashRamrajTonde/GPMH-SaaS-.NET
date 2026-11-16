using System.Text.RegularExpressions;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Citizens.Queries;

public sealed record GetCitizensQuery(
    string? Search = null,
    string? WardNo = null,
    string? ExemptionCategory = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedCitizensDto>;

public sealed record GetCitizenByIdQuery(Guid Id) : IRequest<CitizenDto>;

public sealed class CitizenQueriesHandler(
    ICitizenRepository citizenRepository,
    ITenantContext tenantContext,
    IPiiProtectionService piiService)
    : IRequestHandler<GetCitizensQuery, PagedCitizensDto>,
      IRequestHandler<GetCitizenByIdQuery, CitizenDto>
{
    public async Task<PagedCitizensDto> Handle(GetCitizensQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        int boundedPage = Math.Max(1, request.Page);
        int boundedPageSize = Math.Clamp(request.PageSize, 1, 100);

        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        string? blindIndex = null;
        if (search is not null)
        {
            string digitsOnly = Regex.Replace(search, @"[\s-]+", string.Empty);
            if (digitsOnly.Length == 12 && digitsOnly.All(char.IsAsciiDigit))
            {
                // ADR-015: Never query cleartext Aadhaar columns; match exclusively on HMAC-SHA256 blind index.
                blindIndex = piiService.ComputeBlindIndex(digitsOnly);
                search = null;
            }
        }

        return await citizenRepository
            .SearchAsync(
                tenantId,
                search,
                string.IsNullOrWhiteSpace(request.WardNo) ? null : request.WardNo.Trim(),
                string.IsNullOrWhiteSpace(request.ExemptionCategory) ? null : request.ExemptionCategory.Trim(),
                blindIndex,
                boundedPage,
                boundedPageSize,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CitizenDto> Handle(GetCitizenByIdQuery request, CancellationToken cancellationToken)
    {
        if (!tenantContext.CurrentTenantId.HasValue)
        {
            throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        }

        Citizen citizen = await citizenRepository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Citizen), request.Id);

        int linkedCount = await citizenRepository
            .GetLinkedPropertiesCountAsync(citizen.Id, cancellationToken)
            .ConfigureAwait(false);

        return new CitizenDto(
            citizen.Id,
            citizen.TenantId,
            citizen.CitizenCode,
            citizen.WardNo,
            citizen.FullNameMr,
            citizen.FullNameEn,
            citizen.Gender,
            citizen.MobileNo,
            string.IsNullOrWhiteSpace(citizen.AadhaarLast4) ? null : $"XXXX-XXXX-{citizen.AadhaarLast4}",
            citizen.SocialCategory,
            citizen.ExemptionCategory,
            citizen.IsActive,
            linkedCount,
            citizen.CreatedAtUtc,
            citizen.UpdatedAtUtc);
    }
}

