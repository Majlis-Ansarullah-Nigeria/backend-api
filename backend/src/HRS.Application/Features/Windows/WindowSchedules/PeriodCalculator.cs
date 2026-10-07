using System.Globalization;
using HRS.Domain.Templates;

namespace HRS.Application.Features.Windows.WindowSchedules;

/// <summary>
/// Pure calendar-period math for schedule generation (FR-WIN-002). Period 0 is whichever period
/// (month/quarter/year) `now` currently falls inside; later offsets move forward from there — so
/// "generate 12 months ahead" means the current period plus the next 11, not 12 periods after the
/// current one. Period labels ("October 2026", "Q3 2026", "2026") double as the idempotency key
/// the generator uses to skip periods that already have a window.
/// </summary>
public static class PeriodCalculator
{
    public static int PeriodsAheadFor(ReportTypeFrequency frequency) => frequency switch
    {
        ReportTypeFrequency.Monthly => 12,
        ReportTypeFrequency.Quarterly => 4,
        ReportTypeFrequency.Annual => 1,
        _ => 0,
    };

    public static (string Label, DateTimeOffset End) GetPeriod(ReportTypeFrequency frequency, DateTimeOffset now, int offset)
    {
        switch (frequency)
        {
            case ReportTypeFrequency.Monthly:
                {
                    var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(offset);
                    var end = start.AddMonths(1).AddTicks(-1);
                    return (start.ToString("MMMM yyyy", CultureInfo.InvariantCulture), end);
                }

            case ReportTypeFrequency.Quarterly:
                {
                    var currentQuarterStartMonth = (((now.Month - 1) / 3) * 3) + 1;
                    var start = new DateTimeOffset(now.Year, currentQuarterStartMonth, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(offset * 3);
                    var end = start.AddMonths(3).AddTicks(-1);
                    var quarterNumber = ((start.Month - 1) / 3) + 1;
                    return ($"Q{quarterNumber} {start.Year}", end);
                }

            case ReportTypeFrequency.Annual:
                {
                    var start = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero).AddYears(offset);
                    var end = start.AddYears(1).AddTicks(-1);
                    return (start.Year.ToString(CultureInfo.InvariantCulture), end);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Can't compute periods for this frequency.");
        }
    }
}
