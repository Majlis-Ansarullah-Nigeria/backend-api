using System.ComponentModel.DataAnnotations;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Unit = HRS.Domain.Organization.Unit;

namespace HRS.Application.Features.Organization.BulkImport;

/// <summary>
/// Resolves raw uploaded rows against the current database state (levels, existing units,
/// active heads). Shared by preview (read-only) and commit (re-validates before writing, so a
/// stale client-held preview can never be trusted) — one rule set, never duplicated.
/// </summary>
internal sealed class BulkImportProcessor(IAppDbContext db, IClock clock)
{
    public async Task<Result<IReadOnlyList<ResolvedBulkImportRow>>> ResolveAsync(
        IReadOnlyList<BulkImportRawRow> rawRows, CancellationToken cancellationToken)
    {
        if (rawRows.Count == 0)
        {
            return Result.Failure<IReadOnlyList<ResolvedBulkImportRow>>(
                Error.Validation("EMPTY_FILE", "The file has no data rows."));
        }

        var firstRowHeaders = rawRows[0].Values.Keys;
        var missingHeaders = BulkImportColumns.Required.Where(required => !firstRowHeaders.Contains(required)).ToList();
        if (missingHeaders.Count > 0)
        {
            return Result.Failure<IReadOnlyList<ResolvedBulkImportRow>>(
                Error.Validation(
                    "MISSING_COLUMNS",
                    $"Missing required column(s): {string.Join(", ", missingHeaders)}. Use the downloadable template."));
        }

        var levels = await db.UnitLevels.AsNoTracking().ToListAsync(cancellationToken);
        var levelByName = levels.ToDictionary(l => l.Name, l => l, StringComparer.OrdinalIgnoreCase);
        var levelCodeById = levels.ToDictionary(l => l.Id, l => l.Code);

        var existingUnits = await db.Units.AsNoTracking().Where(u => u.ArchivedAt == null).ToListAsync(cancellationToken);
        var unitsByDisplayCode = new Dictionary<string, Unit>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in existingUnits)
        {
            unitsByDisplayCode[unit.DisplayCode()] = unit;
        }

