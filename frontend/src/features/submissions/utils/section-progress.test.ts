import { describe, expect, it } from "vitest";
import type { TemplateField, TemplateSection } from "@/lib/api/generated/hRSApiV1.schemas";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";
import { isSectionComplete, sectionProgress } from "./section-progress";

function field(overrides: Partial<TemplateField> = {}): TemplateField {
  return {
    key: "field1",
    type: TEMPLATE_FIELD_TYPE.ShortText,
    label: "Field 1",
    helpText: null,
    required: true,
    min: null,
    max: null,
    regex: null,
    defaultValue: null,
    unitLabel: null,
    confidential: false,
    owningDepartmentId: null,
    visibleForLevels: null,
    showIf: null,
    aggregation: 0,
    computedExpression: null,
    options: null,
    columns: null,
    ...overrides,
  };
}

function section(fields: TemplateField[], key = "section1"): TemplateSection {
  return { key, title: "Section", fields };
}

describe("isSectionComplete", () => {
  it("is true when every required field has an answer", () => {
    const s = section([field({ key: "a", required: true }), field({ key: "b", required: false })]);

    expect(isSectionComplete(s, 3, { a: "hello" })).toBe(true);
  });

  it("is false when a required field has no answer", () => {
    const s = section([field({ key: "a", required: true })]);

    expect(isSectionComplete(s, 3, {})).toBe(false);
  });

  it("ignores a required field hidden at this unit level", () => {
    const s = section([field({ key: "a", required: true, visibleForLevels: [1] })]);

    expect(isSectionComplete(s, 3, {})).toBe(true);
  });

  it("ignores a required field hidden by showIf", () => {
    const s = section([
      field({ key: "trigger", required: false }),
      field({ key: "a", required: true, showIf: { fieldKey: "trigger", equalsValue: "yes" } }),
    ]);

    expect(isSectionComplete(s, 3, { trigger: "no" })).toBe(true);
  });

  it("never counts a Computed field as something to fill in", () => {
    const s = section([field({ key: "a", required: true, type: TEMPLATE_FIELD_TYPE.Computed })]);

    expect(isSectionComplete(s, 3, {})).toBe(true);
  });

  it("treats an empty array as unanswered", () => {
    const s = section([field({ key: "a", required: true, type: TEMPLATE_FIELD_TYPE.MultipleChoice })]);

    expect(isSectionComplete(s, 3, { a: [] })).toBe(false);
    expect(isSectionComplete(s, 3, { a: ["x"] })).toBe(true);
  });
});

describe("sectionProgress", () => {
  it("counts completed sections out of the total", () => {
    const sections = [
      section([field({ key: "a", required: true })], "s1"),
      section([field({ key: "b", required: true })], "s2"),
      section([field({ key: "c", required: false })], "s3"),
    ];

    const progress = sectionProgress(sections, 3, { a: "done" });

    expect(progress).toEqual({ completed: 2, total: 3 });
  });
});
