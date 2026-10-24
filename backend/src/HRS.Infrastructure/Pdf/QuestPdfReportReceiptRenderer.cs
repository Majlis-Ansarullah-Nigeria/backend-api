using System.Globalization;
using HRS.Application.Common.Interfaces;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRS.Infrastructure.Pdf;

/// <summary>Renders the plain submission receipt (FR-SUB-010) and FR-HIS-003's branded history
/// download — two different documents sharing the same section/field table layout.</summary>
public sealed class QuestPdfReportReceiptRenderer : IPdfRenderer
{
    public byte[] RenderReportReceipt(ReportReceiptData data, OrganizationBranding branding)
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
                    column.Item().PaddingBottom(6).Element(c => PdfBranding.Header(c, branding));
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
                            text.Span(branding.DateTime(data.GeneratedAt));
                        });
                    });
                });

                page.Content().PaddingTop(15).Element(content => RenderSections(content, data.Sections));

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span(branding.Name).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] RenderHistoryReport(HistoryReportPdfData data, OrganizationBranding branding)
    {
        var qrCodeBytes = GenerateQrCodePng(data.OnlineRecordUrl);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().PaddingBottom(4).Element(c => PdfBranding.Header(c, branding));
                        column.Item().Text(data.ReportTypeName).FontSize(18).Bold();
                        column.Item().Text($"{data.PeriodLabel} · {data.UnitName}").FontSize(11).FontColor(Colors.Grey.Darken1);

                        column.Item().PaddingTop(10).Row(detailRow =>
                        {
                            detailRow.RelativeItem().Text(text =>
                            {
                                text.Span("Reference: ").SemiBold();
                                text.Span(data.ReferenceNumber ?? "—");
                            });
                            detailRow.RelativeItem().AlignRight().Text(text =>
                            {
                                text.Span("Submitted: ").SemiBold();
                                text.Span(branding.DateTime(data.GeneratedAt));
                            });
                        });
                    });

                    row.ConstantItem(70).Column(column =>
                    {
                        column.Item().Image(qrCodeBytes);
                        column.Item().AlignCenter().Text("View online").FontSize(7).FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Content().PaddingTop(15).Element(content => RenderSections(content, data.Sections));

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span(branding.Name).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void RenderSections(IContainer content, IReadOnlyList<ReportReceiptSection> sections)
    {
        content.Column(column =>
        {
            column.Spacing(12);

            foreach (var section in sections)
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
    }

    private static byte[] GenerateQrCodePng(string url)
    {
        using var generator = new QRCodeGenerator();
        using var qrCodeData = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrCodeData);
        return pngQrCode.GetGraphic(10);
    }
}
