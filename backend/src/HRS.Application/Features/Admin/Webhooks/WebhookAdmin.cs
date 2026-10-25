using FluentValidation;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Webhooks;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Admin.Webhooks;

public sealed record WebhookEndpointDto(
    Guid Id,
    string Url,
    IReadOnlyList<WebhookEvent> Events,
    bool IsActive,
    string SecretHint,
    DateTimeOffset CreatedAt,
    int DeliveredLast24Hours,
    int Pending,
    int Failed,
    DateTimeOffset? LastDeliveryAt,
    WebhookDeliveryStatus? LastDeliveryStatus);

/// <summary>The secret is in the response only when it was just created or rotated.</summary>
public sealed record WebhookSecretResult(WebhookEndpointDto Endpoint, string Secret);

public sealed record WebhookDeliveryDto(
    Guid Id,
    WebhookEvent Event,
    WebhookDeliveryStatus Status,
    int Attempts,
    int? LastStatusCode,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset NextAttemptAt);

internal static class WebhookAccess
{
    /// <summary>SRS §3.2 "Manage system settings, integrations": the system administrator.</summary>
    public static Task<bool> AllowedAsync(IAccessPolicy accessPolicy, Guid userId, CancellationToken cancellationToken) =>
        accessPolicy.HasRoleAsync(userId, RoleName.SysAdmin, cancellationToken);

    public static readonly Error Forbidden = Error.Forbidden("FORBIDDEN", "Only the system administrator manages webhooks.");

    public static readonly Error NotFound = Error.NotFound("WEBHOOK_NOT_FOUND", "That webhook doesn't exist.");

    public static async Task<WebhookEndpointDto> ToDtoAsync(IAppDbContext db, WebhookEndpoint endpoint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var deliveries = db.WebhookDeliveries.AsNoTracking().Where(d => d.EndpointId == endpoint.Id);
        var since = now.AddHours(-24);
        var last = await deliveries.OrderByDescending(d => d.CreatedAt).Select(d => new { d.CreatedAt, d.Status }).FirstOrDefaultAsync(cancellationToken);
        return new WebhookEndpointDto(
            endpoint.Id,
            endpoint.Url,
            endpoint.Events,
            endpoint.IsActive,
            WebhookSigning.Hint(endpoint.Secret),
            endpoint.CreatedAt,
            await deliveries.CountAsync(d => d.Status == WebhookDeliveryStatus.Delivered && d.DeliveredAt >= since, cancellationToken),
            await deliveries.CountAsync(d => d.Status == WebhookDeliveryStatus.Pending, cancellationToken),
            await deliveries.CountAsync(d => d.Status == WebhookDeliveryStatus.Failed, cancellationToken),
            last?.CreatedAt,
            last?.Status);
    }
}

public sealed record GetWebhooksQuery(Guid UserId) : IRequest<Result<IReadOnlyList<WebhookEndpointDto>>>;

public sealed class GetWebhooksQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetWebhooksQuery, Result<IReadOnlyList<WebhookEndpointDto>>>
{
    public async ValueTask<Result<IReadOnlyList<WebhookEndpointDto>>> Handle(GetWebhooksQuery request, CancellationToken cancellationToken)
    {
        if (!await WebhookAccess.AllowedAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<WebhookEndpointDto>>(WebhookAccess.Forbidden);
        }

        var endpoints = await db.WebhookEndpoints.AsNoTracking().OrderBy(e => e.CreatedAt).ToListAsync(cancellationToken);
        var result = new List<WebhookEndpointDto>(endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            result.Add(await WebhookAccess.ToDtoAsync(db, endpoint, clock.UtcNow, cancellationToken));
        }

        return Result.Success<IReadOnlyList<WebhookEndpointDto>>(result);
    }
}

public sealed record SaveWebhookCommand(Guid UserId, Guid? WebhookId, string Url, IReadOnlyList<WebhookEvent> Events, bool IsActive)
    : IRequest<Result<WebhookSecretResult>>;

public sealed class SaveWebhookCommandValidator : AbstractValidator<SaveWebhookCommand>
{
    public SaveWebhookCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .MaximumLength(WebhookEndpoint.MaxUrlLength)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage("Use an absolute https:// address.");
        RuleFor(x => x.Events)
            .NotNull()
            .Must(events => events.Any(e => e != WebhookEvent.Ping))
            .WithMessage("Choose at least one event.");
        RuleForEach(x => x.Events).IsInEnum();
    }
}

