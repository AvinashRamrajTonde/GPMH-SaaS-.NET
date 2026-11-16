using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Application.DTOs.Printing;
using GPMH.Domain.ValueObjects;
using MediatR;

namespace GPMH.Application.Features.Namuna9.Queries;

public sealed record GetNamuna9RegisterPdfQuery(
    Guid? AssessmentYearId = null,
    string? WardNo = null,
    string? Search = null,
    string? Format = "C",
    string? Part = "Both",
    string? Style = "color",
    int LeftMarginMm = 10,
    bool PendingOnly = false) : IRequest<GeneratedPdfFileDto>;

public sealed record GetNamuna9CsvQuery(
    Guid? AssessmentYearId = null,
    string? WardNo = null,
    string? Search = null,
    string? Format = "C",
    bool PendingOnly = false) : IRequest<Namuna9CsvFileDto>;

public sealed class GetNamuna9RegisterPdfQueryValidator : AbstractValidator<GetNamuna9RegisterPdfQuery>
{
    public GetNamuna9RegisterPdfQueryValidator()
    {
        RuleFor(x => x.Format)
            .Must(Namuna9Support.IsValidFormat)
            .WithMessage("Format must be A (general tax), B (water tax) or C (combined).");

        RuleFor(x => x.Part)
            .Must(p => p is null || Namuna9PrintNormalization.NormalizePart(p) is "Demand" or "Recovery" or "Both")
            .WithMessage("Part must be Demand, Recovery, or Both.");

        RuleFor(x => x.Style)
            .Must(s => s is null || Namuna9PrintNormalization.NormalizeStyle(s) is "color" or "bw")
            .WithMessage("Style must be color or bw.");

        RuleFor(x => x.LeftMarginMm)
            .InclusiveBetween(0, 30)
            .WithMessage("Left binding margin must be between 0 mm and 30 mm.");

        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.WardNo).MaximumLength(40);
    }
}

public sealed class GetNamuna9CsvQueryValidator : AbstractValidator<GetNamuna9CsvQuery>
{
    public GetNamuna9CsvQueryValidator()
    {
        RuleFor(x => x.Format)
            .Must(Namuna9Support.IsValidFormat)
            .WithMessage("Format must be A (general tax), B (water tax) or C (combined).");

        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.WardNo).MaximumLength(40);
    }
}

internal static class Namuna9PrintNormalization
{
    public static string NormalizePart(string? part)
    {
        if (string.IsNullOrWhiteSpace(part))
        {
            return "Both";
        }

        return part.Trim().ToLowerInvariant() switch
        {
            "demand" or "doc0" or "page1" => "Demand",
            "recovery" or "doc1" or "page2" => "Recovery",
            "both" => "Both",
            _ => part.Trim(),
        };
    }

    public static string NormalizeStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return "color";
        }

        return style.Trim().ToLowerInvariant() switch
        {
            "color" or "clr" => "color",
            "bw" or "monochrome" or "grayscale" => "bw",
            _ => style.Trim().ToLowerInvariant(),
        };
    }

    public static string SafeFileSegment(string value)
    {
        char[] chars = [.. value.Trim().Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')];
        return chars.Length == 0 ? "register" : new string(chars);
    }
}

