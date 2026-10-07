import { useTranslations } from "next-intl";
import { FIELD_TYPE_META, FIELD_TYPE_ORDER } from "@/features/templates/utils/field-type-meta";

export function FieldPalette({ onAdd }: { onAdd: (type: number) => void }) {
  const t = useTranslations("TemplateBuilder");
  return (
    <div className="flex flex-col gap-2 rounded-lg border border-line bg-surface p-3">
      <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{t("addAFieldLabel")}</p>
      <div className="flex flex-col gap-1">
        {FIELD_TYPE_ORDER.map((type) => {
          const meta = FIELD_TYPE_META[type]!;
          const Icon = meta.icon;
          return (
            <button
              key={type}
              type="button"
              onClick={() => onAdd(type)}
              className="flex items-center gap-2 rounded-sm px-2 py-2 text-left text-sm text-ink hover:bg-ground"
            >
              <Icon className="size-4 shrink-0 text-ink-muted" aria-hidden="true" />
              {meta.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
