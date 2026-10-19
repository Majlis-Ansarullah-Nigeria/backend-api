// Matches backend/src/HRS.Domain/Templates/TemplateFieldType.cs's enum order — serialized as a
// plain number. Only the three figure types Compare periods returns are needed here.
export const FIGURE_FIELD_TYPE = {
  Number: 2,
  Currency: 3,
  Percentage: 4,
} as const;

// Matches backend/src/HRS.Domain/Submissions/ReportRevisionAction.cs's enum order.
export const REPORT_REVISION_ACTION = {
  Submitted: 0,
  Withdrawn: 1,
  Resubmitted: 2,
} as const;

export interface FigureChange {
  delta: number;
  /** Null when there's no meaningful base (A is 0) or the field is itself a percentage. */
  percent: number | null;
}

export interface ComparableFigure {
  fieldKey: string;
  label: string;
  type: number;
  periodA: number | null;
  periodB: number | null;
}

export interface SeriesPoint {
  periodLabel: string;
  values: Record<string, number | null>;
}

/** FR-HIS-005's "changed figures": B − A, plus the percent change shown beside it. Null when
 * either side is unanswered — a missing value isn't a change of zero. */
export function figureChange(figure: Pick<ComparableFigure, "type" | "periodA" | "periodB">): FigureChange | null {
  if (figure.periodA == null || figure.periodB == null) {
    return null;
  }

  const delta = figure.periodB - figure.periodA;
  const percent =
    figure.type === FIGURE_FIELD_TYPE.Percentage || figure.periodA === 0 ? null : (delta / Math.abs(figure.periodA)) * 100;
  return { delta, percent };
}

export type Insight =
  | { kind: "rise"; label: string; delta: number; isLargestRise: boolean; periods: number }
  | { kind: "fall"; label: string; from: number; to: number }
  | { kind: "text"; sections: number }
  | { kind: "none" };

const MAX_FALLS = 3;

/** The "What changed" card: a rise in the charted figure (noting when it's the largest
 * period-on-period rise in the trend), every figure that fell, and how many text sections
 * changed. */
export function buildInsights(
  figures: ComparableFigure[],
  series: SeriesPoint[],
  changedTextSections: string[],
  chartFieldKey: string | undefined,
): Insight[] {
  const insights: Insight[] = [];

  const charted = figures.find((figure) => figure.fieldKey === chartFieldKey);
  const chartedChange = charted ? figureChange(charted) : null;
  if (charted && chartedChange && chartedChange.delta > 0) {
    const values = series.map((point) => point.values[charted.fieldKey]).filter((value): value is number => value != null);
    const rises = values.slice(1).map((value, index) => value - values[index]);
    const isLargestRise = rises.length > 1 && chartedChange.delta >= Math.max(...rises);
    insights.push({ kind: "rise", label: charted.label, delta: chartedChange.delta, isLargestRise, periods: values.length });
  }

  for (const figure of figures) {
    const change = figureChange(figure);
    if (change && change.delta < 0 && insights.filter((i) => i.kind === "fall").length < MAX_FALLS) {
      insights.push({ kind: "fall", label: figure.label, from: figure.periodA!, to: figure.periodB! });
    }
  }

  if (changedTextSections.length > 0) {
    insights.push({ kind: "text", sections: changedTextSections.length });
  }

  return insights.length > 0 ? insights : [{ kind: "none" }];
}

const NUMBER_FORMATTER = new Intl.NumberFormat("en", { maximumFractionDigits: 2 });
const PERCENT_FORMATTER = new Intl.NumberFormat("en", { maximumFractionDigits: 1, signDisplay: "exceptZero" });
const SIGNED_FORMATTER = new Intl.NumberFormat("en", { maximumFractionDigits: 2, signDisplay: "exceptZero" });

export function formatFigure(value: number | null, type: number): string {
  if (value == null) {
    return "—";
  }
  const formatted = NUMBER_FORMATTER.format(value);
  return type === FIGURE_FIELD_TYPE.Percentage ? `${formatted}%` : formatted;
}

export function formatDelta(delta: number, type: number): string {
  const formatted = SIGNED_FORMATTER.format(delta);
  return type === FIGURE_FIELD_TYPE.Percentage ? `${formatted} pts` : formatted;
}

export function formatPercent(percent: number): string {
  return `${PERCENT_FORMATTER.format(percent)}%`;
}
