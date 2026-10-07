import { describe, expect, it } from "vitest";
import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";
import { buildSectionSchema } from "./field-schema";
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

describe("buildSectionSchema", () => {
  it("requires a required text field to be non-empty", () => {
    const schema = buildSectionSchema([field({ required: true })], 3, {});

    expect(schema.safeParse({ field_a: "" }).success).toBe(false);
    expect(schema.safeParse({ field_a: "hello" }).success).toBe(true);
  });

  it("allows an optional field to be blank", () => {
    const schema = buildSectionSchema([field({ required: false })], 3, {});

    expect(schema.safeParse({ field_a: "" }).success).toBe(true);
  });

  it("rejects a non-numeric value for a Number field", () => {
    const schema = buildSectionSchema([field({ type: TEMPLATE_FIELD_TYPE.Number })], 3, {});

    expect(schema.safeParse({ field_a: "not a number" }).success).toBe(false);
    expect(schema.safeParse({ field_a: "42" }).success).toBe(true);
  });

  it("enforces min/max bounds on a numeric field", () => {
    const schema = buildSectionSchema([field({ type: TEMPLATE_FIELD_TYPE.Number, min: 0, max: 100 })], 3, {});

    expect(schema.safeParse({ field_a: "-1" }).success).toBe(false);
    expect(schema.safeParse({ field_a: "101" }).success).toBe(false);
    expect(schema.safeParse({ field_a: "50" }).success).toBe(true);
  });

  it("enforces a regex on a text field", () => {
    const schema = buildSectionSchema([field({ regex: "^[A-Z]+$" })], 3, {});

    expect(schema.safeParse({ field_a: "lowercase" }).success).toBe(false);
    expect(schema.safeParse({ field_a: "UPPERCASE" }).success).toBe(true);
  });

  it("requires at least one option for a required MultipleChoice field", () => {
    const schema = buildSectionSchema([field({ type: TEMPLATE_FIELD_TYPE.MultipleChoice, required: true })], 3, {});

    expect(schema.safeParse({ field_a: [] }).success).toBe(false);
    expect(schema.safeParse({ field_a: ["red"] }).success).toBe(true);
  });

  it("excludes a field hidden by showIf from the schema, even if required", () => {
    const dependent = field({ required: true, showIf: { fieldKey: "trigger", equalsValue: "yes" } });
    const schema = buildSectionSchema([dependent], 3, { trigger: "no" });

    // The hidden field isn't in the shape at all, so an object without it still parses.
    expect(schema.safeParse({}).success).toBe(true);
  });

  it("excludes a Computed field from the schema entirely", () => {
    const computed = field({ type: TEMPLATE_FIELD_TYPE.Computed, required: true, computedExpression: "a + b" });
    const schema = buildSectionSchema([computed], 3, {});

    expect(schema.safeParse({}).success).toBe(true);
  });

  it("validates a DateRange field's nested start/end shape", () => {
    const schema = buildSectionSchema([field({ type: TEMPLATE_FIELD_TYPE.DateRange, required: true })], 3, {});

    expect(schema.safeParse({ field_a: { start: "", end: "2026-01-01" } }).success).toBe(false);
    expect(schema.safeParse({ field_a: { start: "2026-01-01", end: "2026-01-31" } }).success).toBe(true);
  });
});
