using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.Printing;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Agents.Queries;

// 1. Public Referral Code Validation (Approved agents only, zero PII leaked)
public sealed record ValidateAgentReferralQuery(string AgentCode) : IRequest<ReferralValidationDto>;

public sealed class ValidateAgentReferralQueryHandler(IAgentRepository agentRepository)
    : IRequestHandler<ValidateAgentReferralQuery, ReferralValidationDto>
{
    public async Task<ReferralValidationDto> Handle(ValidateAgentReferralQuery request, CancellationToken cancellationToken)
    {
        string normalized = (request.AgentCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new ReferralValidationDto(false, string.Empty, null, null);
        }

        FieldAgent? agent = await agentRepository
            .FindByAgentCodeAsync(normalized, cancellationToken)
            .ConfigureAwait(false);

        if (agent is null || agent.KycStatus != AgentKycStatus.Approved)
        {
            return new ReferralValidationDto(false, normalized, null, null);
        }

        return new ReferralValidationDto(true, agent.AgentCode, agent.FullName, agent.District);
    }
}

// 2. Agent Self-Service Dashboard (8 KPIs)
public sealed record GetAgentDashboardQuery : IRequest<AgentDashboardDto>;

public sealed class GetAgentDashboardQueryHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetAgentDashboardQuery, AgentDashboardDto>
{
    public async Task<AgentDashboardDto> Handle(GetAgentDashboardQuery request, CancellationToken cancellationToken)
    {
        FieldAgent agent = await AgentQueryHelper
            .ResolveApprovedCurrentAgentAsync(agentRepository, currentUserService, cancellationToken)
            .ConfigureAwait(false);

        return await agentRepository.GetAgentDashboardAsync(agent.Id, cancellationToken).ConfigureAwait(false);
    }
}

// 3. Agent Onboarded Villages & Upcoming 90-Day Renewals
public sealed record GetAgentVillagesQuery : IRequest<AgentVillagesResponseDto>;

public sealed class GetAgentVillagesQueryHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetAgentVillagesQuery, AgentVillagesResponseDto>
{
    public async Task<AgentVillagesResponseDto> Handle(GetAgentVillagesQuery request, CancellationToken cancellationToken)
    {
        FieldAgent agent = await AgentQueryHelper
            .ResolveApprovedCurrentAgentAsync(agentRepository, currentUserService, cancellationToken)
            .ConfigureAwait(false);

        return await agentRepository.GetAgentVillagesAsync(agent.Id, cancellationToken).ConfigureAwait(false);
    }
}

// 4. Agent Commission Ledger
public sealed record GetAgentCommissionsQuery(string? Status = null, string? Search = null)
    : IRequest<IReadOnlyList<AgentCommissionItemDto>>;

public sealed class GetAgentCommissionsQueryHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetAgentCommissionsQuery, IReadOnlyList<AgentCommissionItemDto>>
{
    public async Task<IReadOnlyList<AgentCommissionItemDto>> Handle(
        GetAgentCommissionsQuery request,
        CancellationToken cancellationToken)
    {
        FieldAgent agent = await AgentQueryHelper
            .ResolveApprovedCurrentAgentAsync(agentRepository, currentUserService, cancellationToken)
            .ConfigureAwait(false);

        return await agentRepository
            .ListAgentCommissionsAsync(agent.Id, request.Status, request.Search, cancellationToken)
            .ConfigureAwait(false);
    }
}

// 5. Agent Payout History
public sealed record GetAgentPayoutsQuery : IRequest<IReadOnlyList<AgentPayoutItemDto>>;

public sealed class GetAgentPayoutsQueryHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetAgentPayoutsQuery, IReadOnlyList<AgentPayoutItemDto>>
{
    public async Task<IReadOnlyList<AgentPayoutItemDto>> Handle(
        GetAgentPayoutsQuery request,
        CancellationToken cancellationToken)
    {
        FieldAgent agent = await AgentQueryHelper
            .ResolveApprovedCurrentAgentAsync(agentRepository, currentUserService, cancellationToken)
            .ConfigureAwait(false);

        return await agentRepository.ListAgentPayoutsAsync(agent.Id, cancellationToken).ConfigureAwait(false);
    }
}

