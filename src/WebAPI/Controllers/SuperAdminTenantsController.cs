using GPMH.Application.DTOs.Printing;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Application.Features.SuperAdmin.Commands;
using GPMH.Application.Features.SuperAdmin.Queries;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/superadmin")]
public sealed class SuperAdminTenantsController(ISender sender) : ControllerBase
{
    [HttpGet("stats")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [ProducesResponseType(typeof(SuperAdminDashboardStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SuperAdminDashboardStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        SuperAdminDashboardStatsDto stats = await sender
            .Send(new GetSuperAdminStatsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(stats);
    }

    [HttpGet("tenants")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminTenantSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SuperAdminTenantSummaryDto>>> ListTenants(
        [FromQuery] string? search = null,
        [FromQuery] string? district = null,
        [FromQuery] string? taluka = null,
        [FromQuery] string? status = null,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SuperAdminTenantSummaryDto> tenants = await sender
            .Send(new GetSuperAdminTenantsQuery(search, district, taluka, status, includeDeleted), cancellationToken)
            .ConfigureAwait(false);

        return Ok(tenants);
    }

    [HttpGet("tenants/{id:guid}")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [ProducesResponseType(typeof(SuperAdminTenantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SuperAdminTenantDetailDto>> GetTenantDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        SuperAdminTenantDetailDto detail = await sender
            .Send(new GetSuperAdminTenantDetailQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return Ok(detail);
    }

    [HttpPost("tenants/provision")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(ProvisionedTenantResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProvisionedTenantResultDto>> ProvisionTenantDirect(
        [FromBody] ProvisionTenantDirectCommand command,
        CancellationToken cancellationToken)
    {
        ProvisionedTenantResultDto provisioned = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, provisioned);
    }

    [HttpPost("tenants/{id:guid}/status")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(SuperAdminTenantSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SuperAdminTenantSummaryDto>> UpdateTenantStatus(
        Guid id,
        [FromBody] UpdateTenantStatusRequest request,
        CancellationToken cancellationToken)
    {
        SuperAdminTenantSummaryDto updated = await sender
            .Send(new UpdateTenantStatusCommand(id, request.Action, request.TrialDays, request.DurationDays, request.Reason), cancellationToken)
            .ConfigureAwait(false);

        return Ok(updated);
    }

    [HttpPost("tenants/{id:guid}/reset-admin-password")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(ResetTenantAdminPasswordResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResetTenantAdminPasswordResultDto>> ResetAdminPassword(
        Guid id,
        [FromBody] ResetTenantAdminPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ResetTenantAdminPasswordResultDto result = await sender
            .Send(new ResetTenantAdminPasswordCommand(id, request.NewPassword, request.TargetUsername), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpDelete("tenants/{id:guid}")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SoftDeleteTenant(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new SoftDeleteTenantCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("tenants/{id:guid}/restore")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(SuperAdminTenantSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SuperAdminTenantSummaryDto>> RestoreTenant(
        Guid id,
        CancellationToken cancellationToken)
    {
        SuperAdminTenantSummaryDto restored = await sender
            .Send(new RestoreTenantCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return Ok(restored);
    }

    [HttpDelete("tenants/{id:guid}/permanent")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PermanentDeleteTenant(
        Guid id,
        [FromQuery] string? confirmLgdCode = null,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PermanentDeleteTenantRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        string code = !string.IsNullOrWhiteSpace(request?.ConfirmLgdCode)
            ? request.ConfirmLgdCode
            : confirmLgdCode ?? string.Empty;

        await sender.Send(new PermanentDeleteTenantCommand(id, code), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("tenants/{id:guid}/impersonate")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [ProducesResponseType(typeof(SupportImpersonationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupportImpersonationResultDto>> StartImpersonation(
        Guid id,
        [FromBody] StartImpersonationRequest request,
        CancellationToken cancellationToken)
    {
        SupportImpersonationResultDto result = await sender
            .Send(new SupportImpersonationCommand(id, request.Reason), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost("impersonate/exit")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportExit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExitImpersonation(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ExitImpersonationRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        await sender
            .Send(new ExitSupportImpersonationCommand(request?.TargetTenantId), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    [HttpGet("payments")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantPaymentSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantPaymentSubmissionDto>>> ListPayments(
        [FromQuery] string? status = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TenantPaymentSubmissionDto> items = await sender
            .Send(new GetSuperAdminPaymentsQuery(status, tenantId, search), cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpPost("payments/{id:guid}/approve")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(TenantPaymentSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantPaymentSubmissionDto>> ApprovePayment(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApproveTenantPaymentRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        TenantPaymentSubmissionDto result = await sender
            .Send(new ApproveTenantPaymentCommand(id, request?.DurationDays ?? 365, request?.Notes), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost("payments/{id:guid}/reject")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(TenantPaymentSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantPaymentSubmissionDto>> RejectPayment(
        Guid id,
        [FromBody] RejectTenantPaymentRequest request,
        CancellationToken cancellationToken)
    {
        TenantPaymentSubmissionDto result = await sender
            .Send(new RejectTenantPaymentCommand(id, request.Reason), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("payments/{id:guid}/invoice.pdf")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPaymentInvoicePdf(Guid id, CancellationToken cancellationToken)
    {
        GeneratedPdfFileDto file = await sender
            .Send(new GetSuperAdminPaymentInvoicePdfQuery(id), cancellationToken)
            .ConfigureAwait(false);

        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }

    [HttpGet("audit-logs")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminAuditLogItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SuperAdminAuditLogItemDto>>> ListAuditLogs(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityName = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SuperAdminAuditLogItemDto> items = await sender
            .Send(new GetSuperAdminAuditLogsQuery(tenantId, action, entityName, fromDate, toDate, search, limit), cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpGet("audit-logs/export.csv")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAuditLogsCsv(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityName = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        GeneratedPdfFileDto csv = await sender
            .Send(new ExportSuperAdminAuditLogsCsvQuery(tenantId, action, entityName, fromDate, toDate, search), cancellationToken)
            .ConfigureAwait(false);

        return File(csv.Content, "text/csv; charset=utf-8", csv.FileName);
    }

    [HttpGet("platform-settings")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdminOrSupportRead)]
    [ProducesResponseType(typeof(PlatformPaymentAccountDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformPaymentAccountDto>> GetPlatformSettings(CancellationToken cancellationToken)
    {
        PlatformPaymentAccountDto settings = await sender
            .Send(new GetPlatformPaymentAccountQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(settings);
    }

    [HttpPut("platform-settings")]
    [Authorize(Policy = AuthPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(PlatformPaymentAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlatformPaymentAccountDto>> UpdatePlatformSettings(
        [FromBody] UpdatePlatformPaymentAccountCommand command,
        CancellationToken cancellationToken)
    {
        PlatformPaymentAccountDto updated = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(updated);
    }
}

public sealed record UpdateTenantStatusRequest(
    string Action,
    int? TrialDays = null,
    int? DurationDays = null,
    string? Reason = null);

public sealed record ResetTenantAdminPasswordRequest(
    string NewPassword,
    string? TargetUsername = null);

public sealed record PermanentDeleteTenantRequest(string ConfirmLgdCode);

public sealed record StartImpersonationRequest(string Reason);

public sealed record ExitImpersonationRequest(Guid? TargetTenantId = null);

public sealed record ApproveTenantPaymentRequest(
    int DurationDays = 365,
    string? Notes = null);

public sealed record RejectTenantPaymentRequest(string Reason);
