import { useTranslations } from "next-intl";
import { Controller, type FieldErrors, type UseFormReturn } from "react-hook-form";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";
import { TEMPLATE_FIELD_TYPE } from "../field-types";
import { FieldShell } from "./FieldShell";
import { FileUploadField } from "./FileUploadField";
import { MemberListField } from "./MemberListField";
import { RepeatableTableField } from "./RepeatableTableField";

function errorMessage(errors: FieldErrors, key: string): string | undefined {
  const error = errors[key];
  return typeof error?.message === "string" ? error.message : undefined;
}

export function FieldRenderer({
  field,
  form,
  uploadContext,
}: {
  field: TemplateField;
  form: UseFormReturn<Record<string, unknown>>;
  uploadContext?: { reportId: string; unitId: string };
}) {
  const t = useTranslations("FormEngine");
  const error = errorMessage(form.formState.errors, field.key);
  const shellProps = { fieldKey: field.key, label: field.label, helpText: field.helpText, required: field.required, error };

  switch (field.type) {
    case TEMPLATE_FIELD_TYPE.ShortText:
      return (
        <FieldShell {...shellProps}>
          <Input id={field.key} placeholder={field.placeholder ?? undefined} aria-invalid={!!error} {...form.register(field.key)} />
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.LongText:
      return (
        <FieldShell {...shellProps}>
          <Textarea id={field.key} placeholder={field.placeholder ?? undefined} aria-invalid={!!error} {...form.register(field.key)} />
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.Number:
    case TEMPLATE_FIELD_TYPE.Currency:
    case TEMPLATE_FIELD_TYPE.Percentage:
      return (
        <FieldShell {...shellProps}>
          <div className="flex items-center gap-2">
            <Input id={field.key} inputMode="decimal" aria-invalid={!!error} {...form.register(field.key)} />
            {field.unitLabel ? (
              <span className="text-sm text-ink-muted">{field.unitLabel}</span>
            ) : field.type === TEMPLATE_FIELD_TYPE.Percentage ? (
              <span className="text-sm text-ink-muted">%</span>
            ) : null}
          </div>
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.Date:
      return (
        <FieldShell {...shellProps}>
          <Input id={field.key} type="date" aria-invalid={!!error} {...form.register(field.key)} />
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.DateRange:
      return (
        <FieldShell {...shellProps}>
          <div className="grid grid-cols-2 gap-2">
            <Input type="date" aria-label={`${field.label} start`} {...form.register(`${field.key}.start`)} />
            <Input type="date" aria-label={`${field.label} end`} {...form.register(`${field.key}.end`)} />
          </div>
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.YesNo:
      return (
        <FieldShell {...shellProps}>
          <div className="flex gap-3" role="radiogroup" aria-label={field.label}>
            <label className="flex items-center gap-2 text-sm text-ink">
              <input type="radio" value="true" {...form.register(field.key)} />
              {t("yes")}
            </label>
            <label className="flex items-center gap-2 text-sm text-ink">
              <input type="radio" value="false" {...form.register(field.key)} />
              {t("no")}
            </label>
          </div>
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.SingleChoice:
      return (
        <FieldShell {...shellProps}>
          <div className="flex flex-col gap-2" role="radiogroup" aria-label={field.label}>
            {(field.options ?? []).map((option) => (
              <label key={option.value} className="flex items-center gap-2 text-sm text-ink">
                <input type="radio" value={option.value} {...form.register(field.key)} />
                {option.label}
              </label>
            ))}
          </div>
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.Dropdown:
      return (
        <FieldShell {...shellProps}>
          <select
            id={field.key}
            aria-invalid={!!error}
            className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
            {...form.register(field.key)}
          >
            <option value="">{t("selectPlaceholder")}</option>
            {(field.options ?? []).map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.MultipleChoice:
      return (
        <FieldShell {...shellProps}>
          <Controller
            control={form.control}
            name={field.key as never}
            defaultValue={[] as never}
            render={({ field: controller }) => {
              const selected: string[] = Array.isArray(controller.value) ? controller.value : [];
              return (
                <div className="flex flex-col gap-2">
                  {(field.options ?? []).map((option) => (
                    <label key={option.value} className="flex items-center gap-2 text-sm text-ink">
                      <Checkbox
                        checked={selected.includes(option.value)}
                        onCheckedChange={(checked) =>
                          controller.onChange(
                            checked === true ? [...selected, option.value] : selected.filter((v) => v !== option.value),
                          )
                        }
                      />
                      {option.label}
                    </label>
                  ))}
                </div>
              );
            }}
          />
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.Rating: {
      const scale = Array.from({ length: (field.max ? Number(field.max) : 5) - (field.min ? Number(field.min) : 1) + 1 }, (_, i) =>
        (field.min ? Number(field.min) : 1) + i,
      );
      return (
        <FieldShell {...shellProps}>
          <Controller
            control={form.control}
            name={field.key as never}
            defaultValue={"" as never}
            render={({ field: controller }) => (
              <div className="flex gap-1" role="radiogroup" aria-label={field.label}>
                {scale.map((value) => (
                  <button
                    key={value}
                    type="button"
                    aria-pressed={String(controller.value) === String(value)}
                    onClick={() => controller.onChange(String(value))}
                    className="flex size-9 items-center justify-center rounded-sm border border-line-strong text-sm font-medium aria-pressed:border-green-600 aria-pressed:bg-mint aria-pressed:text-green-700"
                  >
                    {value}
                  </button>
                ))}
              </div>
            )}
          />
        </FieldShell>
      );
    }

    case TEMPLATE_FIELD_TYPE.FileUpload:
      return (
        <FieldShell {...shellProps}>
          <FileUploadField field={field} form={form} uploadContext={uploadContext} />
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.Location:
      return (
        <FieldShell {...shellProps}>
          <div className="grid grid-cols-2 gap-2">
            <Input inputMode="decimal" placeholder="Latitude" aria-label={`${field.label} latitude`} {...form.register(`${field.key}.latitude`)} />
            <Input inputMode="decimal" placeholder="Longitude" aria-label={`${field.label} longitude`} {...form.register(`${field.key}.longitude`)} />
          </div>
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.RepeatableTable:
      return (
        <FieldShell {...shellProps}>
          <RepeatableTableField field={field} form={form} />
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.MemberList:
      return (
        <FieldShell {...shellProps}>
          <MemberListField field={field} form={form} />
        </FieldShell>
      );

    case TEMPLATE_FIELD_TYPE.Computed:
      // Rendered separately by SectionForm (via useComputedFields) — it's derived, not entered.
      return null;

    default:
      return null;
  }
}
