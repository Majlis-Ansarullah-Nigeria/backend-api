import { useMemo } from "react";
import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";
import { evaluateExpression } from "@/lib/form-engine/expression-evaluator";
import { TEMPLATE_FIELD_TYPE } from "./field-types";

/** Recalculates every Computed field (M3.3's shared evaluator) whenever an answer changes — the
 * same live-recompute behavior FR-TPL-005 and the "Attendance rate" mockup example show. A
 * malformed expression (shouldn't happen — TemplateSchemaValidator parses it at save time)
 * surfaces as null rather than throwing, so one bad field can't take down the whole form. */
export function useComputedFields(fields: TemplateField[], answers: Record<string, unknown>): Record<string, number | null> {
  return useMemo(() => {
    const results: Record<string, number | null> = {};
    for (const field of fields) {
      if (field.type !== TEMPLATE_FIELD_TYPE.Computed || !field.computedExpression) {
        continue;
      }
      const result = evaluateExpression(field.computedExpression, answers);
      results[field.key] = result.success ? result.value : null;
    }
    return results;
  }, [fields, answers]);
}
