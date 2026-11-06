using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Common;

/// <summary>The unit a report belongs to, with its ancestors in the Zone → Dila → Muqam tree.</summary>
public record ReportingUnit(
    OrganizationLevel Level,
    Guid UnitId,
    Guid? MuqamId,
    Guid? DilaId,
    Guid? ZoneId);

/// <summary>
/// Who may see and act on reports.
/// - National Secretariat (NationalAdmin, SuperAdmin): sees every report and is the only reviewer.
/// - A unit head acts only on their own unit's report.
/// - Observers are read-only: a Dila head sees their Muqamaat's reports, a Zone head sees their Dilaat's reports.
/// </summary>
public static class ReportingScope
{
    public static bool IsSecretariat(ICurrentUserService user) =>
        user.IsInRole(Shared.Authorization.Roles.NationalAdmin) || user.IsInRole(Shared.Authorization.Roles.SuperAdmin);

    /// <summary>
    /// Resolves the current user's own reporting unit from their account, and its ancestors
    /// from the organisation tree (the tree is authoritative, not the token).
    /// </summary>
    public static async Task<(ReportingUnit? Unit, string? Error)> ResolveOwnUnitAsync(
        ICurrentUserService user,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        switch (user.OrganizationLevel)
        {
            case OrganizationLevel.Muqam:
            {
                if (!user.MuqamId.HasValue)
                {
                    return (null, "Your account is not linked to a Muqam. Ask the National Secretariat to update it.");
                }

                var muqam = await context.Muqams
                    .Where(m => m.Id == user.MuqamId.Value)
                    .Select(m => new { m.Id, m.DilaId, ZoneId = m.Dila != null ? m.Dila.ZoneId : null })
                    .FirstOrDefaultAsync(cancellationToken);

                return muqam is null
                    ? (null, "Your Muqam was not found.")
                    : (new ReportingUnit(OrganizationLevel.Muqam, muqam.Id, muqam.Id, muqam.DilaId, muqam.ZoneId), null);
            }

            case OrganizationLevel.Dila:
            {
                if (!user.DilaId.HasValue)
                {
                    return (null, "Your account is not linked to a Dila. Ask the National Secretariat to update it.");
                }

                var dila = await context.Dilas
                    .Where(d => d.Id == user.DilaId.Value)
                    .Select(d => new { d.Id, d.ZoneId })
                    .FirstOrDefaultAsync(cancellationToken);

                return dila is null
                    ? (null, "Your Dila was not found.")
                    : (new ReportingUnit(OrganizationLevel.Dila, dila.Id, null, dila.Id, dila.ZoneId), null);
            }

            case OrganizationLevel.Zone:
            {
                if (!user.ZoneId.HasValue)
                {
                    return (null, "Your account is not linked to a Zone. Ask the National Secretariat to update it.");
                }

                var zoneExists = await context.Zones.AnyAsync(z => z.Id == user.ZoneId.Value, cancellationToken);

                return zoneExists
                    ? (new ReportingUnit(OrganizationLevel.Zone, user.ZoneId.Value, null, null, user.ZoneId.Value), null)
                    : (null, "Your Zone was not found.");
            }

            case OrganizationLevel.National:
                return (new ReportingUnit(OrganizationLevel.National, Guid.Empty, null, null, null), null);

            default:
                return (null, "Your account cannot submit reports.");
        }
    }

    /// <summary>Limits a report query to what the current user may see.</summary>
    public static IQueryable<ReportSubmission> VisibleTo(this IQueryable<ReportSubmission> query, ICurrentUserService user)
    {
        if (IsSecretariat(user) || user.OrganizationLevel == OrganizationLevel.National)
        {
            return query;
        }

        switch (user.OrganizationLevel)
        {
            case OrganizationLevel.Zone when user.ZoneId.HasValue:
                var zoneId = user.ZoneId.Value;
                return query.Where(s =>
                    (s.OrganizationLevel == OrganizationLevel.Zone && s.UnitId == zoneId) ||
                    (s.OrganizationLevel == OrganizationLevel.Dila && s.ZoneId == zoneId));

            case OrganizationLevel.Dila when user.DilaId.HasValue:
                var dilaId = user.DilaId.Value;
                return query.Where(s =>
                    (s.OrganizationLevel == OrganizationLevel.Dila && s.UnitId == dilaId) ||
                    (s.OrganizationLevel == OrganizationLevel.Muqam && s.DilaId == dilaId));

            case OrganizationLevel.Muqam when user.MuqamId.HasValue:
                var muqamId = user.MuqamId.Value;
                return query.Where(s => s.OrganizationLevel == OrganizationLevel.Muqam && s.UnitId == muqamId);

            default:
                return query.Where(_ => false);
        }
    }

    /// <summary>True when the report belongs to the current user's own unit (they may edit it).</summary>
    public static bool OwnsReport(ICurrentUserService user, ReportSubmission submission) =>
        submission.OrganizationLevel == user.OrganizationLevel && submission.OrganizationLevel switch
        {
            OrganizationLevel.Muqam => user.MuqamId == submission.UnitId,
            OrganizationLevel.Dila => user.DilaId == submission.UnitId,
            OrganizationLevel.Zone => user.ZoneId == submission.UnitId,
            OrganizationLevel.National => true,
            _ => false
        };
}
