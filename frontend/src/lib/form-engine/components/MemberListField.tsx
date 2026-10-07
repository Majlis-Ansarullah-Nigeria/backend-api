import { Plus, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { Controller, type UseFormReturn } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";

/** No richer sub-field shape is modeled for member lists yet — just a growable list of names. */
export function MemberListField({ field, form }: { field: TemplateField; form: UseFormReturn<Record<string, unknown>> }) {
  const t = useTranslations("FormEngine");

  return (
    <Controller
      control={form.control}
      name={field.key as never}
      defaultValue={[] as never}
      render={({ field: controller }) => {
        const names: string[] = Array.isArray(controller.value) ? controller.value : [];

        const updateName = (index: number, value: string) => {
          const next = [...names];
          next[index] = value;
          controller.onChange(next);
        };

        const removeName = (index: number) => {
          controller.onChange(names.filter((_, i) => i !== index));
        };

        return (
          <div className="flex flex-col gap-2 rounded-md border border-line bg-surface p-3">
            {names.length === 0 ? (
              <p className="text-sm text-ink-muted">{t("noMembersYet")}</p>
            ) : (
              names.map((name, index) => (
                <div key={index} className="flex gap-2">
                  <Input
                    value={name}
                    onChange={(event) => updateName(index, event.target.value)}
                    aria-label={t("memberLabel", { number: index + 1 })}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={t("removeMember", { number: index + 1 })}
                    onClick={() => removeName(index)}
                  >
                    <Trash2 className="size-4" aria-hidden="true" />
                  </Button>
                </div>
              ))
            )}
            <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => controller.onChange([...names, ""])}>
              <Plus aria-hidden="true" />
              {t("addMember")}
            </Button>
          </div>
        );
      }}
    />
  );
}
