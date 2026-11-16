using System.Globalization;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Printing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GPMH.Infrastructure.Printing;

public sealed class Namuna8ExtractPdfDocument(
    IReadOnlyList<Namuna8ExtractPdfDocumentModel> extracts,
    IQrCodeGeneratorService qrCodeGenerator)
    : IDocument
{
    public DocumentMetadata GetMetadata() => new()
    {
        Title = "Namuna 8 Statutory Property Assessment Extract (Rule 32(1))",
        Author = "GPMH e-GramPanchayat SaaS",
        Subject = extracts.Count == 1
            ? $"Namuna 8 Extract - Ward {extracts[0].WardNo} / Property {extracts[0].PropertyNo}"
            : $"Namuna 8 Ward Register Extract ({extracts.Count} Properties)",
    };

    public void Compose(IDocumentContainer container)
    {
        DevanagariFontRegistry.EnsureInitialized();

        Namuna8ExtractPdfDocumentModel headerModel = extracts[0];
        bool isSingleExtract = extracts.Count == 1;

        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(8, Unit.Millimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(style => style
                .FontFamily(DevanagariFontRegistry.PrimaryFontFamily, DevanagariFontRegistry.FallbackFontFamily)
                .FontSize(7f)
                .FontColor(Colors.Black));

            page.Header().Column(col =>
            {
                col.Spacing(3);

                col.Item().Text(
                    "(टीप :- सदर मालमत्ता आकारणी नक्कल केवळ ग्रामपंचायत कर आकारणी बाबत असून इतर कोणत्याही हक्काबाबत हा दस्त कायदेशीर पुरावा सांगत नाही, नमूद मोजमापे तांत्रिक दृष्ट्या प्रमाणित तथा प्रमाणबद्ध केलेली नाहीत)")
                    .FontSize(5.8f)
                    .FontColor("#475569")
                    .AlignCenter();

                col.Item().Border(0.8f).BorderColor("#0f172a").Row(row =>
                {
                    row.RelativeItem(1.25f).BorderRight(0.8f).BorderColor("#0f172a").Padding(4).Column(left =>
                    {
                        left.Spacing(1);
                        left.Item().Text("महाराष्ट्र शासन • ग्रामविकास विभाग").FontSize(6.5f).Bold().FontColor("#0f6b61");
                        left.Item().Text($"ग्रामपंचायत: {headerModel.PanchayatNameMr}").FontSize(8f).Bold();
                        left.Item().Text($"गाव / प्रभाग: {headerModel.VillageNameMr} (वॉर्ड {headerModel.WardNo})").FontSize(6.8f);
                        left.Item().Text($"तालुका: {headerModel.TehsilName} | जि.: {headerModel.DistrictName}").FontSize(6.8f);
                        left.Item().Text($"LGD कोड: {headerModel.LgdCode} {(string.IsNullOrWhiteSpace(headerModel.Pincode) ? "" : $"| पिन: {headerModel.Pincode}")}").FontSize(6.3f).FontColor("#334155");
                    });

                    row.RelativeItem(2.35f).BorderRight(0.8f).BorderColor("#0f172a").Padding(4).AlignMiddle().Column(center =>
                    {
                        center.Spacing(1.5f);
                        center.Item().AlignCenter().Text("महाराष्ट्र ग्रामपंचायत लेखा संहिता २०११ — नियम ३२ (१)")
                            .FontSize(7.5f).Bold().FontColor("#0f6b61");
                        center.Item().AlignCenter().Text("नमुना ८ — कर आकारणी नोंदवही उतारा")
                            .FontSize(11f).Bold().FontColor("#0f172a");
                        center.Item().AlignCenter().Text(
                            $"सन {headerModel.RatePeriodName} या ४ वर्षांच्या कालावधीसाठी कर आकारणी | वर्ष: {headerModel.AssessmentYearLabel}")
                            .FontSize(7.2f);
                        center.Item().AlignCenter().Text(
                            $"दस्तऐवज क्र.: {headerModel.DocumentRefNo}   •   दिनांक: {headerModel.IssuedDateDisplay}   •   एकूण नोंदी: {extracts.Count}")
                            .FontSize(6.8f).Bold();
                    });

                    row.RelativeItem(0.9f).Padding(4).AlignCenter().AlignMiddle().Column(qr =>
                    {
                        qr.Item().AlignCenter().Width(48).Height(48)
                            .Image(qrCodeGenerator.GeneratePng(headerModel.VerifyUrl, 5))
                            .FitArea();
                        qr.Item().PaddingTop(1).AlignCenter().Text("पडताळणी QR").FontSize(6f).Bold().FontColor("#0f6b61");
                    });
                });
            });

            page.Content().PaddingTop(4).Column(content =>
            {
                content.Spacing(4);

                if (isSingleExtract)
                {
                    RenderSingleSummaryBoxes(content, headerModel);
                }

                content.Item().Element(c => RenderStatutory29ColumnTable(c, extracts));

                if (isSingleExtract)
                {
                    RenderRemarksAndBoundariesBox(content, headerModel);
                }

                content.Item().ShowEntire().PaddingTop(6).Element(c => RenderSignatureFooter(c, headerModel));
            });

            page.Footer().PaddingTop(3).Row(f =>
            {
                f.RelativeItem().Text($"GPMH e-GramPanchayat SaaS • ADR-017 Statutory A4 Landscape Extract • LGD {headerModel.LgdCode}")
                    .FontSize(6f).FontColor("#64748b");
                f.AutoItem().Text(text =>
                {
                    text.Span("पृष्ठ ").FontSize(6f).FontColor("#64748b");
                    text.CurrentPageNumber().FontSize(6f).Bold().FontColor("#0f172a");
                    text.Span(" / ").FontSize(6f).FontColor("#64748b");
                    text.TotalPages().FontSize(6f).Bold().FontColor("#0f172a");
                });
            });
        });
    }

    private static void RenderSingleSummaryBoxes(ColumnDescriptor col, Namuna8ExtractPdfDocumentModel m)
    {
        col.Item().Border(0.75f).BorderColor("#0f172a").Row(row =>
        {
            row.RelativeItem().BorderRight(0.75f).BorderColor("#cbd5e1").Padding(4).Column(b1 =>
            {
                b1.Item().Text("१. मालमत्ता व धारक तपशील").FontSize(6.8f).Bold().FontColor("#0f6b61");
                b1.Item().Text($"मिळकत क्र.: {m.PropertyNo} (प्रभाग {m.WardNo})").FontSize(6.5f);
                b1.Item().Text($"मालक: {m.OwnersSummaryMr}").FontSize(6.5f).Bold();
                b1.Item().Text($"भोगवटादार: {m.OccupierName}").FontSize(6.5f);
                b1.Item().Text($"आधार: {m.PrimaryOwnerAadhaarMasked ?? "-"}  |  मो.: {m.PrimaryOwnerMobileMasked ?? "-"}").FontSize(6.2f).FontColor("#475569");
            });

            row.RelativeItem().BorderRight(0.75f).BorderColor("#cbd5e1").Padding(4).Column(b2 =>
            {
                b2.Item().Text("२. बांधकाम व क्षेत्रफळ तपशील").FontSize(6.8f).Bold().FontColor("#0f6b61");
                b2.Item().Text($"रस्ता / गल्ली: {(string.IsNullOrWhiteSpace(m.StreetOrLaneName) ? "-" : m.StreetOrLaneName)}").FontSize(6.5f);
                b2.Item().Text($"वर्णन: {m.PropertyDescriptionMr}").FontSize(6.5f);
                b2.Item().Text($"बांधकाम वर्ष: {m.ConstructionYearsDisplay}").FontSize(6.5f);
                b2.Item().Text($"क्षेत्र: {FmtDec(m.TotalAreaSqFt)} चौ.फू. ({FmtDec(m.TotalAreaSqM)} चौ.मी.)").FontSize(6.5f).Bold();
            });

            row.RelativeItem().BorderRight(0.75f).BorderColor("#cbd5e1").Padding(4).Column(b3 =>
            {
                b3.Item().Text("३. नागरी सुविधा व स्थिती").FontSize(6.8f).Bold().FontColor("#0f6b61");
                b3.Item().Text($"नळ जोडणी: {m.WaterConnectionLabelMr}").FontSize(6.5f);
                b3.Item().Text($"कर माफी: {(m.IsTaxExempt ? "होय (सवलत लागू)" : "नाही")}").FontSize(6.5f);
                b3.Item().Text($"मिळकत स्थिती: {m.Status}").FontSize(6.5f);
                b3.Item().Text($"एकूण चालू कर: ₹ {FmtMoney(m.TotalAnnualTax)}").FontSize(7f).Bold().FontColor("#0f6b61");
            });

            row.RelativeItem().Padding(4).Column(b4 =>
            {
                b4.Item().Text("४. चतुर्सिमा (Boundaries)").FontSize(6.8f).Bold().FontColor("#0f6b61");
                b4.Item().Text($"पूर्व: {m.BoundaryEast ?? "-"}").FontSize(6.4f);
                b4.Item().Text($"पश्चिम: {m.BoundaryWest ?? "-"}").FontSize(6.4f);
                b4.Item().Text($"उत्तर: {m.BoundaryNorth ?? "-"}").FontSize(6.4f);
                b4.Item().Text($"दक्षिण: {m.BoundarySouth ?? "-"}").FontSize(6.4f);
            });
        });
    }

    private static void RenderRemarksAndBoundariesBox(ColumnDescriptor col, Namuna8ExtractPdfDocumentModel m)
    {
        col.Item().Border(0.75f).BorderColor("#0f172a").Background("#f8fafc").Padding(4).Column(box =>
        {
            box.Item().Text("शेरा / फेरफार / विशेष आदेश व भरणा सूचना:").FontSize(6.8f).Bold();
            box.Item().Text($"• {m.Remarks}").FontSize(6.4f);
            string upiLine = !string.IsNullOrWhiteSpace(m.UpiId)
                ? $"  |  UPI ID: {m.UpiId}"
                : string.Empty;
            box.Item().Text(
                $"• देय कर रक्कम रु. {FmtMoney(m.TotalAnnualTax)}/- विहित मुदतीत ग्रामपंचायत कार्यालयात अथवा ऑनलाईन प्रणालीद्वारे अदा करावी.{upiLine}")
                .FontSize(6.4f);
        });
    }

    private static void RenderStatutory29ColumnTable(IContainer container, IReadOnlyList<Namuna8ExtractPdfDocumentModel> rows)
    {
        float[] colWidths =
        [
            2.0f, // 1 SrNo
            4.5f, // 2 Street
            2.7f, // 3 Gut/Survey
            3.0f, // 4 PropNo
            6.5f, // 5 Owner
            5.0f, // 6 Occupier
            5.5f, // 7 Description
            2.8f, // 8 BuiltYear
            4.7f, // 9 Area SqFt / SqM
            2.6f, // 10 Open Land RR
            2.6f, // 11 Building Land RR
            2.8f, // 12 Construction RR
            2.5f, // 13 Depreciation %
            2.5f, // 14 Weightage
            4.2f, // 15 Capital Value
            2.5f, // 16 Tax Rate
            3.0f, // 17 House Tax
            2.8f, // 18 Lighting Tax
            2.8f, // 19 Health Tax
            2.8f, // 20 Water Tax
            2.8f, // 21 Sanitation Tax
            2.6f, // 22 Toilet Tax
            3.4f, // 23 Total Tax
            2.3f, // 24 Rev House
            2.3f, // 25 Rev Light
            2.3f, // 26 Rev Health
            2.3f, // 27 Rev Water
            2.5f, // 28 Rev Total
            5.5f, // 29 Remarks
        ];

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                foreach (float w in colWidths)
                {
                    cols.RelativeColumn(w);
                }
            });

            static IContainer HeaderCell(IContainer cell, string bg = "#f1f5f9") =>
                cell.Border(0.5f).BorderColor("#0f172a").Background(bg).Padding(2).AlignCenter().AlignMiddle();

            static IContainer NumCell(IContainer cell, string bg = "#e2e8f0") =>
                cell.Border(0.5f).BorderColor("#0f172a").Background(bg).PaddingVertical(1.5f).AlignCenter().AlignMiddle();

            static IContainer DataCell(IContainer cell, string bg = "#ffffff") =>
                cell.Border(0.5f).BorderColor("#0f172a").Background(bg).Padding(2).AlignMiddle();

            table.Header(header =>
            {
                // Row 1 (Columns 1..9 with RowSpan 2)
                string[] directHeaders1To9 =
                [
                    "१. अ.क्र.",
                    "२. रस्त्याचे नाव / गल्ली",
                    "३. गट / सर्व्हे क्र.",
                    "४. मिळकत क्र.",
                    "५. मालमत्ता धारकाचे नाव",
                    "६. भोगवटादाराचे नाव",
                    "७. मिळकतीचे वर्णन",
                    "८. बांधकाम वर्ष",
                    "९. क्षेत्रफळ चौ.फू. (चौ.मी.)",
                ];

                for (uint i = 0; i < directHeaders1To9.Length; i++)
                {
                    header.Cell().Row(1).Column(i + 1).RowSpan(2).Element(c => HeaderCell(c))
                        .Text(directHeaders1To9[i]).FontSize(5.5f).Bold();
                }

                // Columns 10..12 Group Header
                header.Cell().Row(1).Column(10).ColumnSpan(3).Element(c => HeaderCell(c))
                    .Text("रेडीरेकनर दर प्रती चौ.मी. (रू.)").FontSize(5.5f).Bold();

                // Columns 13..16 Direct Headers RowSpan 2
                header.Cell().Row(1).Column(13).RowSpan(2).Element(c => HeaderCell(c))
                    .Text("१३. घसारा %").FontSize(5.5f).Bold();
                header.Cell().Row(1).Column(14).RowSpan(2).Element(c => HeaderCell(c))
                    .Text("१४. भारांक").FontSize(5.5f).Bold();
                header.Cell().Row(1).Column(15).RowSpan(2).Element(c => HeaderCell(c))
                    .Text("१५. भांडवली मूल्य (रू.)").FontSize(5.5f).Bold();
                header.Cell().Row(1).Column(16).RowSpan(2).Element(c => HeaderCell(c))
                    .Text("१६. कर दर (पैसे)").FontSize(5.5f).Bold();

                // Columns 17..23 Group Header
                header.Cell().Row(1).Column(17).ColumnSpan(7).Element(c => HeaderCell(c))
                    .Text("कराची रक्कम (रुपये)").FontSize(5.5f).Bold();

                // Columns 24..28 Group Header
                header.Cell().Row(1).Column(24).ColumnSpan(5).Element(c => HeaderCell(c))
                    .Text("अपिलाचे निकाल व त्यानंतर केलेले फेरफार (रुपये)").FontSize(5.5f).Bold();

                // Column 29 Direct Header RowSpan 2
                header.Cell().Row(1).Column(29).RowSpan(2).Element(c => HeaderCell(c))
                    .Text("२९. शेरा / आदेश नोंद").FontSize(5.5f).Bold();

                // Row 2 Subheaders: 10..12
                string[] sub10To12 = ["१०. जमीन (खुली)", "११. इमारत", "१२. बांधकाम RR"];
                for (uint i = 0; i < sub10To12.Length; i++)
                {
                    header.Cell().Row(2).Column(10 + i).Element(c => HeaderCell(c))
                        .Text(sub10To12[i]).FontSize(5.3f).Bold();
                }

                // Row 2 Subheaders: 17..23
                string[] sub17To23 =
                [
                    "१७. घरपट्टी",
                    "१८. दिवाबत्ती",
                    "१९. आरोग्य कर",
                    "२०. पाणीपट्टी",
                    "२१. सफाई कर",
                    "२२. शौ. कर",
                    "२३. एकूण चालू कर",
                ];
                for (uint i = 0; i < sub17To23.Length; i++)
                {
                    string bg = i == 6 ? "#fef08a" : "#f1f5f9";
                    header.Cell().Row(2).Column(17 + i).Element(c => HeaderCell(c, bg))
                        .Text(sub17To23[i]).FontSize(5.3f).Bold();
                }

                // Row 2 Subheaders: 24..28
                string[] sub24To28 =
                [
                    "२४. घरपट्टी",
                    "२५. दिवाबत्ती",
                    "२६. आरोग्य कर",
                    "२७. पाणीपट्टी",
                    "२८. एकूण फेरफार",
                ];
                for (uint i = 0; i < sub24To28.Length; i++)
                {
                    header.Cell().Row(2).Column(24 + i).Element(c => HeaderCell(c))
                        .Text(sub24To28[i]).FontSize(5.3f).Bold();
                }

                // Row 3 Column Numbers १..२९
                for (uint cIdx = 1; cIdx <= 29; cIdx++)
                {
                    string bg = cIdx == 23 ? "#fef08a" : "#e2e8f0";
                    header.Cell().Row(3).Column(cIdx).Element(c => NumCell(c, bg))
                        .Text(cIdx.ToString(CultureInfo.InvariantCulture)).FontSize(5.8f).Bold();
                }
            });

            foreach (Namuna8ExtractPdfDocumentModel r in rows)
            {
                table.Cell().Element(c => DataCell(c)).AlignCenter().Text(r.SrNo.ToString(CultureInfo.InvariantCulture)).FontSize(6f);
                table.Cell().Element(c => DataCell(c)).Text(DashIfEmpty(r.StreetOrLaneName)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).AlignCenter().Text(DashIfEmpty(r.CitySurveyOrGutNo)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).AlignCenter().Text($"{r.WardNo}/{r.PropertyNo}").FontSize(6f).Bold();
                table.Cell().Element(c => DataCell(c)).Text(r.OwnersSummaryMr).FontSize(6f).Bold();
                table.Cell().Element(c => DataCell(c)).Text(DashIfEmpty(r.OccupierName)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).Text(DashIfEmpty(r.PropertyDescriptionMr)).FontSize(5.6f);
                table.Cell().Element(c => DataCell(c)).AlignCenter().Text(DashIfEmpty(r.ConstructionYearsDisplay)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text($"{FmtDec(r.TotalAreaSqFt)} sq.ft\n({FmtDec(r.TotalAreaSqM)} sq.m)").FontSize(5.6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoneyOrDash(r.OpenLandRrRate)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoneyOrDash(r.BuildingLandRate)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoneyOrDash(r.ConstructionRrRate)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).AlignCenter().Text(DashIfEmpty(r.DepreciationPercentDisplay)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).AlignCenter().Text(DashIfEmpty(r.UsageWeightageDisplay)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoney(r.TotalCapitalValue)).FontSize(5.8f).Bold();
                table.Cell().Element(c => DataCell(c)).AlignCenter().Text(DashIfEmpty(r.TaxRateDisplay)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoney(r.BuildingTaxComponent)).FontSize(6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoney(r.LightingTax)).FontSize(6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoney(r.HealthTax)).FontSize(6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoney(r.WaterTax)).FontSize(6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoney(r.SanitationTax)).FontSize(6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtMoney(r.ToiletTax)).FontSize(6f);
                table.Cell().Element(c => DataCell(c, "#fef9c3")).AlignRight().Text(FmtMoney(r.TotalAnnualTax)).FontSize(6.4f).Bold();
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtNullable(r.RevisionHouseTax)).FontSize(5.6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtNullable(r.RevisionLightingTax)).FontSize(5.6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtNullable(r.RevisionHealthTax)).FontSize(5.6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtNullable(r.RevisionWaterTax)).FontSize(5.6f);
                table.Cell().Element(c => DataCell(c)).AlignRight().Text(FmtNullable(r.RevisionTotalTax)).FontSize(5.8f);
                table.Cell().Element(c => DataCell(c)).Text(DashIfEmpty(r.Remarks)).FontSize(5.5f);
            }

            if (rows.Count > 1)
            {
                table.Cell().ColumnSpan(16).Element(c => DataCell(c, "#f8fafc")).AlignRight()
                    .Text($"एकूण ({rows.Count} मिळकती):").FontSize(6.5f).Bold();
                table.Cell().Element(c => DataCell(c, "#f8fafc")).AlignRight().Text(FmtMoney(rows.Sum(x => x.BuildingTaxComponent))).FontSize(6f).Bold();
                table.Cell().Element(c => DataCell(c, "#f8fafc")).AlignRight().Text(FmtMoney(rows.Sum(x => x.LightingTax))).FontSize(6f).Bold();
                table.Cell().Element(c => DataCell(c, "#f8fafc")).AlignRight().Text(FmtMoney(rows.Sum(x => x.HealthTax))).FontSize(6f).Bold();
                table.Cell().Element(c => DataCell(c, "#f8fafc")).AlignRight().Text(FmtMoney(rows.Sum(x => x.WaterTax))).FontSize(6f).Bold();
                table.Cell().Element(c => DataCell(c, "#f8fafc")).AlignRight().Text(FmtMoney(rows.Sum(x => x.SanitationTax))).FontSize(6f).Bold();
                table.Cell().Element(c => DataCell(c, "#f8fafc")).AlignRight().Text(FmtMoney(rows.Sum(x => x.ToiletTax))).FontSize(6f).Bold();
                table.Cell().Element(c => DataCell(c, "#fef08a")).AlignRight().Text(FmtMoney(rows.Sum(x => x.TotalAnnualTax))).FontSize(6.5f).Bold();
                table.Cell().ColumnSpan(6).Element(c => DataCell(c, "#f8fafc")).Text(string.Empty);
            }
        });
    }

    private static void RenderSignatureFooter(IContainer container, Namuna8ExtractPdfDocumentModel m)
    {
        container.Row(row =>
        {
            row.Spacing(12);

            RenderSignerBox(row.RelativeItem(), "कर संग्राहक / लिपिक", m.OperatorName, m.OperatorSignatureBytes, m.PanchayatNameMr);
            RenderStampBox(row.RelativeItem(0.8f), m.OfficialStampBytes, m.PanchayatNameMr);
            RenderSignerBox(row.RelativeItem(), "सरपंच", m.SarpanchName, m.SarpanchSignatureBytes, m.PanchayatNameMr);
            RenderSignerBox(row.RelativeItem(), "ग्रामसेवक / ग्रामपंचायत अधिकारी", m.GramsevakName, m.GramsevakSignatureBytes, m.PanchayatNameMr);
        });
    }

    private static void RenderSignerBox(
        IContainer container,
        string roleTitle,
        string? name,
        byte[]? sigBytes,
        string gpName)
    {
        container.Border(0.6f).BorderColor("#cbd5e1").Padding(4).Column(c =>
        {
            c.Item().Height(26).AlignCenter().AlignMiddle().Element(imgSlot =>
            {
                if (sigBytes is { Length: > 0 })
                {
                    imgSlot.Image(sigBytes).FitArea();
                }
                else
                {
                    imgSlot.Text("____________________").FontSize(7f).FontColor("#94a3b8");
                }
            });
            c.Item().AlignCenter().Text(roleTitle).FontSize(6.8f).Bold();
            if (!string.IsNullOrWhiteSpace(name))
            {
                c.Item().AlignCenter().Text(name).FontSize(6.3f);
            }
            c.Item().AlignCenter().Text($"ग्रा.पं. {gpName}").FontSize(5.8f).FontColor("#475569");
        });
    }

    private static void RenderStampBox(IContainer container, byte[]? stampBytes, string gpName)
    {
        container.Border(0.6f).BorderColor("#cbd5e1").Padding(4).AlignCenter().AlignMiddle().Column(c =>
        {
            c.Item().Height(28).AlignCenter().AlignMiddle().Element(slot =>
            {
                if (stampBytes is { Length: > 0 })
                {
                    slot.Image(stampBytes).FitArea();
                }
                else
                {
                    slot.Text("[ ग्रामपंचायत शिक्का ]").FontSize(6.5f).FontColor("#64748b");
                }
            });
            c.Item().AlignCenter().Text($"अधिकृत मुद्रा • ग्रा.पं. {gpName}").FontSize(5.6f).FontColor("#475569");
        });
    }

    private static string DashIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? "-" : s;

    private static string FmtDec(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FmtMoney(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);

    private static string FmtMoneyOrDash(decimal value) => value <= 0m ? "-" : Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);

    private static string FmtNullable(decimal? value) => value.HasValue ? FmtMoney(value.Value) : "-";
}
