using System.Globalization;
using HRS.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRS.Infrastructure.Pdf;

/// <summary>A plain, functional receipt (FR-SUB-010) — not FR-HIS-003's branded/QR-code
/// history download (M9).</summary>
public sealed class QuestPdfReportReceiptRenderer : IPdfRenderer
{
    public byte[] RenderReportReceipt(ReportReceiptData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text(data.ReportTypeName).FontSize(18).Bold();
                    column.Item().Text($"{data.PeriodLabel} · {data.UnitName}").FontSize(11).FontColor(Colors.Grey.Darken1);

                    column.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Reference: ").SemiBold();
                            text.Span(data.ReferenceNumber ?? "DRAFT — not yet submitted");
                        });
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span(data.ReferenceNumber is null ? "Generated: " : "Submitted: ").SemiBold();
                            text.Span(data.GeneratedAt.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture));
                        });
                    });
                });

                page.Content().PaddingTop(15).Column(column =>
                {
                    column.Spacing(12);

                    foreach (var section in data.Sections)
                    {
                        column.Item().Column(sectionColumn =>
                        {
                            sectionColumn.Item().Text(section.Title).FontSize(13).Bold();
                            sectionColumn.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                });

                                foreach (var field in section.Fields)
                                {
                                    table.Cell().Padding(3).Text(field.Label).FontColor(Colors.Grey.Darken2);
                                    table.Cell().Padding(3).Text(field.Value);
                                }
                            });
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Hierarchical Reporting Service").FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }
}
