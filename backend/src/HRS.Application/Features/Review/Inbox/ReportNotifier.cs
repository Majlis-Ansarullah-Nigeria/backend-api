using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Inbox;

/// <summary>Shared by Acknowledge and Return (FR-NOT-002's "report acknowledged"/"returned for
/// revision" events) — resolves the unit's CURRENT active head(s), same as SendNudgeCommandHandler
/// (M6.4), since a report belongs to the unit, not whoever happened to submit it (hrs-domain:
/// "Heads change; history stays with the unit"). No outbox/template system exists yet (M8), so
/// this queues plain emails directly, same as the nudge.</summary>
internal static class ReportNotifier
{
    public static async Task NotifyUnitHeadsAsync(
        IAppDbContext db,
        IIdentityService identityService,
        IEmailSender emailSender,
        Guid unitId,
        DateTimeOffset now,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var headUserIds = await db.RoleAssignments
            .AsNoTracking()
            .Where(a => a.Role == RoleName.UnitHead && a.UnitId == unitId
                && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var headUserId in headUserIds)
        {
            var head = await identityService.GetUserAsync(headUserId, cancellationToken);
            if (head is null)
            {
                continue;
            }

            await emailSender.QueueAsync(new EmailMessage(head.Email, subject, body), cancellationToken);
        }
    }
}
