import { useWatch, type UseFormReturn } from "react-hook-form";
import type { TemplateField, TemplateSection } from "@/lib/api/generated/hRSApiV1.schemas";
import { TEMPLATE_FIELD_TYPE } from "../field-types";
import { visibleFields } from "../field-visibility";
import { useComputedFields } from "../use-computed-fields";
import { FieldRenderer } from "./FieldRenderer";
import { FieldShell } from "./FieldShell";

/** Renders one template section: filters fields by the viewer's unit level and each field's
 * showIf trigger (FR-TPL-003, FR-TPL-004), then renders every visible field — Computed fields
 * read-only via M3.3's evaluator, everything else as an interactive input bound to `form`. */
export function SectionForm({
  section,
  unitLevel,
  form,
  uploadContext,
}: {
  section: TemplateSection;
  unitLevel: number;
  form: UseFormReturn<Record<string, unknown>>;
  uploadContext?: { reportId: string; unitId: string };
}) {
  const answers = useWatch({ control: form.control });
  const shown = visibleFields(section.fields, unitLevel, answers);
  const computed = useComputedFields(section.fields, answers);

  return (
    <div className="flex flex-col gap-4">
      {shown.map((field: TemplateField) =>
        field.type === TEMPLATE_FIELD_TYPE.Computed ? (
          <FieldShell key={field.key} fieldKey={field.key} label={field.label} helpText={field.helpText} required={false}>
            <p id={field.key} className="text-base font-medium text-ink">
              {computed[field.key] ?? "—"}
              {field.unitLabel ? <span className="ml-1 text-sm text-ink-muted">{field.unitLabel}</span> : null}
            </p>
          </FieldShell>
        ) : (
          <FieldRenderer key={field.key} field={field} form={form} uploadContext={uploadContext} />
        ),
      )}
    </div>
  );
}