        var now = clock.UtcNow;
        var unitsWithActiveHead = (await db.RoleAssignments
                .AsNoTracking()
                .Where(a => a.Role == RoleName.UnitHead && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
                .Select(a => a.UnitId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        // Process in level-depth order (Zone/Dept before Dila before Muqam) so a parent created
        // earlier in this same batch is resolvable even if it appears later in the file; a
        // stable OrderBy preserves file order within the same depth.
        var ordered = rawRows
            .Select((row, index) => (row, index))
            .OrderBy(x => levelByName.TryGetValue(Get(x.row, BulkImportColumns.Level), out var level) ? level.Depth : int.MaxValue)
            .ThenBy(x => x.index)
            .ToList();

        var results = new ResolvedBulkImportRow[rawRows.Count];
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (row, index) in ordered)
        {
            results[index] = ResolveRow(row, levelByName, levelCodeById, unitsByDisplayCode, unitsWithActiveHead, seenCodes, now);
        }

        return Result.Success<IReadOnlyList<ResolvedBulkImportRow>>(results);
    }

    private static ResolvedBulkImportRow ResolveRow(
        BulkImportRawRow row,
        Dictionary<string, UnitLevel> levelByName,
        Dictionary<Guid, UnitLevelCode> levelCodeById,
        Dictionary<string, Unit> unitsByDisplayCode,
        HashSet<Guid> unitsWithActiveHead,
        HashSet<string> seenCodes,
        DateTimeOffset now)
    {
        var levelInput = Get(row, BulkImportColumns.Level);
        var code = Get(row, BulkImportColumns.Code);
        var name = Get(row, BulkImportColumns.Name);
        var parentCodeInput = Get(row, BulkImportColumns.ParentCode);
        var address = NullIfBlank(Get(row, BulkImportColumns.Address));
        var contactPhone = NullIfBlank(Get(row, BulkImportColumns.ContactPhone));
        var contactEmail = NullIfBlank(Get(row, BulkImportColumns.ContactEmail));
        var headName = Get(row, BulkImportColumns.HeadName);
        var headEmail = Get(row, BulkImportColumns.HeadEmail);

        string? issue = Validate(levelInput, code, name, headEmail, levelByName, out var level);

        Unit? parent = null;
        if (issue is null)
        {
            if (!seenCodes.Add(code))
            {
                issue = "Duplicate code in file.";
            }
            else if (!unitsByDisplayCode.TryGetValue(parentCodeInput, out parent))
            {
                issue = string.IsNullOrEmpty(parentCodeInput)
                    ? "Parent code is required for this level."
                    : $"Parent code \"{parentCodeInput}\" not found.";
            }
            else if (!UnitHierarchyRules.CanBeChildOf(level!.Code, levelCodeById[parent.LevelId]))
            {
                issue = $"\"{(parentCodeInput.Length == 0 ? "National" : parentCodeInput)}\" is not a valid parent for a {level.Name}.";
            }
        }

        if (issue is not null)
        {
            return new ResolvedBulkImportRow(
                row.RowNumber, BulkImportOutcome.Error, levelInput, code, string.Empty, name, parentCodeInput,
                address, contactPhone, contactEmail, headName, headEmail, issue, level, parent, null, null, false);
        }

        var localCode = ExtractLocalCode(code);
        var existing = unitsByDisplayCode.TryGetValue(code, out var found) ? found : null;
        var isNew = existing is null;

        var newUnit = isNew
            ? Unit.Create(
                Guid.CreateVersion7(), localCode, name, level!.Id, parent!, address, contactPhone, contactEmail,
                latitude: null, longitude: null, now)
            : null;

        var hasBlankHead = string.IsNullOrEmpty(headEmail);
        var existingHasActiveHead = !isNew && unitsWithActiveHead.Contains(existing!.Id);

        BulkImportOutcome outcome;
        string? warning;

        if (hasBlankHead)
        {
            outcome = BulkImportOutcome.Warning;
            warning = "No head; unit will be vacant.";
        }
        else if (existingHasActiveHead)
        {
            // Plan decision: bulk import never replaces an active head — only M2.4's
            // interactive "assign a head" flow does that, with its own term dates.
            outcome = BulkImportOutcome.Warning;
            warning = "Unit already has a head; not changed by import.";
        }
        else
        {
            outcome = isNew ? BulkImportOutcome.New : BulkImportOutcome.Update;
            warning = null;
        }

        var shouldSetHead = !hasBlankHead && !existingHasActiveHead;

        var resolved = new ResolvedBulkImportRow(
            row.RowNumber, outcome, levelInput, code, localCode, name, parentCodeInput,
            address, contactPhone, contactEmail, headName, headEmail, warning, level, parent, existing, newUnit, shouldSetHead);

        // Register this row's unit (new or existing) under its code so a later, deeper row in
        // this same batch can resolve it as a parent.
        unitsByDisplayCode[code] = resolved.ResolvedUnit!;

        return resolved;
    }

    private static string? Validate(
        string levelInput, string code, string name, string headEmail,
        Dictionary<string, UnitLevel> levelByName, out UnitLevel? level)
    {
        if (!levelByName.TryGetValue(levelInput, out level) || level.Code == UnitLevelCode.National)
        {
            return $"Unknown level \"{levelInput}\".";
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return "Code is required.";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }

        if (!string.IsNullOrEmpty(headEmail) && !new EmailAddressAttribute().IsValid(headEmail))
        {
            return "Invalid email.";
        }

        return null;
    }

    private static string ExtractLocalCode(string compoundCode)
    {
        var lastDash = compoundCode.LastIndexOf('-');
        return lastDash < 0 ? compoundCode : compoundCode[(lastDash + 1)..];
    }

    private static string Get(BulkImportRawRow row, string column) =>
        row.Values.TryGetValue(column, out var value) ? value.Trim() : string.Empty;

    private static string? NullIfBlank(string value) => value.Length == 0 ? null : value;
}
