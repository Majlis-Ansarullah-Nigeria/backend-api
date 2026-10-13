// Matches backend/src/HRS.Domain/Notifications/AnnouncementAudience.cs's enum order exactly.
export const ANNOUNCEMENT_AUDIENCE = {
  AllHeads: 0,
  ZoneHeads: 1,
  DilaHeads: 2,
  MuqamHeads: 3,
} as const;

export const ANNOUNCEMENT_AUDIENCE_ORDER: number[] = [
  ANNOUNCEMENT_AUDIENCE.AllHeads,
  ANNOUNCEMENT_AUDIENCE.ZoneHeads,
  ANNOUNCEMENT_AUDIENCE.DilaHeads,
  ANNOUNCEMENT_AUDIENCE.MuqamHeads,
];

const AUDIENCE_LABELS: Record<number, string> = {
  [ANNOUNCEMENT_AUDIENCE.AllHeads]: "All heads",
  [ANNOUNCEMENT_AUDIENCE.ZoneHeads]: "Zone heads",
  [ANNOUNCEMENT_AUDIENCE.DilaHeads]: "Dila heads",
  [ANNOUNCEMENT_AUDIENCE.MuqamHeads]: "Muqam heads",
};

export function announcementAudienceLabel(audience: number): string {
  return AUDIENCE_LABELS[audience] ?? "Unknown audience";
}
