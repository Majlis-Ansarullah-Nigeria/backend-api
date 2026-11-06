using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// The period in which units submit a report.
/// StartDate opens the window and EndDate closes it. Each level has its own due date:
/// open → due is on time, due → close is accepted but late, after close the report is locked.
/// An extension moves the due and close dates for one unit. The server clock decides (BR-WIN-07).
/// </summary>
public class SubmissionWindow : AuditableEntity, IAggregateRoot
{
    public Guid ReportTemplateId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Due date per level; when not set, the level is due when the window closes.
    public DateTime? MuqamDueAt { get; private set; }
    public DateTime? DilaDueAt { get; private set; }
    public DateTime? ZoneDueAt { get; private set; }

    // Navigation properties
    public ReportTemplate ReportTemplate { get; private set; } = default!;

    private readonly List<ReportSubmission> _submissions = new();
    public IReadOnlyCollection<ReportSubmission> Submissions => _submissions.AsReadOnly();

    private readonly List<WindowExtension> _extensions = new();
    public IReadOnlyCollection<WindowExtension> Extensions => _extensions.AsReadOnly();

    private SubmissionWindow() { } // EF Core

    public SubmissionWindow(Guid reportTemplateId, string name, DateTime startDate, DateTime endDate, string? description = null)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException("End date must be after start date");
        }

        ReportTemplateId = reportTemplateId;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Description = description;
    }

    public void UpdateWindow(string name, DateTime startDate, DateTime endDate, string? description = null)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException("End date must be after start date");
        }

        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Description = description;
        SetDueDates(MuqamDueAt, DilaDueAt, ZoneDueAt);
    }

    /// <summary>Due dates must fall inside the window. Null means "due when the window closes".</summary>
    public void SetDueDates(DateTime? muqamDueAt, DateTime? dilaDueAt, DateTime? zoneDueAt)
    {
        foreach (var due in new[] { muqamDueAt, dilaDueAt, zoneDueAt })
        {
            if (due.HasValue && (due.Value < StartDate || due.Value > EndDate))
            {
                throw new ArgumentException("Due dates must be between the window's start and end dates");
            }
        }

        MuqamDueAt = muqamDueAt;
        DilaDueAt = dilaDueAt;
        ZoneDueAt = zoneDueAt;
    }

    /// <summary>Warnings the Secretariat should see, e.g. a parent level due before its children.</summary>
    public IReadOnlyList<string> DueDateWarnings()
    {
        var warnings = new List<string>();
        if (DueAt(OrganizationLevel.Dila) < DueAt(OrganizationLevel.Muqam))
        {
            warnings.Add("Dilaat are due before their Muqamaat, so they cannot use their Muqamaat's reports.");
        }

        if (DueAt(OrganizationLevel.Zone) < DueAt(OrganizationLevel.Dila))
        {
            warnings.Add("Zones are due before their Dilaat, so they cannot use their Dilaat's reports.");
        }

        return warnings;
    }

    public DateTime DueAt(OrganizationLevel level) => level switch
    {
        OrganizationLevel.Muqam => MuqamDueAt ?? EndDate,
        OrganizationLevel.Dila => DilaDueAt ?? EndDate,
        OrganizationLevel.Zone => ZoneDueAt ?? EndDate,
        _ => EndDate
    };

    public WindowExtension? ExtensionFor(OrganizationLevel level, Guid unitId) =>
        _extensions.FirstOrDefault(e => e.OrganizationLevel == level && e.UnitId == unitId);

    /// <summary>Due date for one unit, after any extension.</summary>
    public DateTime DueAtFor(OrganizationLevel level, Guid unitId) =>
        ExtensionFor(level, unitId)?.DueAt ?? DueAt(level);

    /// <summary>Close date for one unit, after any extension.</summary>
    public DateTime ClosesAtFor(OrganizationLevel level, Guid unitId)
    {
        var extension = ExtensionFor(level, unitId);
        return extension is null ? EndDate : (extension.ClosesAt > EndDate ? extension.ClosesAt : EndDate);
    }

    /// <summary>The server clock decides whether a window is open (BR-WIN-07).</summary>
    public bool IsOpen(DateTime nowUtc) => IsActive && nowUtc >= StartDate && nowUtc <= EndDate;

    /// <summary>Whether this unit may still submit, taking its extension into account.</summary>
    public bool IsOpenFor(DateTime nowUtc, OrganizationLevel level, Guid unitId) =>
        IsActive && nowUtc >= StartDate && nowUtc <= ClosesAtFor(level, unitId);

    public bool IsLateFor(DateTime nowUtc, OrganizationLevel level, Guid unitId) =>
        nowUtc > DueAtFor(level, unitId);

    /// <summary>Gives one unit more time. Replaces any earlier extension for that unit.</summary>
    public WindowExtension GrantExtension(OrganizationLevel level, Guid unitId, DateTime dueAt, DateTime closesAt, string reason)
    {
        if (closesAt < dueAt)
        {
            throw new ArgumentException("The extended close date must be on or after the extended due date");
        }

        if (dueAt <= DueAt(level))
        {
            throw new ArgumentException("An extension must move the due date later than the level's due date");
        }

        var existing = ExtensionFor(level, unitId);
        if (existing is not null)
        {
            existing.Update(dueAt, closesAt, reason);
            return existing;
        }

        var extension = new WindowExtension(Id, level, unitId, dueAt, closesAt, reason);
        _extensions.Add(extension);
        return extension;
    }

    public void RemoveExtension(Guid extensionId)
    {
        var extension = _extensions.FirstOrDefault(e => e.Id == extensionId);
        if (extension is not null)
        {
            _extensions.Remove(extension);
        }
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}

/// <summary>More time for one unit in one window.</summary>
public class WindowExtension : AuditableEntity
{
    public Guid SubmissionWindowId { get; private set; }
    public OrganizationLevel OrganizationLevel { get; private set; }
    public Guid UnitId { get; private set; }
    public DateTime DueAt { get; private set; }
    public DateTime ClosesAt { get; private set; }
    public string Reason { get; private set; } = default!;

    private WindowExtension() { } // EF Core

    internal WindowExtension(Guid submissionWindowId, OrganizationLevel level, Guid unitId, DateTime dueAt, DateTime closesAt, string reason)
    {
        SubmissionWindowId = submissionWindowId;
        OrganizationLevel = level;
        UnitId = unitId;
        Update(dueAt, closesAt, reason);
    }

    internal void Update(DateTime dueAt, DateTime closesAt, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required for an extension");
        }

        DueAt = dueAt;
        ClosesAt = closesAt;
        Reason = reason.Trim();
    }
}
