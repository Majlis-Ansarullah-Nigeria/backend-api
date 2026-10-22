"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import type { FieldTargetDto, ReportTypeSummary, UnitLevelSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetFieldTargetsQueryKey,
  useGetFieldTargets,
  useGetReportType,
  useSetFieldTargets,
} from "@/lib/api/generated/templates/templates";

/**
 * FR-TPL-011's field-level targets: one optional target per level the report type is filed at.
 * Saved straight to the report type (not the draft), so changing a target never needs a new
 * template version and applies to dashboards right away.
 */
export function FieldTargetsTab({ reportTypeId, fieldKey, levels }: { reportTypeId: string; fieldKey: string; levels: UnitLevelSummary[] }) {
  const t = useTranslations("TemplateBuilder");
  const reportType = useGetReportType<ReportTypeSummary, ApiError>(reportTypeId);
  const targets = useGetFieldTargets<FieldTargetDto[], ApiError>(reportTypeId);

  if (reportType.isPending || targets.isPending) {
    return <p className="text-sm text-ink-muted">{t("targetsLoading")}</p>;
  }

  const filedLevels = levels.filter((level) => (reportType.data?.applicableLevels ?? []).map(Number).includes(Number(level.code)));
  const initial: Record<number, string> = Object.fromEntries(
    (targets.data ?? [])
      .filter((target) => target.fieldKey === fieldKey)
      .map((target) => [Number(target.level), String(Number(target.value))]),
  );

  // Remount the form whenever the field or its saved targets change, so it starts from them.
  return (
    <TargetsForm
      key={`${fieldKey}:${JSON.stringify(initial)}`}
      reportTypeId={reportTypeId}
      fieldKey={fieldKey}
      levels={filedLevels}
      initial={initial}
    />
  );
}

function TargetsForm({
  reportTypeId,
  fieldKey,
  levels,
  initial,
}: {
  reportTypeId: string;
  fieldKey: string;
  levels: UnitLevelSummary[];
  initial: Record<number, string>;
}) {
  const t = useTranslations("TemplateBuilder");
  const queryClient = useQueryClient();
  const save = useSetFieldTargets();
  const [values, setValues] = useState<Record<number, string>>(initial);
  const [saved, setSaved] = useState(false);

  const raw = (level: UnitLevelSummary) => values[Number(level.code)]?.trim() ?? "";
  const invalid = levels.some((level) => raw(level) !== "" && (Number.isNaN(Number(raw(level))) || Number(raw(level)) < 0));

  const submit = () => {
    setSaved(false);
    save.mutate(
      {
        id: reportTypeId,
        fieldKey,
        data: { targets: levels.map((level) => ({ level: level.code, value: raw(level) === "" ? null : Number(raw(level)) })) },
      },
      {
        onSuccess: () => {
          setSaved(true);
          queryClient.invalidateQueries({ queryKey: getGetFieldTargetsQueryKey(reportTypeId) });
        },
      },
    );
  };

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-ink-muted">{t("targetsHelp")}</p>
      {levels.map((level) => (
        <div key={level.id} className="flex flex-col gap-1.5">
          <Label htmlFor={`target-${level.id}`}>{t("targetForLevel", { level: level.name })}</Label>
          <Input
            id={`target-${level.id}`}
            inputMode="decimal"
            value={values[Number(level.code)] ?? ""}
            onChange={(event) => {
              setSaved(false);
              setValues((current) => ({ ...current, [Number(level.code)]: event.target.value }));
            }}
            placeholder={t("targetPlaceholder")}
          />
        </div>
      ))}
      {invalid ? <p className="text-sm text-danger">{t("targetInvalid")}</p> : null}
      {save.error instanceof ApiError ? <p className="text-sm text-danger">{save.error.message}</p> : null}
      {saved ? (
        <p role="status" className="text-sm text-green-700">
          {t("targetsSaved")}
        </p>
      ) : null}
      <Button type="button" size="sm" className="self-start" onClick={submit} disabled={invalid || save.isPending}>
        {t("saveTargets")}
      </Button>
    </div>
  );
}
