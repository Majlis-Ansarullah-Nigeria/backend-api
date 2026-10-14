"use client";

import { CheckCircle2, Download, FileText, Table2, XCircle } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api/client";
import type { ExportSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { getExportDownloadUrl, useGetExport } from "@/lib/api/generated/exports/exports";
import { BULK_EXPORT_FORMAT, BULK_EXPORT_STATUS } from "@/features/exports/utils/export-status";

const POLL_INTERVAL_MS = 4000;

export function ExportStatusScreen({ id }: { id: string }) {
  const t = useTranslations("Exports");
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const query = useGetExport<ExportSummary, ApiError>(id, {
    query: {
      refetchInterval: (q) => {
        const status = q.state.data?.status;
        return status === BULK_EXPORT_STATUS.Ready || status === BULK_EXPORT_STATUS.Failed ? false : POLL_INTERVAL_MS;
      },
    },
  });

  const download = async () => {
    setDownloading(true);
    setDownloadError(null);
    try {
      const { url } = await getExportDownloadUrl(id);
      window.location.assign(url);
    } catch (error) {
      setDownloadError(error instanceof ApiError ? error.message : t("downloadFailed"));
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
  const FormatIcon = data.format === BULK_EXPORT_FORMAT.ZipOfPdfs ? FileText : Table2;

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("statusPageTitle")}</h1>
        <Link href="/secretariat/exports" className="mt-1 inline-block text-sm text-green-700 underline-offset-2 hover:underline">
          {t("backToExports")}
        </Link>
      </div>

      <div className="flex flex-col items-center gap-4 rounded-lg border border-line bg-surface p-8 text-center">
        <FormatIcon className="size-10 text-green-700" aria-hidden="true" />

        {data.status === BULK_EXPORT_STATUS.Ready && (
          <>
            <CheckCircle2 className="size-8 text-green-600" aria-hidden="true" />
            <p className="text-lg font-semibold text-ink">{t("readyMessage", { count: Number(data.reportCount) })}</p>
            {downloadError && <p className="text-sm text-danger">{downloadError}</p>}
            <Button type="button" onClick={download} disabled={downloading}>
              <Download className="size-4" aria-hidden="true" />
              {t("downloadAction")}
            </Button>
          </>
        )}

        {data.status === BULK_EXPORT_STATUS.Failed && (
          <>
            <XCircle className="size-8 text-danger" aria-hidden="true" />
            <p className="text-lg font-semibold text-ink">{t("failedMessage")}</p>
            {data.errorMessage && <p className="text-sm text-ink-muted">{data.errorMessage}</p>}
          </>
        )}

        {(data.status === BULK_EXPORT_STATUS.Pending || data.status === BULK_EXPORT_STATUS.Processing) && (
          <p className="text-lg font-semibold text-ink">{t("inProgressMessage")}</p>
        )}
      </div>
    </div>
  );
}
