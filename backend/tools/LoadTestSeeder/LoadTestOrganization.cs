using System.Globalization;
using System.Text.Json;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using HRS.Infrastructure.Identity;
using HRS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRS.LoadTestSeeder;

/// <summary>How big the throwaway organization is: <see cref="Zones"/> zones, each with
/// <see cref="DilaatPerZone"/> dilaat, and the rest of <see cref="TotalUnits"/> spread as
/// muqamaat across the dilaat.</summary>
public sealed record LoadTestShape(int Zones, int DilaatPerZone, int TotalUnits)
{
    /// <summary>NFR-PRF-03's example: 5,000 units (10 zones, 100 dilaat, 4,890 muqamaat).</summary>
    public static readonly LoadTestShape Default = new(10, 10, 5_000);

    public int Dilaat => Zones * DilaatPerZone;

    public int Muqamaat => TotalUnits - Zones - Dilaat;

    public void Validate()
    {
        if (Zones < 1 || DilaatPerZone < 1 || Muqamaat < 0)
        {
            throw new ArgumentException($"{TotalUnits} units can't hold {Zones} zones of {DilaatPerZone} dilaat.");
        }
    }

    /// <summary>Muqamaat under the dila at <paramref name="dilaIndex"/> (0-based across the whole
    /// org): an even split, the first few dilaat taking one extra.</summary>
    public int MuqamaatUnder(int dilaIndex) => (Muqamaat / Dilaat) + (dilaIndex < Muqamaat % Dilaat ? 1 : 0);
}

public sealed record LoadTestHead(string Email, Guid UnitId, string UnitCode, UnitLevelCode Level);

public sealed record LoadTestSeedResult(
    Guid ReportTypeId,
    Guid WindowId,
    string SecretariatEmail,
    IReadOnlyList<LoadTestHead> Heads);

/// <summary>
/// M12.1's throwaway organization for the k6 deadline-spike test (NFR-PRF-03): units coded
/// <c>LT…</c> under National, one head per unit, a load-test report type and an open window.
/// Seeding is idempotent for the organization and users and always opens a fresh window, so every
/// run starts with no reports submitted. <see cref="CleanupAsync"/> removes all of it.
/// </summary>
public static class LoadTestOrganization
{
    public const string CodePrefix = "LT";
    public const string EmailDomain = "loadtest.invalid";
    public const string ReportTypeName = "Load test monthly report";

    /// <summary>The template the k6 script fills in — keys must match perf/k6/deadline-spike.js.</summary>
    public static readonly TemplateSchema Schema = new(
    [
        new TemplateSection("activity", "Activity",
        [
            Field("members", TemplateFieldType.Number, "Total members", required: true),
            Field("meetings", TemplateFieldType.Number, "Meetings held", required: true),
            Field("funds", TemplateFieldType.Currency, "Funds collected", required: false),
        ]),
        new TemplateSection("narrative", "Narrative",
        [
            Field("highlights", TemplateFieldType.LongText, "Highlights", required: false),
        ]),
    ]);

    public static string SecretariatEmail => $"lt-secretariat@{EmailDomain}";

    public static string HeadEmail(string unitCode) => $"lt-{unitCode.ToLowerInvariant()}@{EmailDomain}";

    public static async Task<LoadTestSeedResult> SeedAsync(
        AppDbContext db, LoadTestShape shape, string password, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        shape.Validate();

        var units = await EnsureUnitsAsync(db, shape, now, cancellationToken);
        var heads = await EnsureUsersAsync(db, units, password, now, cancellationToken);
        var reportTypeId = await EnsureReportTypeAsync(db, now, cancellationToken);

        // Reference numbers are per period and unit (GMR-2026-10-LTZ01…), so a second window in the
        // same month would collide with the last run's submissions. Each run replaces the last.
        await DeleteWindowsAsync(db, cancellationToken);
        var windowId = await OpenWindowAsync(db, reportTypeId, now, cancellationToken);

        return new LoadTestSeedResult(reportTypeId, windowId, SecretariatEmail, heads);
    }

