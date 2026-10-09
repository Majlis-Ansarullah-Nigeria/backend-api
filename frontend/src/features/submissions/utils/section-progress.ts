import type { TemplateSection } from "@/lib/api/generated/hRSApiV1.schemas";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";
import { visibleFields } from "@/lib/form-engine/field-visibility";

/** FR-SUB-004's progress percentage: a section is "complete" once every visible required field
 * has an answer — Computed fields are derived, never counted as something to fill in. */
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

export function isSectionComplete(section: TemplateSection, unitLevel: number, answers: Record<string, unknown>): boolean {
  const requiredVisibleFields = visibleFields(section.fields, unitLevel, answers).filter(
    (field) => field.required && field.type !== TEMPLATE_FIELD_TYPE.Computed,
  );

  return requiredVisibleFields.every((field) => hasAnswer(answers[field.key]));
}

export interface SectionProgress {
  completed: number;
  total: number;
}

export function sectionProgress(sections: TemplateSection[], unitLevel: number, answers: Record<string, unknown>): SectionProgress {
  return {
    completed: sections.filter((section) => isSectionComplete(section, unitLevel, answers)).length,
    total: sections.length,
  };
}
