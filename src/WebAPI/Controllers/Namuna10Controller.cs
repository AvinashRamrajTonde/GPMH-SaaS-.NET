using GPMH.Application.DTOs.Namuna10;
using GPMH.Application.Features.Namuna10.Commands;
using GPMH.Application.Features.Namuna10.Queries;
using GPMH.Domain.Enums;
using GPMH.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

public sealed record CollectTaxRequest(
    Guid PropertyId,
    Guid AssessmentYearId,
    Guid OperationId,
    decimal AmountPaid,
    PaymentMode PaymentMode,
    DateOnly? ReceiptDate = null,
    string? BookNo = null,
    string? PayerName = null,
    string? ReferenceNo = null,
    string? BankName = null,
    DateOnly? ChequeDate = null,
    string? Remarks = null,
    bool ApplyEligibleEarlyRebate = false);

[ApiController]
[Authorize(Policy = AuthPolicies.RequirePropertyRead)]
[Route("api/v1/namuna-10")]
public sealed class Namuna10Controller(ISender sender) : ControllerBase
{
    [HttpGet("register")]
    [ProducesResponseType(typeof(Namuna10RegisterWorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Namuna10RegisterWorkspaceDto>> GetRegister(
        [FromQuery] Guid? yearId = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        Namuna10RegisterWorkspaceDto result = await sender
            .Send(new GetNamuna10RegisterQuery(yearId, wardNo, search, status), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("receipts")]
    [ProducesResponseType(typeof(Namuna10ReceiptsPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Namuna10ReceiptsPageDto>> ListReceipts(
        [FromQuery] Guid? yearId = null,
        [FromQuery] Guid? propertyId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] PaymentMode? mode = null,
        [FromQuery] string? wardNo = null,
        [FromQuery] string? search = null,
        [FromQuery] bool includeCancelled = true,
        CancellationToken cancellationToken = default)
    {
        Namuna10ReceiptsPageDto result = await sender
            .Send(
                new GetNamuna10ReceiptsQuery(
                    yearId,
                    propertyId,
                    fromDate,
                    toDate,
                    mode,
                    wardNo,
                    search,
                    includeCancelled),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("receipts/{id:guid}")]
    [ProducesResponseType(typeof(TaxReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaxReceiptDto>> GetReceiptById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        TaxReceiptDto receipt = await sender
            .Send(new GetNamuna10ReceiptByIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return Ok(receipt);
    }

    [HttpPost("collections")]
    [Authorize(Policy = AuthPolicies.RequireGpStaff)]
    [ProducesResponseType(typeof(TaxReceiptDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TaxReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaxReceiptDto>> Collect(
        [FromBody] CollectTaxRequest request,
        CancellationToken cancellationToken)
    {
        TaxReceiptDto result = await sender.Send(
            new CollectTaxCommand(
                PropertyId: request.PropertyId,
                AssessmentYearId: request.AssessmentYearId,
                OperationId: request.OperationId,
                AmountPaid: request.AmountPaid,
                PaymentMode: request.PaymentMode,
                ReceiptDate: request.ReceiptDate,
                BookNo: request.BookNo,
                PayerName: request.PayerName,
                ReferenceNo: request.ReferenceNo,
                BankName: request.BankName,
                ChequeDate: request.ChequeDate,
                Remarks: request.Remarks,
                ApplyEligibleEarlyRebate: request.ApplyEligibleEarlyRebate),
            cancellationToken).ConfigureAwait(false);

        if (result.IdempotentReplay)
        {
            return Ok(result);
        }

        return CreatedAtAction(nameof(GetReceiptById), new { id = result.ReceiptId }, result);
    }
}

