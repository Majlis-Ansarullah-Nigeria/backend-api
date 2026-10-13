// Matches backend/src/HRS.Domain/Notifications/NotificationEventType.cs's enum order exactly —
// the API serializes it as a plain number.
export const NOTIFICATION_EVENT_TYPE = {
  WindowOpened: 0,
  Reminder: 1,
  Overdue: 2,
  ExtensionGranted: 3,
  ReportAcknowledged: 4,
  ReturnedForRevision: 5,
  CommentAdded: 6,
  ChildSubmitted: 7,
  TemplateChanged: 8,
  Escalated: 9,
  Announcement: 10,
} as const;

/** Display order matching FR-NOT-002's own event list, not the enum's declaration order, plus
 * the two later additions (FR-NOT-005's Escalated, FR-NOT-006's Announcement) appended at the end. */
export const NOTIFICATION_EVENT_TYPE_ORDER: number[] = [
  NOTIFICATION_EVENT_TYPE.WindowOpened,
  NOTIFICATION_EVENT_TYPE.Reminder,
  NOTIFICATION_EVENT_TYPE.Overdue,
  NOTIFICATION_EVENT_TYPE.ExtensionGranted,
  NOTIFICATION_EVENT_TYPE.ReportAcknowledged,
  NOTIFICATION_EVENT_TYPE.ReturnedForRevision,
  NOTIFICATION_EVENT_TYPE.CommentAdded,
  NOTIFICATION_EVENT_TYPE.ChildSubmitted,
  NOTIFICATION_EVENT_TYPE.TemplateChanged,
  NOTIFICATION_EVENT_TYPE.Escalated,
  NOTIFICATION_EVENT_TYPE.Announcement,
];

const EVENT_TYPE_LABELS: Record<number, string> = {
  [NOTIFICATION_EVENT_TYPE.WindowOpened]: "Window opened",
  [NOTIFICATION_EVENT_TYPE.Reminder]: "Reminder",
  [NOTIFICATION_EVENT_TYPE.Overdue]: "Overdue",
  [NOTIFICATION_EVENT_TYPE.ExtensionGranted]: "Extension granted",
  [NOTIFICATION_EVENT_TYPE.ReportAcknowledged]: "Report acknowledged",
  [NOTIFICATION_EVENT_TYPE.ReturnedForRevision]: "Returned for revision",
  [NOTIFICATION_EVENT_TYPE.CommentAdded]: "Comment added",
  [NOTIFICATION_EVENT_TYPE.ChildSubmitted]: "Child submitted (to observer)",
  [NOTIFICATION_EVENT_TYPE.TemplateChanged]: "Template changed",
  [NOTIFICATION_EVENT_TYPE.Escalated]: "Escalation",
  [NOTIFICATION_EVENT_TYPE.Announcement]: "Announcement",
};

export function notificationEventTypeLabel(eventType: number): string {
  return EVENT_TYPE_LABELS[eventType] ?? "Unknown event";
}

/** Matches backend Features/Notifications/Center/GetMyNotificationsQuery.cs's NotificationFilter
 * enum order exactly. */
export const NOTIFICATION_FILTER = {
  All: 0,
  Unread: 1,
  Deadlines: 2,
} as const;

export type NotificationIconName =
  | "calendar"
  | "clock"
  | "alert-triangle"
  | "check-circle"
  | "corner-up-left"
  | "message-square"
  | "file-text"
  | "pencil"
  | "megaphone";

export interface NotificationEventStyle {
  icon: NotificationIconName;
  background: string;
  foreground: string;
}

/** Row icon + color per event type — the mockup's own palette (docs/design/README.md's design
 * tokens), reused rather than invented: each type borrows the closest matching status chip pair. */
const EVENT_TYPE_STYLES: Record<number, NotificationEventStyle> = {
  [NOTIFICATION_EVENT_TYPE.WindowOpened]: { icon: "calendar", background: "var(--status-upcoming-bg)", foreground: "var(--status-upcoming-fg)" },
  [NOTIFICATION_EVENT_TYPE.Reminder]: { icon: "clock", background: "var(--status-late-bg)", foreground: "var(--status-late-fg)" },
  [NOTIFICATION_EVENT_TYPE.Overdue]: { icon: "alert-triangle", background: "var(--status-overdue-bg)", foreground: "var(--status-overdue-fg)" },
  [NOTIFICATION_EVENT_TYPE.ExtensionGranted]: { icon: "calendar", background: "var(--status-upcoming-bg)", foreground: "var(--status-upcoming-fg)" },
  [NOTIFICATION_EVENT_TYPE.ReportAcknowledged]: { icon: "check-circle", background: "var(--status-acknowledged-bg)", foreground: "var(--status-acknowledged-fg)" },
  [NOTIFICATION_EVENT_TYPE.ReturnedForRevision]: { icon: "corner-up-left", background: "var(--status-returned-bg)", foreground: "var(--status-returned-fg)" },
  [NOTIFICATION_EVENT_TYPE.CommentAdded]: { icon: "message-square", background: "var(--status-submitted-bg)", foreground: "var(--status-submitted-fg)" },
  [NOTIFICATION_EVENT_TYPE.ChildSubmitted]: { icon: "file-text", background: "var(--status-in-review-bg)", foreground: "var(--status-in-review-fg)" },
  [NOTIFICATION_EVENT_TYPE.TemplateChanged]: { icon: "pencil", background: "var(--status-not-started-bg)", foreground: "var(--status-not-started-fg)" },
  [NOTIFICATION_EVENT_TYPE.Escalated]: { icon: "alert-triangle", background: "var(--status-overdue-bg)", foreground: "var(--status-overdue-fg)" },
  [NOTIFICATION_EVENT_TYPE.Announcement]: { icon: "megaphone", background: "var(--status-upcoming-bg)", foreground: "var(--status-upcoming-fg)" },
};

const DEFAULT_STYLE: NotificationEventStyle = { icon: "file-text", background: "var(--status-not-started-bg)", foreground: "var(--status-not-started-fg)" };

export function notificationEventTypeStyle(eventType: number): NotificationEventStyle {
  return EVENT_TYPE_STYLES[eventType] ?? DEFAULT_STYLE;
}
