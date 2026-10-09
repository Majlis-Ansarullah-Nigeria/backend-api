import { z, type ZodTypeAny } from "zod";
import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";
import { NUMERIC_FIELD_TYPES, TEMPLATE_FIELD_TYPE, TEXT_FIELD_TYPES } from "./field-types";
import { visibleFields } from "./field-visibility";

// Every answer is kept as a string (or a plain string-keyed structure) in form state — not
// z.coerce.number(), which breaks react-hook-form's generic type inference for registered
// inputs (the same reasoning as features/templates/schemas/report-type-form.ts's
// observerDepth field). Numeric parsing happens where the value is consumed, not here.

function numericBounds(schema: ZodTypeAny, field: TemplateField): ZodTypeAny {
  let next = schema;
  if (field.min !== null && field.min !== undefined) {
    const min = Number(field.min);
    next = (next as z.ZodString).refine((v) => v === "" || Number(v) >= min, { message: `Must be at least ${min}` });
  }
  if (field.max !== null && field.max !== undefined) {
    const max = Number(field.max);
    next = (next as z.ZodString).refine((v) => v === "" || Number(v) <= max, { message: `Must be at most ${max}` });
  }
  return next;
}

function buildFieldSchema(field: TemplateField): ZodTypeAny {
  if (NUMERIC_FIELD_TYPES.includes(field.type)) {
    let schema: ZodTypeAny = z.string().refine((v) => v === "" || !Number.isNaN(Number(v)), { message: "Must be a number" });
    schema = numericBounds(schema, field);
    return field.required ? (schema as z.ZodString).min(1, "Required") : schema;
  }

  if (TEXT_FIELD_TYPES.includes(field.type)) {
    let schema = z.string();
    if (field.min !== null && field.min !== undefined) {
      schema = schema.min(Number(field.min), `Must be at least ${field.min} characters`);
    }
    if (field.max !== null && field.max !== undefined) {
      schema = schema.max(Number(field.max), `Must be at most ${field.max} characters`);
    }
    if (field.regex) {
      schema = schema.regex(new RegExp(field.regex), "Doesn't match the expected format");
    }
    return field.required ? schema.min(1, "Required") : schema.optional().or(z.literal(""));
  }

  switch (field.type) {
    case TEMPLATE_FIELD_TYPE.Date:
    case TEMPLATE_FIELD_TYPE.YesNo:
    case TEMPLATE_FIELD_TYPE.SingleChoice:
    case TEMPLATE_FIELD_TYPE.Dropdown:
      return field.required ? z.string().min(1, "Required") : z.string().optional().or(z.literal(""));

    case TEMPLATE_FIELD_TYPE.DateRange: {
      const part = field.required ? z.string().min(1, "Required") : z.string().optional().or(z.literal(""));
      return z.object({ start: part, end: part });
    }

    case TEMPLATE_FIELD_TYPE.Location: {
      const part = field.required ? z.string().min(1, "Required") : z.string().optional().or(z.literal(""));
      return z.object({ latitude: part, longitude: part });
    }

    case TEMPLATE_FIELD_TYPE.MultipleChoice:
      return field.required ? z.array(z.string()).min(1, "Choose at least one") : z.array(z.string());

    case TEMPLATE_FIELD_TYPE.MemberList:
      return field.required ? z.array(z.string()).min(1, "Add at least one member") : z.array(z.string());

    case TEMPLATE_FIELD_TYPE.RepeatableTable:
      return field.required
        ? z.array(z.record(z.string(), z.string())).min(1, "Add at least one row")
        : z.array(z.record(z.string(), z.string()));

    case TEMPLATE_FIELD_TYPE.FileUpload:
      return field.required ? z.array(z.unknown()).min(1, "Attach at least one file") : z.array(z.unknown());

    default:
      return z.unknown().optional();
  }
}

/** Builds validation only for the fields actually visible right now — a hidden field (per-level
 * or showIf) can't be required, since the person filling the form never sees it. Computed
 * fields are excluded entirely: they're derived, not entered. */
export function buildSectionSchema(fields: TemplateField[], unitLevel: number, answers: Record<string, unknown>) {
  const shape: Record<string, ZodTypeAny> = {};
  for (const field of visibleFields(fields, unitLevel, answers)) {
    if (field.type === TEMPLATE_FIELD_TYPE.Computed) {
      continue;
    }
    shape[field.key] = buildFieldSchema(field);
  }
  return z.object(shape);
}

/** The whole report form's schema (every section merged) — FR-SUB-002's inline validation. One
 * combined resolver rather than swapping a per-section one lets react-hook-form keep showing a
 * field's error even after the user has paged away from its section. */
export function buildFormSchema(sections: readonly { fields: TemplateField[] }[], unitLevel: number, answers: Record<string, unknown>) {
  const shape: Record<string, ZodTypeAny> = {};
  for (const section of sections) {
    Object.assign(shape, buildSectionSchema(section.fields, unitLevel, answers).shape);
  }
  return z.object(shape);
}
