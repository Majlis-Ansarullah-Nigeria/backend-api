using System.Globalization;
using ManagementApi.Application.Reports.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ManagementApi.Infrastructure.Reports;

/// <summary>
/// Renders a report as an A4 PDF with QuestPDF: a branded cover banner, summary tiles,
/// colour-coded sections with every question and answer, the review history and page numbers.
/// </summary>
public class ReportPdfRenderer : IReportPdfRenderer
{
    // Brand
    private const string Green = "#1B5E20";
    private const string GreenMid = "#2E7D32";
    private const string GreenSoft = "#E8F5E9";
    private const string Gold = "#C9A227";
    private const string GoldSoft = "#FFF8E1";
    private const string Ink = "#1F2937";
    private const string Muted = "#6B7280";
    private const string Line = "#E5E7EB";
    private const string Zebra = "#F9FAFB";

    // Each section gets its own colour so long reports are easy to scan.
    private static readonly (string Strong, string Soft)[] SectionColours =
    {
        ("#2E7D32", "#E8F5E9"),
        ("#00796B", "#E0F2F1"),
        ("#1565C0", "#E3F2FD"),
        ("#E65100", "#FFF3E0"),
        ("#6A1B9A", "#F3E5F5"),
        ("#AD1457", "#FCE4EC"),
        ("#827717", "#F9FBE7"),
    };

    private static readonly Dictionary<string, (string Label, string Fg, string Bg)> Statuses = new()
    {
        ["Draft"] = ("Draft", "#374151", "#F3F4F6"),
        ["Submitted"] = ("Submitted", "#1565C0", "#E3F2FD"),
        ["InReview"] = ("In review", "#6A1B9A", "#F3E5F5"),
        ["Acknowledged"] = ("Acknowledged", "#2E7D32", "#E8F5E9"),
        ["Returned"] = ("Returned", "#E65100", "#FFF3E0"),
        ["Missed"] = ("Missed", "#C62828", "#FFEBEE"),
    };

