import { FIGURE_FIELD_TYPE } from "@/features/history/utils/comparison";

const PLAIN = new Intl.NumberFormat("en", { maximumFractionDigits: 2 });
const COMPACT = new Intl.NumberFormat("en", { notation: "compact", maximumFractionDigits: 2 });

/** A figure as dashboards show it: "3,702", "61%", or "NGN 3.85M" (currency is compacted once
 * it reaches a million, with its unit label in front). */
export function formatIndicator(value: number | null | undefined, type: number, unitLabel?: string | null): string {
  if (value == null) {
    return "—";
  }
  if (type === FIGURE_FIELD_TYPE.Percentage) {
    return `${PLAIN.format(value)}%`;
  }
  if (type === FIGURE_FIELD_TYPE.Currency) {
    const amount = Math.abs(value) >= 1_000_000 ? COMPACT.format(value) : PLAIN.format(value);
    return unitLabel ? `${unitLabel} ${amount}` : amount;
  }
  return PLAIN.format(value);
}

/** The last figure that exists, and the one before it, with their positions in the series. */
export function latestTwo(values: (number | null)[]): { latest: number | null; latestIndex: number; previous: number | null; previousIndex: number } {
  let latestIndex = -1;
  let previousIndex = -1;
  for (let i = values.length - 1; i >= 0; i--) {
    if (values[i] == null) {
      continue;
    }
    if (latestIndex === -1) {
      latestIndex = i;
    } else {
      previousIndex = i;
      break;
    }
  }
  return {
    latest: latestIndex === -1 ? null : values[latestIndex],
    latestIndex,
    previous: previousIndex === -1 ? null : values[previousIndex],
    previousIndex,
  };
}

export type ChangeKind = "points" | "percent" | "absolute";

/** How a change is best read for each figure type: percentage fields move in points, currency
 * in percent, counts in plain units. */
export function changeKind(type: number): ChangeKind {
  return type === FIGURE_FIELD_TYPE.Percentage ? "points" : type === FIGURE_FIELD_TYPE.Currency ? "percent" : "absolute";
}

/** The change between two figures in that kind; null when it can't be computed. */
export function change(previous: number | null, latest: number | null, type: number): number | null {
  if (previous == null || latest == null) {
    return null;
  }
  if (changeKind(type) === "percent") {
    return previous === 0 ? null : ((latest - previous) / Math.abs(previous)) * 100;
  }
  return latest - previous;
}

const SIGNED = new Intl.NumberFormat("en", { maximumFractionDigits: 1, signDisplay: "exceptZero" });

export function formatSigned(value: number): string {
  return SIGNED.format(value);
}

/** Share of a target reached, whole percent; null without a positive target. */
export function percentOfTarget(value: number | null, target: number | null): number | null {
  if (value == null || target == null || target <= 0) {
    return null;
  }
  return Math.round((value / target) * 100);
}

/** Where "% of target" turns from amber to green. Not in the SRS — chosen to match the mockup,
 * which shows 71% and below amber and 83% and above green. */
export const TARGET_ON_TRACK_PERCENT = 75;
