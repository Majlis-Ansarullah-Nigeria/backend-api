import { describe, expect, it } from "vitest";
import {
  applicableLevelsLabel,
  categoryLabel,
  frequencyLabel,
  levelLabel,
  REPORT_TYPE_CATEGORY,
  REPORT_TYPE_FREQUENCY,
  UNIT_LEVEL_CODE,
} from "./report-type";

describe("categoryLabel", () => {
  it("labels known categories", () => {
    expect(categoryLabel(REPORT_TYPE_CATEGORY.GeneralMonthly)).toBe("General Monthly");
    expect(categoryLabel(REPORT_TYPE_CATEGORY.Survey)).toBe("Survey");
  });

  it("falls back for an unknown category", () => {
    expect(categoryLabel(99)).toBe("Unknown category");
  });
});

describe("frequencyLabel", () => {
  it("labels known frequencies", () => {
    expect(frequencyLabel(REPORT_TYPE_FREQUENCY.AdHoc)).toBe("Ad-hoc");
  });
});

describe("levelLabel", () => {
  it("labels known levels", () => {
    expect(levelLabel(UNIT_LEVEL_CODE.Muqam)).toBe("Muqam");
  });
});

describe("applicableLevelsLabel", () => {
  it("orders levels by hierarchy depth regardless of input order", () => {
    expect(applicableLevelsLabel([UNIT_LEVEL_CODE.Muqam, UNIT_LEVEL_CODE.Zone, UNIT_LEVEL_CODE.Dila])).toBe(
      "Zone, Dila, Muqam",
    );
  });

  it("includes National when present", () => {
    expect(applicableLevelsLabel([UNIT_LEVEL_CODE.National])).toBe("National");
  });
});