/// <summary>FR-ADM-003: creates an endpoint (and returns its new secret, once) or updates one (no
/// secret in the response). Audited; the secret never goes into the audit log.</summary>
public sealed class SaveWebhookCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IAuditLog auditLog)
    : IRequestHandler<SaveWebhookCommand, Result<WebhookSecretResult>>
{
    public async ValueTask<Result<WebhookSecretResult>> Handle(SaveWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!await WebhookAccess.AllowedAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<WebhookSecretResult>(WebhookAccess.Forbidden);
        }

        var now = clock.UtcNow;
        WebhookEndpoint endpoint;
        var secret = string.Empty;
        object? before = null;
        if (request.WebhookId is { } id)
        {
            var existing = await db.WebhookEndpoints.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<WebhookSecretResult>(WebhookAccess.NotFound);
            }

            before = new { existing.Url, Events = existing.Events.Select(e => e.ToString()).ToList(), existing.IsActive };
            existing.Update(request.Url, request.Events, request.IsActive, now);
            endpoint = existing;
        }
        else
        {
            secret = WebhookSigning.NewSecret();
            endpoint = WebhookEndpoint.Create(Guid.CreateVersion7(), request.Url, request.Events, secret, request.UserId, now);
            db.WebhookEndpoints.Add(endpoint);
        }

        auditLog.Add(new AuditEntry(
            AuditActions.WebhookChanged, request.UserId, "webhook", endpoint.Id.ToString(),
            $"{(before is null ? "Added" : "Changed")} webhook {endpoint.Url}",
            Before: before,
            After: new { endpoint.Url, Events = endpoint.Events.Select(e => e.ToString()).ToList(), endpoint.IsActive }));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new WebhookSecretResult(await WebhookAccess.ToDtoAsync(db, endpoint, now, cancellationToken), secret));
    }
}

public sealed record DeleteWebhookCommand(Guid UserId, Guid WebhookId) : IRequest<Result>;

public sealed class DeleteWebhookCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IAuditLog auditLog)
    : IRequestHandler<DeleteWebhookCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!await WebhookAccess.AllowedAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure(WebhookAccess.Forbidden);
        }

        var endpoint = await db.WebhookEndpoints.FirstOrDefaultAsync(e => e.Id == request.WebhookId, cancellationToken);
        if (endpoint is null)
        {
            return Result.Failure(WebhookAccess.NotFound);
        }

        db.WebhookEndpoints.Remove(endpoint);
        auditLog.Add(new AuditEntry(AuditActions.WebhookChanged, request.UserId, "webhook", endpoint.Id.ToString(), $"Removed webhook {endpoint.Url}"));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record RotateWebhookSecretCommand(Guid UserId, Guid WebhookId) : IRequest<Result<WebhookSecretResult>>;

public sealed class RotateWebhookSecretCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IAuditLog auditLog)
    : IRequestHandler<RotateWebhookSecretCommand, Result<WebhookSecretResult>>
{
    public async ValueTask<Result<WebhookSecretResult>> Handle(RotateWebhookSecretCommand request, CancellationToken cancellationToken)
    {
        if (!await WebhookAccess.AllowedAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<WebhookSecretResult>(WebhookAccess.Forbidden);
        }

        var endpoint = await db.WebhookEndpoints.FirstOrDefaultAsync(e => e.Id == request.WebhookId, cancellationToken);
        if (endpoint is null)
        {
            return Result.Failure<WebhookSecretResult>(WebhookAccess.NotFound);
        }

        var secret = WebhookSigning.NewSecret();
        endpoint.RotateSecret(secret, clock.UtcNow);
        auditLog.Add(new AuditEntry(AuditActions.WebhookChanged, request.UserId, "webhook", endpoint.Id.ToString(), $"Rotated the secret of {endpoint.Url}"));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new WebhookSecretResult(await WebhookAccess.ToDtoAsync(db, endpoint, clock.UtcNow, cancellationToken), secret));
    }
}

/// <summary>Queues a "ping" delivery so the administrator can check the receiver end to end.</summary>
public sealed record SendTestWebhookCommand(Guid UserId, Guid WebhookId) : IRequest<Result>;

public sealed class SendTestWebhookCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<SendTestWebhookCommand, Result>
{
    public async ValueTask<Result> Handle(SendTestWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!await WebhookAccess.AllowedAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure(WebhookAccess.Forbidden);
        }

        if (!await db.WebhookEndpoints.AnyAsync(e => e.Id == request.WebhookId, cancellationToken))
        {
            return Result.Failure(WebhookAccess.NotFound);
        }

        var now = clock.UtcNow;
        db.WebhookDeliveries.Add(WebhookDelivery.Create(Guid.CreateVersion7(), request.WebhookId, WebhookEvent.Ping, WebhookPayloads.Ping(now), now));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GetWebhookDeliveriesQuery(Guid UserId, Guid WebhookId) : IRequest<Result<IReadOnlyList<WebhookDeliveryDto>>>;

public sealed class GetWebhookDeliveriesQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<GetWebhookDeliveriesQuery, Result<IReadOnlyList<WebhookDeliveryDto>>>
{
    public async ValueTask<Result<IReadOnlyList<WebhookDeliveryDto>>> Handle(GetWebhookDeliveriesQuery request, CancellationToken cancellationToken)
    {
        if (!await WebhookAccess.AllowedAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<WebhookDeliveryDto>>(WebhookAccess.Forbidden);
        }

        var deliveries = await db.WebhookDeliveries.AsNoTracking()
            .Where(d => d.EndpointId == request.WebhookId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(50)
            .Select(d => new WebhookDeliveryDto(d.Id, d.Event, d.Status, d.Attempts, d.LastStatusCode, d.LastError, d.CreatedAt, d.DeliveredAt, d.NextAttemptAt))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<WebhookDeliveryDto>>(deliveries);
    }
}
