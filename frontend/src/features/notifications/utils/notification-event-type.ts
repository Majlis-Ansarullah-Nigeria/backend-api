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
} as const;

/** Display order matching FR-NOT-002's own event list, not the enum's declaration order. */
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
};

export function notificationEventTypeLabel(eventType: number): string {
  return EVENT_TYPE_LABELS[eventType] ?? "Unknown event";
}