    private static readonly Lazy<byte[]?> Logo = new(() =>
    {
        using var stream = typeof(ReportPdfRenderer).Assembly.GetManifestResourceStream("ManagementApi.Infrastructure.Reports.maan-logo.png");
        if (stream is null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    });

    private static readonly TimeZoneInfo Lagos = FindLagos();

    static ReportPdfRenderer()
    {
        // MAAN is a non-profit organisation, which the Community licence covers.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(ReportPdfModel model) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(34);
            page.MarginVertical(26);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontFamily("Lato").FontSize(10).FontColor(Ink).LineHeight(1.3f));

            page.Header().Element(c => Header(c, model));
            page.Content().PaddingTop(14).Element(c => Content(c, model));
            page.Footer().Element(c => Footer(c, model));
        }))
        .WithMetadata(new DocumentMetadata
        {
            Title = $"{model.Title} — {model.UnitName}",
            Author = "Majlis Ansarullah Nigeria",
            Subject = model.ReferenceNumber ?? model.Title,
            Creator = "MAAN Portal",
        })
        .GeneratePdf();

    // ---------- Header ----------

    private static void Header(IContainer container, ReportPdfModel m)
    {
        container.Column(column =>
        {
            // Page 1: the full banner.
            column.Item().ShowOnce().Column(banner =>
            {
                banner.Item().Background(Green).CornerRadius(10).Padding(18).Row(row =>
                {
                    if (Logo.Value is { } logo)
                    {
                        row.ConstantItem(58).Height(58).Background(Colors.White).CornerRadius(29).Padding(5).Image(logo).FitArea();
                        row.ConstantItem(14);
                    }

                    row.RelativeItem().AlignMiddle().Column(text =>
                    {
                        text.Item().Text("MAJLIS ANSARULLAH NIGERIA").FontSize(8.5f).Bold().FontColor(Gold).LetterSpacing(0.12f);
                        text.Item().PaddingTop(2).Text(m.Title).FontSize(19).Bold().FontColor(Colors.White);
                        text.Item().Text(t =>
                        {
                            t.Span(m.WindowName ?? "Report").FontColor("#C8E6C9").FontSize(10.5f);
                            t.Span($"  ·  Template v{m.TemplateVersion}").FontColor("#A5D6A7").FontSize(9);
                        });
                    });

                    row.ConstantItem(150).AlignMiddle().AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text("REFERENCE").FontSize(7.5f).Bold().FontColor("#A5D6A7").LetterSpacing(0.1f);
                        right.Item().AlignRight().Text(m.ReferenceNumber ?? "Not yet assigned").FontSize(10).Bold().FontColor(Colors.White);
                        right.Item().PaddingTop(6).AlignRight().Row(pills =>
                        {
                            pills.AutoItem().Element(c => StatusPill(c, m.Status, onDark: true));
                            if (m.IsLate)
                            {
                                pills.AutoItem().PaddingLeft(4).Background("#FFE0B2").CornerRadius(9).PaddingVertical(2.5f).PaddingHorizontal(8)
                                    .Text("Late").FontSize(8.5f).Bold().FontColor("#BF360C");
                            }
                        });
                    });
                });
                banner.Item().PaddingTop(4).PaddingHorizontal(10).Height(3).Background(Gold);
            });

            // Later pages: a slim running header.
            column.Item().SkipOnce().BorderBottom(1).BorderColor(Line).PaddingBottom(6).Row(row =>
            {
                if (Logo.Value is { } logo)
                {
                    row.ConstantItem(20).Height(20).Image(logo).FitArea();
                    row.ConstantItem(8);
                }

                row.RelativeItem().AlignMiddle().Text(t =>
                {
                    t.Span("Majlis Ansarullah Nigeria").Bold().FontColor(Green).FontSize(9);
                    t.Span($"   {m.Title} · {m.UnitName}").FontColor(Muted).FontSize(9);
                });
                row.AutoItem().AlignMiddle().Text(m.ReferenceNumber ?? string.Empty).FontSize(9).Bold().FontColor(Muted);
            });
        });
    }

    private static void StatusPill(IContainer container, string status, bool onDark = false)
    {
        var (label, fg, bg) = Statuses.TryGetValue(status, out var s) ? s : (status, Ink, "#F3F4F6");
        container.Background(onDark ? "#FFFFFF" : bg).CornerRadius(9).PaddingVertical(2.5f).PaddingHorizontal(9)
            .Text(label).FontSize(8.5f).Bold().FontColor(fg);
    }

    // ---------- Content ----------

    private static void Content(IContainer container, ReportPdfModel m)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column.Item().Element(c => SummaryTiles(c, m));

            if (!string.IsNullOrWhiteSpace(m.ReturnReason))
            {
                column.Item().Background("#FFF3E0").BorderLeft(4).BorderColor("#E65100").CornerRadius(6).Padding(10).Column(note =>
                {
                    note.Item().Text("Returned for changes").Bold().FontColor("#BF360C");
                    note.Item().PaddingTop(2).Text(m.ReturnReason).FontColor(Ink);
                });
            }

            foreach (var section in m.Sections)
            {
                column.Item().Element(c => Section(c, section));
            }

            if (m.History.Count > 0)
            {
                column.Item().Element(c => History(c, m.History));
            }

            column.Item().PaddingTop(6).AlignCenter().Text(t =>
            {
                t.Span("This report belongs to ").FontColor(Muted).FontSize(8.5f);
                t.Span(m.UnitName).Bold().FontColor(Muted).FontSize(8.5f);
                t.Span(" and was produced from the MAAN Portal. All times are Lagos time (WAT).").FontColor(Muted).FontSize(8.5f);
            });
        });
    }

    private static void SummaryTiles(IContainer container, ReportPdfModel m)
    {
        container.Row(row =>
        {
            row.Spacing(8);
            row.RelativeItem().Element(c => Tile(c, "#2E7D32", "#E8F5E9", "UNIT", m.UnitName, m.ParentUnits ?? $"{m.Level} level"));
            row.RelativeItem().Element(c => Tile(c, "#1565C0", "#E3F2FD", "SUBMITTED",
                m.SubmittedAtUtc is { } at ? LagosDateTime(at) : "Not submitted",
                m.SubmittedAtUtc is null ? null : $"by {m.SubmittedBy}"));
            row.RelativeItem().Element(c => Tile(c, "#E65100", "#FFF3E0", "DUE",
                m.DueAtUtc is { } due ? LagosDateTime(due) : "No due date",
                m.AcknowledgedAtUtc is { } ack ? $"Acknowledged {LagosDate(ack)}" : m.IsLate ? "Submitted after the due date" : null));
            row.RelativeItem().Element(c => CompletenessTile(c, m));
        });
    }

    private static void Tile(IContainer container, string strong, string soft, string label, string value, string? detail)
    {
        container.Background(soft).BorderLeft(4).BorderColor(strong).CornerRadius(6).Padding(9).Column(c =>
        {
            c.Item().Text(label).FontSize(7.5f).Bold().FontColor(strong).LetterSpacing(0.1f);
            c.Item().PaddingTop(2).Text(value).FontSize(10.5f).Bold();
            if (!string.IsNullOrWhiteSpace(detail))
            {
                c.Item().Text(detail).FontSize(8.5f).FontColor(Muted);
            }
        });
    }

    private static void CompletenessTile(IContainer container, ReportPdfModel m)
    {
        const string strong = "#6A1B9A";
        var share = m.QuestionCount == 0 ? 0f : (float)m.AnsweredCount / m.QuestionCount;
        container.Background("#F3E5F5").BorderLeft(4).BorderColor(strong).CornerRadius(6).Padding(9).Column(c =>
        {
            c.Item().Text("ANSWERED").FontSize(7.5f).Bold().FontColor(strong).LetterSpacing(0.1f);
            c.Item().PaddingTop(2).Text(t =>
            {
                t.Span($"{m.AnsweredCount}").FontSize(10.5f).Bold();
                t.Span($" of {m.QuestionCount} questions").FontSize(9).FontColor(Muted);
            });
            c.Item().PaddingTop(5).Height(6).Row(bar =>
            {
                if (share > 0)
                {
                    bar.RelativeItem(Math.Max(share, 0.02f)).Background(strong).CornerRadius(3);
                }

                if (share < 1)
                {
                    bar.RelativeItem(1 - share).Background("#E1BEE7").CornerRadius(3);
                }
            });
        });
    }

    private static void Section(IContainer container, ReportPdfSection section)
    {
        var (strong, soft) = SectionColours[(section.Number - 1) % SectionColours.Length];
        var answered = section.Answers.Count(a => !string.IsNullOrWhiteSpace(a.Answer));

        container.Column(column =>
        {
            // Keep the heading with at least its first answers.
            column.Item().EnsureSpace(90).Background(soft).CornerRadius(8).Padding(9).Row(row =>
            {
                row.ConstantItem(24).Height(24).Background(strong).CornerRadius(12).AlignCenter().AlignMiddle()
                    .Text(section.Number.ToString(CultureInfo.InvariantCulture)).FontSize(10).Bold().FontColor(Colors.White);
                row.ConstantItem(10);
                row.RelativeItem().AlignMiddle().Column(title =>
                {
                    title.Item().Text(section.Title).FontSize(12.5f).Bold().FontColor(strong);
                    if (!string.IsNullOrWhiteSpace(section.Description))
                    {
                        title.Item().Text(section.Description).FontSize(8.5f).Italic().FontColor(Muted);
                    }
                });
                row.AutoItem().AlignMiddle().Background(Colors.White).CornerRadius(9).PaddingVertical(2).PaddingHorizontal(8)
                    .Text($"{answered}/{section.Answers.Count} answered").FontSize(8).Bold().FontColor(strong);
            });

            if (section.Answers.Count == 0)
            {
                column.Item().Padding(8).Text("No questions in this section.").Italic().FontColor(Muted);
                return;
            }

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(30);
                    cols.RelativeColumn(5);
                    cols.RelativeColumn(4);
                });

                var i = 0;
                foreach (var a in section.Answers)
                {
                    var background = i++ % 2 == 0 ? "#FFFFFF" : Zebra;
                    IContainer Cell(IContainer c) => c.Background(background).BorderBottom(0.6f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(5);

                    table.Cell().Element(Cell).Text($"{a.Number}.").FontSize(8.5f).Bold().FontColor(strong);
                    table.Cell().Element(Cell).Text(a.Question).FontSize(9.5f).FontColor("#374151");
                    table.Cell().Element(Cell).Element(c =>
                    {
                        if (string.IsNullOrWhiteSpace(a.Answer))
                        {
                            c.Text(a.IsRequired ? "Not answered (required)" : "Not answered").FontSize(9).Italic()
                                .FontColor(a.IsRequired ? "#C62828" : "#9CA3AF");
                        }
                        else
                        {
                            c.BorderLeft(2).BorderColor(strong).PaddingLeft(6).Text(a.Answer).FontSize(9.5f).SemiBold().FontColor(Ink);
                        }
                    });
                }
            });
        });
    }

    private static void History(IContainer container, List<ReportPdfEvent> events)
    {
        container.Column(column =>
        {
            column.Item().EnsureSpace(70).Background(GoldSoft).CornerRadius(8).Padding(9).Text("Review history").FontSize(12.5f).Bold().FontColor("#8D6E00");
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(110);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(4);
                });

                foreach (var e in events)
                {
                    IContainer Cell(IContainer c) => c.BorderBottom(0.6f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(5);
                    table.Cell().Element(Cell).Text(LagosDateTime(e.AtUtc)).FontSize(9).FontColor(Muted);
                    table.Cell().Element(Cell).Column(c =>
                    {
                        c.Item().Element(x => StatusPill(x, e.What));
                        c.Item().PaddingTop(2).Text(e.Who).FontSize(8.5f).FontColor(Muted);
                    });
                    table.Cell().Element(Cell).Text(string.IsNullOrWhiteSpace(e.Note) ? "–" : e.Note).FontSize(9);
                }
            });
        });
    }

    // ---------- Footer ----------

    private static void Footer(IContainer container, ReportPdfModel m)
    {
        container.Column(column =>
        {
            column.Item().Height(1.5f).Background(Gold);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("Majlis Ansarullah Nigeria").Bold().FontColor(GreenMid).FontSize(8);
                    t.Span($"  ·  Generated {LagosDateTime(m.GeneratedAtUtc)} (Lagos time)").FontColor(Muted).FontSize(8);
                });
                row.AutoItem().Text(t =>
                {
                    t.Span("Page ").FontColor(Muted).FontSize(8);
                    t.CurrentPageNumber().Bold().FontColor(Green).FontSize(8);
                    t.Span(" of ").FontColor(Muted).FontSize(8);
                    t.TotalPages().FontColor(Muted).FontSize(8);
                });
            });
        });
    }

    // ---------- Time ----------

    private static TimeZoneInfo FindLagos()
    {
        foreach (var id in new[] { "Africa/Lagos", "W. Central Africa Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("WAT", TimeSpan.FromHours(1), "West Africa Time", "WAT");
    }

    private static DateTime ToLagos(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Lagos);

    private static string LagosDate(DateTime utc) => ToLagos(utc).ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string LagosDateTime(DateTime utc) =>
        ToLagos(utc).ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture).Replace("AM", "am").Replace("PM", "pm");
}
