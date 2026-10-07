import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";

/** No visibleForLevels means visible at every level — FR-TPL-003's "per-level visibility" is an
 * opt-in restriction, not a requirement to list every level. */
export function isFieldVisibleForLevel(field: TemplateField, unitLevel: number): boolean {
  if (!field.visibleForLevels || field.visibleForLevels.length === 0) {
    return true;
  }
  return field.visibleForLevels.includes(unitLevel);
}

/** FR-TPL-004's own example: "If 'Held general meeting?' = Yes, ask attendance." A field with
 * no showIf always shows. Comparison is string-based (TemplateShowIf.equalsValue is author-
 * entered text) — the answer is coerced to the same canonical string form a yes/no field, a
 * number field, or free text would naturally produce, then compared case-insensitively. */
export function isFieldShown(field: TemplateField, answers: Record<string, unknown>): boolean {
  if (!field.showIf) {
    return true;
  }
  const answer = answers[field.showIf.fieldKey];
  return toComparableString(answer).toLowerCase() === field.showIf.equalsValue.trim().toLowerCase();
}

export function visibleFields(fields: TemplateField[], unitLevel: number, answers: Record<string, unknown>): TemplateField[] {
  return fields.filter((field) => isFieldVisibleForLevel(field, unitLevel) && isFieldShown(field, answers));
}

function toComparableString(value: unknown): string {
  if (value === null || value === undefined) {
    return "";
  }
  if (typeof value === "boolean") {
    return value ? "true" : "false";
  }
  return String(value).trim();
}
