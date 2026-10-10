"use client";

import { useTranslations } from "next-intl";
import { useState } from "react";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Textarea } from "@/components/ui/textarea";
import type { PullContextView } from "@/lib/api/generated/hRSApiV1.schemas";
import { isPullEligibleStatus } from "@/features/submissions/utils/pull-figures";
import { reportStatusChipVariant } from "@/features/submissions/utils/report-status";

/** FR-SUB-007's panel: which children are included, which are missing, and a note on overrides —
 * shown inline above the form (not a modal) to match the Dila-PullFigures mockup. */
export function PullFiguresPanel({
  pullContext,
  onPull,
  isPending,
  error,
}: {
  pullContext: PullContextView;
  onPull: (includedUnitIds: string[], overrideNote: string) => void;
  isPending: boolean;
  error?: string;
}) {
  const t = useTranslations("ReportForm");
  const eligibleIds = pullContext.children.filter((c) => isPullEligibleStatus(Number(c.status))).map((c) => c.unitId);

  const [includedIds, setIncludedIds] = useState<Set<string>>(
    () => new Set(pullContext.summary?.includedUnitIds ?? eligibleIds),
  );
  const [note, setNote] = useState(pullContext.summary?.overrideNote ?? "");

  const toggle = (unitId: string) => {
    setIncludedIds((prev) => {
      const next = new Set(prev);
      if (next.has(unitId)) {
        next.delete(unitId);
      } else {
        next.add(unitId);
      }
      return next;
    });
  };

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-base font-bold text-ink">{t("includedChildrenLabel", { plural: pullContext.childLevelPluralName ?? "" })}</h2>
        <span className="text-sm font-bold text-green-700">
          {includedIds.size} / {pullContext.children.length}
        </span>
      </div>
      <p className="mt-1 text-sm text-ink-muted">{t("onlySubmittedCanBePulledHint")}</p>

      <div className="mt-3 flex flex-col divide-y divide-line">
        {pullContext.children.map((child) => {
          const eligible = isPullEligibleStatus(Number(child.status));
          return (
            <label key={child.unitId} className="flex items-center gap-3 py-2">
              <Checkbox
                aria-label={child.unitName}
                checked={includedIds.has(child.unitId)}
                disabled={!eligible}
                onCheckedChange={() => toggle(child.unitId)}
              />
              <span className="flex-1 text-sm font-medium text-ink">{child.unitName}</span>
              <StatusChip status={reportStatusChipVariant(Number(child.status))} />
            </label>
          );
        })}
      </div>

      <div className="mt-4 flex flex-col gap-1.5">
        <label htmlFor="pull-override-note" className="text-sm font-semibold text-ink">
          {t("noteOnOverridesLabel")}
        </label>
        <Textarea id="pull-override-note" rows={2} value={note} onChange={(event) => setNote(event.target.value)} />
      </div>

      {error ? <p className="mt-2 text-sm text-danger">{error}</p> : null}

      <div className="mt-4 flex justify-end">
        <Button type="button" onClick={() => onPull([...includedIds], note)} disabled={isPending}>
          {pullContext.summary ? t("pullAgainButton") : t("pullButton")}
        </Button>
      </div>

      {pullContext.summary ? (
        <p className="mt-3 text-sm text-ink-muted">
          {t("pulledSummaryLine", { includedCount: pullContext.summary.includedUnitIds.length })}
          {pullContext.summary.missingUnitIds.length > 0
            ? ` ${t("pulledSummaryMissing", { missingCount: pullContext.summary.missingUnitIds.length })}`
            : null}
        </p>
      ) : null}
    </div>
  );
}
