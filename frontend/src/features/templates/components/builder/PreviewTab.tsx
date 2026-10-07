"use client";

import { Monitor, Smartphone } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import type { TemplateSchema, UnitLevelSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { SectionForm } from "@/lib/form-engine/components/SectionForm";
import { cn } from "@/lib/utils";

type Frame = "desktop" | "mobile";

export function PreviewTab({ schema, levels }: { schema: TemplateSchema; levels: UnitLevelSummary[] }) {
  const t = useTranslations("TemplateBuilder");
  const [levelCode, setLevelCode] = useState(() => Number(levels[0]?.code ?? 0));
  const [frame, setFrame] = useState<Frame>("desktop");
  const form = useForm<Record<string, unknown>>();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="preview-level" className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
            {t("previewAsLevelLabel")}
          </label>
          <select
            id="preview-level"
            value={levelCode}
            onChange={(event) => setLevelCode(Number(event.target.value))}
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            {levels.map((level) => (
              <option key={level.id} value={level.code}>
                {level.name}
              </option>
            ))}
          </select>
        </div>

        <div className="flex gap-1 rounded-md bg-ground p-1">
          <FrameButton active={frame === "desktop"} onClick={() => setFrame("desktop")} label={t("desktopFrame")} icon={Monitor} />
          <FrameButton active={frame === "mobile"} onClick={() => setFrame("mobile")} label={t("mobileFrame")} icon={Smartphone} />
        </div>
      </div>

      <div className={cn("rounded-lg border border-line bg-surface p-6", frame === "mobile" && "mx-auto max-w-sm")}>
        {schema.sections.length === 0 ? (
          <p className="text-sm text-ink-muted">{t("noSectionsYet")}</p>
        ) : (
          <div className="flex flex-col gap-6">
            {schema.sections.map((section) => (
              <div key={section.key} className="flex flex-col gap-4">
                <h3 className="text-base font-bold text-ink">{section.title}</h3>
                <SectionForm section={section} unitLevel={levelCode} form={form} />
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function FrameButton({
  active,
  onClick,
  label,
  icon: Icon,
}: {
  active: boolean;
  onClick: () => void;
  label: string;
  icon: typeof Monitor;
}) {
  return (
    <Button type="button" variant={active ? "primary" : "ghost"} size="sm" aria-pressed={active} onClick={onClick}>
      <Icon className="size-4" aria-hidden="true" />
      {label}
    </Button>
  );
}