// 6. QuestPDF Payout Settlement Voucher Download
public sealed record GetAgentPayoutVoucherPdfQuery(
    Guid PayoutId,
    bool RestrictToCurrentAgent) : IRequest<GeneratedPdfFileDto>;

public sealed class GetAgentPayoutVoucherPdfQueryHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService,
    IStatutoryPdfRenderer pdfRenderer)
    : IRequestHandler<GetAgentPayoutVoucherPdfQuery, GeneratedPdfFileDto>
{
    public async Task<GeneratedPdfFileDto> Handle(
        GetAgentPayoutVoucherPdfQuery request,
        CancellationToken cancellationToken)
    {
        Guid? restrictToAgentId = null;
        if (request.RestrictToCurrentAgent)
        {
            FieldAgent agent = await AgentQueryHelper
                .ResolveApprovedCurrentAgentAsync(agentRepository, currentUserService, cancellationToken)
                .ConfigureAwait(false);
            restrictToAgentId = agent.Id;
        }

        AgentPayoutVoucherPdfModel model = await agentRepository
            .BuildPayoutVoucherModelAsync(request.PayoutId, restrictToAgentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(AgentPayout), request.PayoutId);

        byte[] pdfBytes = pdfRenderer.RenderAgentPayoutVoucherPdf(model);
        return new GeneratedPdfFileDto($"GPMH-Voucher-{model.VoucherNo}.pdf", pdfBytes);
    }
}

// 7. SuperAdmin Filtered Agent Directory
public sealed record GetSuperAdminAgentsFilteredQuery(
    string? District = null,
    string? Taluka = null,
    string? Status = null,
    string? Search = null) : IRequest<IReadOnlyList<SuperAdminAgentSummaryDto>>;

public sealed class GetSuperAdminAgentsFilteredQueryHandler(IAgentRepository agentRepository)
    : IRequestHandler<GetSuperAdminAgentsFilteredQuery, IReadOnlyList<SuperAdminAgentSummaryDto>>
{
    public Task<IReadOnlyList<SuperAdminAgentSummaryDto>> Handle(
        GetSuperAdminAgentsFilteredQuery request,
        CancellationToken cancellationToken) =>
        agentRepository.ListSuperAdminAgentDirectoryAsync(
            request.District,
            request.Taluka,
            request.Status,
            request.Search,
            cancellationToken);
}

// 8. SuperAdmin Settlement Hub
public sealed record GetSuperAdminAgentSettlementsQuery : IRequest<SuperAdminAgentSettlementsDto>;

public sealed class GetSuperAdminAgentSettlementsQueryHandler(IAgentRepository agentRepository)
    : IRequestHandler<GetSuperAdminAgentSettlementsQuery, SuperAdminAgentSettlementsDto>
{
    public Task<SuperAdminAgentSettlementsDto> Handle(
        GetSuperAdminAgentSettlementsQuery request,
        CancellationToken cancellationToken) =>
        agentRepository.GetSuperAdminSettlementsAsync(cancellationToken);
}

internal static class AgentQueryHelper
{
    public static async Task<FieldAgent> ResolveApprovedCurrentAgentAsync(
        IAgentRepository agentRepository,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        Guid userId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated Field Agent context is required.");

        FieldAgent agent = await agentRepository.FindByUserIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), userId);

        if (agent.KycStatus != AgentKycStatus.Approved)
        {
            throw new AgentStatusGatedException(
                agent.AgentCode,
                agent.KycStatus,
                agent.StatusReason,
                $"Agent account '{agent.AgentCode}' is '{agent.KycStatus}' and not approved.");
        }

        return agent;
    }
}

