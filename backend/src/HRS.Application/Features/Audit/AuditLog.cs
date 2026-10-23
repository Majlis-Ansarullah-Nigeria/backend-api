using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Audit;

namespace HRS.Application.Features.Audit;

public sealed class AuditLog(IAppDbContext db, IRequestContext requestContext, IClock clock) : IAuditLog
{
    private const int MaxUserAgentLength = 512;

    public void Add(AuditEntry entry) => db.AuditEvents.Add(ToEvent(entry));

    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    private AuditEvent ToEvent(AuditEntry entry)
    {
        var userAgent = requestContext.UserAgent;
        return AuditEvent.Create(
            Guid.CreateVersion7(),
            clock.UtcNow,
            entry.Action,
            entry.ActorUserId,
            entry.ActorEmail,
            entry.EntityType,
            entry.EntityId,
            entry.UnitId,
            entry.Summary,
            entry.Before is null ? null : JsonSerializer.Serialize(entry.Before, JsonSerializerOptions.Web),
            entry.After is null ? null : JsonSerializer.Serialize(entry.After, JsonSerializerOptions.Web),
            requestContext.IpAddress,
            userAgent is { Length: > MaxUserAgentLength } ? userAgent[..MaxUserAgentLength] : userAgent);
    }
}
