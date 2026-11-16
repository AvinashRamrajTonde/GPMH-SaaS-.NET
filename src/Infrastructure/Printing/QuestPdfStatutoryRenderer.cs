using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Printing;
using QuestPDF.Fluent;

namespace GPMH.Infrastructure.Printing;

public sealed class QuestPdfStatutoryRenderer(IQrCodeGeneratorService qrCodeGenerator)
    : IStatutoryPdfRenderer
{
    public byte[] RenderNamuna8ExtractPdf(IReadOnlyList<Namuna8ExtractPdfDocumentModel> extracts)
    {
        ArgumentNullException.ThrowIfNull(extracts);
        if (extracts.Count == 0)
        {
            throw new ArgumentException("At least one extract row is required to generate Namuna 8 PDF.", nameof(extracts));
        }

        DevanagariFontRegistry.EnsureInitialized();
        Namuna8ExtractPdfDocument document = new(extracts, qrCodeGenerator);
        return document.GeneratePdf();
    }

    public byte[] RenderDigitalNameplatesPdf(IReadOnlyList<DigitalNameplateItemData> plates, string layoutMode = "bulk")
    {
        ArgumentNullException.ThrowIfNull(plates);
        if (plates.Count == 0)
        {
            throw new ArgumentException("At least one nameplate is required to generate PDF.", nameof(plates));
        }

        DevanagariFontRegistry.EnsureInitialized();
        DigitalNameplatePdfDocument document = new(plates, layoutMode, qrCodeGenerator);
        return document.GeneratePdf();
    }
}

