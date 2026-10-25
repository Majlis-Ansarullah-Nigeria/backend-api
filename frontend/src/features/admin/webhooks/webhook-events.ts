/** Mirrors the backend WebhookEvent enum (FR-ADM-003); Ping is the test delivery only. */
export const WEBHOOK_EVENT = {
  ReportSubmitted: 0,
  ReportAcknowledged: 1,
  Ping: 2,
} as const;

export const SUBSCRIBABLE_EVENTS = [WEBHOOK_EVENT.ReportSubmitted, WEBHOOK_EVENT.ReportAcknowledged] as const;

export const WEBHOOK_DELIVERY_STATUS = {
  Pending: 0,
  Delivered: 1,
  Failed: 2,
} as const;

/** The event's name on the wire — a technical identifier receivers match on, so not translated. */
export function webhookEventName(event: number): string {
  return event === WEBHOOK_EVENT.ReportSubmitted ? "report.submitted" : event === WEBHOOK_EVENT.ReportAcknowledged ? "report.acknowledged" : "ping";
}

export function isHttpsUrl(value: string): boolean {
  try {
    return new URL(value).protocol === "https:";
  } catch {
    return false;
  }
}
