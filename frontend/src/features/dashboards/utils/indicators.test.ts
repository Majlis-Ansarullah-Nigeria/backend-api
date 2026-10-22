import { describe, expect, it } from "vitest";
import { FIGURE_FIELD_TYPE } from "@/features/history/utils/comparison";
import { change, changeKind, formatIndicator, formatSigned, latestTwo, percentOfTarget } from "./indicators";

describe("formatIndicator", () => {
  it("formats counts, percentages and currency", () => {
    expect(formatIndicator(3702, FIGURE_FIELD_TYPE.Number)).toBe("3,702");
    expect(formatIndicator(61, FIGURE_FIELD_TYPE.Percentage)).toBe("61%");
    expect(formatIndicator(3_850_000, FIGURE_FIELD_TYPE.Currency, "NGN")).toBe("NGN 3.85M");
    expect(formatIndicator(412_500, FIGURE_FIELD_TYPE.Currency, "NGN")).toBe("NGN 412,500");
    expect(formatIndicator(null, FIGURE_FIELD_TYPE.Number)).toBe("—");
  });
});

describe("latestTwo", () => {
  it("skips periods without a figure", () => {
    expect(latestTwo([5, null, 7, null])).toEqual({ latest: 7, latestIndex: 2, previous: 5, previousIndex: 0 });
    expect(latestTwo([null, null])).toEqual({ latest: null, latestIndex: -1, previous: null, previousIndex: -1 });
  });
});

describe("change", () => {
  it("reads percentages in points, currency in percent and counts in units", () => {
    expect(changeKind(FIGURE_FIELD_TYPE.Percentage)).toBe("points");
    expect(change(63, 61, FIGURE_FIELD_TYPE.Percentage)).toBe(-2);
    expect(change(100, 106, FIGURE_FIELD_TYPE.Currency)).toBeCloseTo(6);
    expect(change(3120, 3702, FIGURE_FIELD_TYPE.Number)).toBe(582);
    expect(change(0, 5, FIGURE_FIELD_TYPE.Currency)).toBeNull();
    expect(change(null, 5, FIGURE_FIELD_TYPE.Number)).toBeNull();
    expect(formatSigned(582)).toBe("+582");
  });
});

describe("percentOfTarget", () => {
  it("is the rounded share of a positive target", () => {
    expect(percentOfTarget(1064, 1156)).toBe(92);
    expect(percentOfTarget(5, 0)).toBeNull();
    expect(percentOfTarget(null, 10)).toBeNull();
  });
});
