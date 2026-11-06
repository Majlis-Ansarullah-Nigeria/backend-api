using System.Text.RegularExpressions;
using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// A kind of report (e.g. General Monthly Report). Templates are versions of a type.
/// The code starts every reference number, so it is locked once a report of the type is submitted.
/// </summary>
public partial class ReportType : AuditableEntity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public ReportFrequency Frequency { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ReportType() { } // EF Core

    public ReportType(string name, string code, ReportFrequency frequency, string? description = null)
    {
        Name = RequireName(name);
        Code = NormalizeCode(code);
        Frequency = frequency;
        Description = description;
    }

    public void Update(string name, string code, ReportFrequency frequency, string? description, bool codeLocked)
    {
        var normalized = NormalizeCode(code);
        if (codeLocked && normalized != Code)
        {
            throw new InvalidOperationException("The code cannot change after a report of this type has been submitted, because it is part of issued reference numbers.");
        }

        Name = RequireName(name);
        Code = normalized;
        Frequency = frequency;
        Description = description;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>2 to 6 capital letters or digits, starting with a letter.</summary>
    public static string NormalizeCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(normalized))
        {
            throw new ArgumentException("The code must be 2 to 6 letters or digits and start with a letter, e.g. GMR.");
        }

        return normalized;
    }

    private static string RequireName(string name) =>
        string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required") : name.Trim();

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,5}$")]
    private static partial Regex CodePattern();
}
