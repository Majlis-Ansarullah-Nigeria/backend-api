// Matches backend/src/HRS.Domain/Organization/UnitLevelCode.cs's enum order — serialized as a
// plain number. Display names always come from the API (UnitLevel's configurable names).
export const UNIT_LEVEL = {
  National: 0,
  Zone: 1,
  Dila: 2,
  Muqam: 3,
  Department: 4,
} as const;

// Matches backend/src/HRS.Application/Features/Analytics/GetSecretariatOverviewQuery.cs.
export const WINDOW_EVENT_KIND = {
  Opens: 0,
  Closes: 1,
} as const;

export interface PeriodCounts {
  periodLabel: string;
  isClosed: boolean;
  expected: number;
  onTime: number;
  late: number;
  missed: number;
  drafting: number;
  notStarted: number;
}

/** Whole-number share of a total; 0 when there's nothing expected. */
export function percentOf(part: number, total: number): number {
  return total === 0 ? 0 : Math.round((part / total) * 100);
}

export interface ComplianceKpis {
  lastFull: PeriodCounts | null;
  onTimePercent: number | null;
  /** Points up (+) or down (−) versus the full period before it. */
  onTimeChange: number | null;
  averageOnTimePercent: number | null;
  latePercent: number | null;
  missedPercent: number | null;
}

/** FR-DSH-001's KPI tiles: the last *closed* period (an open one isn't final yet), its change
 * from the closed period before it, and the on-time average across every closed period shown. */
export function complianceKpis(periods: PeriodCounts[]): ComplianceKpis {
  const closed = periods.filter((period) => period.isClosed && period.expected > 0);
  const lastFull = closed.at(-1) ?? null;
  const previous = closed.at(-2) ?? null;

  if (!lastFull) {
    return { lastFull: null, onTimePercent: null, onTimeChange: null, averageOnTimePercent: null, latePercent: null, missedPercent: null };
  }

  const onTimePercent = percentOf(lastFull.onTime, lastFull.expected);
  const average = closed.reduce((sum, period) => sum + period.onTime / period.expected, 0) / closed.length;
  return {
    lastFull,
    onTimePercent,
    onTimeChange: previous ? onTimePercent - percentOf(previous.onTime, previous.expected) : null,
    averageOnTimePercent: Math.round(average * 100),
    latePercent: percentOf(lastFull.late, lastFull.expected),
    missedPercent: percentOf(lastFull.missed, lastFull.expected),
  };
}

/** The mockups' two-colour threshold: half or more submitted reads as on track. */
export const ON_TRACK_THRESHOLD = 50;

export function isOnTrack(percent: number): boolean {
  return percent >= ON_TRACK_THRESHOLD;
}
