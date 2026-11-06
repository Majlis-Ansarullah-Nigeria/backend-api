using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Records that a due-date reminder was sent to a unit, so re-running the job never sends it twice.
/// Kind is the number of days before the unit's due date (3, 1 or 0).
/// </summary>
public class ReportReminder : BaseEntity
{
    public Guid SubmissionWindowId { get; private set; }
    public OrganizationLevel OrganizationLevel { get; private set; }
    public Guid UnitId { get; private set; }
    public int DaysBeforeDue { get; private set; }
    public DateTime SentAt { get; private set; }

    private ReportReminder() { } // EF Core

    public ReportReminder(Guid submissionWindowId, OrganizationLevel level, Guid unitId, int daysBeforeDue, DateTime sentAt)
    {
        SubmissionWindowId = submissionWindowId;
        OrganizationLevel = level;
        UnitId = unitId;
        DaysBeforeDue = daysBeforeDue;
        SentAt = sentAt;
    }
}
