using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Invitations;
using HRS.Domain.Notifications;
using HRS.Domain.Observer;
using HRS.Domain.Organization;
using HRS.Domain.Review;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HRS.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    DbSet<Invitation> Invitations { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<NotificationTemplate> NotificationTemplates { get; }

    DbSet<Nudge> Nudges { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<PrivateNote> PrivateNotes { get; }

    DbSet<PushSubscription> PushSubscriptions { get; }

    DbSet<ReportAttachment> ReportAttachments { get; }

    DbSet<ReportComment> ReportComments { get; }

    DbSet<ReportInstance> ReportInstances { get; }

    DbSet<ReportType> ReportTypes { get; }

    DbSet<RoleAssignment> RoleAssignments { get; }

    DbSet<SavedView> SavedViews { get; }

    DbSet<SubmissionWindow> SubmissionWindows { get; }

    DbSet<TemplateVersion> TemplateVersions { get; }

    DbSet<UnitLevel> UnitLevels { get; }

    DbSet<Unit> Units { get; }

    DbSet<WindowJobRun> WindowJobRuns { get; }

    DbSet<WindowSchedule> WindowSchedules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Exposes the change tracker's entry for a tracked entity — needed to set a
    /// concurrency token's OriginalValue to a client-supplied If-Match value before saving
    /// (ReportInstance.RowVersion), which EF's own DbSet API has no way to express.</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
