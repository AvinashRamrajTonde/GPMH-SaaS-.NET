using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.Auth;
using GPMH.Application.Features.Agents.Commands;
using GPMH.Application.Features.Agents.Queries;
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

