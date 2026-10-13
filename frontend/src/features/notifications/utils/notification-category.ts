// Matches backend/src/HRS.Domain/Notifications/NotificationCategory.cs's enum order exactly.
export const NOTIFICATION_CATEGORY = {
  WindowOpened: 0,
  DeadlineReminders: 1,
  ReportStatusChanged: 2,
  Comments: 3,
  Announcements: 4,
} as const;

/** Row order and copy straight from the Profile mockup's preference matrix. */
export const NOTIFICATION_CATEGORY_ORDER: number[] = [
  NOTIFICATION_CATEGORY.WindowOpened,
  NOTIFICATION_CATEGORY.DeadlineReminders,
  NOTIFICATION_CATEGORY.ReportStatusChanged,
  NOTIFICATION_CATEGORY.Comments,
  NOTIFICATION_CATEGORY.Announcements,
];

const CATEGORY_LABELS: Record<number, string> = {
  [NOTIFICATION_CATEGORY.WindowOpened]: "Window opened",
  [NOTIFICATION_CATEGORY.DeadlineReminders]: "Deadline reminders",
  [NOTIFICATION_CATEGORY.ReportStatusChanged]: "Report returned or acknowledged",
  [NOTIFICATION_CATEGORY.Comments]: "Comments on my reports",
  [NOTIFICATION_CATEGORY.Announcements]: "Announcements",
};

export function notificationCategoryLabel(category: number): string {
  return CATEGORY_LABELS[category] ?? "Unknown category";
}
