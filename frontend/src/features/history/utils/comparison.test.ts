import { describe, expect, it } from "vitest";
import { buildInsights, FIGURE_FIELD_TYPE, figureChange, formatDelta, formatFigure, formatPercent } from "./comparison";

const members = { fieldKey: "members", label: "Total members", type: FIGURE_FIELD_TYPE.Number, periodA: 142, periodB: 148 };
const families = { fieldKey: "families", label: "Families supported", type: FIGURE_FIELD_TYPE.Number, periodA: 3, periodB: 1 };
const funds = { fieldKey: "funds", label: "Funds collected", type: FIGURE_FIELD_TYPE.Currency, periodA: 398000, periodB: 412500 };

const series = [140, 141, 142, 148].map((value, index) => ({ periodLabel: `P${index}`, values: { members: value } }));

describe("figureChange", () => {
  it("returns the difference and percent change", () => {
    const change = figureChange(funds);
    expect(change?.delta).toBe(14500);
    expect(change?.percent).toBeCloseTo(3.64, 2);
  });

  it("is null when either side is unanswered", () => {
    expect(figureChange({ ...members, periodA: null })).toBeNull();
  });

  it("has no percent for a zero base or for percentage fields", () => {
    expect(figureChange({ ...members, periodA: 0 })?.percent).toBeNull();
    expect(figureChange({ type: FIGURE_FIELD_TYPE.Percentage, periodA: 40, periodB: 50 })).toEqual({ delta: 10, percent: null });
  });
});

describe("buildInsights", () => {
  it("notes the charted rise as the largest, every fall, and changed text sections", () => {
    const insights = buildInsights([members, funds, families], series, ["Highlights", "Plans"], "members");

    expect(insights).toEqual([
      { kind: "rise", label: "Total members", delta: 6, isLargestRise: true, periods: 4 },
      { kind: "fall", label: "Families supported", from: 3, to: 1 },
      { kind: "text", sections: 2 },
    ]);
  });

  it("doesn't call a rise the largest when an earlier one was bigger", () => {
    const bumpy = [100, 130, 131, 137].map((value) => ({ periodLabel: "P", values: { members: value } }));
    const [rise] = buildInsights([{ ...members, periodA: 131, periodB: 137 }], bumpy, [], "members");

    expect(rise).toMatchObject({ kind: "rise", isLargestRise: false });
  });

  it("falls back to a single 'none' insight when nothing changed", () => {
    expect(buildInsights([{ ...members, periodB: 142 }], series, [], "members")).toEqual([{ kind: "none" }]);
  });
});

describe("formatting", () => {
  it("formats figures, signed deltas and percents", () => {
    expect(formatFigure(412500, FIGURE_FIELD_TYPE.Currency)).toBe("412,500");
    expect(formatFigure(null, FIGURE_FIELD_TYPE.Number)).toBe("—");
    expect(formatFigure(45, FIGURE_FIELD_TYPE.Percentage)).toBe("45%");
    expect(formatDelta(6, FIGURE_FIELD_TYPE.Number)).toBe("+6");
    expect(formatDelta(-2, FIGURE_FIELD_TYPE.Number)).toBe("-2");
    expect(formatDelta(0, FIGURE_FIELD_TYPE.Number)).toBe("0");
    expect(formatDelta(5, FIGURE_FIELD_TYPE.Percentage)).toBe("+5 pts");
    expect(formatPercent(3.64)).toBe("+3.6%");
  });
});
