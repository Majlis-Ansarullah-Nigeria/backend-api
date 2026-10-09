import { describe, expect, it } from "vitest";
import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";
import { formatAnswerForDisplay } from "./answer-display";

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

describe("formatAnswerForDisplay", () => {
  it("shows an em dash for an unanswered field", () => {
    expect(formatAnswerForDisplay(field(), "")).toBe("—");
    expect(formatAnswerForDisplay(field(), null)).toBe("—");
  });

  it("shows plain text as-is", () => {
    expect(formatAnswerForDisplay(field(), "hello")).toBe("hello");
  });

  it("converts a YesNo answer to a word", () => {
    const f = field({ type: TEMPLATE_FIELD_TYPE.YesNo });
    expect(formatAnswerForDisplay(f, "true")).toBe("Yes");
    expect(formatAnswerForDisplay(f, "false")).toBe("No");
  });

  it("resolves a SingleChoice value to its option label", () => {
    const f = field({
      type: TEMPLATE_FIELD_TYPE.SingleChoice,
      options: [
        { value: "red", label: "Red" },
        { value: "blue", label: "Blue" },
      ],
    });
    expect(formatAnswerForDisplay(f, "red")).toBe("Red");
  });

  it("joins MultipleChoice option labels", () => {
    const f = field({
      type: TEMPLATE_FIELD_TYPE.MultipleChoice,
      options: [
        { value: "red", label: "Red" },
        { value: "blue", label: "Blue" },
      ],
    });
    expect(formatAnswerForDisplay(f, ["red", "blue"])).toBe("Red, Blue");
  });

  it("formats a Currency value with its unit label", () => {
    const f = field({ type: TEMPLATE_FIELD_TYPE.Currency, unitLabel: "NGN" });
    expect(formatAnswerForDisplay(f, "412500")).toBe("412,500 NGN");
  });

  it("formats a Percentage value with a percent sign", () => {
    const f = field({ type: TEMPLATE_FIELD_TYPE.Percentage });
    expect(formatAnswerForDisplay(f, "47")).toBe("47%");
  });

  it("formats a DateRange as start – end", () => {
    const f = field({ type: TEMPLATE_FIELD_TYPE.DateRange });
    expect(formatAnswerForDisplay(f, { start: "2026-09-01", end: "2026-09-30" })).toBe("2026-09-01 – 2026-09-30");
  });

  it("counts RepeatableTable rows", () => {
    const f = field({ type: TEMPLATE_FIELD_TYPE.RepeatableTable });
    expect(formatAnswerForDisplay(f, [{ a: "1" }, { a: "2" }])).toBe("2 rows");
    expect(formatAnswerForDisplay(f, [{ a: "1" }])).toBe("1 row");
  });

  it("lists attachment file names for a FileUpload field, even when nothing else is answered", () => {
    const f = field({ type: TEMPLATE_FIELD_TYPE.FileUpload });
    expect(formatAnswerForDisplay(f, [{ fileName: "site.jpg" }, { fileName: "receipt.pdf" }])).toBe("site.jpg, receipt.pdf");
    expect(formatAnswerForDisplay(f, [])).toBe("No files attached");
  });
});
