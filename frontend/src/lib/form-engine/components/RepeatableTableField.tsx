import { Plus, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useFieldArray, type UseFormReturn } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";

export function RepeatableTableField({
  field,
  form,
}: {
  field: TemplateField;
  form: UseFormReturn<Record<string, unknown>>;
}) {
  const t = useTranslations("FormEngine");
  const { fields, append, remove } = useFieldArray({ control: form.control, name: field.key as never });
  const columns = field.columns ?? [];

  const emptyRow = () => Object.fromEntries(columns.map((column) => [column.key, ""]));

  return (
    <div className="flex flex-col gap-2 rounded-md border border-line bg-surface p-3">
      {fields.length === 0 ? (
        <p className="text-sm text-ink-muted">{t("noRowsYet")}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="text-xs uppercase tracking-wide text-ink-muted">
                {columns.map((column) => (
                  <th key={column.key} scope="col" className="px-2 py-1 font-medium">
                    {column.label}
                  </th>
                ))}
                <th scope="col" className="w-10" />
              </tr>
            </thead>
            <tbody>
              {fields.map((row, index) => (
                <tr key={row.id}>
                  {columns.map((column) => (
                    <td key={column.key} className="px-2 py-1">
                      <Input {...form.register(`${field.key}.${index}.${column.key}` as never)} />
                    </td>
                  ))}
                  <td className="px-2 py-1">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label={t("removeRow", { number: index + 1 })}
                      onClick={() => remove(index)}
                    >
                      <Trash2 className="size-4" aria-hidden="true" />
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => append(emptyRow())}>
        <Plus aria-hidden="true" />
        {t("addRow")}
      </Button>
    </div>
  );
}
