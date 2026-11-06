using System.Globalization;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.ReportTypes;

/// <summary>
/// FR-SUB-010's per-type reference code (ReportType.Code). Optional on create: left blank, a type
/// gets its category's code (GMR, QTR…) or, if another type already has it, the next numbered one
/// (GMR2, GMR3…) — the same rule the AddReportTypeCodes migration used for existing types.
/// </summary>
public static class ReportTypeCodes
{
    public static readonly Error Taken = Error.Conflict("REPORT_TYPE_CODE_TAKEN", "Another report type already uses that code.");

    public static readonly Error Locked = Error.Conflict(
        "REPORT_TYPE_CODE_LOCKED", "Reports of this type have already been submitted, so its code can't change.");

    public static readonly Error NoneFree = Error.Validation(
        "REPORT_TYPE_CODE_REQUIRED", "Every automatic code for this category is in use; choose a code.");

    /// <summary>The next free code for the category, or null once every numbered one that fits
    /// in <see cref="ReportType.MaxCodeLength"/> characters is taken.</summary>
    public static async Task<string?> SuggestAsync(IAppDbContext db, ReportTypeCategory category, CancellationToken cancellationToken)
    {
        var baseCode = ReportReferenceNumber.CategoryCode(category);
        var taken = (await db.ReportTypes.AsNoTracking()
                .Where(r => r.Code.StartsWith(baseCode))
                .Select(r => r.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        if (!taken.Contains(baseCode))
        {
            return baseCode;
        }

        for (var n = 2; ; n++)
        {
            var candidate = baseCode + n.ToString(CultureInfo.InvariantCulture);
            if (candidate.Length > ReportType.MaxCodeLength)
            {
                return null;
            }

            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    public static Task<bool> IsTakenAsync(IAppDbContext db, string code, Guid? exceptReportTypeId, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return db.ReportTypes.AsNoTracking().AnyAsync(r => r.Code == normalized && r.Id != exceptReportTypeId, cancellationToken);
    }

    /// <summary>Whether any report of this type has a reference number yet.</summary>
    public static Task<bool> HasIssuedReferencesAsync(IAppDbContext db, Guid reportTypeId, CancellationToken cancellationToken) =>
        db.ReportInstances.AsNoTracking()
            .Where(i => i.ReferenceNumber != null)
            .Join(db.SubmissionWindows.AsNoTracking().Where(w => w.ReportTypeId == reportTypeId), i => i.WindowId, w => w.Id, (i, _) => i.Id)
            .AnyAsync(cancellationToken);
}
