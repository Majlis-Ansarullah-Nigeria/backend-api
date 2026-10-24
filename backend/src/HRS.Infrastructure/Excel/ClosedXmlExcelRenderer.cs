using System.Globalization;
using ClosedXML.Excel;
using HRS.Application.Common.Interfaces;

namespace HRS.Infrastructure.Excel;

/// <summary>FR-HIS-003's single-report Excel and FR-HIS-004's bulk workbook.</summary>
public sealed class ClosedXmlExcelRenderer : IExcelRenderer
{
    public byte[] RenderSingleReport(ReportReceiptData data, OrganizationBranding branding)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");

        sheet.Cell(1, 1).Value = data.ReportTypeName;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        sheet.Cell(2, 1).Value = $"{data.PeriodLabel} · {data.UnitName}";
        sheet.Cell(3, 1).Value = $"Reference: {data.ReferenceNumber ?? "DRAFT — not yet submitted"}";
        sheet.Cell(4, 1).Value = $"Generated: {branding.DateTime(data.GeneratedAt)}";

        var row = 6;
        foreach (var section in data.Sections)
        {
            sheet.Cell(row, 1).Value = section.Title;
            sheet.Cell(row, 1).Style.Font.SetBold();
            row++;

            foreach (var field in section.Fields)
            {
                sheet.Cell(row, 1).Value = field.Label;
                sheet.Cell(row, 2).Value = field.Value;
                row++;
            }

            row++;
        }

        sheet.Columns(1, 2).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] RenderBulkWorkbook(IReadOnlyList<BulkExportReportRow> rows, OrganizationBranding branding)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Reports");

        // Different report types have entirely different fields — the column set is the union
        // of every exported row's field keys (FR-HIS-004: "a row per report and a column per
        // field"), in first-seen order so related report types' shared fields line up.
        var fieldKeysInOrder = new List<string>();
        var labelByFieldKey = new Dictionary<string, string>();
        foreach (var row in rows)
        {
            foreach (var (fieldKey, (label, _)) in row.AnswersByFieldKey)
            {
                if (labelByFieldKey.TryAdd(fieldKey, label))
                {
                    fieldKeysInOrder.Add(fieldKey);
                }
            }
        }

        string[] fixedColumns = ["Report type", "Unit", "Period", "Reference", "Submitted", "Status"];
        for (var col = 0; col < fixedColumns.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = fixedColumns[col];
        }

        for (var col = 0; col < fieldKeysInOrder.Count; col++)
        {
            sheet.Cell(1, fixedColumns.Length + col + 1).Value = labelByFieldKey[fieldKeysInOrder[col]];
        }

        sheet.Row(1).Style.Font.SetBold();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var excelRow = rowIndex + 2;

            sheet.Cell(excelRow, 1).Value = row.ReportTypeName;
            sheet.Cell(excelRow, 2).Value = row.UnitName;
            sheet.Cell(excelRow, 3).Value = row.PeriodLabel;
            sheet.Cell(excelRow, 4).Value = row.ReferenceNumber ?? "";
            sheet.Cell(excelRow, 5).Value = row.SubmittedAt is { } submittedAt ? branding.Date(submittedAt) : "";
            sheet.Cell(excelRow, 6).Value = row.Status;

            for (var col = 0; col < fieldKeysInOrder.Count; col++)
            {
                if (row.AnswersByFieldKey.TryGetValue(fieldKeysInOrder[col], out var answer))
                {
                    sheet.Cell(excelRow, fixedColumns.Length + col + 1).Value = answer.Value;
                }
            }
        }

        sheet.Columns(1, fixedColumns.Length + fieldKeysInOrder.Count).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
