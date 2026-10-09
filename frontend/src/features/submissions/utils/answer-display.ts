import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";

/** Mirrors the backend's AnswerDisplay (FR-SUB-006's review summary) — not pixel-perfect for
 * every field type, just legible. FileUpload fields read from the field's own value (an array of
 * attachment summaries, per M5.4), not from a separate lookup — the client already has it. */
export function formatAnswerForDisplay(field: TemplateField, value: unknown): string {
  if (field.type === TEMPLATE_FIELD_TYPE.FileUpload) {
    const files = Array.isArray(value) ? (value as { fileName: string }[]) : [];
    return files.length === 0 ? "No files attached" : files.map((f) => f.fileName).join(", ");
  }

  if (!hasAnswer(value)) {
    return "—";
  }

  switch (field.type) {
    case TEMPLATE_FIELD_TYPE.YesNo:
      return String(value) === "true" ? "Yes" : "No";
    case TEMPLATE_FIELD_TYPE.SingleChoice:
    case TEMPLATE_FIELD_TYPE.Dropdown:
      return optionLabel(field, String(value));
    case TEMPLATE_FIELD_TYPE.MultipleChoice:
      return (Array.isArray(value) ? value : []).map((v) => optionLabel(field, String(v))).join(", ");
    case TEMPLATE_FIELD_TYPE.MemberList:
      return (Array.isArray(value) ? value : []).join(", ");
    case TEMPLATE_FIELD_TYPE.DateRange: {
      const v = value as { start?: string; end?: string };
      return `${v.start ?? ""} – ${v.end ?? ""}`;
    }
    case TEMPLATE_FIELD_TYPE.Location: {
      const v = value as { latitude?: string; longitude?: string };
      return `${v.latitude ?? ""}, ${v.longitude ?? ""}`;
    }
    case TEMPLATE_FIELD_TYPE.RepeatableTable: {
      const count = Array.isArray(value) ? value.length : 0;
      return count === 1 ? "1 row" : `${count} rows`;
    }
    case TEMPLATE_FIELD_TYPE.Currency:
    case TEMPLATE_FIELD_TYPE.Number:
      return formatNumber(value, field.unitLabel);
    case TEMPLATE_FIELD_TYPE.Percentage:
      return formatNumber(value) + "%";
    default:
      return String(value);
  }
}

function hasAnswer(value: unknown): boolean {
  if (value === null || value === undefined || value === "") {
    return false;
  }
  if (Array.isArray(value)) {
    return value.length > 0;
  }
  if (typeof value === "object") {
    return Object.values(value as Record<string, unknown>).some(hasAnswer);
  }
  return true;
}

function optionLabel(field: TemplateField, value: string): string {
  return field.options?.find((o) => o.value === value)?.label ?? value;
}

function formatNumber(value: unknown, unitLabel?: string | null): string {
  const number = Number(value);
  if (Number.isNaN(number)) {
    return String(value);
  }
  const formatted = number.toLocaleString(undefined, { maximumFractionDigits: 2 });
  return unitLabel ? `${formatted} ${unitLabel}` : formatted;
}
