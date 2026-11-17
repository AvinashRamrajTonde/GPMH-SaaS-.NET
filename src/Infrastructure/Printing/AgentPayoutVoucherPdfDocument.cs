using System.Globalization;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Agents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GPMH.Infrastructure.Printing;

/// <summary>
/// QuestPDF A4 Portrait Devanagari + English Settlement Voucher for disbursed Field Agent commission payouts.
/// </summary>
public sealed class AgentPayoutVoucherPdfDocument(
    AgentPayoutVoucherPdfModel model,
    IQrCodeGeneratorService qrCodeGenerator) : IDocument
{
    private const string TealHex = "#0f766e";
    private const string EmeraldDarkHex = "#065f46";
    private const string SlateBorderHex = "#cbd5e1";

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Payout Voucher {model.VoucherNo}",
        Author = model.PlatformAccountName,
        Subject = $"GPMH Field Agent Commission Settlement Voucher - {model.AgentCode}",
    };

    public void Compose(IDocumentContainer container)
    {
        string qrPayload = $"VCH:{model.VoucherNo}|AGT:{model.AgentCode}|UTR:{model.UtrReference}|AMT:{model.PayoutAmount:0.00}";
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

                // Top Banner
                col.Item().Border(1.2f).BorderColor(TealHex).Padding(12).Row(hdr =>
                {
                    hdr.RelativeItem().Column(left =>
                    {
                        left.Spacing(2);
                        left.Item().Text("एजंट कमिशन पेमेंट व्हाउचर / COMMISSION PAYOUT VOUCHER")
                            .FontSize(11f).ExtraBold().FontColor(TealHex);
                        left.Item().Text(model.PlatformAccountName)
                            .FontSize(14f).Bold().FontColor("#0f172a");
                        if (!string.IsNullOrWhiteSpace(model.PlatformGstin))
                        {
                            left.Item().Text($"GSTIN: {model.PlatformGstin}")
                                .FontSize(8.2f).SemiBold().FontColor("#334155");
                        }
                        left.Item().Text($"Helpline: {model.PlatformSupportPhone ?? "-"}  |  {model.PlatformSupportEmail ?? "-"}")
                            .FontSize(8f).FontColor("#475569");
                    });

                    hdr.ConstantItem(175).AlignRight().Column(right =>
                    {
                        right.Spacing(2);
                        right.Item().AlignRight().Text("VOUCHER NO.")
                            .FontSize(7.5f).Bold().FontColor("#64748b");
                        right.Item().AlignRight().Text(model.VoucherNo)
                            .FontSize(13f).ExtraBold().FontColor(EmeraldDarkHex);
                        right.Item().AlignRight().Text($"FY: {model.FinancialYear}")
                            .FontSize(8.2f).FontColor("#334155");
                        right.Item().AlignRight().Text($"Payment Date: {model.PaymentDate:dd/MM/yyyy}")
                            .FontSize(8.2f).FontColor("#334155");
                    });
                });

                // Payee Agent & Bank Settlement Details
                col.Item().Row(parties =>
                {
                    parties.Spacing(10);

                    parties.RelativeItem().Border(0.8f).BorderColor(SlateBorderHex).Background("#f8fafc").Padding(10).Column(ag =>
                    {
                        ag.Spacing(2.5f);
                        ag.Item().Text("PAYEE FIELD AGENT (प्राप्तकर्ता प्रतिनिधी)")
                            .FontSize(7.5f).Bold().FontColor(TealHex);
                        ag.Item().Text($"{model.AgentName} ({model.AgentCode})")
                            .FontSize(10.5f).Bold();
                        ag.Item().Text($"District: {model.AgentDistrict}  |  Mobile: {model.AgentMobile}")
                            .FontSize(8.3f).FontColor("#334155");
                    });

                    parties.RelativeItem().Border(0.8f).BorderColor(SlateBorderHex).Background("#f0fdfa").Padding(10).Column(bank =>
                    {
                        bank.Spacing(2.5f);
                        bank.Item().Text("DISBURSEMENT & UTR DETAILS (बँक भरणा तपशील)")
                            .FontSize(7.5f).Bold().FontColor(EmeraldDarkHex);
                        bank.Item().Text($"Mode: {model.PaymentMode}  |  UTR: {model.UtrReference}")
                            .FontSize(8.8f).Bold().FontColor("#0f172a");
                        if (!string.IsNullOrWhiteSpace(model.AgentUpiId))
                        {
                            bank.Item().Text($"UPI ID: {model.AgentUpiId}").FontSize(8.2f).FontColor("#065f46");
                        }
                        if (!string.IsNullOrWhiteSpace(model.AgentBankName))
                        {
                            bank.Item().Text($"Bank: {model.AgentBankName} ({model.AgentIfscCode ?? "-"})")
                                .FontSize(8f).FontColor("#334155");
                        }
                        if (!string.IsNullOrWhiteSpace(model.AgentAccountNumber))
                        {
                            bank.Item().Text($"A/C No.: {model.AgentAccountNumber}")
                                .FontSize(8f).FontColor("#334155");
                        }
                    });
                });

                // Settled GP Commission Lines Table
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(28);
                        cols.RelativeColumn(2.6f);
                        cols.RelativeColumn(1.2f);
                        cols.RelativeColumn(1.1f);
                        cols.RelativeColumn(0.8f);
                        cols.RelativeColumn(1.2f);
                    });

                    static IContainer ThCell(IContainer c) =>
                        c.Border(0.6f).BorderColor(TealHex).Background("#f0fdfa").PaddingVertical(5).PaddingHorizontal(6);

                    static IContainer TdCell(IContainer c) =>
                        c.Border(0.5f).BorderColor(SlateBorderHex).PaddingVertical(5).PaddingHorizontal(6);

                    table.Header(th =>
                    {
                        th.Cell().Element(ThCell).AlignCenter().Text("#").Bold();
                        th.Cell().Element(ThCell).AlignLeft().Text("Gram Panchayat / LGD").Bold();
                        th.Cell().Element(ThCell).AlignLeft().Text("Type").Bold();
                        th.Cell().Element(ThCell).AlignRight().Text("Collected (₹)").Bold();
                        th.Cell().Element(ThCell).AlignRight().Text("Rate %").Bold();
                        th.Cell().Element(ThCell).AlignRight().Text("Commission (₹)").Bold();
                    });

                    if (model.Lines.Count == 0)
                    {
                        table.Cell().ColumnSpan(6).Element(TdCell).AlignLeft()
                            .Text("FIFO Commission Settlement Adjustment — Consolidated Disbursement");
                    }
                    else
                    {
                        int idx = 1;
                        foreach (AgentPayoutVoucherLineDto line in model.Lines)
                        {
                            table.Cell().Element(TdCell).AlignCenter().Text(idx.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(TdCell).AlignLeft().Text($"{line.PanchayatNameMr} ({line.PanchayatNameEn})\nLGD: {line.LgdCode}");
                            table.Cell().Element(TdCell).AlignLeft().Text(line.CommissionType);
                            table.Cell().Element(TdCell).AlignRight().Text(line.CollectedAmount.ToString("N2", CultureInfo.InvariantCulture));
                            table.Cell().Element(TdCell).AlignRight().Text($"{line.RatePctSnapshot:0.##}%");
                            table.Cell().Element(TdCell).AlignRight().Text($"₹{line.CommissionAmount.ToString("N2", CultureInfo.InvariantCulture)}").Bold();
                            idx++;
                        }
                    }

                    IContainer TotCell(IContainer c) =>
                        c.Border(0.8f).BorderColor(TealHex).Background("#ecfdf5").PaddingVertical(6).PaddingHorizontal(6);

                    table.Cell().ColumnSpan(5).Element(TotCell).AlignRight().Text("Net Disbursed Payout Amount (एकूण वितरित रक्कम):").Bold();
                    table.Cell().Element(TotCell).AlignRight().Text($"₹{model.PayoutAmount.ToString("N2", CultureInfo.InvariantCulture)}")
                        .FontSize(10.5f).ExtraBold().FontColor(EmeraldDarkHex);
                });

                // Amount in Marathi words + Remarks
                col.Item().Border(0.7f).BorderColor(SlateBorderHex).Background("#f8fafc").Padding(9).Column(box =>
                {
                    box.Spacing(3);
                    box.Item().Row(w =>
                    {
                        w.RelativeItem().Text(t =>
                        {
                            t.Span("अक्षरी रक्कम: ").Bold().FontColor("#475569");
                            t.Span(model.AmountInWordsMr).Bold().FontColor("#0f172a");
                        });
                        w.AutoItem().Text($"STATUS: {model.Status.ToUpperInvariant()}").Bold().FontColor(EmeraldDarkHex);
                    });
                    if (!string.IsNullOrWhiteSpace(model.Remarks))
                    {
                        box.Item().Text($"Remarks: {model.Remarks}").FontSize(8.2f).FontColor("#334155");
                    }
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
                            txt.Item().Text("Verified Agent Disbursement").Bold().FontColor(TealHex);
                            txt.Item().Text($"Voucher: {model.VoucherNo}").FontSize(8f).FontColor("#334155");
                            txt.Item().Text($"Issued: {model.IssuedAtUtc:dd/MM/yyyy HH:mm} UTC").FontSize(7.8f).FontColor("#64748b");
                        });
                    });

                    ft.RelativeItem().AlignRight().AlignBottom().Column(sig =>
                    {
                        sig.Item().AlignRight().PaddingBottom(16).Text($"For {model.PlatformAccountName}")
                            .FontSize(8.5f).Bold().FontColor("#334155");
                        sig.Item().AlignRight().BorderTop(0.6f).BorderColor("#475569").PaddingTop(3)
                            .Text("Authorized Finance / SuperAdmin Signatory").FontSize(8f).FontColor("#475569");
                    });
                });
            });
        });
    }
}