public sealed class GetNamuna9PrintAndCsvQueryHandlers(
    INamuna9Repository repository,
    IPrintDataRepository printDataRepository,
    IStatutoryPdfRenderer pdfRenderer,
    ITenantContext tenantContext)
    : IRequestHandler<GetNamuna9RegisterPdfQuery, GeneratedPdfFileDto>,
      IRequestHandler<GetNamuna9CsvQuery, Namuna9CsvFileDto>
{
    public async Task<GeneratedPdfFileDto> Handle(
        GetNamuna9RegisterPdfQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        Namuna9WorkspaceDto workspace = await Namuna9Support
            .BuildWorkspaceAsync(
                repository,
                request.AssessmentYearId,
                request.WardNo,
                request.Search,
                request.Format,
                request.PendingOnly,
                cancellationToken)
            .ConfigureAwait(false);

        Namuna9GramPanchayatHeaderDto header = await repository
            .LoadPrintHeaderAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        string format = Namuna9Support.NormalizeFormat(request.Format);
        string part = Namuna9PrintNormalization.NormalizePart(request.Part);
        string style = Namuna9PrintNormalization.NormalizeStyle(request.Style);
        int leftMarginMm = Math.Clamp(request.LeftMarginMm, 0, 30);

        Namuna9RegisterPdfDocumentModel model = new(
            Header: header,
            Workspace: workspace,
            Format: format,
            RegisterPart: part,
            Style: style,
            LeftMarginMm: leftMarginMm,
            WardFilterLabel: string.IsNullOrWhiteSpace(request.WardNo) ? null : request.WardNo.Trim());

        byte[] pdfBytes = pdfRenderer.RenderNamuna9RegisterPdf(model);

        string detailsJson = JsonSerializer.Serialize(new
        {
            yearLabel = workspace.SelectedYear.YearLabel,
            format,
            part,
            style,
            leftMarginMm,
            wardNo = request.WardNo,
            pendingOnly = request.PendingOnly,
            rowCount = workspace.Items.Count,
        });

        await printDataRepository
            .RecordPrintAuditAsync(
                tenantId,
                "Namuna9RegisterPrinted",
                $"year:{workspace.SelectedYear.YearLabel}",
                detailsJson,
                cancellationToken)
            .ConfigureAwait(false);

        string yearSlug = Namuna9PrintNormalization.SafeFileSegment(workspace.SelectedYear.YearLabel);
        string fileName = $"namuna9-register-{yearSlug}-format-{format.ToLowerInvariant()}.pdf";
        return new GeneratedPdfFileDto(fileName, pdfBytes);
    }

    public async Task<Namuna9CsvFileDto> Handle(
        GetNamuna9CsvQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        Namuna9WorkspaceDto workspace = await Namuna9Support
            .BuildWorkspaceAsync(
                repository,
                request.AssessmentYearId,
                request.WardNo,
                request.Search,
                request.Format,
                request.PendingOnly,
                cancellationToken)
            .ConfigureAwait(false);

        string format = Namuna9Support.NormalizeFormat(request.Format);
        byte[] csvBytes = BuildCsvBytes(workspace, format);

        string detailsJson = JsonSerializer.Serialize(new
        {
            yearLabel = workspace.SelectedYear.YearLabel,
            format,
            wardNo = request.WardNo,
            pendingOnly = request.PendingOnly,
            rowCount = workspace.Items.Count,
        });

        await printDataRepository
            .RecordPrintAuditAsync(
                tenantId,
                "Namuna9CsvExported",
                $"year:{workspace.SelectedYear.YearLabel}",
                detailsJson,
                cancellationToken)
            .ConfigureAwait(false);

        string yearSlug = Namuna9PrintNormalization.SafeFileSegment(workspace.SelectedYear.YearLabel);
        string fileName = $"namuna9-register-{yearSlug}-format-{format.ToLowerInvariant()}.csv";
        return new Namuna9CsvFileDto(fileName, csvBytes);
    }

    private static byte[] BuildCsvBytes(Namuna9WorkspaceDto workspace, string format)
    {
        StringBuilder sb = new();

        List<string> headers = BuildCsvHeaders(format);
        sb.AppendLine(string.Join(',', headers.Select(EscapeCsv)));

        int sr = 0;
        foreach (Namuna9DemandRowDto row in workspace.Items)
        {
            sr++;
            List<string> cols = BuildCsvRowCells(sr, row, format);
            sb.AppendLine(string.Join(',', cols.Select(EscapeCsv)));
        }

        List<string> totals = BuildCsvTotalsRow(workspace.Items, format);
        sb.AppendLine(string.Join(',', totals.Select(EscapeCsv)));

        byte[] preamble = Encoding.UTF8.GetPreamble();
        byte[] body = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] output = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, output, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, output, preamble.Length, body.Length);
        return output;
    }

    private static List<string> BuildCsvHeaders(string format)
    {
        List<string> cols =
        [
            "अ.क्र. (Sr)",
            "वॉर्ड (Ward)",
            "मिळकत क्र. (Property No)",
            "मालमत्ता धारकाचे नाव (Owner Name)",
            "स्थिती (Status)",
        ];

        if (format is "A" or "C")
        {
            cols.AddRange([
                "घरपट्टी मागील (House Arrears)",
                "घरपट्टी चालू (House Current)",
                "घरपट्टी एकूण (House Total)",
                "दिवाबत्ती मागील (Lighting Arrears)",
                "दिवाबत्ती चालू (Lighting Current)",
                "दिवाबत्ती एकूण (Lighting Total)",
                "आरोग्य मागील (Health Arrears)",
                "आरोग्य चालू (Health Current)",
                "आरोग्य एकूण (Health Total)",
                "स्वच्छता मागील (Sanitation Arrears)",
                "स्वच्छता चालू (Sanitation Current)",
                "स्वच्छता एकूण (Sanitation Total)",
                "इतर मागील (Other Arrears)",
                "इतर चालू (Other Current)",
                "इतर एकूण (Other Total)",
            ]);
        }

        if (format is "B" or "C")
        {
            cols.AddRange([
                "सामान्य पाणी मागील (GenWater Arrears)",
                "सामान्य पाणी चालू (GenWater Current)",
                "सामान्य पाणी एकूण (GenWater Total)",
                "विशेष पाणी मागील (SpecWater Arrears)",
                "विशेष पाणी चालू (SpecWater Current)",
                "विशेष पाणी एकूण (SpecWater Total)",
            ]);
        }

        if (format is "A" or "C")
        {
            cols.AddRange([
                "नोटीस फी (Notice Fee)",
                "वारंट फी (Warrant Fee)",
                "दंड (Penalty)",
                "सूट (Rebate)",
            ]);
        }

        cols.AddRange([
            "एकूण मागणी (Net Demand)",
            "एकूण वसुली (Collected)",
            "मंजूर सूट (Rebate Granted)",
            "एकूण बाकी (Balance Dues)",
        ]);

        return cols;
    }

    private static List<string> BuildCsvRowCells(int sr, Namuna9DemandRowDto r, string format)
    {
        string owner = string.IsNullOrWhiteSpace(r.PrimaryOwnerNameMr)
            ? (r.PrimaryOwnerNameEn ?? "-")
            : r.PrimaryOwnerNameMr;

        List<string> cols =
        [
            sr.ToString(CultureInfo.InvariantCulture),
            r.WardNo,
            r.PropertyNo,
            owner,
            r.Status,
        ];

        decimal sanitationArr = r.SanitationTax.Arrears + r.ToiletTax.Arrears;
        decimal sanitationCur = r.SanitationTax.Current + r.ToiletTax.Current;
        decimal sanitationTot = r.SanitationTax.Total + r.ToiletTax.Total;
        decimal penaltyTotal = r.PenaltyAmount + r.CarriedFeesAndPenalty;

        if (format is "A" or "C")
        {
            cols.AddRange([
                Fmt(r.HouseTax.Arrears), Fmt(r.HouseTax.Current), Fmt(r.HouseTax.Total),
                Fmt(r.LightingTax.Arrears), Fmt(r.LightingTax.Current), Fmt(r.LightingTax.Total),
                Fmt(r.HealthTax.Arrears), Fmt(r.HealthTax.Current), Fmt(r.HealthTax.Total),
                Fmt(sanitationArr), Fmt(sanitationCur), Fmt(sanitationTot),
                Fmt(r.OtherTax.Arrears), Fmt(r.OtherTax.Current), Fmt(r.OtherTax.Total),
            ]);
        }

        if (format is "B" or "C")
        {
            cols.AddRange([
                Fmt(r.GeneralWaterTax.Arrears), Fmt(r.GeneralWaterTax.Current), Fmt(r.GeneralWaterTax.Total),
                Fmt(r.SpecialWaterTax.Arrears), Fmt(r.SpecialWaterTax.Current), Fmt(r.SpecialWaterTax.Total),
            ]);
        }

        if (format is "A" or "C")
        {
            cols.AddRange([
                Fmt(r.NoticeFee),
                Fmt(r.WarrantFee),
                Fmt(penaltyTotal),
                Fmt(r.RebateAmount),
            ]);
        }

        (decimal formatDemand, decimal formatCollected, decimal formatRebate, decimal formatBalance) =
            Namuna9FormatMath.ComputeFormatTotals(r, format);

        cols.AddRange([
            Fmt(formatDemand),
            Fmt(formatCollected),
            Fmt(formatRebate),
            Fmt(formatBalance),
        ]);

        return cols;
    }

    private static List<string> BuildCsvTotalsRow(IReadOnlyList<Namuna9DemandRowDto> items, string format)
    {
        List<string> cols = [string.Empty, string.Empty, string.Empty, "एकूण बेरीज (Total)", string.Empty];

        if (format is "A" or "C")
        {
            cols.AddRange([
                Fmt(items.Sum(r => r.HouseTax.Arrears)),
                Fmt(items.Sum(r => r.HouseTax.Current)),
                Fmt(items.Sum(r => r.HouseTax.Total)),
                Fmt(items.Sum(r => r.LightingTax.Arrears)),
                Fmt(items.Sum(r => r.LightingTax.Current)),
                Fmt(items.Sum(r => r.LightingTax.Total)),
                Fmt(items.Sum(r => r.HealthTax.Arrears)),
                Fmt(items.Sum(r => r.HealthTax.Current)),
                Fmt(items.Sum(r => r.HealthTax.Total)),
                Fmt(items.Sum(r => r.SanitationTax.Arrears + r.ToiletTax.Arrears)),
                Fmt(items.Sum(r => r.SanitationTax.Current + r.ToiletTax.Current)),
                Fmt(items.Sum(r => r.SanitationTax.Total + r.ToiletTax.Total)),
                Fmt(items.Sum(r => r.OtherTax.Arrears)),
                Fmt(items.Sum(r => r.OtherTax.Current)),
                Fmt(items.Sum(r => r.OtherTax.Total)),
            ]);
        }

        if (format is "B" or "C")
        {
            cols.AddRange([
                Fmt(items.Sum(r => r.GeneralWaterTax.Arrears)),
                Fmt(items.Sum(r => r.GeneralWaterTax.Current)),
                Fmt(items.Sum(r => r.GeneralWaterTax.Total)),
                Fmt(items.Sum(r => r.SpecialWaterTax.Arrears)),
                Fmt(items.Sum(r => r.SpecialWaterTax.Current)),
                Fmt(items.Sum(r => r.SpecialWaterTax.Total)),
            ]);
        }

        if (format is "A" or "C")
        {
            cols.AddRange([
                Fmt(items.Sum(r => r.NoticeFee)),
                Fmt(items.Sum(r => r.WarrantFee)),
                Fmt(items.Sum(r => r.PenaltyAmount + r.CarriedFeesAndPenalty)),
                Fmt(items.Sum(r => r.RebateAmount)),
            ]);
        }

        decimal totalDemand = items.Sum(r => Namuna9FormatMath.ComputeFormatTotals(r, format).Demand);
        decimal totalCollected = items.Sum(r => Namuna9FormatMath.ComputeFormatTotals(r, format).Collected);
        decimal totalRebate = items.Sum(r => Namuna9FormatMath.ComputeFormatTotals(r, format).Rebate);
        decimal totalBalance = items.Sum(r => Namuna9FormatMath.ComputeFormatTotals(r, format).Balance);

        cols.AddRange([Fmt(totalDemand), Fmt(totalCollected), Fmt(totalRebate), Fmt(totalBalance)]);
        return cols;
    }

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string safe = value;
        if (safe.Length > 0 && (safe[0] is '=' or '+' or '-' or '@' or '\t' or '\r'))
        {
            // Avoid escaping negative numbers if any, though statutory tax values are >= 0.
            if (!decimal.TryParse(safe, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                safe = "'" + safe;
            }
        }

        if (safe.IndexOfAny([',', '"', '\r', '\n']) >= 0)
        {
            return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return safe;
    }
}
