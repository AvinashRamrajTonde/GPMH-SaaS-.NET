using System.Globalization;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Printing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GPMH.Infrastructure.Printing;

public sealed class DigitalNameplatePdfDocument(
    IReadOnlyList<DigitalNameplateItemData> plates,
    string layoutMode,
    IQrCodeGeneratorService qrCodeGenerator)
    : IDocument
{
    public DocumentMetadata GetMetadata() => new()
    {
        Title = "Digital Property Nameplate & QR Sticker Sheet",
        Author = "GPMH e-GramPanchayat SaaS",
    };

    public void Compose(IDocumentContainer container)
    {
        DevanagariFontRegistry.EnsureInitialized();
        bool singleMode = string.Equals(layoutMode, "single", StringComparison.OrdinalIgnoreCase);

        if (singleMode)
        {
            foreach (DigitalNameplateItemData plate in plates)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(10, Unit.Millimetre);
                    page.PageColor("#f8fafc");
                    page.DefaultTextStyle(style => style
                        .FontFamily(DevanagariFontRegistry.PrimaryFontFamily, DevanagariFontRegistry.FallbackFontFamily)
                        .FontSize(9f)
                        .FontColor("#0f172a"));

                    page.Content().AlignMiddle().Element(c => RenderLargeSinglePlate(c, plate));
                });
            }
        }
        else
        {
            const int perPage = 6; // 3 cols x 2 rows per A4 Landscape
            for (int offset = 0; offset < plates.Count; offset += perPage)
            {
                List<DigitalNameplateItemData> chunk = [.. plates.Skip(offset).Take(perPage)];
                int pageIndex = (offset / perPage) + 1;
                int totalPages = (int)Math.Ceiling(plates.Count / (double)perPage);

                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(7, Unit.Millimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(style => style
                        .FontFamily(DevanagariFontRegistry.PrimaryFontFamily, DevanagariFontRegistry.FallbackFontFamily)
                        .FontSize(7.5f)
                        .FontColor("#0f172a"));

                    page.Header().PaddingBottom(4).Row(h =>
                    {
                        h.RelativeItem().Text(
                            $"नमुना ८ — डिजिटल मालमत्ता QR नामफलक शीट (6-in-1 A4) • ग्रा.पं. {chunk[0].PanchayatNameMr} (LGD {chunk[0].LgdCode})")
                            .FontSize(8.5f).Bold().FontColor("#0c3a36");
                        h.AutoItem().Text($"पृष्ठ {pageIndex} / {totalPages}").FontSize(7.5f).FontColor("#64748b");
                    });

                    page.Content().Table(grid =>
                    {
                        grid.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });

                        for (int idx = 0; idx < chunk.Count; idx++)
                        {
                            DigitalNameplateItemData plate = chunk[idx];
                            grid.Cell().Padding(3).Element(c => RenderCompactStickerCard(c, plate));
                        }
                    });
                });
            }
        }
    }

    private void RenderLargeSinglePlate(IContainer container, DigitalNameplateItemData plate)
    {
        container.Border(3f).BorderColor("#0c3a36").Background("#ffffff").Padding(10).Column(outer =>
        {
            outer.Spacing(8);

            // Header Banner
            outer.Item().Background("#0c3a36").Padding(10).Column(hdr =>
            {
                hdr.Item().AlignCenter().Text("महाराष्ट्र शासन — ग्रामविकास विभाग")
                    .FontSize(9f).FontColor("#a7f3d0");
                hdr.Item().AlignCenter().Text($"ग्रामपंचायत {plate.PanchayatNameMr} ({plate.PanchayatNameEn})")
                    .FontSize(17f).Bold().FontColor(Colors.White);
                hdr.Item().AlignCenter().Text(
                    $"ता. {plate.TehsilName}, जि. {plate.DistrictName} • LGD: {plate.LgdCode} {(string.IsNullOrWhiteSpace(plate.Pincode) ? "" : $"• पिन: {plate.Pincode}")}")
                    .FontSize(9f).FontColor("#e2e8f0");
                hdr.Item().PaddingTop(2).AlignCenter().Text("महाराष्ट्र ग्रामपंचायत अधिनियम (नमुना ८ — अधिकृत डिजिटल मालमत्ता नामफलक)")
                    .FontSize(8.5f).Bold().FontColor("#fde68a");
            });

            // Middle Row: QR Panel + Details
            outer.Item().Row(mid =>
            {
                mid.Spacing(14);

                mid.ConstantItem(200).Border(1f).BorderColor("#cbd5e1").Background("#f8fafc").Padding(8).Column(qrCol =>
                {
                    qrCol.Spacing(4);
                    qrCol.Item().AlignCenter().Width(105).Height(105)
                        .Image(qrCodeGenerator.GeneratePng(plate.VerifyUrl, 6))
                        .FitArea();
                    qrCol.Item().AlignCenter().Text("पडताळणी QR (Scan Verify)").FontSize(8f).Bold().FontColor("#0f6b61");

                    if (!string.IsNullOrWhiteSpace(plate.UpiPayUri))
                    {
                        qrCol.Item().PaddingTop(4).AlignCenter().Width(80).Height(80)
                            .Image(qrCodeGenerator.GeneratePng(plate.UpiPayUri, 5))
                            .FitArea();
                        qrCol.Item().AlignCenter().Text($"UPI कर भरणा: {plate.UpiId}").FontSize(7f).Bold().FontColor("#1d4ed8");
                    }

                    qrCol.Item().PaddingTop(2).AlignCenter().Text($"UID: {plate.UniqueCode}").FontSize(7f).FontColor("#475569");
                });

                mid.RelativeItem().Column(info =>
                {
                    info.Spacing(8);

                    info.Item().Row(topIds =>
                    {
                        topIds.Spacing(8);
                        topIds.RelativeItem().Border(1f).BorderColor("#0f6b61").Background("#ecfdf5").Padding(8).Column(idBox =>
                        {
                            idBox.Item().Text("मालमत्ता क्रमांक (Property No)").FontSize(8f).FontColor("#0f6b61");
                            idBox.Item().Text(plate.PropertyNo).FontSize(20f).Bold().FontColor("#0c3a36");
                        });
                        topIds.RelativeItem().Border(1f).BorderColor("#cbd5e1").Background("#f8fafc").Padding(8).Column(wardBox =>
                        {
                            wardBox.Item().Text("प्रभाग / वॉर्ड क्रमांक").FontSize(8f).FontColor("#475569");
                            wardBox.Item().Text($"वॉर्ड क्र. {plate.WardNo}").FontSize(18f).Bold().FontColor("#0f172a");
                        });
                    });

                    info.Item().Border(1f).BorderColor("#cbd5e1").Padding(8).Column(ownerBox =>
                    {
                        ownerBox.Item().Text("मिळकतधारकाचे नाव (मालक)").FontSize(8f).FontColor("#475569");
                        ownerBox.Item().Text(plate.OwnerNameMr).FontSize(15f).Bold().FontColor("#0f172a");
                        ownerBox.Item().Text(plate.OwnerNameEn).FontSize(9f).FontColor("#475569");
                        if (!string.IsNullOrWhiteSpace(plate.CoOwnerNamesMr))
                        {
                            ownerBox.Item().PaddingTop(2).Text($"सहमालक: {plate.CoOwnerNamesMr}").FontSize(8.5f).FontColor("#334155");
                        }
                    });

                    info.Item().Row(meta =>
                    {
                        meta.Spacing(8);
                        meta.RelativeItem().Border(1f).BorderColor("#cbd5e1").Padding(7).Column(m1 =>
                        {
                            m1.Item().Text("भोगवटादार / वापर").FontSize(7.5f).FontColor("#64748b");
                            m1.Item().Text(plate.OccupierLabelMr).FontSize(9.5f).Bold();
                            m1.Item().Text(plate.ConstructionAndUsageMr).FontSize(8.5f);
                        });
                        meta.RelativeItem().Border(1f).BorderColor("#cbd5e1").Padding(7).Column(m2 =>
                        {
                            m2.Item().Text("एकूण क्षेत्रफळ व वार्षिक कर").FontSize(7.5f).FontColor("#64748b");
                            m2.Item().Text($"{plate.TotalAreaSqFt:0.00} चौ.फू. ({plate.TotalAreaSqM:0.00} चौ.मी.)").FontSize(9.5f).Bold();
                            m2.Item().Text(plate.IsTaxExempt
                                ? "कर माफी लागू (₹ ०)"
                                : $"वार्षिक कर: ₹ {Math.Round(plate.TotalAnnualTax, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture)}")
                                .FontSize(9.5f).Bold().FontColor("#0f6b61");
                        });
                    });
                });
            });

            outer.Item().BorderTop(1f).BorderColor("#cbd5e1").PaddingTop(5).Row(ftr =>
            {
                ftr.RelativeItem().Text("वेळेवर कर भरा — गावाच्या सर्वांगीण विकासात मोलाचे सहकार्य करा!").FontSize(8.5f).Bold().FontColor("#0f6b61");
                ftr.AutoItem().Text($"सरपंच / ग्रामसेवक • ग्रामपंचायत {plate.PanchayatNameMr}").FontSize(8f).FontColor("#475569");
            });
        });
    }

    private void RenderCompactStickerCard(IContainer container, DigitalNameplateItemData plate)
    {
        container.Height(235).Border(1.5f).BorderColor("#0c3a36").Background(Colors.White).Column(card =>
        {
            card.Item().Background("#0c3a36").PaddingVertical(4).PaddingHorizontal(6).Row(top =>
            {
                top.RelativeItem().Text($"ग्रा.पं. {plate.PanchayatNameMr}").FontSize(8.5f).Bold().FontColor(Colors.White);
                top.AutoItem().Text("नमुना ८ QR पाटी").FontSize(7f).Bold().FontColor("#fde68a");
            });

            card.Item().Padding(6).Column(body =>
            {
                body.Spacing(4);

                body.Item().Row(mainRow =>
                {
                    mainRow.Spacing(6);

                    mainRow.ConstantItem(82).Column(qrStack =>
                    {
                        qrStack.Spacing(2);
                        qrStack.Item().AlignCenter().Width(74).Height(74)
                            .Image(qrCodeGenerator.GeneratePng(plate.VerifyUrl, 4))
                            .FitArea();
                        qrStack.Item().AlignCenter().Text("पडताळणी QR").FontSize(5.8f).Bold().FontColor("#0f6b61");

                        if (!string.IsNullOrWhiteSpace(plate.UpiPayUri))
                        {
                            qrStack.Item().PaddingTop(2).AlignCenter().Width(58).Height(58)
                                .Image(qrCodeGenerator.GeneratePng(plate.UpiPayUri, 3))
                                .FitArea();
                            qrStack.Item().AlignCenter().Text("UPI कर भरणा").FontSize(5.5f).Bold().FontColor("#1d4ed8");
                        }
                    });

                    mainRow.RelativeItem().Column(det =>
                    {
                        det.Spacing(3);
                        det.Item().Background("#ecfdf5").Border(0.6f).BorderColor("#0f6b61").Padding(3)
                            .Text($"प्रभाग {plate.WardNo}  •  घर क्र. {plate.PropertyNo}")
                            .FontSize(8.5f).Bold().FontColor("#0c3a36");
                        det.Item().Text(plate.OwnerNameMr).FontSize(9f).Bold();
                        det.Item().Text(plate.OwnerNameEn).FontSize(7f).FontColor("#475569");
                        if (!string.IsNullOrWhiteSpace(plate.CoOwnerNamesMr))
                        {
                            det.Item().Text($"सहमालक: {plate.CoOwnerNamesMr}").FontSize(6.4f).FontColor("#475569");
                        }
                        det.Item().Text(plate.ConstructionAndUsageMr).FontSize(6.5f);
                        det.Item().Text($"क्षेत्र: {plate.TotalAreaSqFt:0.##} चौ.फू. ({plate.TotalAreaSqM:0.##} चौ.मी.)").FontSize(6.5f);
                        det.Item().Text(plate.IsTaxExempt
                            ? "कर माफ (₹ ०)"
                            : $"वार्षिक कर: ₹ {Math.Round(plate.TotalAnnualTax, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture)}")
                            .FontSize(7.5f).Bold().FontColor("#0f6b61");
                    });
                });
            });

            card.Item().ExtendVertical().AlignBottom().Background("#f1f5f9").BorderTop(0.5f).BorderColor("#cbd5e1")
                .PaddingVertical(3).PaddingHorizontal(6).Row(f =>
                {
                    f.RelativeItem().Text($"UID: {plate.UniqueCode}").FontSize(5.5f).FontColor("#475569");
                    f.AutoItem().Text("वेळेवर कर भरा!").FontSize(6f).Bold().FontColor("#0f6b61");
                });
        });
    }
}
