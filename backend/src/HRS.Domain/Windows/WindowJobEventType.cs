namespace HRS.Domain.Windows;

/// <summary>The window-lifecycle events the M4.4 worker detects (BR-WIN-08). Each is logged once
/// per (window, unit, EventKey) — M8's outbox dispatcher is what actually turns a row here into a
/// sent notification; this just guarantees the detection itself is idempotent.</summary>
public enum WindowJobEventType
{
    Opened,
    Reminder,
    Overdue,
    Closed,
    Escalated,
}
