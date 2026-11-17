using System.Globalization;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.SuperAdmin;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GPMH.Infrastructure.Printing;

/// <summary>
/// QuestPDF A4 Portrait GST Tax Invoice for approved Gram Panchayat platform subscription payments.
/// </summary>
public sealed class SubscriptionGstInvoicePdfDocument(
    GstSubscriptionInvoicePdfModel model,
    IQrCodeGeneratorService qrCodeGenerator) : IDocument
{
    private const string NavyHex = "#1e3a8a";
    private const string TealHex = "#0f766e";
    private const string SlateBorderHex = "#cbd5e1";

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Tax Invoice {model.InvoiceNo}",
        Author = model.VendorAccountName,
        Subject = $"GPMH SaaS Subscription GST Invoice - LGD {model.LgdCode}",
    };

    public void Compose(IDocumentContainer container)
    {
        decimal halfRate = Math.Round(model.GstPercent / 2m, 2, MidpointRounding.AwayFromZero);
        decimal halfTax = Math.Round(model.GstAmount / 2m, 2, MidpointRounding.AwayFromZero);
        decimal sgst = model.GstAmount - halfTax;

        string qrPayload = $"INV:{model.InvoiceNo}|GP:{model.LgdCode}|UTR:{model.UtrReference}|AMT:{model.TotalAmountPaid:0.00}";
        byte[] qrBytes = qrCodeGenerator.GeneratePng(qrPayload, pixelsPerModule: 4);

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(14, Unit.Millimetre);
            page.DefaultTextStyle(s => s
                .FontFamily(DevanagariFontRegistry.PrimaryFontFamily)
                .FontSize(9f)
                .FontColor("#0f172a"));

            page.Content().Column(col =>
            {
                col.Spacing(10);

                // Top Header Bar
                col.Item().Border(1.2f).BorderColor(NavyHex).Padding(12).Row(hdr =>
                {
                    hdr.RelativeItem().Column(left =>
                    {
                        left.Spacing(2);
                        left.Item().Text("TAX INVOICE / जीएसटी कर बीजक")
                            .FontSize(11f).ExtraBold().FontColor(NavyHex);
                        left.Item().Text(model.VendorAccountName)
                            .FontSize(14f).Bold().FontColor("#0f172a");
                        if (!string.IsNullOrWhiteSpace(model.VendorGstin))
                        {
                            left.Item().Text($"GSTIN: {model.VendorGstin}  |  SAC: 998314")
                                .FontSize(8.5f).SemiBold().FontColor("#334155");
                        }
                        left.Item().Text($"Helpline: {model.SupportPhone ?? "-"}  |  {model.SupportEmail ?? "-"}")
                            .FontSize(8f).FontColor("#475569");
                    });

                    hdr.ConstantItem(170).AlignRight().Column(right =>
                    {
                        right.Spacing(2);
                        right.Item().AlignRight().Text("INVOICE NO.")
                            .FontSize(7.5f).Bold().FontColor("#64748b");
                        right.Item().AlignRight().Text(model.InvoiceNo)
                            .FontSize(13f).ExtraBold().FontColor(TealHex);
                        right.Item().AlignRight().Text($"Issued: {model.IssuedAtUtc:dd/MM/yyyy}")
                            .FontSize(8.2f).FontColor("#334155");
                        right.Item().AlignRight().Text($"Payment Date: {model.PaymentDate:dd/MM/yyyy}")
                            .FontSize(8.2f).FontColor("#334155");
                    });
                });

                // Billed To + Subscription Window
                col.Item().Row(parties =>
                {
                    parties.Spacing(10);

                    parties.RelativeItem().Border(0.8f).BorderColor(SlateBorderHex).Background("#f8fafc").Padding(10).Column(billTo =>
                    {
                        billTo.Spacing(2.5f);
                        billTo.Item().Text("BILLED TO (GRAM PANCHAYAT)").FontSize(7.5f).Bold().FontColor(NavyHex);
                        billTo.Item().Text($"{model.PanchayatNameMr} ({model.PanchayatNameEn})").FontSize(10.5f).Bold();
                        billTo.Item().Text($"LGD Code: {model.LgdCode}").FontSize(8.5f).SemiBold().FontColor(TealHex);
                        billTo.Item().Text($"Taluka: {model.TehsilName}  |  District: {model.DistrictName}").FontSize(8.3f).FontColor("#334155");
                        if (!string.IsNullOrWhiteSpace(model.OfficeAddress) || !string.IsNullOrWhiteSpace(model.Pincode))
                        {
                            billTo.Item().Text($"{model.OfficeAddress ?? string.Empty} {model.Pincode ?? string.Empty}".Trim())
                                .FontSize(8f).FontColor("#475569");
                        }
                    });

                    parties.RelativeItem().Border(0.8f).BorderColor(SlateBorderHex).Background("#f0fdfa").Padding(10).Column(sub =>
                    {
                        sub.Spacing(2.5f);
                        sub.Item().Text("SUBSCRIPTION & PAYMENT META").FontSize(7.5f).Bold().FontColor(TealHex);
                        sub.Item().Text($"Plan: {model.PlanName} ({model.DurationDays} Days)").FontSize(9.5f).Bold();
                        sub.Item().Text($"Valid Until: {(model.ValidUntilUtc.HasValue ? model.ValidUntilUtc.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "-")}")
                            .FontSize(8.5f).SemiBold().FontColor("#065f46");
                        sub.Item().Text($"Mode: {model.PaymentMode}  |  UTR: {model.UtrReference}")
                            .FontSize(8.3f).FontColor("#0f172a");
                        sub.Item().Text($"Beneficiary Bank: {model.VendorBankName} (IFSC: {model.VendorIfscCode})")
                            .FontSize(7.8f).FontColor("#475569");
                    });
                });

                // Line-Item Tax Breakdown Table
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(32);
                        cols.RelativeColumn(3.2f);
                        cols.RelativeColumn(1.0f);
                        cols.RelativeColumn(1.1f);
                        cols.RelativeColumn(1.1f);
                        cols.RelativeColumn(1.2f);
                    });

                    static IContainer ThCell(IContainer c) =>
                        c.Border(0.6f).BorderColor(NavyHex).Background("#eff6ff").PaddingVertical(5).PaddingHorizontal(6);

                    static IContainer TdCell(IContainer c) =>
                        c.Border(0.5f).BorderColor(SlateBorderHex).PaddingVertical(6).PaddingHorizontal(6);

                    table.Header(th =>
                    {
                        th.Cell().Element(ThCell).AlignCenter().Text("#").Bold();
                        th.Cell().Element(ThCell).AlignLeft().Text("Description of Service").Bold();
                        th.Cell().Element(ThCell).AlignCenter().Text("SAC").Bold();
                        th.Cell().Element(ThCell).AlignRight().Text("Taxable (₹)").Bold();
                        th.Cell().Element(ThCell).AlignRight().Text("GST (₹)").Bold();
                        th.Cell().Element(ThCell).AlignRight().Text("Total (₹)").Bold();
                    });

                    table.Cell().Element(TdCell).AlignCenter().Text("1");
                    table.Cell().Element(TdCell).AlignLeft().Text(
                        $"GPMH SaaS e-GramPanchayat Annual Platform License ({model.PlanName} · {model.DurationDays} Days)\n" +
                        $"CGST @ {halfRate:0.##}%: ₹{halfTax:0.00}  |  SGST @ {halfRate:0.##}%: ₹{sgst:0.00}");
                    table.Cell().Element(TdCell).AlignCenter().Text("998314");
                    table.Cell().Element(TdCell).AlignRight().Text(model.TaxableBaseAmount.ToString("N2", CultureInfo.InvariantCulture));
                    table.Cell().Element(TdCell).AlignRight().Text(model.GstAmount.ToString("N2", CultureInfo.InvariantCulture));
                    table.Cell().Element(TdCell).AlignRight().Text($"₹{model.TotalAmountPaid.ToString("N2", CultureInfo.InvariantCulture)}").Bold();

                    IContainer TotCell(IContainer c) =>
                        c.Border(0.8f).BorderColor(TealHex).Background("#f0fdfa").PaddingVertical(6).PaddingHorizontal(6);

                    table.Cell().ColumnSpan(3).Element(TotCell).AlignRight().Text("Grand Total Paid (GST Inclusive):").Bold();
                    table.Cell().Element(TotCell).AlignRight().Text(model.TaxableBaseAmount.ToString("N2", CultureInfo.InvariantCulture)).Bold();
                    table.Cell().Element(TotCell).AlignRight().Text(model.GstAmount.ToString("N2", CultureInfo.InvariantCulture)).Bold();
                    table.Cell().Element(TotCell).AlignRight().Text($"₹{model.TotalAmountPaid.ToString("N2", CultureInfo.InvariantCulture)}")
                        .FontSize(10.5f).ExtraBold().FontColor(TealHex);
                });

                // Amount in words strip
                col.Item().Border(0.7f).BorderColor(SlateBorderHex).Background("#f8fafc").Padding(9).Row(w =>
                {
                    w.RelativeItem().Text(t =>
                    {
                        t.Span("अक्षरी एकूण रक्कम: ").Bold().FontColor("#475569");
                        t.Span(model.AmountInWordsMr).Bold().FontColor("#0f172a");
                    });
                    w.AutoItem().Text("STATUS: PAID & VERIFIED").Bold().FontColor("#047857");
                });

                // QR + Signatory Footer
                col.Item().PaddingTop(14).BorderTop(0.7f).BorderColor(SlateBorderHex).PaddingTop(10).Row(ft =>
                {
                    ft.AutoItem().Row(qr =>
                    {
                        qr.AutoItem().Border(0.6f).BorderColor(SlateBorderHex).Padding(3).Width(56).Height(56)
                            .Image(qrBytes).FitArea();
                        qr.AutoItem().PaddingLeft(8).AlignMiddle().Column(txt =>
                        {
                            txt.Item().Text("Verified Platform Settlement").Bold().FontColor(NavyHex);
                            txt.Item().Text($"Invoice: {model.InvoiceNo}").FontSize(8f).FontColor("#334155");
                            txt.Item().Text($"Platform UPI: {model.VendorUpiId}").FontSize(7.8f).FontColor("#64748b");
                        });
                    });

                    ft.RelativeItem().AlignRight().AlignBottom().Column(sig =>
                    {
                        sig.Item().AlignRight().PaddingBottom(16).Text($"For {model.VendorAccountName}")
                            .FontSize(8.5f).Bold().FontColor("#334155");
                        sig.Item().AlignRight().BorderTop(0.6f).BorderColor("#475569").PaddingTop(3)
                            .Text("Authorized Billing Signatory (System Generated)").FontSize(8f).FontColor("#475569");
                    });
                });
            });
        });
    }
}

