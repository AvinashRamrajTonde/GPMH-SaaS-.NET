using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Mutations;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Application.DTOs.Namuna9Ka;
using GPMH.Application.DTOs.Namuna10;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.DTOs.SuperAdmin;
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

    public byte[] RenderMutationRegisterPdf(MutationRegisterPdfDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        DevanagariFontRegistry.EnsureInitialized();
        MutationRegisterPdfDocument document = new(model);
        return document.GeneratePdf();
    }

    public byte[] RenderNamuna9RegisterPdf(Namuna9RegisterPdfDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        DevanagariFontRegistry.EnsureInitialized();
        Namuna9RegisterPdfDocument document = new(model);
        return document.GeneratePdf();
    }

    public byte[] RenderNamuna9KaBillsPdf(Namuna9KaPdfDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        DevanagariFontRegistry.EnsureInitialized();
        Namuna9KaBillPdfDocument document = new(model, qrCodeGenerator);
        return document.GeneratePdf();
    }

    public byte[] RenderNamuna10DualReceiptPdf(Namuna10DualReceiptPdfModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        DevanagariFontRegistry.EnsureInitialized();
        Namuna10DualReceiptPdfDocument document = new(model, qrCodeGenerator);
        return document.GeneratePdf();
    }

    public byte[] RenderSubscriptionGstInvoicePdf(GstSubscriptionInvoicePdfModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        DevanagariFontRegistry.EnsureInitialized();
        SubscriptionGstInvoicePdfDocument document = new(model, qrCodeGenerator);
        return document.GeneratePdf();
    }
}

