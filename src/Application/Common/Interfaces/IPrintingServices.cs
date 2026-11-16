using GPMH.Application.DTOs.Mutations;
using GPMH.Application.DTOs.Printing;

namespace GPMH.Application.Common.Interfaces;

public interface IExtractVerificationTokenService
{
    string GenerateToken(ExtractVerifyPayloadDto payload);

    bool TryValidateToken(string token, out ExtractVerifyPayloadDto? payload);
}

public interface IQrCodeGeneratorService
{
    string GenerateSvg(string payload, int pixelsPerModule = 4);

    byte[] GeneratePng(string payload, int pixelsPerModule = 6);
}

public interface IStatutoryPdfRenderer
{
    byte[] RenderNamuna8ExtractPdf(IReadOnlyList<Namuna8ExtractPdfDocumentModel> extracts);

    byte[] RenderDigitalNameplatesPdf(IReadOnlyList<DigitalNameplateItemData> plates, string layoutMode = "bulk");

    byte[] RenderMutationRegisterPdf(MutationRegisterPdfDocumentModel model);
}

public interface IPrintDataRepository
{
    Task<IReadOnlyList<Namuna8ExtractPdfDocumentModel>> LoadNamuna8ExtractsAsync(
        Guid tenantId,
        Guid? propertyId,
        string? wardNo,
        Guid? assessmentYearId,
        IReadOnlyCollection<Guid>? propertyIds,
        string frontendBaseUrl,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DigitalNameplateItemData>> LoadNameplatesAsync(
        Guid tenantId,
        Guid? propertyId,
        string? wardNo,
        IReadOnlyCollection<Guid>? propertyIds,
        string frontendBaseUrl,
        CancellationToken cancellationToken);

    Task<VerifiedExtractPublicDto?> LoadVerifiedExtractPublicAsync(
        Guid tenantId,
        Guid propertyId,
        Guid assessmentYearId,
        string token,
        DateTimeOffset issuedAtUtc,
        CancellationToken cancellationToken);

    Task RecordPrintAuditAsync(
        Guid tenantId,
        string action,
        string entityId,
        string detailsJson,
        CancellationToken cancellationToken);
}

