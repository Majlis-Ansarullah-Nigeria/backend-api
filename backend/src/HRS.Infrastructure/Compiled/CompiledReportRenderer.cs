using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Review.Compiled;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfColors = QuestPDF.Helpers.Colors;
using PdfContainer = QuestPDF.Infrastructure.IContainer;
using WordParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WordTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using PdfDocument = QuestPDF.Fluent.Document;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace HRS.Infrastructure.Compiled;

/// <summary>
/// FR-REV-006's exports of the compiled national report: a branded PDF (QuestPDF, like the other
/// PDFs) and an editable Word document (OpenXML) the Secretariat can rework before circulating.
/// Both follow the on-screen contents: cover and summary, submission overview, one section per
/// template section, zone highlights, and the appendix of zone tables.
/// </summary>
public sealed class CompiledReportRenderer : ICompiledReportRenderer
{
    public byte[] RenderPdf(CompiledReportView report, OrganizationBranding branding)
    {
        var figuresByKey = report.Sections.SelectMany(s => s.Figures).ToDictionary(f => f.FieldKey);

        var document = PdfDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Element(c => HRS.Infrastructure.Pdf.PdfBranding.Header(c, branding));
                            column.Item().Text("National Secretariat").FontColor(PdfColors.Grey.Darken1);
                        });
                        row.AutoItem().AlignRight().Text(report.ReferenceNumber).FontSize(9).SemiBold();
                    });
                    header.Item().PaddingTop(6).LineHorizontal(2).LineColor(branding.PrimaryColor);
                });

                page.Content().PaddingTop(16).Column(column =>
                {
                    column.Spacing(14);
                    column.Item().Text($"National {report.ReportTypeName}".ToUpperInvariant()).FontSize(9).SemiBold().FontColor(branding.PrimaryColor);
                    column.Item().Text(report.PeriodLabel).FontSize(24).Bold();

                    column.Item().Element(c => Heading(c, "1 · Summary"));
                    column.Item().Text(string.IsNullOrWhiteSpace(report.Narrative) ? "No summary has been written yet." : report.Narrative);

                    column.Item().Element(c => Heading(c, "2 · Submission overview"));
                    column.Item().Text(CompiledReportText.OverviewSentence(report, branding));
                    column.Item().Row(row =>
                    {
                        foreach (var level in report.Overview.Levels)
                        {
                            row.RelativeItem().Padding(4).Background(PdfColors.Grey.Lighten4).Padding(8).Column(tile =>
                            {
                                tile.Item().Text(level.LevelPluralName.ToUpperInvariant()).FontSize(8).FontColor(PdfColors.Grey.Darken1);
                                tile.Item().Text($"{level.Submitted} / {level.Expected}").FontSize(16).Bold();
                            });
                        }
                    });

                    var number = 3;
                    foreach (var section in report.Sections)
                    {
                        column.Item().Element(c => Heading(c, $"{number++} · {section.Title}"));
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });
                            HeaderCell(table, "Figure");
                            HeaderCell(table, report.PeriodLabel);
                            HeaderCell(table, report.PreviousPeriodLabel ?? "Previous");
                            HeaderCell(table, "Change");
                            HeaderCell(table, "Target");
                            foreach (var figure in section.Figures)
                            {
                                BodyCell(table, figure.Label);
                                BodyCell(table, CompiledReportText.Figure(figure.Value, figure.Type, figure.UnitLabel));
                                BodyCell(table, CompiledReportText.Figure(figure.Previous, figure.Type, figure.UnitLabel));
                                BodyCell(table, CompiledReportText.Change(figure.Value, figure.Previous, figure.Type));
                                BodyCell(table, CompiledReportText.Figure(figure.Target, figure.Type, figure.UnitLabel));
                            }
                        });
                    }

                    column.Item().Element(c => Heading(c, $"{number++} · Zone highlights"));
                    var highlights = report.Sections.SelectMany(s => s.Highlights).ToList();
                    if (highlights.Count == 0)
                    {
                        column.Item().Text("No zone figures have been acknowledged yet.").FontColor(PdfColors.Grey.Darken1);
                    }

                    foreach (var highlight in highlights)
                    {
                        column.Item().Text($"• {CompiledReportText.Highlight(highlight, figuresByKey.GetValueOrDefault(highlight.FieldKey))}");
                    }

                    column.Item().Element(c => Heading(c, "Appendix: zone tables"));
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            foreach (var _ in report.Fields)
                            {
                                columns.RelativeColumn(2);
                            }
                        });
                        HeaderCell(table, "Zone");
                        HeaderCell(table, "Submitted");
                        foreach (var field in report.Fields)
                        {
                            HeaderCell(table, field.Label);
                        }

                        foreach (var zone in report.Zones)
                        {
                            BodyCell(table, zone.Name);
                            BodyCell(table, $"{zone.Submitted} / {zone.Expected}");
                            for (var i = 0; i < report.Fields.Count; i++)
                            {
                                BodyCell(table, CompiledReportText.Figure(zone.Values[i], report.Fields[i].Type, report.Fields[i].UnitLabel));
                            }
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void Heading(PdfContainer container, string text) =>
        container.PaddingTop(6).Text(text).FontSize(14).Bold();

    private static void HeaderCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(1).BorderColor(PdfColors.Grey.Lighten1).PaddingVertical(4).Text(text).FontSize(8).SemiBold().FontColor(PdfColors.Grey.Darken1);

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(0.5f).BorderColor(PdfColors.Grey.Lighten3).PaddingVertical(4).Text(text);

    public byte[] RenderWord(CompiledReportView report, OrganizationBranding branding)
    {
        var figuresByKey = report.Sections.SelectMany(s => s.Figures).ToDictionary(f => f.FieldKey);

        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var body = new Body();
            main.Document = new WordDocument(body);

            if (branding is { Logo: { } logo, LogoContentType: "image/png" })
            {
                body.Append(WordLogo.Paragraph(main, logo));
            }

            body.Append(Paragraph(branding.Name, bold: true));
            body.Append(Paragraph($"National Secretariat · {report.ReferenceNumber}"));
            body.Append(Paragraph($"National {report.ReportTypeName}", size: 20));
            body.Append(Paragraph(report.PeriodLabel, bold: true, size: 48));

            body.Append(Paragraph("1 · Summary", bold: true, size: 28));
            foreach (var line in (string.IsNullOrWhiteSpace(report.Narrative) ? "No summary has been written yet." : report.Narrative).Split('\n'))
            {
                body.Append(Paragraph(line.TrimEnd('\r')));
            }

            body.Append(Paragraph("2 · Submission overview", bold: true, size: 28));
            body.Append(Paragraph(CompiledReportText.OverviewSentence(report, branding)));
            body.Append(Table(
                ["Level", "Submitted", "Expected"],
                report.Overview.Levels.Select(l => new[] { l.LevelPluralName, l.Submitted.ToString(System.Globalization.CultureInfo.InvariantCulture), l.Expected.ToString(System.Globalization.CultureInfo.InvariantCulture) })));

            var number = 3;
            foreach (var section in report.Sections)
            {
                body.Append(Paragraph($"{number++} · {section.Title}", bold: true, size: 28));
                body.Append(Table(
                    ["Figure", report.PeriodLabel, report.PreviousPeriodLabel ?? "Previous", "Change", "Target"],
                    section.Figures.Select(f => new[]
                    {
                        f.Label,
                        CompiledReportText.Figure(f.Value, f.Type, f.UnitLabel),
                        CompiledReportText.Figure(f.Previous, f.Type, f.UnitLabel),
                        CompiledReportText.Change(f.Value, f.Previous, f.Type),
                        CompiledReportText.Figure(f.Target, f.Type, f.UnitLabel),
                    })));
            }

            body.Append(Paragraph($"{number} · Zone highlights", bold: true, size: 28));
            var highlights = report.Sections.SelectMany(s => s.Highlights).ToList();
            if (highlights.Count == 0)
            {
                body.Append(Paragraph("No zone figures have been acknowledged yet."));
            }

            foreach (var highlight in highlights)
            {
                body.Append(Paragraph($"• {CompiledReportText.Highlight(highlight, figuresByKey.GetValueOrDefault(highlight.FieldKey))}"));
            }

            body.Append(Paragraph("Appendix: zone tables", bold: true, size: 28));
            body.Append(Table(
                ["Zone", "Submitted", .. report.Fields.Select(f => f.Label)],
                report.Zones.Select(z => new[] { z.Name, $"{z.Submitted} / {z.Expected}" }
                    .Concat(report.Fields.Select((f, i) => CompiledReportText.Figure(z.Values[i], f.Type, f.UnitLabel)))
                    .ToArray())));

            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static WordParagraph Paragraph(string text, bool bold = false, int? size = null)
    {
        var properties = new RunProperties();
        if (bold)
        {
            properties.Append(new Bold());
        }

        if (size is { } halfPoints)
        {
            properties.Append(new FontSize { Val = halfPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        return new WordParagraph(new Run(properties, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static WordTable Table(IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var table = new WordTable(new TableProperties(
            new TableBorders(
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2 }),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }));

        table.Append(Row(headers, bold: true));
        foreach (var row in rows)
        {
            table.Append(Row(row, bold: false));
        }

        return table;
    }

    private static TableRow Row(IEnumerable<string> cells, bool bold) =>
        new([.. cells.Select(text => new TableCell(Paragraph(text, bold)))]);
}
