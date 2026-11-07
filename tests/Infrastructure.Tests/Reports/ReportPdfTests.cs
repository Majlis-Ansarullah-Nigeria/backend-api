using System.Text.Json;
using FluentAssertions;
using ManagementApi.Application.Reports.Pdf;
using ManagementApi.Domain.Enums;
using ManagementApi.Infrastructure.Reports;
using Xunit;

namespace ManagementApi.Infrastructure.Tests.Reports;

public class ReportPdfTests
{
    [Fact]
    public void Renders_a_multi_page_pdf_with_the_logo()
    {
        var model = new ReportPdfModel
        {
            Title = "Dila Monthly Report",
            TemplateVersion = 2,
            WindowName = "November 2026",
            ReferenceNumber = "GMR-2026-11-Z01D01",
            Status = "Returned",
            IsLate = true,
            Level = "Dila",
            UnitName = "Ikeja Dila",
            ParentUnits = "Lagos Zone",
            SubmittedBy = "Musa Bello",
            SubmittedAtUtc = new DateTime(2026, 11, 6, 20, 0, 0, DateTimeKind.Utc),
            DueAtUtc = new DateTime(2026, 11, 5, 22, 59, 0, DateTimeKind.Utc),
            ReturnReason = "Please add the attendance figures.",
            Sections = Enumerable.Range(1, 8).Select(s => new ReportPdfSection(s, $"Section {s}", "About this section",
                Enumerable.Range(1, 12).Select(q => new ReportPdfAnswer(s * 100 + q, $"Question {q} of section {s}?", q % 3 == 0 ? null : $"Answer {q}", q % 2 == 0)).ToList())).ToList(),
            History = new() { new(DateTime.UtcNow, "National Secretariat", "Returned", "Missing figures") },
            GeneratedAtUtc = DateTime.UtcNow,
        };

        var pdf = new ReportPdfRenderer().Render(model);

        pdf.Length.Should().BeGreaterThan(20_000); // includes the embedded logo
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        if (Environment.GetEnvironmentVariable("REPORT_PDF_SAMPLE") is { Length: > 0 } path)
        {
            File.WriteAllBytes(path, pdf);
        }
    }

    [Theory]
    [InlineData(QuestionType.Number, "1234", "1,234")]
    [InlineData(QuestionType.YesNo, "true", "Yes")]
    [InlineData(QuestionType.Checkbox, "[\"Tabligh\",\"Tarbiyya\"]", "Tabligh, Tarbiyya")]
    [InlineData(QuestionType.Date, "\"2026-03-14\"", "14 Mar 2026")]
    [InlineData(QuestionType.Text, "\"  \"", null)]
    public void Answers_are_written_the_way_a_reader_would(QuestionType type, string json, string? expected)
    {
        GetSubmissionPdfQueryHandler.Format(type, JsonDocument.Parse(json).RootElement).Should().Be(expected);
    }
}
