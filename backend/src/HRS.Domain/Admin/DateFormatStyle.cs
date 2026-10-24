namespace HRS.Domain.Admin;

/// <summary>FR-ADM-001's date format: how dates read across the app, PDFs and emails.</summary>
public enum DateFormatStyle
{
    /// <summary>4 Oct 2026</summary>
    DayMonthYear,

    /// <summary>Oct 4, 2026</summary>
    MonthDayYear,

    /// <summary>2026-10-04</summary>
    Iso,
}
