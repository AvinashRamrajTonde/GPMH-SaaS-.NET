using System.Globalization;
using GPMH.Application.DTOs.Namuna9;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GPMH.Infrastructure.Printing;

public sealed class Namuna9RegisterPdfDocument(Namuna9RegisterPdfDocumentModel model) : IDocument
{
    private static readonly TimeZoneInfo IstTimeZone = ResolveIstTimeZone();

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Namuna 9 Demand & Recovery Register — FY {model.Workspace.SelectedYear.YearLabel} (Format {model.Format})",
        Author = "GPMH e-GramPanchayat SaaS",
        Subject = $"Format {model.Format} ({model.RegisterPart}) — {model.Header.PanchayatNameEn}",
    };

    public void Compose(IDocumentContainer container)
    {
        DevanagariFontRegistry.EnsureInitialized();

        bool isColor = string.Equals(model.Style, "color", StringComparison.OrdinalIgnoreCase);
        Palette palette = isColor ? Palette.Color : Palette.BlackAndWhite;
        IReadOnlyList<TaxHeadDescriptor> heads = GetHeadsForFormat(model.Format);
        List<PreparedRow> prepared = [.. model.Workspace.Items.Select((row, idx) => PrepareRow(idx + 1, row, model.Format))];

        bool renderDemand = model.RegisterPart is "Demand" or "Both";
        bool renderRecovery = model.RegisterPart is "Recovery" or "Both";

        if (renderDemand)
        {
            ComposeDemandPage(container, palette, heads, prepared);
        }

        if (renderRecovery)
        {
            ComposeRecoveryPage(container, palette, heads, prepared);
        }
    }

    private void ComposeDemandPage(
        IDocumentContainer container,
        Palette palette,
        IReadOnlyList<TaxHeadDescriptor> heads,
        IReadOnlyList<PreparedRow> rows)
    {
        float leftMargin = model.LeftMarginMm <= 0 ? 5f : Math.Clamp(model.LeftMarginMm, 4, 30);
        float fontBase = model.Format == "C" ? 5.2f : model.Format == "A" ? 5.8f : 6.5f;
        float headerFont = model.Format == "C" ? 4.8f : model.Format == "A" ? 5.3f : 6.0f;

        container.Page(page =>
        {
            ConfigurePage(page, leftMargin);

            string titleMr = model.Format switch
            {
                "A" => "सामान्य कर मागणी नोंदवही",
                "B" => "पाणीपट्टी मागणी नोंदवही",
                _ => "एकत्रित मागणी नोंदवही",
            };
            string subtitleMr = $"सन {model.Workspace.SelectedYear.YearLabel} या आर्थिक वर्षी आकारणी केलेल्या करांची मागणी नोंदवही";

            page.Header().Column(col => ComposeHeaderBlock(col, palette, titleMr, subtitleMr, showVillage: true));

            page.Content().PaddingTop(3).Column(content =>
            {
                content.Spacing(3);

                content.Item().Table(table =>
                {
                    int headSubCols = heads.Count * 3;
                    int totalCols = 4 + headSubCols + 5 + 1;

                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(0.8f); // SrNo
                        cols.RelativeColumn(0.8f); // Ward
                        cols.RelativeColumn(1.5f); // Property No
                        cols.RelativeColumn(model.Format == "C" ? 4.4f : 5.4f); // Owner Name

                        for (int i = 0; i < headSubCols; i++)
                        {
                            cols.RelativeColumn(1f);
                        }

                        cols.RelativeColumn(1f);   // Notice
                        cols.RelativeColumn(1f);   // Warrant
                        cols.RelativeColumn(1f);   // Penalty
                        cols.RelativeColumn(1f);   // Rebate
                        cols.RelativeColumn(1.35f); // Net Demand
                        cols.RelativeColumn(model.Format == "C" ? 2.2f : 2.8f); // Receipt ref
                    });

                    table.Header(h =>
                    {
                        uint demandSpan = (uint)(headSubCols + 5);
                        uint receiptCol = (uint)totalCols;

                        h.Cell().Row(1).Column(1).RowSpan(3).Element(c => Cell(c, palette.DarkBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text("अ.क्र.").FontSize(headerFont).Bold().FontColor(palette.DarkFg);
                        h.Cell().Row(1).Column(2).RowSpan(3).Element(c => Cell(c, palette.DarkBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text("वॉर्ड").FontSize(headerFont).Bold().FontColor(palette.DarkFg);
                        h.Cell().Row(1).Column(3).RowSpan(3).Element(c => Cell(c, palette.DarkBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text("मिळकत क्र.").FontSize(headerFont).Bold().FontColor(palette.DarkFg);
                        h.Cell().Row(1).Column(4).RowSpan(3).Element(c => Cell(c, palette.DarkBg, palette.GridBorder, 2f).AlignCenter().AlignMiddle())
                            .Text("मालमत्ता धारकाचे नाव").FontSize(headerFont).Bold().FontColor(palette.DarkFg);

                        h.Cell().Row(1).Column(5).ColumnSpan(demandSpan).Element(c => Cell(c, palette.DarkBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text("मागणी (रुपये)").FontSize(headerFont + 0.5f).Bold().FontColor(palette.DarkFg);

                        h.Cell().Row(1).Column(receiptCol).RowSpan(3).Element(c => Cell(c, palette.DarkBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text("पावती क्रमांक व तारीख").FontSize(headerFont).Bold().FontColor(palette.DarkFg);

                        uint colCursor = 5;
                        foreach (TaxHeadDescriptor head in heads)
                        {
                            h.Cell().Row(2).Column(colCursor).ColumnSpan(3)
                                .Element(c => Cell(c, palette.LtBg, palette.GridBorder, 1.2f).AlignCenter().AlignMiddle())
                                .Text(head.LabelMr).FontSize(headerFont).Bold().FontColor(palette.LtFg);

                            h.Cell().Row(3).Column(colCursor).Element(c => Cell(c, palette.SubBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                                .Text("मा.").FontSize(headerFont - 0.3f).Bold().FontColor(palette.SubFg);
                            h.Cell().Row(3).Column(colCursor + 1).Element(c => Cell(c, palette.SubBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                                .Text("चा.").FontSize(headerFont - 0.3f).Bold().FontColor(palette.SubFg);
                            h.Cell().Row(3).Column(colCursor + 2).Element(c => Cell(c, palette.SubBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                                .Text("ए").FontSize(headerFont - 0.3f).Bold().FontColor(palette.SubFg);

                            colCursor += 3;
                        }

                        string[] feeLabels = ["नोटीस फी", "वारंट फी", "दंड", "सूट"];
                        foreach (string lbl in feeLabels)
                        {
                            h.Cell().Row(2).Column(colCursor).RowSpan(2)
                                .Element(c => Cell(c, palette.FeeBg, palette.GridBorder, 1.2f).AlignCenter().AlignMiddle())
                                .Text(lbl).FontSize(headerFont).Bold().FontColor(palette.FeeFg);
                            colCursor++;
                        }

                        h.Cell().Row(2).Column(colCursor).RowSpan(2)
                            .Element(c => Cell(c, palette.GtHeadBg, palette.GridBorder, 1.2f).AlignCenter().AlignMiddle())
                            .Text("एकूण मागणी").FontSize(headerFont + 0.2f).Bold().FontColor(palette.GtHeadFg);
                    });

                    for (int rIdx = 0; rIdx < rows.Count; rIdx++)
                    {
                        PreparedRow pr = rows[rIdx];
                        string rowBg = rIdx % 2 == 1 ? palette.AltRowBg : "#ffffff";

                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text(pr.SrNo.ToString(CultureInfo.InvariantCulture)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text(pr.Row.WardNo).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text(pr.Row.PropertyNo).FontSize(fontBase).Bold();
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 2f).AlignLeft().AlignMiddle())
                            .Text(pr.OwnerNameMr).FontSize(fontBase).SemiBold();

                        foreach (TaxHeadDescriptor head in heads)
                        {
                            HeadCellValues v = head.Selector(pr);
                            table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1f).AlignRight().AlignMiddle())
                                .Text(Fmt(v.DemandArrears)).FontSize(fontBase);
                            table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1f).AlignRight().AlignMiddle())
                                .Text(Fmt(v.DemandCurrent)).FontSize(fontBase);
                            table.Cell().Element(c => Cell(c, palette.RowTotalBg, palette.GridBorder, 1f).AlignRight().AlignMiddle())
                                .Text(Fmt(v.DemandTotal)).FontSize(fontBase).Bold();
                        }

                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.NoticeFee)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.WarrantFee)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.Penalty)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.Rebate)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.GridBorder, 1.2f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.FormatDemand)).FontSize(fontBase).Bold();

                        string receiptDisplay = FormatReceiptRef(pr.Row);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text(receiptDisplay).FontSize(fontBase - 0.5f).FontColor("#475569");
                    }

                    int padCount = Math.Max(0, 8 - rows.Count);
                    for (int p = 0; p < padCount; p++)
                    {
                        for (int cIdx = 0; cIdx < totalCols; cIdx++)
                        {
                            table.Cell().Element(c => Cell(c, "#ffffff", palette.GridBorder, 1f).Height(13))
                                .Text(string.Empty);
                        }
                    }

                    // Page Totals row
                    table.Cell().ColumnSpan(4).Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 2f).AlignCenter().AlignMiddle())
                        .Text("या नोंदवहीची एकूण बेरीज").FontSize(fontBase + 0.3f).Bold();

                    foreach (TaxHeadDescriptor head in heads)
                    {
                        decimal sArr = rows.Sum(r => head.Selector(r).DemandArrears);
                        decimal sCur = rows.Sum(r => head.Selector(r).DemandCurrent);
                        decimal sTot = rows.Sum(r => head.Selector(r).DemandTotal);
                        table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignRight().AlignMiddle())
                            .Text(Fmt(sArr)).FontSize(fontBase).Bold();
                        table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignRight().AlignMiddle())
                            .Text(Fmt(sCur)).FontSize(fontBase).Bold();
                        table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignRight().AlignMiddle())
                            .Text(Fmt(sTot)).FontSize(fontBase).Bold();
                    }

                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.NoticeFee))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.WarrantFee))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.Penalty))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.Rebate))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1.2f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.FormatDemand))).FontSize(fontBase + 0.2f).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignCenter().AlignMiddle())
                        .Text($"{rows.Count} नोंदी").FontSize(fontBase - 0.3f).Bold();
                });

                // Head-wise Summary Bar
                content.Item().Border(0.7f).BorderColor(palette.MetaBorder).Background(palette.MetaBg).Padding(4).Column(sum =>
                {
                    sum.Spacing(2);
                    sum.Item().Text("नोंदवहीची करनिहाय एकूण मागणी बेरीज").FontSize(6.8f).Bold().FontColor(palette.LtFg);
                    sum.Item().Row(chips =>
                    {
                        chips.Spacing(6);
                        foreach (TaxHeadDescriptor head in heads)
                        {
                            decimal headSum = rows.Sum(r => head.Selector(r).DemandTotal);
                            chips.AutoItem().Text($"{head.ShortMr}: ₹{Fmt(headSum)}").FontSize(6.3f).SemiBold();
                        }

                        if (model.Format is "A" or "C")
                        {
                            chips.AutoItem().Text($"नोटीस: ₹{Fmt(rows.Sum(r => r.NoticeFee))}").FontSize(6.3f);
                            chips.AutoItem().Text($"वारंट: ₹{Fmt(rows.Sum(r => r.WarrantFee))}").FontSize(6.3f);
                            chips.AutoItem().Text($"दंड: ₹{Fmt(rows.Sum(r => r.Penalty))}").FontSize(6.3f);
                            chips.AutoItem().Text($"सूट: ₹{Fmt(rows.Sum(r => r.Rebate))}").FontSize(6.3f);
                        }

                        chips.RelativeItem().AlignRight()
                            .Text($"एकूण मागणी: ₹{Fmt(rows.Sum(r => r.FormatDemand))}")
                            .FontSize(7f).Bold().FontColor(palette.DarkBg);
                    });
                });
            });

            page.Footer().Column(col => ComposeFooterBlock(col, palette));
        });
    }

    private void ComposeRecoveryPage(
        IDocumentContainer container,
        Palette palette,
        IReadOnlyList<TaxHeadDescriptor> heads,
        IReadOnlyList<PreparedRow> rows)
    {
        float leftMargin = model.LeftMarginMm <= 0 ? 5f : Math.Clamp(model.LeftMarginMm, 4, 30);
        float fontBase = model.Format == "C" ? 4.8f : model.Format == "A" ? 5.4f : 6.2f;
        float headerFont = model.Format == "C" ? 4.5f : model.Format == "A" ? 5.0f : 5.8f;

        container.Page(page =>
        {
            ConfigurePage(page, leftMargin);

            string titleMr = model.Format switch
            {
                "A" => "सामान्य कर वसुली व बाकी नोंदवही",
                "B" => "पाणीपट्टी वसुली व बाकी नोंदवही",
                _ => "एकत्रित कर वसुली व बाकी नोंदवही",
            };
            string subtitleMr = $"सन {model.Workspace.SelectedYear.YearLabel} या वर्षी आकारणी केलेल्या करांची वसुली, मंजूर सूट व थकबाकी नोंदवही";

            page.Header().Column(col => ComposeHeaderBlock(col, palette, titleMr, subtitleMr, showVillage: false));

            page.Content().PaddingTop(3).Column(content =>
            {
                content.Spacing(3);

                content.Item().Table(table =>
                {
                    int vasuliCols = (heads.Count * 2) + 4; // Ma/Cha per head + Notice + Warrant + Penalty + Total Collected
                    int rebateCols = heads.Count;
                    int balanceCols = heads.Count + 4;      // Per head + Notice + Warrant + Penalty + Total Balance
                    int totalCols = 1 + vasuliCols + rebateCols + balanceCols;

                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.65f); // Ward/Property No

                        for (int i = 0; i < heads.Count * 2; i++)
                        {
                            cols.RelativeColumn(1f);
                        }

                        cols.RelativeColumn(1f);    // Notice Coll
                        cols.RelativeColumn(1f);    // Warrant Coll
                        cols.RelativeColumn(1f);    // Penalty Coll
                        cols.RelativeColumn(1.35f); // Total Collected

                        for (int i = 0; i < rebateCols; i++)
                        {
                            cols.RelativeColumn(1f);
                        }

                        for (int i = 0; i < heads.Count; i++)
                        {
                            cols.RelativeColumn(1f);
                        }

                        cols.RelativeColumn(1f);    // Notice Bal
                        cols.RelativeColumn(1f);    // Warrant Bal
                        cols.RelativeColumn(1f);    // Penalty Bal
                        cols.RelativeColumn(1.35f); // Total Balance
                    });

                    table.Header(h =>
                    {
                        uint vasuliStart = 2;
                        uint rebateStart = vasuliStart + (uint)vasuliCols;
                        uint balanceStart = rebateStart + (uint)rebateCols;

                        h.Cell().Row(1).Column(1).RowSpan(3)
                            .Element(c => Cell(c, palette.DarkBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text("वॉर्ड / मिळकत क्र.").FontSize(headerFont).Bold().FontColor(palette.DarkFg);

                        h.Cell().Row(1).Column(vasuliStart).ColumnSpan((uint)vasuliCols)
                            .Element(c => Cell(c, palette.GroupRecoveryBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text("वसुली (Recovered)").FontSize(headerFont + 0.5f).Bold().FontColor(palette.GroupFg);

                        h.Cell().Row(1).Column(rebateStart).ColumnSpan((uint)rebateCols)
                            .Element(c => Cell(c, palette.GroupRebateBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text("मंजूर सूट").FontSize(headerFont + 0.5f).Bold().FontColor(palette.GroupFg);

                        h.Cell().Row(1).Column(balanceStart).ColumnSpan((uint)balanceCols)
                            .Element(c => Cell(c, palette.GroupBalanceBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text("बाकी (Balance Dues)").FontSize(headerFont + 0.5f).Bold().FontColor(palette.GroupFg);

                        // Row 2-3 Vasuli headers
                        uint cVasuli = vasuliStart;
                        foreach (TaxHeadDescriptor head in heads)
                        {
                            h.Cell().Row(2).Column(cVasuli).ColumnSpan(2)
                                .Element(c => Cell(c, palette.LtBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                                .Text(head.ShortMr).FontSize(headerFont).Bold().FontColor(palette.LtFg);
                            h.Cell().Row(3).Column(cVasuli)
                                .Element(c => Cell(c, palette.SubBg, palette.GridBorder, 0.8f).AlignCenter().AlignMiddle())
                                .Text("मा.").FontSize(headerFont - 0.3f).Bold().FontColor(palette.SubFg);
                            h.Cell().Row(3).Column(cVasuli + 1)
                                .Element(c => Cell(c, palette.SubBg, palette.GridBorder, 0.8f).AlignCenter().AlignMiddle())
                                .Text("चा.").FontSize(headerFont - 0.3f).Bold().FontColor(palette.SubFg);
                            cVasuli += 2;
                        }

                        string[] collFees = ["नोटीस", "वारंट", "दंड"];
                        foreach (string lbl in collFees)
                        {
                            h.Cell().Row(2).Column(cVasuli).RowSpan(2)
                                .Element(c => Cell(c, palette.FeeBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                                .Text(lbl).FontSize(headerFont).Bold().FontColor(palette.FeeFg);
                            cVasuli++;
                        }

                        h.Cell().Row(2).Column(cVasuli).RowSpan(2)
                            .Element(c => Cell(c, palette.DarkBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                            .Text("एकूण वसुली").FontSize(headerFont).Bold().FontColor(palette.DarkFg);

                        // Row 2-3 Rebate headers
                        uint cRebate = rebateStart;
                        foreach (TaxHeadDescriptor head in heads)
                        {
                            h.Cell().Row(2).Column(cRebate).RowSpan(2)
                                .Element(c => Cell(c, palette.RebateHeadBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                                .Text(head.ShortMr).FontSize(headerFont - 0.2f).Bold().FontColor(palette.RebateHeadFg);
                            cRebate++;
                        }

                        // Row 2-3 Balance headers
                        uint cBal = balanceStart;
                        foreach (TaxHeadDescriptor head in heads)
                        {
                            h.Cell().Row(2).Column(cBal).RowSpan(2)
                                .Element(c => Cell(c, palette.BalanceHeadBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                                .Text(head.ShortMr).FontSize(headerFont - 0.2f).Bold().FontColor(palette.BalanceHeadFg);
                            cBal++;
                        }

                        foreach (string lbl in collFees)
                        {
                            h.Cell().Row(2).Column(cBal).RowSpan(2)
                                .Element(c => Cell(c, palette.FeeBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                                .Text(lbl).FontSize(headerFont).Bold().FontColor(palette.FeeFg);
                            cBal++;
                        }

                        h.Cell().Row(2).Column(cBal).RowSpan(2)
                            .Element(c => Cell(c, palette.DarkBg, palette.GridBorder, 1f).AlignCenter().AlignMiddle())
                            .Text("एकूण बाकी").FontSize(headerFont).Bold().FontColor(palette.DarkFg);
                    });

                    for (int rIdx = 0; rIdx < rows.Count; rIdx++)
                    {
                        PreparedRow pr = rows[rIdx];
                        string rowBg = rIdx % 2 == 1 ? palette.AltRowBg : "#ffffff";

                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 1.5f).AlignCenter().AlignMiddle())
                            .Text($"{pr.Row.WardNo}/{pr.Row.PropertyNo}").FontSize(fontBase).Bold();

                        foreach (TaxHeadDescriptor head in heads)
                        {
                            HeadCellValues v = head.Selector(pr);
                            table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                                .Text(Fmt(v.CollectedArrears)).FontSize(fontBase);
                            table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                                .Text(Fmt(v.CollectedCurrent)).FontSize(fontBase);
                        }

                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.NoticeCollected)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.WarrantCollected)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.PenaltyCollected)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, palette.RowTotalBg, palette.GridBorder, 1f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.FormatCollected)).FontSize(fontBase).Bold();

                        foreach (TaxHeadDescriptor head in heads)
                        {
                            HeadCellValues v = head.Selector(pr);
                            table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                                .Text(Fmt(v.RebateAttributed)).FontSize(fontBase);
                        }

                        foreach (TaxHeadDescriptor head in heads)
                        {
                            HeadCellValues v = head.Selector(pr);
                            table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                                .Text(Fmt(v.BalanceTotal)).FontSize(fontBase);
                        }

                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.NoticeRemaining)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.WarrantRemaining)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, rowBg, palette.GridBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.PenaltyRemaining)).FontSize(fontBase);
                        table.Cell().Element(c => Cell(c, palette.RowTotalBg, palette.GridBorder, 1f).AlignRight().AlignMiddle())
                            .Text(Fmt(pr.FormatBalance)).FontSize(fontBase).Bold();
                    }

                    int padCount = Math.Max(0, 8 - rows.Count);
                    for (int p = 0; p < padCount; p++)
                    {
                        for (int cIdx = 0; cIdx < totalCols; cIdx++)
                        {
                            table.Cell().Element(c => Cell(c, "#ffffff", palette.GridBorder, 0.8f).Height(13))
                                .Text(string.Empty);
                        }
                    }

                    // Totals Row
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1.5f).AlignCenter().AlignMiddle())
                        .Text("बेरीज").FontSize(fontBase + 0.2f).Bold();

                    foreach (TaxHeadDescriptor head in heads)
                    {
                        decimal cArr = rows.Sum(r => head.Selector(r).CollectedArrears);
                        decimal cCur = rows.Sum(r => head.Selector(r).CollectedCurrent);
                        table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(cArr)).FontSize(fontBase).Bold();
                        table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(cCur)).FontSize(fontBase).Bold();
                    }

                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.NoticeCollected))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.WarrantCollected))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.PenaltyCollected))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.FormatCollected))).FontSize(fontBase).Bold();

                    foreach (TaxHeadDescriptor head in heads)
                    {
                        decimal rebSum = rows.Sum(r => head.Selector(r).RebateAttributed);
                        table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(rebSum)).FontSize(fontBase).Bold();
                    }

                    foreach (TaxHeadDescriptor head in heads)
                    {
                        decimal balSum = rows.Sum(r => head.Selector(r).BalanceTotal);
                        table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                            .Text(Fmt(balSum)).FontSize(fontBase).Bold();
                    }

                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.NoticeRemaining))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.WarrantRemaining))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 0.8f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.PenaltyRemaining))).FontSize(fontBase).Bold();
                    table.Cell().Element(c => Cell(c, palette.GrandTotalBg, palette.OuterBorder, 1f).AlignRight().AlignMiddle())
                        .Text(Fmt(rows.Sum(r => r.FormatBalance))).FontSize(fontBase).Bold();
                });

                // 4 Summary KPI Boxes matching namuna9_register_print.html doc1
                content.Item().Row(kpis =>
                {
                    kpis.Spacing(6);
                    ComposeKpiCard(kpis.RelativeItem(), "संपूर्ण नोंदवही मागणी", rows.Sum(r => r.FormatDemand), palette.KpiBgDemand, palette.KpiAccentDemand);
                    ComposeKpiCard(kpis.RelativeItem(), "संपूर्ण नोंदवही वसुली", rows.Sum(r => r.FormatCollected), palette.KpiBgCollected, palette.KpiAccentCollected);
                    ComposeKpiCard(kpis.RelativeItem(), "संपूर्ण नोंदवही मंजूर सूट", rows.Sum(r => r.Rebate), palette.KpiBgRebate, palette.KpiAccentRebate);
                    ComposeKpiCard(kpis.RelativeItem(), "संपूर्ण नोंदवही बाकी", rows.Sum(r => r.FormatBalance), palette.KpiBgBalance, palette.KpiAccentBalance);
                });
            });

            page.Footer().Column(col => ComposeFooterBlock(col, palette));
        });
    }

    private static void ConfigurePage(PageDescriptor page, float leftMarginMm)
    {
        page.Size(PageSizes.A4.Landscape());
        page.MarginTop(5, Unit.Millimetre);
        page.MarginBottom(5, Unit.Millimetre);
        page.MarginRight(5, Unit.Millimetre);
        page.MarginLeft(leftMarginMm, Unit.Millimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(style => style
            .FontFamily(DevanagariFontRegistry.PrimaryFontFamily, DevanagariFontRegistry.FallbackFontFamily)
            .FontSize(6f)
            .FontColor(Colors.Black));
    }

    private void ComposeHeaderBlock(
        ColumnDescriptor col,
        Palette palette,
        string titleMr,
        string subtitleMr,
        bool showVillage)
    {
        col.Spacing(2.5f);

        col.Item().BorderBottom(0.6f).BorderColor("#94a3b8").PaddingBottom(1.5f).Row(top =>
        {
            top.RelativeItem().Text("ई-ग्रामपंचायत ERP | नमुना ९ • महाराष्ट्र ग्रामपंचायत कर व फी नियम १९६०")
                .FontSize(6f).Bold().FontColor("#475569");
            top.AutoItem().Text(model.Workspace.SelectedYear.IsLocked
                ? "संगणकीकृत वैधानिक नोंदवही [लॉक वर्ष]"
                : "संगणकीकृत वैधानिक नोंदवही")
                .FontSize(6f).Bold().FontColor("#475569");
        });

        col.Item().Row(hd =>
        {
            hd.ConstantItem(26).Height(26).Background(palette.DarkBg).AlignCenter().AlignMiddle()
                .Text(model.Format).FontSize(14f).Bold().FontColor(palette.DarkFg);

            hd.RelativeItem().AlignCenter().Column(c =>
            {
                c.Item().AlignCenter().Text("नमुना ९ • (नियम २२(९), ३२(४), ५(६) व (७) पहा)")
                    .FontSize(6.5f).Bold().FontColor(palette.LtFg);
                c.Item().AlignCenter().Text(titleMr)
                    .FontSize(12f).Bold().FontColor(palette.TitleColor);
                c.Item().AlignCenter().Text(subtitleMr)
                    .FontSize(6.5f).FontColor("#475569");
            });

            hd.ConstantItem(26);
        });

        string wardDisplay = string.IsNullOrWhiteSpace(model.WardFilterLabel) ? "सर्व वॉर्ड" : model.WardFilterLabel;
        string villagePart = showVillage ? $"   |   गाव : {model.Header.VillageNameMr}" : string.Empty;
        string pinPart = string.IsNullOrWhiteSpace(model.Header.Pincode) ? string.Empty : $"   |   पिन : {model.Header.Pincode}";

        col.Item().Border(0.7f).BorderColor(palette.MetaBorder).Background(palette.MetaBg)
            .PaddingVertical(2.5f).PaddingHorizontal(6f)
            .Text($"ग्रामपंचायत : {model.Header.PanchayatNameMr}{villagePart}   |   तालुका : {model.Header.TehsilName}   |   जिल्हा : {model.Header.DistrictName}   |   LGD : {model.Header.LgdCode}{pinPart}   |   वॉर्ड : {wardDisplay}   |   फॉरमॅट : {model.Format}")
            .FontSize(6.2f).SemiBold().FontColor("#0f172a");
    }

    private void ComposeFooterBlock(ColumnDescriptor col, Palette palette)
    {
        DateTimeOffset istNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, IstTimeZone);
        string dateStr = istNow.ToString("dd/MM/yyyy HH:mm 'IST'", CultureInfo.InvariantCulture);

        col.Item().PaddingTop(3).BorderTop(0.6f).BorderColor(palette.MetaBorder).PaddingTop(2.5f).Row(f =>
        {
            // Gramsevak Signature
            f.RelativeItem(1.2f).Row(gs =>
            {
                if (model.Header.GramsevakSignatureBytes is { Length: > 0 })
                {
                    gs.AutoItem().Width(38).Height(16).AlignMiddle().Image(model.Header.GramsevakSignatureBytes).FitArea();
                    gs.ConstantItem(4);
                }

                gs.RelativeItem().AlignMiddle().Column(gCol =>
                {
                    gCol.Item().Text($"ग्रामसेवक / ग्रामपंचायत अधिकारी: {model.Header.GramsevakName ?? "—"}")
                        .FontSize(5.8f).Bold().FontColor("#1e293b");
                    gCol.Item().Text($"ग्रामपंचायत {model.Header.PanchayatNameMr}")
                        .FontSize(5.2f).FontColor("#475569");
                });
            });

            // Center Stamp & Print Info
            f.RelativeItem(1.1f).AlignCenter().AlignMiddle().Text(text =>
            {
                text.Span($"मुद्रण: {dateStr}  •  पान ").FontSize(5.6f).FontColor("#64748b");
                text.CurrentPageNumber().FontSize(5.8f).Bold();
                text.Span(" / ").FontSize(5.6f).FontColor("#64748b");
                text.TotalPages().FontSize(5.8f).Bold();
            });

            // Sarpanch Signature
            f.RelativeItem(1.2f).AlignRight().Row(sp =>
            {
                sp.RelativeItem().AlignRight().AlignMiddle().Column(sCol =>
                {
                    sCol.Item().AlignRight().Text($"सरपंच: {model.Header.SarpanchName ?? "—"}")
                        .FontSize(5.8f).Bold().FontColor("#1e293b");
                    sCol.Item().AlignRight().Text($"तालुका {model.Header.TehsilName}, जि. {model.Header.DistrictName}")
                        .FontSize(5.2f).FontColor("#475569");
                });

                if (model.Header.SarpanchSignatureBytes is { Length: > 0 })
                {
                    sp.ConstantItem(4);
                    sp.AutoItem().Width(38).Height(16).AlignMiddle().Image(model.Header.SarpanchSignatureBytes).FitArea();
                }
            });
        });
    }

    private static void ComposeKpiCard(
        IContainer container,
        string label,
        decimal amount,
        string bgHex,
        string accentHex)
    {
        container
            .Border(0.6f).BorderColor("#cbd5e1")
            .Background(bgHex)
            .Row(row =>
            {
                row.ConstantItem(2.5f).Background(accentHex);
                row.RelativeItem().PaddingVertical(3f).PaddingHorizontal(6f).Column(col =>
                {
                    col.Item().Text(label).FontSize(5.8f).SemiBold().FontColor("#475569");
                    col.Item().Text($"₹{Fmt(amount)}").FontSize(8.5f).Bold().FontColor("#0f172a");
                });
            });
    }

    private static IContainer Cell(IContainer container, string bgHex, string borderHex, float hPad) =>
        container.Border(0.45f).BorderColor(borderHex).Background(bgHex).PaddingVertical(1.5f).PaddingHorizontal(hPad);

    private static string Fmt(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private static string FormatReceiptRef(Namuna9DemandRowDto r)
    {
        if (r.PaidAmount <= 0m)
        {
            return string.Empty;
        }

        return string.Equals(r.Status, "Settled", StringComparison.OrdinalIgnoreCase)
            ? $"वसूल ₹{Fmt(r.PaidAmount)}"
            : $"अंशतः ₹{Fmt(r.PaidAmount)}";
    }

    private static IReadOnlyList<TaxHeadDescriptor> GetHeadsForFormat(string format)
    {
        TaxHeadDescriptor house = new("घरपट्टी", "घर.", pr => pr.House);
        TaxHeadDescriptor lighting = new("दिवाबत्ती कर", "दिवा.", pr => pr.Lighting);
        TaxHeadDescriptor health = new("आरोग्य कर", "आरोग्य", pr => pr.Health);
        TaxHeadDescriptor sanitation = new("स्वच्छता कर", "स्वच्छ.", pr => pr.Sanitation);
        TaxHeadDescriptor other = new("इतर कर", "इतर", pr => pr.Other);
        TaxHeadDescriptor genWater = new("सामान्य पाणीपट्टी", "सा.पाणी", pr => pr.GenWater);
        TaxHeadDescriptor specWater = new("विशेष पाणीपट्टी", "वि.पाणी", pr => pr.SpecWater);

        return format switch
        {
            "A" => [house, lighting, health, sanitation, other],
            "B" => [genWater, specWater],
            _ => [house, lighting, health, sanitation, other, genWater, specWater],
        };
    }

    private static PreparedRow PrepareRow(int srNo, Namuna9DemandRowDto r, string format)
    {
        decimal houseC = r.CollectedByHead.HouseTax;
        decimal lightC = r.CollectedByHead.LightingTax;
        decimal healthC = r.CollectedByHead.HealthTax;
        decimal sanitC = r.CollectedByHead.SanitationTax + r.CollectedByHead.ToiletTax;
        decimal otherC = r.CollectedByHead.OtherTax;
        decimal genWC = r.CollectedByHead.GeneralWaterTax;
        decimal specWC = r.CollectedByHead.SpecialWaterTax;

        decimal houseArr = r.HouseTax.Arrears;
        decimal houseCur = r.HouseTax.Current;
        decimal lightArr = r.LightingTax.Arrears;
        decimal lightCur = r.LightingTax.Current;
        decimal healthArr = r.HealthTax.Arrears;
        decimal healthCur = r.HealthTax.Current;
        decimal sanitArr = r.SanitationTax.Arrears + r.ToiletTax.Arrears;
        decimal sanitCur = r.SanitationTax.Current + r.ToiletTax.Current;
        decimal otherArr = r.OtherTax.Arrears;
        decimal otherCur = r.OtherTax.Current;
        decimal genWArr = r.GeneralWaterTax.Arrears;
        decimal genWCur = r.GeneralWaterTax.Current;
        decimal specWArr = r.SpecialWaterTax.Arrears;
        decimal specWCur = r.SpecialWaterTax.Current;

        decimal houseBal = r.BalanceByHead.HouseTax;
        decimal lightBal = r.BalanceByHead.LightingTax;
        decimal healthBal = r.BalanceByHead.HealthTax;
        decimal sanitBal = r.BalanceByHead.SanitationTax + r.BalanceByHead.ToiletTax;
        decimal otherBal = r.BalanceByHead.OtherTax;
        decimal genWBal = r.BalanceByHead.GeneralWaterTax;
        decimal specWBal = r.BalanceByHead.SpecialWaterTax;

        // Attribute sanctioned rebate across GeneralOrder (matching DemandLedgerCalculator)
        decimal rebateLeft = format == "B" ? 0m : Math.Max(0m, r.RebateAmount);
        decimal houseReb = TakeRebate(houseArr + houseCur, houseC, ref rebateLeft);
        decimal lightReb = TakeRebate(lightArr + lightCur, lightC, ref rebateLeft);
        decimal healthReb = TakeRebate(healthArr + healthCur, healthC, ref rebateLeft);
        decimal sanitReb = TakeRebate(sanitArr + sanitCur, sanitC, ref rebateLeft);
        decimal otherReb = TakeRebate(otherArr + otherCur, otherC, ref rebateLeft);

        bool includeFees = format is "A" or "C";
        decimal noticeFee = includeFees ? r.NoticeFee : 0m;
        decimal warrantFee = includeFees ? r.WarrantFee : 0m;
        decimal penalty = includeFees ? (r.PenaltyAmount + r.CarriedFeesAndPenalty) : 0m;
        decimal rebate = includeFees ? r.RebateAmount : 0m;

        decimal noticeColl = includeFees ? r.CollectedByHead.NoticeFeePaid : 0m;
        decimal warrantColl = includeFees ? r.CollectedByHead.WarrantFeePaid : 0m;
        decimal penaltyColl = includeFees ? r.CollectedByHead.PenaltyPaid : 0m;

        decimal remFeesPool = includeFees ? r.BalanceByHead.FeesAndPenalty : 0m;
        decimal remNotice = includeFees ? Math.Min(Math.Max(0m, noticeFee - noticeColl), remFeesPool) : 0m;
        decimal remWarrant = includeFees ? Math.Min(Math.Max(0m, warrantFee - warrantColl), remFeesPool - remNotice) : 0m;
        decimal remPenalty = includeFees ? Math.Max(0m, remFeesPool - remNotice - remWarrant) : 0m;

        (decimal formatDemand, decimal formatCollected, _, decimal formatBalance) =
            Namuna9FormatMath.ComputeFormatTotals(r, format);

        string ownerMr = string.IsNullOrWhiteSpace(r.PrimaryOwnerNameMr)
            ? (r.PrimaryOwnerNameEn ?? "-")
            : r.PrimaryOwnerNameMr;

        return new PreparedRow(
            SrNo: srNo,
            Row: r,
            OwnerNameMr: ownerMr,
            House: BuildCell(houseArr, houseCur, houseC, houseReb, houseBal),
            Lighting: BuildCell(lightArr, lightCur, lightC, lightReb, lightBal),
            Health: BuildCell(healthArr, healthCur, healthC, healthReb, healthBal),
            Sanitation: BuildCell(sanitArr, sanitCur, sanitC, sanitReb, sanitBal),
            Other: BuildCell(otherArr, otherCur, otherC, otherReb, otherBal),
            GenWater: BuildCell(genWArr, genWCur, genWC, 0m, genWBal),
            SpecWater: BuildCell(specWArr, specWCur, specWC, 0m, specWBal),
            NoticeFee: noticeFee,
            WarrantFee: warrantFee,
            Penalty: penalty,
            Rebate: rebate,
            NoticeCollected: noticeColl,
            WarrantCollected: warrantColl,
            PenaltyCollected: penaltyColl,
            NoticeRemaining: remNotice,
            WarrantRemaining: remWarrant,
            PenaltyRemaining: remPenalty,
            FormatDemand: formatDemand,
            FormatCollected: formatCollected,
            FormatBalance: formatBalance);
    }

    private static HeadCellValues BuildCell(decimal arr, decimal cur, decimal collected, decimal rebate, decimal balance)
    {
        decimal collArr = Math.Min(arr, collected);
        decimal collCur = Math.Max(0m, collected - collArr);
        return new HeadCellValues(arr, cur, arr + cur, collArr, collCur, rebate, balance);
    }

    private static decimal TakeRebate(decimal demandTotal, decimal collectedTotal, ref decimal rebateLeft)
    {
        decimal headAvailable = Math.Max(0m, demandTotal - collectedTotal);
        decimal d = Math.Min(rebateLeft, headAvailable);
        rebateLeft -= d;
        return d;
    }

    private static TimeZoneInfo ResolveIstTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "IST", "IST");
            }
        }
    }

    private sealed record TaxHeadDescriptor(string LabelMr, string ShortMr, Func<PreparedRow, HeadCellValues> Selector);

    private sealed record HeadCellValues(
        decimal DemandArrears,
        decimal DemandCurrent,
        decimal DemandTotal,
        decimal CollectedArrears,
        decimal CollectedCurrent,
        decimal RebateAttributed,
        decimal BalanceTotal);

    private sealed record PreparedRow(
        int SrNo,
        Namuna9DemandRowDto Row,
        string OwnerNameMr,
        HeadCellValues House,
        HeadCellValues Lighting,
        HeadCellValues Health,
        HeadCellValues Sanitation,
        HeadCellValues Other,
        HeadCellValues GenWater,
        HeadCellValues SpecWater,
        decimal NoticeFee,
        decimal WarrantFee,
        decimal Penalty,
        decimal Rebate,
        decimal NoticeCollected,
        decimal WarrantCollected,
        decimal PenaltyCollected,
        decimal NoticeRemaining,
        decimal WarrantRemaining,
        decimal PenaltyRemaining,
        decimal FormatDemand,
        decimal FormatCollected,
        decimal FormatBalance);

    private sealed record Palette(
        string DarkBg,
        string DarkFg,
        string TitleColor,
        string LtBg,
        string LtFg,
        string SubBg,
        string SubFg,
        string FeeBg,
        string FeeFg,
        string GtHeadBg,
        string GtHeadFg,
        string GroupRecoveryBg,
        string GroupRebateBg,
        string GroupBalanceBg,
        string GroupFg,
        string RebateHeadBg,
        string RebateHeadFg,
        string BalanceHeadBg,
        string BalanceHeadFg,
        string MetaBg,
        string MetaBorder,
        string GridBorder,
        string OuterBorder,
        string RowTotalBg,
        string GrandTotalBg,
        string AltRowBg,
        string KpiBgDemand,
        string KpiAccentDemand,
        string KpiBgCollected,
        string KpiAccentCollected,
        string KpiBgRebate,
        string KpiAccentRebate,
        string KpiBgBalance,
        string KpiAccentBalance)
    {
        public static Palette Color { get; } = new(
            DarkBg: "#1e4a3b",
            DarkFg: "#ffffff",
            TitleColor: "#1e4a3b",
            LtBg: "#e2efe9",
            LtFg: "#1e4a3b",
            SubBg: "#f4f9f7",
            SubFg: "#334155",
            FeeBg: "#fef9c3",
            FeeFg: "#713f12",
            GtHeadBg: "#bbf7d0",
            GtHeadFg: "#064e3b",
            GroupRecoveryBg: "#1e4a3b",
            GroupRebateBg: "#854d0e",
            GroupBalanceBg: "#991b1b",
            GroupFg: "#ffffff",
            RebateHeadBg: "#fef3c7",
            RebateHeadFg: "#78350f",
            BalanceHeadBg: "#fee2e2",
            BalanceHeadFg: "#7f1d1d",
            MetaBg: "#f4f9f7",
            MetaBorder: "#a3c9bb",
            GridBorder: "#7baea0",
            OuterBorder: "#528b76",
            RowTotalBg: "#eaf3ef",
            GrandTotalBg: "#dcebe3",
            AltRowBg: "#fafdfc",
            KpiBgDemand: "#f0f9ff",
            KpiAccentDemand: "#0284c7",
            KpiBgCollected: "#ecfdf5",
            KpiAccentCollected: "#059669",
            KpiBgRebate: "#fffbeb",
            KpiAccentRebate: "#d97706",
            KpiBgBalance: "#fef2f2",
            KpiAccentBalance: "#dc2626");

        public static Palette BlackAndWhite { get; } = new(
            DarkBg: "#e2e8f0",
            DarkFg: "#000000",
            TitleColor: "#000000",
            LtBg: "#f1f5f9",
            LtFg: "#000000",
            SubBg: "#f8fafc",
            SubFg: "#000000",
            FeeBg: "#f1f5f9",
            FeeFg: "#000000",
            GtHeadBg: "#cbd5e1",
            GtHeadFg: "#000000",
            GroupRecoveryBg: "#e2e8f0",
            GroupRebateBg: "#f1f5f9",
            GroupBalanceBg: "#cbd5e1",
            GroupFg: "#000000",
            RebateHeadBg: "#f8fafc",
            RebateHeadFg: "#000000",
            BalanceHeadBg: "#f8fafc",
            BalanceHeadFg: "#000000",
            MetaBg: "#f8fafc",
            MetaBorder: "#475569",
            GridBorder: "#475569",
            OuterBorder: "#000000",
            RowTotalBg: "#f1f5f9",
            GrandTotalBg: "#e2e8f0",
            AltRowBg: "#f8fafc",
            KpiBgDemand: "#ffffff",
            KpiAccentDemand: "#334155",
            KpiBgCollected: "#ffffff",
            KpiAccentCollected: "#334155",
            KpiBgRebate: "#ffffff",
            KpiAccentRebate: "#334155",
            KpiBgBalance: "#ffffff",
            KpiAccentBalance: "#000000");
    }
}