    /// <summary>Deletes everything <see cref="SeedAsync"/> created — the report type with its
    /// windows and reports, the LT units, and the loadtest.invalid users. Audit events stay: the
    /// audit log is append-only.</summary>
    public static async Task CleanupAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var pathPrefix = $"nat.{CodePrefix.ToLowerInvariant()}%";
        var emailPattern = $"%@{EmailDomain}";
        await DeleteWindowsAsync(db, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Children before parents; every statement is scoped to the load-test report type, LT
        // unit paths or loadtest.invalid users.
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_revisions WHERE report_instance_id IN (SELECT i.id FROM report_instances i JOIN units u ON u.id = i.unit_id WHERE u.path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_comments WHERE report_instance_id IN (SELECT i.id FROM report_instances i JOIN units u ON u.id = i.unit_id WHERE u.path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_attachments WHERE report_instance_id IN (SELECT i.id FROM report_instances i JOIN units u ON u.id = i.unit_id WHERE u.path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM private_notes WHERE observer_unit_id IN (SELECT id FROM units WHERE path LIKE {pathPrefix})
               OR report_instance_id IN (SELECT i.id FROM report_instances i JOIN units u ON u.id = i.unit_id WHERE u.path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_instances WHERE unit_id IN (SELECT id FROM units WHERE path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_instances WHERE window_id IN (SELECT w.id FROM submission_windows w JOIN report_types t ON t.id = w.report_type_id WHERE t.name = {ReportTypeName});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM submission_windows WHERE report_type_id IN (SELECT id FROM report_types WHERE name = {ReportTypeName});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM template_versions WHERE report_type_id IN (SELECT id FROM report_types WHERE name = {ReportTypeName});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_types WHERE name = {ReportTypeName};
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM nudges WHERE unit_id IN (SELECT id FROM units WHERE path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM window_extensions WHERE unit_id IN (SELECT id FROM units WHERE path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM window_reopens WHERE unit_id IN (SELECT id FROM units WHERE path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM window_job_runs WHERE unit_id IN (SELECT id FROM units WHERE path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM invitations WHERE pending_head_of_unit_id IN (SELECT id FROM units WHERE path LIKE {pathPrefix});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM notifications WHERE user_id IN (SELECT id FROM users WHERE email LIKE {emailPattern});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM outbox_messages WHERE recipient_user_id IN (SELECT id FROM users WHERE email LIKE {emailPattern});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM role_assignments WHERE unit_id IN (SELECT id FROM units WHERE path LIKE {pathPrefix})
               OR user_id IN (SELECT id FROM users WHERE email LIKE {emailPattern});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM users WHERE email LIKE {emailPattern};
            """, cancellationToken);

        // Deepest first (muqamaat, dilaat, zones), so no unit is deleted while a child still points at it.
        foreach (var depth in new[] { 4, 3, 2 })
        {
            await db.Database.ExecuteSqlAsync($"""
                DELETE FROM units WHERE path LIKE {pathPrefix} AND length(path) - length(replace(path, '.', '')) + 1 = {depth};
                """, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Removes every load-test window with the reports filed against it.</summary>
    private static async Task DeleteWindowsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var name = ReportTypeName;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_revisions WHERE report_instance_id IN (SELECT i.id FROM report_instances i JOIN submission_windows w ON w.id = i.window_id JOIN report_types t ON t.id = w.report_type_id WHERE t.name = {name});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_comments WHERE report_instance_id IN (SELECT i.id FROM report_instances i JOIN submission_windows w ON w.id = i.window_id JOIN report_types t ON t.id = w.report_type_id WHERE t.name = {name});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_attachments WHERE report_instance_id IN (SELECT i.id FROM report_instances i JOIN submission_windows w ON w.id = i.window_id JOIN report_types t ON t.id = w.report_type_id WHERE t.name = {name});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM private_notes WHERE report_instance_id IN (SELECT i.id FROM report_instances i JOIN submission_windows w ON w.id = i.window_id JOIN report_types t ON t.id = w.report_type_id WHERE t.name = {name});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM report_instances WHERE window_id IN (SELECT w.id FROM submission_windows w JOIN report_types t ON t.id = w.report_type_id WHERE t.name = {name});
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM submission_windows WHERE report_type_id IN (SELECT id FROM report_types WHERE name = {name});
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static TemplateField Field(string key, TemplateFieldType type, string label, bool required) =>
        new(key, type, label, null, required, null, null, null, null, null, false,
            null, null, null, AggregationRule.Sum, null, null, null);

    private static string Code(int zone, int? dila = null, int? muqam = null) =>
        string.Create(CultureInfo.InvariantCulture, $"{CodePrefix}Z{zone:00}")
        + (dila is { } d ? string.Create(CultureInfo.InvariantCulture, $"D{d:00}") : "")
        + (muqam is { } m ? string.Create(CultureInfo.InvariantCulture, $"M{m:00}") : "");

    private static async Task<List<(Unit Unit, UnitLevelCode Level)>> EnsureUnitsAsync(
        AppDbContext db, LoadTestShape shape, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var national = await db.Units.SingleAsync(u => u.Id == OrganizationSeedIds.NationalUnitId, cancellationToken);
        var prefix = $"{national.Path}.{CodePrefix.ToLowerInvariant()}";
        var existing = await db.Units.Where(u => u.Path.StartsWith(prefix)).ToDictionaryAsync(u => u.Code, cancellationToken);

        var result = new List<(Unit, UnitLevelCode)>(shape.TotalUnits);
        Unit Ensure(string code, string name, Guid levelId, Unit parent, UnitLevelCode level)
        {
            if (!existing.TryGetValue(code, out var unit))
            {
                unit = Unit.Create(Guid.CreateVersion7(), code, name, levelId, parent, null, null, null, null, null, now);
                db.Units.Add(unit);
                existing[code] = unit;
            }

            result.Add((unit, level));
            return unit;
        }

        var dilaIndex = 0;
        for (var z = 1; z <= shape.Zones; z++)
        {
            var zone = Ensure(Code(z), $"Load Test Zone {z}", OrganizationSeedIds.ZoneLevelId, national, UnitLevelCode.Zone);
            for (var d = 1; d <= shape.DilaatPerZone; d++, dilaIndex++)
            {
                var dila = Ensure(Code(z, d), $"Load Test Dila {z}-{d}", OrganizationSeedIds.DilaLevelId, zone, UnitLevelCode.Dila);
                for (var m = 1; m <= shape.MuqamaatUnder(dilaIndex); m++)
                {
                    Ensure(Code(z, d, m), $"Load Test Muqam {z}-{d}-{m}", OrganizationSeedIds.MuqamLevelId, dila, UnitLevelCode.Muqam);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static async Task<List<LoadTestHead>> EnsureUsersAsync(
        AppDbContext db, List<(Unit Unit, UnitLevelCode Level)> units, string password, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await db.Users.Where(u => u.Email!.EndsWith("@" + EmailDomain))
            .ToDictionaryAsync(u => u.Email!, cancellationToken);
        var headUnitIds = (await db.RoleAssignments
                .Where(a => a.Role == RoleName.UnitHead && a.EndsAt == null)
                .Select(a => new { a.UnitId, a.UserId })
                .ToListAsync(cancellationToken))
            .Select(a => (a.UnitId, a.UserId))
            .ToHashSet();

        // PBKDF2 at 100k iterations costs tens of milliseconds a hash, so 5,000 users would take
        // minutes. Every load-test user shares one password, and the hash embeds its own salt, so
        // one hash verifies for all of them.
        var hash = new PasswordHasher<ApplicationUser>().HashPassword(null!, password);

        ApplicationUser EnsureUser(string email, string name)
        {
            if (!existing.TryGetValue(email, out var user))
            {
                user = new ApplicationUser
                {
                    Id = Guid.CreateVersion7(),
                    Name = name,
                    UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                };
                existing[email] = user;
                db.Users.Add(user);
            }

            user.PasswordHash = hash;
            return user;
        }

        void Assign(ApplicationUser user, Guid unitId, RoleName role)
        {
            if (headUnitIds.Add((unitId, user.Id)))
            {
                db.RoleAssignments.Add(new RoleAssignment(Guid.CreateVersion7(), user.Id, unitId, role, null, now.AddDays(-1), null, null));
            }
        }

        var secretariat = EnsureUser(SecretariatEmail, "Load Test Secretariat");
        if (!await db.RoleAssignments.AnyAsync(a => a.UserId == secretariat.Id && a.Role == RoleName.Secretariat, cancellationToken))
        {
            db.RoleAssignments.Add(new RoleAssignment(
                Guid.CreateVersion7(), secretariat.Id, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat, null, now.AddDays(-1), null, null));
        }

        var heads = new List<LoadTestHead>(units.Count);
        foreach (var (unit, level) in units)
        {
            var email = HeadEmail(unit.Code);
            var user = EnsureUser(email, $"Head of {unit.Name}");
            Assign(user, unit.Id, RoleName.UnitHead);
            heads.Add(new LoadTestHead(email, unit.Id, unit.Code, level));
        }

        await db.SaveChangesAsync(cancellationToken);
        return heads;
    }

    private static async Task<Guid> EnsureReportTypeAsync(AppDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reportType = await db.ReportTypes.FirstOrDefaultAsync(r => r.Name == ReportTypeName, cancellationToken);
        if (reportType is not null)
        {
            return reportType.Id;
        }

        reportType = ReportType.Create(
            Guid.CreateVersion7(), ReportTypeName, "Throwaway report type for the k6 deadline-spike test.",
            ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
            [UnitLevelCode.Zone, UnitLevelCode.Dila, UnitLevelCode.Muqam], UnitLevelCode.National,
            observerDepth: 1, allowAttachments: false, allowNilReport: false, code: "LTR", now: now);
        var version = TemplateVersion.CreateDraft(Guid.CreateVersion7(), reportType.Id, 1, JsonSerializer.Serialize(Schema, JsonSerializerOptions.Web), now);
        version.Publish(now);

        db.ReportTypes.Add(reportType);
        db.TemplateVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        return reportType.Id;
    }

    /// <summary>A fresh window per run: opened an hour ago, due in two hours at every level (the
    /// "all units submitting within 2 hours of a deadline" of NFR-PRF-03).</summary>
    private static async Task<Guid> OpenWindowAsync(AppDbContext db, Guid reportTypeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var due = now.AddHours(2);
        var window = SubmissionWindow.Create(
            Guid.CreateVersion7(), reportTypeId, string.Create(CultureInfo.InvariantCulture, $"Load test {now:yyyy-MM-dd HH:mm}"),
            now.AddHours(-1), graceDays: 1, closesAt: now.AddDays(3),
            [new WindowLevelDueDate(UnitLevelCode.Zone, due), new WindowLevelDueDate(UnitLevelCode.Dila, due), new WindowLevelDueDate(UnitLevelCode.Muqam, due)],
            now);
        db.SubmissionWindows.Add(window);
        await db.SaveChangesAsync(cancellationToken);
        return window.Id;
    }
}
