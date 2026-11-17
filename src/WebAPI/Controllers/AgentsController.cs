using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.Auth;
using GPMH.Application.DTOs.Onboarding;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.Features.Agents.Commands;
using GPMH.Application.Features.Agents.Queries;
using GPMH.Application.Features.Onboarding.Commands;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Route("api/v1/agents")]
public sealed class AgentsController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(AgentRegistrationResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentRegistrationResultDto>> Register(
        [FromForm] RegisterAgentFormRequest form,
        CancellationToken cancellationToken)
    {
        KycFileUploadInput? aadhaarDoc = await ReadFormFileAsync(form.AadhaarDocument, cancellationToken).ConfigureAwait(false);
        KycFileUploadInput? panDoc = await ReadFormFileAsync(form.PanDocument, cancellationToken).ConfigureAwait(false);

        AgentRegistrationResultDto result = await sender.Send(
            new RegisterAgentCommand(
                form.FullName,
                form.MobileNo,
                form.Email,
                form.Password,
                form.Address,
                form.VillageOrCity,
                form.District,
                form.TalukasCsv,
                form.IdProofType,
                form.AadhaarNumber,
                form.PanNumber,
                form.BankName,
                form.BankBranch,
                form.AccountNumber,
                form.IfscCode,
                form.UpiId,
                form.RequestedCommissionRatePct,
                aadhaarDoc,
                panDoc),
            cancellationToken).ConfigureAwait(false);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] AgentLoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthTokenBundleDto bundle = await sender
            .Send(new AgentLoginCommand(request.Identifier, request.Password), cancellationToken)
            .ConfigureAwait(false);

        return AuthCookieHelper.BuildAuthResponse(this, bundle, AuthCookieHelper.IsMobileClient(Request, request.ClientType));
    }

    [AllowAnonymous]
    [HttpGet("validate-referral/{agentCode}")]
    [ProducesResponseType(typeof(ReferralValidationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReferralValidationDto>> ValidateReferral(
        string agentCode,
        CancellationToken cancellationToken)
    {
        ReferralValidationDto result = await sender
            .Send(new ValidateAgentReferralQuery(agentCode), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AgentProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AgentProfileDto>> GetMe(CancellationToken cancellationToken)
    {
        AgentProfileDto profile = await sender
            .Send(new GetCurrentAgentProfileQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(profile);
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AgentDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        AgentDashboardDto dashboard = await sender
            .Send(new GetAgentDashboardQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(dashboard);
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpGet("villages")]
    [ProducesResponseType(typeof(AgentVillagesResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentVillagesResponseDto>> GetVillages(CancellationToken cancellationToken)
    {
        AgentVillagesResponseDto villages = await sender
            .Send(new GetAgentVillagesQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(villages);
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpGet("commissions")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentCommissionItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgentCommissionItemDto>>> ListCommissions(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AgentCommissionItemDto> rows = await sender
            .Send(new GetAgentCommissionsQuery(status, search), cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows);
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpGet("payouts")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentPayoutItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgentPayoutItemDto>>> ListPayouts(CancellationToken cancellationToken)
    {
        IReadOnlyList<AgentPayoutItemDto> rows = await sender
            .Send(new GetAgentPayoutsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows);
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpPost("payouts/request")]
    [ProducesResponseType(typeof(AgentPayoutItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentPayoutItemDto>> RequestPayout(
        [FromBody] RequestAgentPayoutCommand command,
        CancellationToken cancellationToken)
    {
        AgentPayoutItemDto created = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(AgentProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentProfileDto>> UpdateProfile(
        [FromBody] UpdateAgentProfileCommand command,
        CancellationToken cancellationToken)
    {
        AgentProfileDto updated = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(updated);
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangeAgentPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpPost("register-gp")]
    [ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OnboardingStatusDto>> RegisterGp(
        [FromBody] RegisterAgentGpCommand command,
        CancellationToken cancellationToken)
    {
        OnboardingStatusDto created = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [Authorize(Policy = AuthPolicies.RequireFieldAgent)]
    [HttpGet("payouts/{id:guid}/voucher.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadPayoutVoucherPdf(Guid id, CancellationToken cancellationToken)
    {
        GeneratedPdfFileDto file = await sender
            .Send(new GetAgentPayoutVoucherPdfQuery(id, RestrictToCurrentAgent: true), cancellationToken)
            .ConfigureAwait(false);

        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }

    private static async Task<KycFileUploadInput?> ReadFormFileAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        using MemoryStream ms = new();
        await file.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return new KycFileUploadInput(file.FileName, file.ContentType, ms.ToArray());
    }
}

public sealed class RegisterAgentFormRequest
{
    public string FullName { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string VillageOrCity { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string TalukasCsv { get; set; } = string.Empty;
    public string IdProofType { get; set; } = "AADHAAR";
    public string AadhaarNumber { get; set; } = string.Empty;
    public string PanNumber { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? UpiId { get; set; }
    public decimal RequestedCommissionRatePct { get; set; } = 15m;
    public IFormFile? AadhaarDocument { get; set; }
    public IFormFile? PanDocument { get; set; }
}

public sealed record AgentLoginRequest(
    string Identifier,
    string Password,
    string? ClientType = null);

