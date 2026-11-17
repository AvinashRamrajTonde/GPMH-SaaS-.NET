using System.Globalization;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna10;
using GPMH.Domain.Enums;
using GPMH.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GPMH.Infrastructure.Printing;

/// <summary>
/// Rule 32(5) Maharashtra Statutory Namuna 10 Dual-Copy Receipt on A4 Landscape (297mm × 210mm):
/// Left 138mm Office Record Copy (Teal #0f6b61) | 8mm center cut line | Right 138mm Citizen Original Copy (Deep Blue #0369a1).
/// </summary>
public sealed class Namuna10DualReceiptPdfDocument(
    Namuna10DualReceiptPdfModel model,
    IQrCodeGeneratorService qrCodeGenerator) : IDocument
{
    private const string TealHex = "#0f6b61";
    private const string TealSoftHex = "#f0fdfa";
    private const string TealBorderHex = "#99f6e4";
    private const string BlueHex = "#0369a1";
    private const string BlueSoftHex = "#f0f9ff";
    private const string BlueBorderHex = "#bae6fd";
    private const string SlateBorderHex = "#cbd5e1";
    private const string CancelRedHex = "#b91c1c";
    private const string CancelBgHex = "#fef2f2";

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Namuna 10 Receipt - {model.ReceiptNo}",
        Author = model.PanchayatNameEn,
        Subject = $"Maharashtra Village Panchayats Taxation Rules 1960 - Rule 32(5) Tax Receipt ({model.AssessmentYearLabel})",
    };

    public void Compose(IDocumentContainer container)
    {
        byte[] verifyQrPng = qrCodeGenerator.GeneratePng(model.VerifyUrl, pixelsPerModule: 4);

        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.MarginVertical(5.5f, Unit.Millimetre);
            page.MarginHorizontal(5.5f, Unit.Millimetre);
            page.DefaultTextStyle(s => s
                .FontFamily(DevanagariFontRegistry.PrimaryFontFamily)
                .FontSize(7.2f)
                .FontColor("#0f172a"));

            page.Content().Row(row =>
            {
                row.ConstantItem(138, Unit.Millimetre)
                    .Element(c => ComposeCopyCard(c, verifyQrPng, isOfficeCopy: true));

                row.ConstantItem(8, Unit.Millimetre)
                    .Element(ComposeCenterCutLine);

                row.ConstantItem(138, Unit.Millimetre)
                    .Element(c => ComposeCopyCard(c, verifyQrPng, isOfficeCopy: false));
            });
        });
    }

    private static void ComposeCenterCutLine(IContainer container)
    {
        container.AlignCenter().Column(col =>
        {
            col.Item().ExtendVertical().AlignCenter().Width(14).Svg("""
                <svg viewBox="0 0 14 520" xmlns="http://www.w3.org/2000/svg">
                  <line x1="7" y1="4" x2="7" y2="230" stroke="#94a3b8" stroke-width="1" stroke-dasharray="4 4"/>
                  <line x1="7" y1="290" x2="7" y2="516" stroke="#94a3b8" stroke-width="1" stroke-dasharray="4 4"/>
                </svg>
                """);
            col.Item().AlignCenter().PaddingVertical(2).RotateLayoutCounterclockwise()
                .Text("--- येथून कापा / CUT HERE ---")
                .FontSize(5.5f)
                .SemiBold()
                .FontColor("#64748b");
        });
    }

    private void ComposeCopyCard(IContainer container, byte[] verifyQrPng, bool isOfficeCopy)
    {
        string accentHex = isOfficeCopy ? TealHex : BlueHex;
        string softBgHex = isOfficeCopy ? TealSoftHex : BlueSoftHex;
        string softBorderHex = isOfficeCopy ? TealBorderHex : BlueBorderHex;
        string badgeLabel = isOfficeCopy
            ? "दप्तर प्रत (Office Record Copy)"
            : "मूळ प्रत (करदात्यासाठी / Citizen Original)";

        string receiptDateStr = model.ReceiptDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        DateTimeOffset istNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(model.CollectedAtUtc, "Asia/Kolkata");
        string collectedTimeStr = istNow.ToString("dd/MM/yyyy HH:mm 'IST'", CultureInfo.InvariantCulture);

        container
            .Border(1.2f)
            .BorderColor(accentHex)
            .Background("#ffffff")
            .Column(card =>
            {
                // Top color accent strip
                card.Item().Height(3).Background(accentHex);

                card.Item().PaddingHorizontal(8).PaddingTop(5).PaddingBottom(6).Column(inner =>
                {
                    inner.Spacing(3.5f);

                    // 1. Statutory Masthead & Copy Badge
                    inner.Item().Row(hdr =>
                    {
                        hdr.RelativeItem().Column(left =>
                        {
                            left.Item().Text("नमुना क्र. १० (नियम ३२(५) अन्वये कर भरणा पावती)")
                                .FontSize(6.5f).SemiBold().FontColor("#475569");
                            left.Item().Text($"{model.PanchayatNameMr} ग्रामपंचायत")
                                .FontSize(10f).Bold().FontColor(accentHex);
                            left.Item().Text($"{model.PanchayatNameEn} | ता. {model.TehsilName}, जि. {model.DistrictName} | LGD: {model.LgdCode}")
                                .FontSize(6.4f).FontColor("#475569");
                        });

                        hdr.AutoItem().AlignTop().AlignRight().Column(badgeCol =>
                        {
                            badgeCol.Item()
                                .Border(0.7f)
                                .BorderColor(softBorderHex)
                                .Background(softBgHex)
                                .PaddingVertical(2.5f)
                                .PaddingHorizontal(6)
                                .Text(badgeLabel)
                                .FontSize(6.2f)
                                .Bold()
                                .FontColor(accentHex);
                        });
                    });

                    // 2. Cancelled Receipt Banner (mandatory statutory tamper-evident warning)
                    if (model.IsCancelled)
                    {
                        string cancelledAtStr = model.CancelledAtUtc.HasValue
                            ? TimeZoneInfo.ConvertTimeBySystemTimeZoneId(model.CancelledAtUtc.Value, "Asia/Kolkata")
                                .ToString("dd/MM/yyyy HH:mm 'IST'", CultureInfo.InvariantCulture)
                            : "-";
                        string reasonText = string.IsNullOrWhiteSpace(model.CancelReason)
                            ? "कार्यालयीन कारणास्तव रद्द"
                            : model.CancelReason.Trim();

                        inner.Item()
                            .Border(1.2f)
                            .BorderColor(CancelRedHex)
                            .Background(CancelBgHex)
                            .PaddingVertical(3.5f)
                            .PaddingHorizontal(6)
                            .Column(cancelCol =>
                            {
                                cancelCol.Item().AlignCenter()
                                    .Text("रद्द पावती / CANCELLED RECEIPT")
                                    .FontSize(9f).ExtraBold().FontColor(CancelRedHex);
                                cancelCol.Item().AlignCenter()
                                    .Text($"रद्द दि.: {cancelledAtStr}  |  कारण: {reasonText}")
                                    .FontSize(6.2f).SemiBold().FontColor("#991b1b");
                            });
                    }

                    // 3. Receipt Meta Strip
                    inner.Item()
                        .Border(0.6f)
                        .BorderColor(SlateBorderHex)
                        .Background("#f8fafc")
                        .PaddingVertical(3)
                        .PaddingHorizontal(5)
                        .Column(metaWrap =>
                        {
                            metaWrap.Spacing(1.5f);
                            metaWrap.Item().Row(meta =>
                            {
                                meta.RelativeItem(1.35f).Text(t =>
                                {
                                    t.Span("पावती क्र.: ").FontSize(6.4f).FontColor("#475569");
                                    t.Span(model.ReceiptNo).FontSize(7.2f).Bold().FontColor(accentHex);
                                });
                                meta.RelativeItem(0.85f).Text(t =>
                                {
                                    t.Span("पुस्तक क्र.: ").FontSize(6.4f).FontColor("#475569");
                                    t.Span(model.BookNo).FontSize(7f).Bold();
                                });
                                meta.RelativeItem(0.95f).Text(t =>
                                {
                                    t.Span("दि.: ").FontSize(6.4f).FontColor("#475569");
                                    t.Span(receiptDateStr).FontSize(7f).Bold();
                                });
                                meta.RelativeItem(0.9f).AlignRight().Text(t =>
                                {
                                    t.Span("आ. वर्ष: ").FontSize(6.4f).FontColor("#475569");
                                    t.Span(model.AssessmentYearLabel).FontSize(7f).Bold();
                                });
                            });

                            if (!string.IsNullOrWhiteSpace(model.BillNo))
                            {
                                metaWrap.Item().Row(subMeta =>
                                {
                                    subMeta.RelativeItem().Text($"मागणी बिल क्र.: {model.BillNo}")
                                        .FontSize(6f).FontColor("#475569");
                                    subMeta.AutoItem().Text($"नोंद वेळ: {collectedTimeStr}")
                                        .FontSize(6f).FontColor("#64748b");
                                });
                            }
                        });

                    // 4. Taxpayer & Property Grid (ADR-015 strictly masked Aadhaar & Mobile only)
                    inner.Item().Column(det =>
                    {
                        det.Spacing(1.8f);
                        det.Item().Row(r1 =>
                        {
                            r1.RelativeItem(1.4f).Text(t =>
                            {
                                t.Span("करदाता (मालक): ").FontSize(6.5f).FontColor("#475569");
                                t.Span(model.PrimaryOwnerNameMr).FontSize(7.4f).Bold();
                                if (!string.IsNullOrWhiteSpace(model.PrimaryOwnerNameEn) &&
                                    !string.Equals(model.PrimaryOwnerNameEn, model.PrimaryOwnerNameMr, StringComparison.Ordinal))
                                {
                                    t.Span($" ({model.PrimaryOwnerNameEn})").FontSize(6.2f).FontColor("#475569");
                                }
                            });
                            r1.RelativeItem(0.9f).AlignRight().Text(t =>
                            {
                                t.Span("भोगवटदार: ").FontSize(6.5f).FontColor("#475569");
                                t.Span(model.OccupierName).FontSize(6.8f).SemiBold();
                            });
                        });

                        det.Item().Row(r2 =>
                        {
                            string survey = string.IsNullOrWhiteSpace(model.CitySurveyOrGutNo) ? "-" : model.CitySurveyOrGutNo;
                            r2.RelativeItem().Text(t =>
                            {
                                t.Span("मिळकत क्र.: ").FontSize(6.5f).FontColor("#475569");
                                t.Span(model.PropertyNo).FontSize(7f).Bold().FontColor(accentHex);
                                t.Span("   |   वॉर्ड: ").FontSize(6.5f).FontColor("#475569");
                                t.Span(model.WardNo).FontSize(7f).Bold();
                                t.Span("   |   गट/सर्व्हे: ").FontSize(6.5f).FontColor("#475569");
                                t.Span(survey).FontSize(6.8f).SemiBold();
                            });
                        });

                        det.Item().Row(r3 =>
                        {
                            r3.RelativeItem().Text(t =>
                            {
                                t.Span("भरणा प्रकार: ").FontSize(6.5f).FontColor("#475569");
                                t.Span(FormatPaymentModeMr(model.PaymentMode)).FontSize(7f).Bold();
                                t.Span("   |   भरणादार: ").FontSize(6.5f).FontColor("#475569");
                                t.Span(model.PayerName).FontSize(6.8f).SemiBold();
                            });

                            if (!string.IsNullOrWhiteSpace(model.PrimaryOwnerMobileMasked) ||
                                !string.IsNullOrWhiteSpace(model.PrimaryOwnerAadhaarMasked))
                            {
                                List<string> piiParts = [];
                                if (!string.IsNullOrWhiteSpace(model.PrimaryOwnerMobileMasked))
                                {
                                    piiParts.Add($"मो.: {model.PrimaryOwnerMobileMasked}");
                                }

                                if (!string.IsNullOrWhiteSpace(model.PrimaryOwnerAadhaarMasked))
                                {
                                    piiParts.Add($"आधार: {model.PrimaryOwnerAadhaarMasked}");
                                }

                                r3.AutoItem().AlignRight().Text(string.Join(" | ", piiParts))
                                    .FontSize(6f).FontColor("#64748b");
                            }
                        });

                        if (!string.IsNullOrWhiteSpace(model.ReferenceNo) ||
                            !string.IsNullOrWhiteSpace(model.BankName) ||
                            model.ChequeDate.HasValue)
                        {
                            List<string> txParts = [];
                            if (!string.IsNullOrWhiteSpace(model.ReferenceNo))
                            {
                                txParts.Add($"UTR/धनादेश क्र.: {model.ReferenceNo}");
                            }

                            if (!string.IsNullOrWhiteSpace(model.BankName))
                            {
                                txParts.Add($"बँक: {model.BankName}");
                            }

                            if (model.ChequeDate.HasValue)
                            {
                                txParts.Add($"धनादेश दि.: {model.ChequeDate.Value:dd/MM/yyyy}");
                            }

                            det.Item().Text(string.Join("   |   ", txParts))
                                .FontSize(6.1f).SemiBold().FontColor("#334155");
                        }
                    });

                    // 5. Statutory Acknowledgement Banner
                    inner.Item()
                        .BorderLeft(2f)
                        .BorderColor("#d97706")
                        .Background("#fffbeb")
                        .PaddingVertical(2.2f)
                        .PaddingHorizontal(5)
                        .Text("यांजकडून वर नमूद आर्थिक वर्षासाठी खालीलप्रमाणे ग्रामपंचायत कर व फी ची रक्कम रितसर प्राप्त झाली.")
                        .FontSize(6.1f).Italic().FontColor("#334155");

                    // 6. Itemized Tax Allocation Breakdown Table
                    ComposeBreakdownTable(inner.Item(), accentHex, softBgHex);

                    // 7. Marathi Amount in Words + Remaining Balance Strip
                    inner.Item()
                        .Border(0.7f)
                        .BorderColor(SlateBorderHex)
                        .Background("#f8fafc")
                        .PaddingVertical(3)
                        .PaddingHorizontal(5)
                        .Row(wordsRow =>
                        {
                            wordsRow.RelativeItem().AlignMiddle().Text(t =>
                            {
                                t.Span("अक्षरी रक्कम: ").FontSize(6.4f).Bold().FontColor("#475569");
                                t.Span(model.AmountInWordsMr).FontSize(6.9f).Bold().FontColor("#0f172a");
                            });
                            wordsRow.AutoItem().AlignMiddle().PaddingLeft(6).Text(t =>
                            {
                                t.Span("शिल्लक थकबाकी: ").FontSize(6.3f).Bold().FontColor("#475569");
                                t.Span($"₹{Fmt(model.RemainingBalanceAfterPayment)}")
                                    .FontSize(7.4f)
                                    .Bold()
                                    .FontColor(model.RemainingBalanceAfterPayment > 0m ? CancelRedHex : "#047857");
                            });
                        });

                    if (!string.IsNullOrWhiteSpace(model.Remarks))
                    {
                        inner.Item().Text($"शेरा / नोंद: {model.Remarks.Trim()}")
                            .FontSize(6f).FontColor("#475569");
                    }

                    // 8. Verification QR, GP Seal & Signatures Footer
                    inner.Item().ExtendVertical().AlignBottom().PaddingTop(3)
                        .BorderTop(0.6f).BorderColor(SlateBorderHex).PaddingTop(4)
                        .Row(footer =>
                        {
                            // Verification QR (left)
                            footer.AutoItem().AlignBottom().Row(qrBox =>
                            {
                                qrBox.AutoItem()
                                    .Border(0.6f)
                                    .BorderColor(accentHex)
                                    .Padding(2)
                                    .Width(40)
                                    .Height(40)
                                    .Image(verifyQrPng)
                                    .FitArea();

                                qrBox.AutoItem().PaddingLeft(4).AlignMiddle().Column(qrTxt =>
                                {
                                    qrTxt.Item().Text("ई-पडताळणी QR").FontSize(6f).Bold().FontColor(accentHex);
                                    qrTxt.Item().Text(model.ReceiptNo).FontSize(5.7f).SemiBold().FontColor("#334155");
                                    qrTxt.Item().Text(model.IsCancelled ? "STATUS: CANCELLED" : "STATUS: VALID")
                                        .FontSize(5.4f).Bold().FontColor(model.IsCancelled ? CancelRedHex : "#047857");
                                });
                            });

                            // Center GP Stamp + Thank You
                            footer.RelativeItem().AlignBottom().AlignCenter().Column(stampCol =>
                            {
                                if (model.OfficialStampBytes is { Length: > 0 })
                                {
                                    stampCol.Item().AlignCenter().Height(24).Width(36)
                                        .Image(model.OfficialStampBytes).FitArea();
                                }

                                stampCol.Item().AlignCenter()
                                    .Text("ग्रामपंचायत अधिकृत शिक्का")
                                    .FontSize(5.3f).SemiBold().FontColor("#64748b");
                            });

                            // Right Signatures (Clerk & Gramsevak / Sarpanch)
                            footer.AutoItem().AlignBottom().Row(sigs =>
                            {
                                sigs.AutoItem().Width(58).AlignBottom().Column(clerk =>
                                {
                                    if (model.OperatorSignatureBytes is { Length: > 0 })
                                    {
                                        clerk.Item().AlignCenter().Height(18).Width(50)
                                            .Image(model.OperatorSignatureBytes).FitArea();
                                    }
                                    else
                                    {
                                        clerk.Item().PaddingTop(14);
                                    }

                                    if (!string.IsNullOrWhiteSpace(model.OperatorName))
                                    {
                                        clerk.Item().AlignCenter().Text(model.OperatorName)
                                            .FontSize(5.2f).FontColor("#475569");
                                    }

                                    clerk.Item().BorderTop(0.6f).BorderColor("#475569").PaddingTop(1.5f).AlignCenter()
                                        .Text("कर संकलन लिपिक").FontSize(5.8f).Bold();
                                });

                                sigs.ConstantItem(6);

                                sigs.AutoItem().Width(66).AlignBottom().Column(gs =>
                                {
                                    byte[]? gsSig = model.GramsevakSignatureBytes ?? model.SarpanchSignatureBytes;
                                    string? gsName = !string.IsNullOrWhiteSpace(model.GramsevakName)
                                        ? model.GramsevakName
                                        : model.SarpanchName;

                                    if (gsSig is { Length: > 0 })
                                    {
                                        gs.Item().AlignCenter().Height(18).Width(54)
                                            .Image(gsSig).FitArea();
                                    }
                                    else
                                    {
                                        gs.Item().PaddingTop(14);
                                    }

                                    if (!string.IsNullOrWhiteSpace(gsName))
                                    {
                                        gs.Item().AlignCenter().Text(gsName)
                                            .FontSize(5.2f).FontColor("#475569");
                                    }

                                    gs.Item().BorderTop(0.6f).BorderColor("#475569").PaddingTop(1.5f).AlignCenter()
                                        .Text("ग्रामसेवक / सरपंच").FontSize(5.8f).Bold();
                                });
                            });
                        });
                });
            });
    }

    private void ComposeBreakdownTable(IContainer container, string accentHex, string softBgHex)
    {
        TaxReceiptHeadAllocation alloc = model.HeadAllocation;
        DemandHeadBreakdown arr = alloc.ArrearsAllocated ?? DemandHeadBreakdown.Empty;
        DemandHeadBreakdown cur = alloc.CurrentAllocated ?? DemandHeadBreakdown.Empty;

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(18);
                cols.RelativeColumn(3.3f);
                cols.RelativeColumn(1.1f);
                cols.RelativeColumn(1.1f);
                cols.RelativeColumn(1.2f);
            });

            IContainer ThCell(IContainer c) =>
                c.Border(0.5f).BorderColor(SlateBorderHex).Background("#f1f5f9").PaddingVertical(2).PaddingHorizontal(3);

            IContainer TdCell(IContainer c) =>
                c.Border(0.5f).BorderColor(SlateBorderHex).PaddingVertical(1.5f).PaddingHorizontal(3);

            table.Header(th =>
            {
                th.Cell().Element(ThCell).AlignCenter().Text("अ.क्र.").FontSize(6f).Bold();
                th.Cell().Element(ThCell).AlignLeft().Text("कराचा तपशील / Tax Head").FontSize(6f).Bold();
                th.Cell().Element(ThCell).AlignRight().Text("थकबाकी (₹)").FontSize(6f).Bold();
                th.Cell().Element(ThCell).AlignRight().Text("चालू कर (₹)").FontSize(6f).Bold();
                th.Cell().Element(ThCell).AlignRight().Text("एकूण वसूल (₹)").FontSize(6f).Bold().FontColor(accentHex);
            });

            int srNo = 0;
            void AddRow(string label, decimal arrVal, decimal curVal, bool highlightGreen = false)
            {
                srNo++;
                string color = highlightGreen ? "#047857" : "#0f172a";
                table.Cell().Element(TdCell).AlignCenter().Text(srNo.ToString(CultureInfo.InvariantCulture)).FontSize(5.9f).FontColor("#64748b");
                table.Cell().Element(TdCell).AlignLeft().Text(label).FontSize(5.9f).FontColor(color);
                table.Cell().Element(TdCell).AlignRight().Text(Fmt(arrVal)).FontSize(5.9f).FontColor(color);
                table.Cell().Element(TdCell).AlignRight().Text(Fmt(curVal)).FontSize(5.9f).FontColor(color);
                table.Cell().Element(TdCell).AlignRight().Text(Fmt(arrVal + curVal)).FontSize(5.9f).SemiBold().FontColor(color);
            }

            AddRow("इमारत कर (घरपट्टी) / House Tax", arr.HouseTax, cur.HouseTax);
            AddRow("दिवाबत्ती कर / Lighting Tax", arr.LightingTax, cur.LightingTax);
            AddRow("आरोग्य कर / Health Tax", arr.HealthTax, cur.HealthTax);
            AddRow("सफाई कर / Sanitation Tax", arr.SanitationTax, cur.SanitationTax);

            if (arr.ToiletTax + cur.ToiletTax > 0m)
            {
                AddRow("शौचालय कर / Toilet Tax", arr.ToiletTax, cur.ToiletTax);
            }

            AddRow("सामान्य पाणीपट्टी / General Water Tax", arr.GeneralWaterTax, cur.GeneralWaterTax);

            if (arr.SpecialWaterTax + cur.SpecialWaterTax > 0m)
            {
                AddRow("विशेष पाणीपट्टी / Special Water Tax", arr.SpecialWaterTax, cur.SpecialWaterTax);
            }

            if (arr.OtherTax + cur.OtherTax > 0m)
            {
                AddRow("इतर कर / Other Tax", arr.OtherTax, cur.OtherTax);
            }

            decimal currentFeesTotal = alloc.NoticeFeePaid + alloc.WarrantFeePaid + alloc.PenaltyPaid;
            if (arr.FeesAndPenalty + currentFeesTotal > 0m)
            {
                AddRow("नोटीस / वारंट / दंड शुल्क", arr.FeesAndPenalty, currentFeesTotal);
            }

            decimal rebateDisplay = Math.Max(model.DiscountGiven, alloc.RebateApplied);
            if (rebateDisplay > 0m)
            {
                srNo++;
                table.Cell().Element(TdCell).AlignCenter().Text(srNo.ToString(CultureInfo.InvariantCulture)).FontSize(5.9f).FontColor("#047857");
                table.Cell().Element(TdCell).AlignLeft().Text("आगाऊ भरणा सूट (-) / Early Rebate").FontSize(5.9f).FontColor("#047857");
                table.Cell().Element(TdCell).AlignRight().Text("0.00").FontSize(5.9f).FontColor("#047857");
                table.Cell().Element(TdCell).AlignRight().Text($"-{Fmt(rebateDisplay)}").FontSize(5.9f).FontColor("#047857");
                table.Cell().Element(TdCell).AlignRight().Text("माफ / Rebate").FontSize(5.5f).SemiBold().FontColor("#047857");
            }

            if (model.ExcessCredit > 0m)
            {
                AddRow("अतिरिक्त जमा (अग्रीम) / Excess Credit", 0m, model.ExcessCredit, highlightGreen: true);
            }

            decimal totalArrColumn = arr.Total;
            decimal totalCurColumn = Math.Max(0m, model.TotalPaid - totalArrColumn);

            IContainer TotCell(IContainer c) =>
                c.Border(0.7f).BorderColor(accentHex).Background(softBgHex).PaddingVertical(2.2f).PaddingHorizontal(3);

            table.Cell().ColumnSpan(2).Element(TotCell).AlignLeft().Text("एकूण जमा रक्कम (Grand Total Paid)")
                .FontSize(6.5f).Bold().FontColor(accentHex);
            table.Cell().Element(TotCell).AlignRight().Text(Fmt(totalArrColumn)).FontSize(6.5f).Bold();
            table.Cell().Element(TotCell).AlignRight().Text(Fmt(totalCurColumn)).FontSize(6.5f).Bold();
            table.Cell().Element(TotCell).AlignRight().Text($"₹{Fmt(model.TotalPaid)}").FontSize(7.2f).ExtraBold().FontColor(accentHex);
        });
    }

    private static string FormatPaymentModeMr(PaymentMode mode) => mode switch
    {
        PaymentMode.Cash => "रोख (Cash)",
        PaymentMode.Upi => "UPI QR / ऑनलाइन",
        PaymentMode.Cheque => "धनादेश / DD (Cheque)",
        PaymentMode.NeftRtgs => "NEFT / RTGS / IMPS",
        PaymentMode.OnlineGateway => "ऑनलाईन पेमेंट गेटवे",
        _ => mode.ToString(),
    };

    private static string Fmt(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);
}
