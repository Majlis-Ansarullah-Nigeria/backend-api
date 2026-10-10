"use client";

import { CheckCircle2, Download } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api/client";
import type { ReportInstanceForEditing } from "@/lib/api/generated/hRSApiV1.schemas";
import { getReportReceiptPdf, useGetReportInstanceForEditing } from "@/lib/api/generated/submissions/submissions";
import { downloadBlob } from "@/features/organization/utils/bulk-import";
import { reportInstanceStatusChipVariant } from "@/features/submissions/utils/report-status";

export function SubmittedScreen({ id, unitId }: { id: string; unitId: string }) {
  const t = useTranslations("ReportSubmitted");
  const query = useGetReportInstanceForEditing<ReportInstanceForEditing, ApiError>(id, { unit: unitId });
  const [downloading, setDownloading] = useState(false);

  const downloadPdf = async () => {
    setDownloading(true);
    try {
      const blob = (await getReportReceiptPdf(id, { unit: unitId })) as unknown as Blob;
      downloadBlob(blob, "report-receipt.pdf");
    } finally {
      setDownloading(false);
    }
  };

  if (query.isPending) {
    return <p className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</p>;
  }

  if (query.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      </div>
    );
  }

  const data = query.data;

  return (
    <div className="mx-auto flex max-w-md flex-col items-center gap-4 p-4 text-center sm:p-6">
      <div className="flex size-20 items-center justify-center rounded-full bg-mint">
        <CheckCircle2 className="size-10 text-green-700" aria-hidden="true" />
      </div>
      <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
      <p className="text-sm text-ink-muted">{t("body", { period: data.periodLabel })}</p>
      {data.isNil ? <p className="text-sm text-ink-muted">{t("nilReportNote")}</p> : null}

      <div className="mt-2 flex w-full flex-col gap-3 rounded-lg border border-line bg-surface p-4 text-left">
        <div className="flex items-baseline justify-between gap-3 text-sm">
          <span className="text-ink-muted">{t("reference")}</span>
          <span className="font-mono font-semibold text-ink">{data.referenceNumber}</span>
        </div>
        <div className="flex items-baseline justify-between gap-3 text-sm">
          <span className="text-ink-muted">{t("submitted")}</span>
          <span className="font-medium text-ink">{new Date(data.submittedAt ?? "").toLocaleString()}</span>
        </div>
        <div className="flex items-center justify-between gap-3 text-sm">
          <span className="text-ink-muted">{t("status")}</span>
          <StatusChip status={reportInstanceStatusChipVariant(data.status)} />
        </div>
      </div>

      <Button type="button" className="w-full" onClick={() => void downloadPdf()} disabled={downloading}>
        <Download className="size-4" aria-hidden="true" />
        {t("downloadPdf")}
      </Button>
      <Link href="/home" className="w-full">
        <Button type="button" variant="outline" className="w-full">
          {t("backToHome")}
        </Button>
      </Link>

      <p className="mt-2 text-xs text-ink-muted">{t("withdrawNote")}</p>
    </div>
  );
}
