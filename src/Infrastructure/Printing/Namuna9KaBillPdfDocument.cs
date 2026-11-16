using System.Globalization;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9Ka;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GPMH.Infrastructure.Printing;

public sealed class Namuna9KaBillPdfDocument(
    Namuna9KaPdfDocumentModel model,
    IQrCodeGeneratorService qrCodeGenerator)
    : IDocument
{
    private const string TealHex = "#0f6b61";
    private const string TealLightHex = "#ecfdf5";
    private const string MaroonHex = "#881337";
    private const string MaroonLightHex = "#fff1f2";
    private const string BorderSlateHex = "#cbd5e1";

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Namuna 9-Ka Demand Bills — FY {model.Header.AssessmentYearLabel}",
        Author = "GPMH e-GramPanchayat SaaS",
        Subject = $"{model.Header.PanchayatNameEn} ({model.LayoutMode})",
    };

    public void Compose(IDocumentContainer container)
    {
        DevanagariFontRegistry.EnsureInitialized();

        bool isA5 = string.Equals(model.LayoutMode, "a5", StringComparison.OrdinalIgnoreCase);
        bool isBatch2In1 = !isA5 && string.Equals(model.SplitMode, "batch2in1", StringComparison.OrdinalIgnoreCase);

        if (isA5)
        {
            foreach (Namuna9KaBillItemDto bill in model.Bills)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(6, Unit.Millimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(style => style
                        .FontFamily(DevanagariFontRegistry.PrimaryFontFamily, DevanagariFontRegistry.FallbackFontFamily)
                        .FontSize(7f)
                        .FontColor("#0f172a"));

                    page.Content().Element(c => RenderBillCard(c, bill, "मालमत्ताधारक प्रत", TealHex));
                });
            }

            return;
        }

        if (isBatch2In1)
        {
            for (int offset = 0; offset < model.Bills.Count; offset += 2)
            {
                Namuna9KaBillItemDto leftBill = model.Bills[offset];
                Namuna9KaBillItemDto? rightBill = offset + 1 < model.Bills.Count ? model.Bills[offset + 1] : null;

                ComposeLandscapeTwoUpPage(
                    container,
                    left => RenderBillCard(left, leftBill, "मालमत्ताधारक प्रत", MaroonHex),
                    right =>
                    {
                        if (rightBill is not null)
                        {
                            RenderBillCard(right, rightBill, "मालमत्ताधारक प्रत", MaroonHex);
                        }
                        else
                        {
                            RenderEmptySlot(right);
                        }
                    });
            }

            return;
        }

        // Default: 2in1_landscape with dual counterfoil per bill (GP Copy | Cut Line | Taxpayer Copy)
        foreach (Namuna9KaBillItemDto bill in model.Bills)
        {
            ComposeLandscapeTwoUpPage(
                container,
                left => RenderBillCard(left, bill, "ग्रामपंचायत प्रत", TealHex),
                right => RenderBillCard(right, bill, "मालमत्ताधारक प्रत", MaroonHex));
        }
    }

    private static void ComposeLandscapeTwoUpPage(
        IDocumentContainer container,
        Action<IContainer> composeLeft,
        Action<IContainer> composeRight)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(6, Unit.Millimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(style => style
                .FontFamily(DevanagariFontRegistry.PrimaryFontFamily, DevanagariFontRegistry.FallbackFontFamily)
                .FontSize(7f)
                .FontColor("#0f172a"));

            page.Content().Row(row =>
            {
                row.RelativeItem().Element(composeLeft);
                row.ConstantItem(20).Element(ComposeCenterCutLine);
                row.RelativeItem().Element(composeRight);
            });
        });
    }

    private static void ComposeCenterCutLine(IContainer container)
    {
        const string dashedLineSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 550" width="20" height="550">
              <line x1="10" y1="0" x2="10" y2="550" stroke="#64748b" stroke-width="1.2" stroke-dasharray="5,4" />
            </svg>
            """;

        const string scissorsSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="#475569" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="6" cy="6" r="3" />
              <circle cx="6" cy="18" r="3" />
              <line x1="20" y1="4" x2="8.12" y2="15.88" />
              <line x1="14.47" y1="14.48" x2="20" y2="20" />
              <line x1="8.12" y1="8.12" x2="12" y2="12" />
            </svg>
            """;

        container.Layers(layers =>
        {
            layers.Layer().Svg(dashedLineSvg).FitArea();
            layers.PrimaryLayer().AlignCenter().AlignMiddle()
                .Background(Colors.White).PaddingVertical(4).PaddingHorizontal(1)
                .RotateLayoutCounterclockwise()
                .Row(r =>
                {
                    r.Spacing(3);
                    r.AutoItem().AlignMiddle().Width(8).Height(8).Svg(scissorsSvg).FitArea();
                    r.AutoItem().AlignMiddle().Text("येथे कापावे").FontSize(6.5f).Bold().FontColor("#475569");
                });
        });
    }

    private static void RenderEmptySlot(IContainer container)
    {
        container.Border(0.8f).BorderColor("#e2e8f0").Background("#f8fafc")
            .AlignCenter().AlignMiddle()
            .Text("रिक्त बिल जागा").FontSize(8f).FontColor("#94a3b8");
    }

    private void RenderBillCard(
        IContainer container,
        Namuna9KaBillItemDto bill,
        string copyLabelMr,
        string pillBgHex)
    {
        Namuna9KaPdfHeaderModel h = model.Header;
        string ownerMr = string.IsNullOrWhiteSpace(bill.PrimaryOwnerNameMr)
            ? (bill.PrimaryOwnerNameEn ?? "-")
            : bill.PrimaryOwnerNameMr;

        string issueDateStr = bill.IssueDate.HasValue
            ? bill.IssueDate.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        string dueDateStr = bill.DueDate.HasValue
            ? bill.DueDate.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : DateTime.UtcNow.AddDays(model.NoticeGraceDays).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        string billNoStr = string.IsNullOrWhiteSpace(bill.BillNo) ? "PREVIEW" : bill.BillNo;

        container.Border(1.2f).BorderColor(TealHex).Padding(7).Column(card =>
        {
            card.Spacing(4);

            // 1. Gram Panchayat Masthead
            card.Item().BorderBottom(1.3f).BorderColor(TealHex).PaddingBottom(3).Column(gp =>
            {
                gp.Item().AlignCenter().Text($"ग्रामपंचायत {h.PanchayatNameMr}")
                    .FontSize(11.5f).Bold().FontColor(TealHex);
                gp.Item().AlignCenter().Text(
                    $"ता. {h.TehsilName}  |  जि. {h.DistrictName}  |  LGD: {h.LgdCode}")
                    .FontSize(6.8f).SemiBold().FontColor("#475569");
            });

            // 2. Statutory Title & Copy Badge Pill (Teal/Maroon)
            card.Item().Border(0.7f).BorderColor("#fecdd3").Background(MaroonLightHex)
                .PaddingVertical(3).PaddingHorizontal(6).Row(ttl =>
                {
                    ttl.RelativeItem().AlignMiddle()
                        .Text("नमुना ९-क — कराची मागणी पावती व नोटीस")
                        .FontSize(8.2f).Bold().FontColor(MaroonHex);
                    ttl.AutoItem().Background(pillBgHex).PaddingVertical(1.5f).PaddingHorizontal(6)
                        .Text(copyLabelMr).FontSize(6.5f).Bold().FontColor(Colors.White);
                });

            // 3. Bill No / Year / Issue Date / Due Date Meta Strip
            card.Item().BorderBottom(0.6f).BorderColor(BorderSlateHex).PaddingBottom(3).Row(meta =>
            {
                meta.RelativeItem().Text(t =>
                {
                    t.Span("बिल क्र.: ").FontSize(6.8f).FontColor("#475569");
                    t.Span(billNoStr).FontSize(7f).Bold();
                });
                meta.AutoItem().PaddingRight(6).Text(t =>
                {
                    t.Span("आ. वर्ष: ").FontSize(6.8f).FontColor("#475569");
                    t.Span(h.AssessmentYearLabel).FontSize(7f).Bold();
                });
                meta.AutoItem().PaddingRight(6).Text(t =>
                {
                    t.Span("दि.: ").FontSize(6.8f).FontColor("#475569");
                    t.Span(issueDateStr).FontSize(6.8f).Bold();
                });
                meta.AutoItem().Text(t =>
                {
                    t.Span("देय दि.: ").FontSize(6.8f).FontColor(MaroonHex);
                    t.Span(dueDateStr).FontSize(7f).Bold().FontColor(MaroonHex);
                });
            });

            // 4. Taxpayer Block & Dynamic NPCI UPI QR
            card.Item().BorderBottom(0.6f).BorderColor(BorderSlateHex).PaddingBottom(4).Row(ownRow =>
            {
                ownRow.Spacing(6);

                ownRow.RelativeItem().Column(det =>
                {
                    det.Spacing(1.8f);
                    det.Item().Text(t =>
                    {
                        t.Span("मालमत्ताधारक: ").FontSize(7f).FontColor("#475569");
                        t.Span(ownerMr).FontSize(8f).Bold();
                    });
                    det.Item().Text(t =>
                    {
                        t.Span("वॉर्ड क्र.: ").FontSize(6.8f).FontColor("#475569");
                        t.Span(bill.WardNo).FontSize(7.2f).Bold();
                        t.Span("   |   मिळकत क्र.: ").FontSize(6.8f).FontColor("#475569");
                        t.Span(bill.PropertyNo).FontSize(7.2f).Bold();
                    });
                    det.Item().Text($"भोगवटदार: {bill.OccupierName}  |  पाणी नळ: {FormatWaterCodeMr(bill.WaterConnectionCode)}")
                        .FontSize(6.6f).FontColor("#334155");

                    if (!string.IsNullOrWhiteSpace(bill.PrimaryOwnerAadhaarMasked))
                    {
                        det.Item().Text($"आधार क्र.: {bill.PrimaryOwnerAadhaarMasked}")
                            .FontSize(6.1f).FontColor("#64748b");
                    }

                    if (bill.RebateCutoffDate.HasValue && bill.NetPayableAmount > 0m)
                    {
                        string cutoffStr = bill.RebateCutoffDate.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                        det.Item().PaddingTop(1).Text($"दि. {cutoffStr} पर्यंत भरल्यास {model.EarlyRebatePct:0.#}% आगाऊ सूट पात्र")
                            .FontSize(6f).Bold().FontColor(TealHex);
                    }
                });

                ownRow.ConstantItem(64).AlignRight().Column(qrCol =>
                {
                    if (!string.IsNullOrWhiteSpace(bill.UpiPayUri))
                    {
                        qrCol.Item().AlignCenter().Border(0.6f).BorderColor(TealHex).Padding(2)
                            .Width(52).Height(52)
                            .Image(qrCodeGenerator.GeneratePng(bill.UpiPayUri, 4))
                            .FitArea();
                        qrCol.Item().PaddingTop(1).AlignCenter()
                            .Text(string.IsNullOrWhiteSpace(bill.UpiIdUsed) ? "Scan UPI QR" : bill.UpiIdUsed)
                            .FontSize(5f).Bold().FontColor(TealHex);
                    }
                    else
                    {
                        qrCol.Item().Border(0.6f).BorderColor(BorderSlateHex).Background("#f8fafc")
                            .Width(56).Height(44).AlignCenter().AlignMiddle().Padding(3)
                            .Text(bill.NetPayableAmount <= 0m ? "कर निरंक\n₹ 0.00" : "कार्यालयीन\nभरणा")
                            .FontSize(6f).Bold().FontColor("#475569").AlignCenter();
                    }
                });
            });

            // 5. Itemized Tax Breakdown Table
            card.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3.3f); // Head
                    cols.RelativeColumn(1.1f); // Arrears
                    cols.RelativeColumn(1.1f); // Current
                    cols.RelativeColumn(1.2f); // Total
                });

                static IContainer ThCell(IContainer c) =>
                    c.Border(0.5f).BorderColor(BorderSlateHex).Background("#f8fafc").PaddingVertical(2).PaddingHorizontal(3);

                static IContainer TdCell(IContainer c) =>
                    c.Border(0.5f).BorderColor(BorderSlateHex).PaddingVertical(1.8f).PaddingHorizontal(3);

                table.Header(th =>
                {
                    th.Cell().Element(ThCell).AlignLeft().Text("करांचे नाव (Tax Head)").FontSize(6.5f).Bold();
                    th.Cell().Element(ThCell).AlignRight().Text("थकबाकी (₹)").FontSize(6.5f).Bold();
                    th.Cell().Element(ThCell).AlignRight().Text("चालू (₹)").FontSize(6.5f).Bold();
                    th.Cell().Element(ThCell).AlignRight().Text("एकूण (₹)").FontSize(6.5f).Bold();
                });

                Namuna9KaHeadRowDto hd = bill.Heads;

                void AddTaxRow(string label, decimal arrears, decimal current, bool highlightNegative = false)
                {
                    string colorHex = highlightNegative ? "#047857" : "#0f172a";
                    table.Cell().Element(TdCell).AlignLeft().Text(label).FontSize(6.3f).FontColor(colorHex);
                    table.Cell().Element(TdCell).AlignRight().Text(Fmt(arrears)).FontSize(6.3f).FontColor(colorHex);
                    table.Cell().Element(TdCell).AlignRight().Text(Fmt(current)).FontSize(6.3f).FontColor(colorHex);
                    table.Cell().Element(TdCell).AlignRight().Text(Fmt(arrears + current)).FontSize(6.3f).SemiBold().FontColor(colorHex);
                }

                AddTaxRow("इमारत कर (घरपट्टी) / House Tax", hd.BuildingArrears, hd.BuildingCurrent);
                AddTaxRow("दिवाबत्ती कर / Lighting Tax", hd.LightingArrears, hd.LightingCurrent);
                AddTaxRow("आरोग्य कर / Health Tax", hd.HealthArrears, hd.HealthCurrent);
                AddTaxRow("सफाई कर / Sanitation Tax", hd.SanitationArrears, hd.SanitationCurrent);

                if (hd.ToiletArrears + hd.ToiletCurrent > 0m)
                {
                    AddTaxRow("शौचालय कर / Toilet Tax", hd.ToiletArrears, hd.ToiletCurrent);
                }

                AddTaxRow(
                    "पाणीपट्टी (सामान्य/विशेष) / Water Tax",
                    hd.GeneralWaterArrears + hd.SpecialWaterArrears,
                    hd.GeneralWaterCurrent + hd.SpecialWaterCurrent);

                if (hd.OtherArrears + hd.OtherCurrent > 0m)
                {
                    AddTaxRow("इतर कर / Other Tax", hd.OtherArrears, hd.OtherCurrent);
                }

                decimal currentFees = hd.NoticeFee + hd.WarrantFee + hd.PenaltyAmount;
                if (hd.CarriedFeesAndPenalty + currentFees > 0m)
                {
                    AddTaxRow("नोटीस / वारंट / दंड शुल्क", hd.CarriedFeesAndPenalty, currentFees);
                }

                if (hd.RebateAmount > 0m)
                {
                    AddTaxRow("वेळेवर भरणा सूट (-) / Early Rebate", 0m, -hd.RebateAmount, highlightNegative: true);
                }

                if (hd.PaidAmount > 0m)
                {
                    AddTaxRow("आधीच जमा रक्कम (-) / Paid", 0m, -hd.PaidAmount, highlightNegative: true);
                }

                // Grand Totals row
                static IContainer TotCell(IContainer c) =>
                    c.Border(0.6f).BorderColor(TealHex).Background("#f1f5f9").PaddingVertical(2.2f).PaddingHorizontal(3);

                table.Cell().Element(TotCell).AlignLeft().Text("एकूण निव्वळ देय मागणी").FontSize(6.7f).Bold();
                table.Cell().Element(TotCell).AlignRight().Text(Fmt(bill.ArrearsAmount)).FontSize(6.7f).Bold();
                table.Cell().Element(TotCell).AlignRight().Text(Fmt(bill.CurrentAmount)).FontSize(6.7f).Bold();
                table.Cell().Element(TotCell).AlignRight().Text(Fmt(bill.NetPayableAmount)).FontSize(6.9f).Bold().FontColor(TealHex);
            });

            // 6. Total Payable Highlight Box with Marathi Amount-in-Words
            card.Item().Border(1f).BorderColor("#059669").Background(TealLightHex)
                .PaddingVertical(4).PaddingHorizontal(7).Row(due =>
                {
                    due.RelativeItem().AlignMiddle().Column(wordsCol =>
                    {
                        wordsCol.Item().Text("एकूण देय रक्कम")
                            .FontSize(6.4f).Bold().FontColor("#065f46");
                        wordsCol.Item().Text($"अक्षरी: {bill.AmountInWordsMr}")
                            .FontSize(6.8f).Bold().FontColor("#0f172a");
                    });
                    due.AutoItem().AlignMiddle()
                        .Text($"₹{bill.NetPayableAmount.ToString("#,##0.00", CultureInfo.InvariantCulture)}")
                        .FontSize(11.5f).ExtraBold().FontColor("#047857");
                });

            // 7. Mandatory Section 129(1) 15-day Notice Clause
            card.Item().Border(0.5f).BorderColor("#fecdd3").Background(MaroonLightHex)
                .PaddingVertical(3.5f).PaddingHorizontal(6).Row(noticeRow =>
                {
                    noticeRow.ConstantItem(2.5f).Background(MaroonHex);
                    noticeRow.RelativeItem().PaddingLeft(5).Text(t =>
                    {
                        t.Span("महत्त्वाची वैधानिक सूचना (महाराष्ट्र ग्रामपंचायत अधिनियम १९५८, कलम १२९(१)): ")
                            .FontSize(5.9f).Bold().FontColor(MaroonHex);
                        t.Span(
                            $"ही मागणी पावती मिळाल्यापासून {model.NoticeGraceDays} दिवसांच्या आत (दि. {dueDateStr} पर्यंत) देय रकमेचा भरणा ग्रामपंचायतीकडे किंवा UPI QR द्वारे करावा; मुदतीत भरणा न केल्यास नियमानुसार दंड व जप्ती कार्यवाही केली जाईल.")
                            .FontSize(5.9f).FontColor("#334155");
                    });
                });

            // 8. Signatories & Official Stamp Footer
            card.Item().ExtendVertical().AlignBottom().PaddingTop(4).Row(sig =>
            {
                // Left: Clerk / Tax Collector
                sig.RelativeItem(1f).AlignBottom().Column(c =>
                {
                    c.Item().AlignCenter().PaddingTop(14).Text(string.IsNullOrWhiteSpace(h.OperatorName) ? string.Empty : h.OperatorName)
                        .FontSize(5.6f).FontColor("#475569");
                    c.Item().BorderTop(0.6f).BorderColor("#334155").PaddingTop(2).AlignCenter()
                        .Text("कर संकलक / लिपिक").FontSize(6.2f).Bold();
                });

                sig.ConstantItem(6);

                // Center: Official Stamp
                sig.ConstantItem(52).AlignBottom().AlignCenter().Column(st =>
                {
                    if (h.OfficialStampBytes is { Length: > 0 })
                    {
                        st.Item().AlignCenter().Width(38).Height(22)
                            .Image(h.OfficialStampBytes).FitArea();
                    }
                    else
                    {
                        st.Item().Height(18);
                    }

                    st.Item().AlignCenter().Text("कार्यालयीन शिक्का").FontSize(5.4f).FontColor("#64748b");
                });

                sig.ConstantItem(6);

                // Right: Gramsevak & Sarpanch Signatures
                sig.RelativeItem(1.25f).AlignBottom().Column(gs =>
                {
                    gs.Item().Row(imgs =>
                    {
                        imgs.RelativeItem().AlignCenter().Element(slot =>
                        {
                            if (h.GramsevakSignatureBytes is { Length: > 0 })
                            {
                                slot.Width(36).Height(18).Image(h.GramsevakSignatureBytes).FitArea();
                            }
                            else
                            {
                                slot.Height(18);
                            }
                        });
                        imgs.RelativeItem().AlignCenter().Element(slot =>
                        {
                            if (h.SarpanchSignatureBytes is { Length: > 0 })
                            {
                                slot.Width(36).Height(18).Image(h.SarpanchSignatureBytes).FitArea();
                            }
                            else
                            {
                                slot.Height(18);
                            }
                        });
                    });

                    gs.Item().BorderTop(0.6f).BorderColor("#334155").PaddingTop(2).AlignCenter()
                        .Text("ग्रामसेवक / सरपंच").FontSize(6.2f).Bold();
                });
            });
        });
    }

    private static string Fmt(decimal amount) => amount.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private static string FormatWaterCodeMr(string code) => code.ToUpperInvariant() switch
    {
        "GENERAL" => "सामान्य नळ",
        "SPECIAL" => "विशेष नळ",
        _ => "नाही",
    };
}

