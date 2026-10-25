namespace HRS.Application.Features.Audit;

/// <summary>FR-AUD-001's audited actions: logins, role changes, unit changes, template publishes,
/// window changes, submissions, reviews, exports and downloads. Names are stable — the viewer
/// filters and the CSV export use them as-is.</summary>
public static class AuditActions
{
    public const string SignedIn = "auth.signed_in";
    public const string SignInFailed = "auth.sign_in_failed";
    public const string MfaEnabled = "auth.mfa_enabled";
    public const string MfaDisabled = "auth.mfa_disabled";
    public const string MfaReset = "user.mfa_reset";
    public const string UserDeactivated = "user.deactivated";
    public const string UserReactivated = "user.reactivated";
    public const string SessionsRevoked = "user.sessions_revoked";
    public const string InvitationSent = "user.invited";
    public const string InvitationAccepted = "user.invitation_accepted";

    public const string RoleAssigned = "role.assigned";

    public const string UnitCreated = "unit.created";
    public const string UnitUpdated = "unit.updated";
    public const string UnitArchived = "unit.archived";
    public const string UnitRestored = "unit.restored";
    public const string UnitMoved = "unit.moved";
    public const string UnitsImported = "unit.imported";

    public const string TemplatePublished = "template.published";

    public const string WindowCreated = "window.created";
    public const string WindowUpdated = "window.updated";
    public const string WindowExtended = "window.extended";
    public const string WindowReopened = "window.reopened";

    public const string ReportSubmitted = "report.submitted";
    public const string ReportWithdrawn = "report.withdrawn";
    public const string ReportAcknowledged = "report.acknowledged";
    public const string ReportReturned = "report.returned";
    public const string ReportFlagged = "report.flagged";
    public const string ReportAssigned = "report.assigned";

    public const string ExportRequested = "export.requested";
    public const string ExportDownloaded = "export.downloaded";
    public const string ReportDownloaded = "report.downloaded";
    public const string CompiledReportDownloaded = "compiled_report.downloaded";
    public const string CompiledReportEdited = "compiled_report.edited";

    public const string AuditExported = "audit.exported";

    public const string SettingsUpdated = "settings.updated";
    public const string NotificationsRetried = "notifications.retried";
    public const string FeatureToggled = "feature.toggled";
    public const string WebhookChanged = "webhook.changed";

    public static readonly IReadOnlyList<string> All =
    [
        SignedIn, SignInFailed, MfaEnabled, MfaDisabled, MfaReset, UserDeactivated, UserReactivated, SessionsRevoked,
        InvitationSent, InvitationAccepted, RoleAssigned, UnitCreated, UnitUpdated, UnitArchived, UnitRestored, UnitMoved,
        UnitsImported, TemplatePublished, WindowCreated, WindowUpdated, WindowExtended, WindowReopened, ReportSubmitted,
        ReportWithdrawn, ReportAcknowledged, ReportReturned, ReportFlagged, ReportAssigned, ExportRequested, ExportDownloaded,
        ReportDownloaded, CompiledReportDownloaded, CompiledReportEdited, AuditExported, SettingsUpdated, NotificationsRetried, FeatureToggled, WebhookChanged,
    ];
}
