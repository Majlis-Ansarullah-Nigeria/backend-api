"use client";

import { ArrowLeft, Download, Lock } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { ReadOnlyBanner } from "@/components/shared/ReadOnlyBanner";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api/client";
import type { ChildReportView } from "@/lib/api/generated/hRSApiV1.schemas";
import { getChildReportPdf, useGetChildReport } from "@/lib/api/generated/observer/observer";
import { downloadBlob } from "@/features/organization/utils/bulk-import";
import { PrivateNotesCard } from "@/features/observer/components/PrivateNotesCard";
import { reportInstanceStatusChipVariant, REPORT_INSTANCE_STATUS } from "@/features/submissions/utils/report-status";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

export function ChildReportScreen({ unitId, reportId }: { unitId: string; reportId: string }) {
  const fmt = useOrgFormat();
  const t = useTranslations("ChildReport");
  const [downloading, setDownloading] = useState(false);

  const query = useGetChildReport<ChildReportView, ApiError>(unitId, reportId);

  const downloadPdf = async () => {
    setDownloading(true);
    try {
      const blob = (await getChildReportPdf(unitId, reportId)) as unknown as Blob;
      downloadBlob(blob, "child-report.pdf");
    } finally {
      setDownloading(false);
    }
  };

  if (query.isPending) {
    return <p className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</p>;
  }

  if (query.isError) {
    if (query.error instanceof ApiError && query.error.code === "NOT_SUBMITTED") {
      return (
        <div className="p-4 sm:p-6">
          <EmptyState icon={Lock} title={t("notSubmittedTitle")} description={query.error.message} />
        </div>
      );
    }

    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      </div>
    );
  }

  const data = query.data;
  const isPostSubmission =
    data.status === REPORT_INSTANCE_STATUS.Submitted ||
    data.status === REPORT_INSTANCE_STATUS.InReview ||
    data.status === REPORT_INSTANCE_STATUS.Returned ||
    data.status === REPORT_INSTANCE_STATUS.Acknowledged;

  return (
    <div className="flex flex-col gap-4 p-4 sm:p-6">
      <div className="flex items-start gap-3">
        <Link
          href="/units"
          aria-label={t("backToGrid")}
          className="mt-1 flex size-9 shrink-0 items-center justify-center rounded-md border border-line-strong hover:bg-ground"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
        </Link>
        <div className="min-w-0 flex-1">
          <h1 className="text-xl font-bold text-ink sm:text-2xl">
            {data.receipt.unitName} · {data.receipt.periodLabel}
          </h1>
          <p className="mt-1 text-sm text-ink-muted">
            {data.receipt.reportTypeName}
            {data.submittedAt ? (
              <>
                {" · "}
                {t("submittedMeta", {
                  date: fmt.dateTime(data.submittedAt),
                  name: data.submittedByName ?? t("unknownSubmitter"),
                })}
              </>
            ) : null}
          </p>
        </div>
      </div>

      <ReadOnlyBanner>{t("readOnlyBanner", { unitName: data.receipt.unitName })}</ReadOnlyBanner>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <StatusChip status={reportInstanceStatusChipVariant(Number(data.status))} />
          {isPostSubmission ? (
            <span
              className={
                data.isLate
                  ? "rounded-md bg-status-late-bg px-2.5 py-1 text-xs font-bold text-status-late-fg"
                  : "rounded-md bg-mint px-2.5 py-1 text-xs font-bold text-green-700"
              }
            >
              {data.isLate ? t("late") : t("onTime")}
            </span>
          ) : null}
        </div>
        <Button type="button" onClick={() => void downloadPdf()} disabled={downloading}>
          <Download className="size-4" aria-hidden="true" />
          {t("downloadPdf")}
        </Button>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {data.receipt.sections.map((section) => (
          <div key={section.title} className="rounded-lg border border-line bg-surface p-4">
            <h2 className="font-bold text-ink">{section.title}</h2>
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

      <PrivateNotesCard unitId={unitId} reportId={reportId} />
    </div>
  );
}
