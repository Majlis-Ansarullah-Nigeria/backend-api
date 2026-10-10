import { ArrowUpFromLine, Pencil } from "lucide-react";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import type { PullSummaryField } from "@/lib/api/generated/hRSApiV1.schemas";
import { shortAggregationLabel } from "@/features/submissions/utils/pull-figures";
import { Label } from "@/components/ui/label";

export function FieldShell({
  fieldKey,
  label,
  helpText,
  required,
  error,
  pullMeta,
  children,
}: {
  fieldKey: string;
  label: string;
  helpText?: string | null;
  required: boolean;
  error?: string;
  /** FR-SUB-007: set only for a Number/Currency/Percentage field the last "Pull from children"
   * touched — shows whether the current value is the pulled aggregate or a manual override, with
   * the per-child breakdown that produced it. */
  pullMeta?: PullSummaryField;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={fieldKey}>
        {label}
        {required ? <span className="ml-0.5 text-danger">*</span> : null}
      </Label>
      {children}
      {helpText ? <p className="text-xs text-ink-muted">{helpText}</p> : null}
      {error ? <p className="text-xs text-danger">{error}</p> : null}
      {pullMeta ? <PullTag pullMeta={pullMeta} /> : null}
    </div>
  );
}

function PullTag({ pullMeta }: { pullMeta: PullSummaryField }) {
  const t = useTranslations("ReportForm");
  const breakdown = pullMeta.breakdown.map((entry) => `${entry.unitName} ${entry.value}`).join(" · ");

  return (
    <div className="flex flex-col gap-0.5 text-xs sm:flex-row sm:items-baseline sm:justify-between sm:gap-2">
      {pullMeta.isOverridden ? (
        <span className="inline-flex items-center gap-1 font-semibold text-status-overdue-fg">
          <Pencil className="size-3" aria-hidden="true" />
          {t("overriddenTag", { value: pullMeta.value })}
        </span>
      ) : (
        <span className="inline-flex items-center gap-1 font-semibold text-green-700">
          <ArrowUpFromLine className="size-3" aria-hidden="true" />
          {t("pulledTag", { rule: shortAggregationLabel(pullMeta.aggregation) })}
        </span>
      )}
      <span className="text-ink-muted">{breakdown}</span>
    </div>
  );
}
