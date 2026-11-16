using GPMH.Application.DTOs.Mutations;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GPMH.Infrastructure.Printing;

public sealed class MutationRegisterPdfDocument(MutationRegisterPdfDocumentModel model) : IDocument
{
    public DocumentMetadata GetMetadata() => new()
    {
        Title = "Namuna 8 Property Mutation Register (फेरफार नोंदवही)",
        Author = "GPMH e-GramPanchayat SaaS",
    };

    public void Compose(IDocumentContainer container)
    {
        DevanagariFontRegistry.EnsureInitialized();

        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(8, Unit.Millimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(style => style
                .FontFamily(DevanagariFontRegistry.PrimaryFontFamily, DevanagariFontRegistry.FallbackFontFamily)
                .FontSize(7.5f)
                .FontColor(Colors.Black));

            page.Header().Column(col =>
            {
                col.Spacing(4);
                col.Item().AlignCenter().Text("मिळकतीच्या फेरफाराचे रजिस्टर (Mutation Register)")
                    .FontSize(13f).Bold().FontColor("#0f172a");

                col.Item().Border(1f).BorderColor("#000000").Row(row =>
                {
                    row.RelativeItem(2).BorderRight(1f).BorderColor("#000000").Padding(4).Column(left =>
                    {
                        left.Item().Text($"ग्रामपंचायत: {model.PanchayatNameMr}").FontSize(8f).Bold();
                        left.Item().Text($"गाव: {model.VillageNameMr} | LGD कोड: {model.LgdCode}").FontSize(7.2f);
                    });
                    row.RelativeItem(2).BorderRight(1f).BorderColor("#000000").Padding(4).Column(mid =>
                    {
                        mid.Item().Text($"तालुका: {model.TehsilName}").FontSize(7.5f).Bold();
                        mid.Item().Text($"जिल्हा: {model.DistrictName}").FontSize(7.5f).Bold();
                    });
                    row.RelativeItem(1).Padding(4).AlignRight().Column(right =>
                    {
                        right.Item().Text($"आर्थिक वर्ष: {model.AssessmentYearLabel}").FontSize(7.2f).Bold();
                        right.Item().Text(string.IsNullOrWhiteSpace(model.WardFilterLabel)
                            ? $"एकूण फेरफार नोंदी: {model.Rows.Count}"
                            : $"वॉर्ड: {model.WardFilterLabel} • नोंदी: {model.Rows.Count}")
                            .FontSize(7.2f);
                    });
                });
            });

            page.Content().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1.8f); // 1 AppNo
                    cols.RelativeColumn(1.4f); // 2 PropNo
                    cols.RelativeColumn(1.8f); // 3 Date
                    cols.RelativeColumn(1.9f); // 4 DocName
                    cols.RelativeColumn(1.5f); // 5 DocNo
                    cols.RelativeColumn(2.4f); // 6 PropDesc
                    cols.RelativeColumn(3.2f); // 7 Old Owners
                    cols.RelativeColumn(3.2f); // 8 New Owners
                    cols.RelativeColumn(4.0f); // 9 Summary
                    cols.RelativeColumn(3.0f); // 10 Resolution
                    cols.RelativeColumn(2.2f); // 11 Officer Signature
                });

                static IContainer HeaderBox(IContainer c) =>
                    c.Border(0.6f).BorderColor("#000000").Background("#f1f5f9").Padding(2.5f).AlignCenter().AlignMiddle();

                static IContainer DataBox(IContainer c) =>
                    c.Border(0.5f).BorderColor("#000000").Padding(2.5f).AlignMiddle();

                table.Header(h =>
                {
                    h.Cell().Row(1).Column(1).RowSpan(2).Element(HeaderBox).Text("फेरफार अ.क्र.").FontSize(6.5f).Bold();
                    h.Cell().Row(1).Column(2).RowSpan(2).Element(HeaderBox).Text("मालमत्ता क्र.").FontSize(6.5f).Bold();
                    h.Cell().Row(1).Column(3).RowSpan(2).Element(HeaderBox).Text("तारीख").FontSize(6.5f).Bold();
                    h.Cell().Row(1).Column(4).ColumnSpan(2).Element(HeaderBox).Text("मिळकतीचा बदल तपशील").FontSize(6.5f).Bold();
                    h.Cell().Row(1).Column(6).RowSpan(2).Element(HeaderBox).Text("मिळकतीचे वर्णन").FontSize(6.5f).Bold();
                    h.Cell().Row(1).Column(7).RowSpan(2).Element(HeaderBox).Text("मूळ मालकाचे नाव").FontSize(6.5f).Bold();
                    h.Cell().Row(1).Column(8).RowSpan(2).Element(HeaderBox).Text("नवीन मालकाचे नाव").FontSize(6.5f).Bold();
                    h.Cell().Row(1).Column(9).RowSpan(2).Element(HeaderBox).Text("ज्या कागदपत्राच्या आधारे फेरफार करण्यात आली त्याचे थोडक्यात टिपण").FontSize(6.2f).Bold();
                    h.Cell().Row(1).Column(10).RowSpan(2).Element(HeaderBox).Text("ठराव क्र. व दिनांक / स्थिती").FontSize(6.4f).Bold();
                    h.Cell().Row(1).Column(11).RowSpan(2).Element(HeaderBox).Text("ग्रामपंचायत अधिकारी सही").FontSize(6.4f).Bold();

                    h.Cell().Row(2).Column(4).Element(HeaderBox).Text("दस्तऐवजाचे नाव").FontSize(6.2f).Bold();
                    h.Cell().Row(2).Column(5).Element(HeaderBox).Text("दस्त क्र.").FontSize(6.2f).Bold();
                });

                foreach (MutationRegisterPdfRow r in model.Rows)
                {
                    table.Cell().Element(DataBox).AlignCenter().Text(r.ApplicationNo).FontSize(6.5f).Bold();
                    table.Cell().Element(DataBox).AlignCenter().Text(r.PropertyNoDisplay).FontSize(6.5f).Bold();
                    table.Cell().Element(DataBox).AlignCenter().Text(r.EntryDateDisplay).FontSize(6.5f);
                    table.Cell().Element(DataBox).AlignCenter().Text(r.MutationTypeLabelMr).FontSize(6.5f);
                    table.Cell().Element(DataBox).AlignCenter().Text(string.IsNullOrWhiteSpace(r.DocumentReferenceNo) ? "-" : r.DocumentReferenceNo).FontSize(6.5f);
                    table.Cell().Element(DataBox).Text(r.PropertyDescriptionMr).FontSize(6.2f);
                    table.Cell().Element(DataBox).Text(r.PreviousOwnersMr).FontSize(6.5f);
                    table.Cell().Element(DataBox).Text(r.ProposedOwnersMr).FontSize(6.5f).Bold();
                    table.Cell().Element(DataBox).Text(r.BriefSummaryMr).FontSize(6.2f);
                    table.Cell().Element(DataBox).AlignCenter().Text(r.ResolutionDisplay).FontSize(6.3f);
                    table.Cell().Element(DataBox).AlignCenter().AlignMiddle().Height(22).Element(sigSlot =>
                    {
                        if (model.GramsevakSignatureBytes is { Length: > 0 })
                        {
                            sigSlot.Image(model.GramsevakSignatureBytes).FitArea();
                        }
                        else
                        {
                            sigSlot.Text(string.Empty);
                        }
                    });
                }

                // Pad standard register blank rows up to minimum 8 rows
                int padCount = Math.Max(0, 8 - model.Rows.Count);
                for (int i = 0; i < padCount; i++)
                {
                    for (int col = 0; col < 11; col++)
                    {
                        table.Cell().Element(DataBox).Height(18).Text(string.Empty);
                    }
                }
            });

            page.Footer().PaddingTop(4).Row(f =>
            {
                f.RelativeItem().Text($"© {DateTime.UtcNow.Year} GPMH e-GramPanchayat ERP • A4 Landscape Statutory Mutation Register").FontSize(6.5f).FontColor("#64748b");
                f.AutoItem().Text(text =>
                {
                    text.Span("पान क्र. ").FontSize(6.5f);
                    text.CurrentPageNumber().FontSize(6.5f).Bold();
                    text.Span(" / ").FontSize(6.5f);
                    text.TotalPages().FontSize(6.5f).Bold();
                });
            });
        });
    }
}

