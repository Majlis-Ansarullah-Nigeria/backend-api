import { describe, expect, it } from "vitest";
import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";
import { isFieldShown, isFieldVisibleForLevel, visibleFields } from "./field-visibility";
import { TEMPLATE_FIELD_TYPE } from "./field-types";

function field(overrides: Partial<TemplateField> = {}): TemplateField {
  return {
    key: "field_a",
    type: TEMPLATE_FIELD_TYPE.ShortText,
    label: "Field A",
    helpText: null,
    required: false,
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

describe("isFieldVisibleForLevel", () => {
  it("is visible everywhere when visibleForLevels is unset", () => {
    expect(isFieldVisibleForLevel(field({ visibleForLevels: null }), 3)).toBe(true);
  });

  it("is visible only at the listed levels", () => {
    const muqamOnly = field({ visibleForLevels: [3] });
    expect(isFieldVisibleForLevel(muqamOnly, 3)).toBe(true);
    expect(isFieldVisibleForLevel(muqamOnly, 1)).toBe(false);
  });
});

describe("isFieldShown", () => {
  it("shows a field with no showIf", () => {
    expect(isFieldShown(field({ showIf: null }), {})).toBe(true);
  });

  it("shows a field when the trigger field's answer matches", () => {
    const dependent = field({ showIf: { fieldKey: "held_meeting", equalsValue: "true" } });
    expect(isFieldShown(dependent, { held_meeting: true })).toBe(true);
  });

  it("hides a field when the trigger field's answer doesn't match", () => {
    const dependent = field({ showIf: { fieldKey: "held_meeting", equalsValue: "true" } });
    expect(isFieldShown(dependent, { held_meeting: false })).toBe(false);
  });

  it("hides a field when the trigger field has no answer yet", () => {
    const dependent = field({ showIf: { fieldKey: "held_meeting", equalsValue: "true" } });
    expect(isFieldShown(dependent, {})).toBe(false);
  });

  it("compares case-insensitively against a text answer", () => {
    const dependent = field({ showIf: { fieldKey: "status", equalsValue: "Active" } });
    expect(isFieldShown(dependent, { status: "active" })).toBe(true);
  });
});

describe("visibleFields", () => {
  it("combines level and showIf filtering", () => {
    const fields = [
      field({ key: "a", visibleForLevels: [3] }),
      field({ key: "b", showIf: { fieldKey: "a", equalsValue: "yes" } }),
      field({ key: "c" }),
    ];

    const result = visibleFields(fields, 3, { a: "yes" });

    expect(result.map((f) => f.key)).toEqual(["a", "b", "c"]);
  });

  it("excludes a field whose level doesn't match even if its showIf would pass", () => {
    const fields = [field({ key: "a", visibleForLevels: [1], showIf: null })];

    expect(visibleFields(fields, 3, {})).toHaveLength(0);
  });
});
