import { describe, expect, it } from "vitest";
import { complianceKpis, isOnTrack, percentOf, type PeriodCounts } from "./compliance";

const period = (label: string, isClosed: boolean, onTime: number, late: number, missed: number, expected = 100): PeriodCounts => ({
  periodLabel: label,
  isClosed,
  expected,
  onTime,
  late,
  missed,
  drafting: 0,
  notStarted: expected - onTime - late - missed,
});

describe("percentOf", () => {
  it("rounds to a whole percent and is 0 for an empty total", () => {
    expect(percentOf(2, 3)).toBe(67);
    expect(percentOf(0, 0)).toBe(0);
  });
});

describe("complianceKpis", () => {
  it("uses the last closed period, its change, and the closed-period average", () => {
    const kpis = complianceKpis([period("Jul", true, 70, 10, 20), period("Aug", true, 88, 6, 6), period("Sep", false, 40, 0, 0)]);

    expect(kpis.lastFull?.periodLabel).toBe("Aug");
    expect(kpis.onTimePercent).toBe(88);
    expect(kpis.onTimeChange).toBe(18);
    expect(kpis.averageOnTimePercent).toBe(79);
    expect(kpis.latePercent).toBe(6);
    expect(kpis.missedPercent).toBe(6);
  });

  it("has no change with a single closed period and nothing at all with none", () => {
    expect(complianceKpis([period("Aug", true, 50, 0, 50)]).onTimeChange).toBeNull();
    expect(complianceKpis([period("Sep", false, 10, 0, 0)])).toMatchObject({ lastFull: null, onTimePercent: null });
  });
});

describe("isOnTrack", () => {
  it("treats half or more as on track", () => {
    expect(isOnTrack(50)).toBe(true);
    expect(isOnTrack(49)).toBe(false);
  });
});
