"use client";

import { Lock } from "lucide-react";
import { useTranslations } from "next-intl";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import type { ApiError } from "@/lib/api/client";
import type { InboxReportDetail } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetInboxReportDetail } from "@/lib/api/generated/review/review";
import { reportInstanceStatusChipVariant } from "@/features/submissions/utils/report-status";

export function InboxDetailPanel({ reportId }: { reportId: string }) {
  const t = useTranslations("Inbox");
  const query = useGetInboxReportDetail<InboxReportDetail, ApiError>(reportId);

  if (query.isPending) {
    return <div className="rounded-lg border border-line bg-surface p-6 text-sm text-ink-muted">{t("loading")}</div>;
  }

  if (query.isError) {
    return (
      <div className="rounded-lg border border-line bg-surface p-2">
        <ErrorState title={t("detailLoadErrorTitle")} onRetry={() => query.refetch()} />
      </div>
    );
  }

  const data = query.data;

  return (
    <div className="flex flex-col gap-4">
      <div className="rounded-lg border border-line bg-surface p-4">
        <h2 className="font-bold text-ink">{data.receipt.unitName}</h2>
        <p className="text-sm text-ink-muted">
          {data.receipt.reportTypeName} · {data.receipt.periodLabel}
        </p>
        {data.receipt.referenceNumber ? (
          <p className="mt-1 font-mono text-xs text-ink-muted">{data.receipt.referenceNumber}</p>
        ) : null}

        <div className="mt-3 flex flex-wrap items-center gap-1.5">
          <StatusChip status={reportInstanceStatusChipVariant(Number(data.status))} />
          {data.isLate ? <StatusChip status="late" label={t("lateChip")} /> : null}
        </div>

        {data.submittedAt ? (
          <p className="mt-2 text-xs text-ink-muted">{t("submittedMeta", { date: new Date(data.submittedAt).toLocaleString() })}</p>
        ) : null}
      </div>

      {data.receipt.sections.map((section) => (
        <div key={section.title} className="rounded-lg border border-line bg-surface p-4">
          <h3 className="font-bold text-ink">{section.title}</h3>
          <dl className="mt-3 flex flex-col gap-2">
            {section.fields.map((field) => (
              <div key={field.label} className="flex items-baseline justify-between gap-3 text-sm">
                <dt className="text-ink-muted">{field.label}</dt>
                <dd className="text-right font-medium text-ink">
                  {field.isMasked ? (
                    <span className="inline-flex items-center gap-1.5 text-ink-muted">
                      <Lock className="size-3.5" aria-hidden="true" />
                      {field.value}
                    </span>
                  ) : (
                    field.value
                  )}
                </dd>
              </div>
            ))}
          </dl>
        </div>
      ))}
    </div>
  );
}
