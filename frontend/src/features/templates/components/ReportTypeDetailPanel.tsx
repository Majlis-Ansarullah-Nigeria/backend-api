"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import type { ApiError } from "@/lib/api/client";
import type { ReportTypeSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetReportTypeQueryKey,
  getGetReportTypesQueryKey,
  useArchiveReportType,
  useGetReportType,
  useRestoreReportType,
  useUpdateReportType,
} from "@/lib/api/generated/templates/templates";
import type { ReportTypeFormValues } from "@/features/templates/schemas/report-type-form";
import { ReportTypeForm } from "./ReportTypeForm";

export function ReportTypeDetailPanel({ reportTypeId }: { reportTypeId: string }) {
  const t = useTranslations("ReportTypes");
  const reportType = useGetReportType<ReportTypeSummary, ApiError>(reportTypeId);

  if (reportType.isPending) {
    return (
      <div className="rounded-lg border border-line bg-surface p-6 text-sm text-ink-muted">{t("detailLoading")}</div>
    );
  }

  if (reportType.isError) {
    return (
      <div className="rounded-lg border border-line bg-surface p-6">
        <ErrorState title={t("detailErrorTitle")} onRetry={() => reportType.refetch()} />
      </div>
    );
  }

  return <ReportTypeDetailForm key={reportType.data.id} reportType={reportType.data} />;
}

function ReportTypeDetailForm({ reportType }: { reportType: ReportTypeSummary }) {
  const t = useTranslations("ReportTypes");
  const queryClient = useQueryClient();
  const updateReportType = useUpdateReportType<ApiError>();
  const archiveReportType = useArchiveReportType<ApiError>();
  const restoreReportType = useRestoreReportType<ApiError>();
  // Remounting the form on cancel is simpler than reaching into its internal react-hook-form
  // instance to reset it — bumping the key re-runs useForm(defaultValues) fresh.
  const [resetKey, setResetKey] = useState(0);

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: getGetReportTypeQueryKey(reportType.id) });
    queryClient.invalidateQueries({ queryKey: getGetReportTypesQueryKey() });
  };

  const onSubmit = (values: ReportTypeFormValues) => {
    updateReportType.mutate(
      {
        id: reportType.id,
        data: {
          name: values.name,
          description: values.description ? values.description : null,
          category: Number(values.category),
          frequency: Number(values.frequency),
          applicableLevels: values.applicableLevels.map(Number),
          recipientLevel: Number(values.recipientLevel),
          observerDepth: Number(values.observerDepth),
          allowAttachments: values.allowAttachments,
        },
      },
      { onSuccess: invalidate },
    );
  };

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-6">
      <div>
        <h2 className="text-lg font-bold text-ink">{reportType.name}</h2>
        {reportType.archivedAt ? (
          <p className="mt-2 inline-flex rounded-full bg-status-missed-bg px-2 py-0.5 text-xs font-medium text-status-missed-fg">
            {t("archived")}
          </p>
        ) : (
          <Link href={`/secretariat/templates/${reportType.id}`} className="mt-2 inline-flex text-sm font-medium text-green-700 hover:underline">
            {t("editTemplate")}
          </Link>
        )}
      </div>

      <ReportTypeForm
        key={resetKey}
        defaultValues={{
          name: reportType.name,
          description: reportType.description ?? "",
          category: String(reportType.category),
          frequency: String(reportType.frequency),
          applicableLevels: reportType.applicableLevels.map(String),
          recipientLevel: String(reportType.recipientLevel),
          observerDepth: String(reportType.observerDepth),
          allowAttachments: reportType.allowAttachments,
        }}
        onSubmit={onSubmit}
        onCancel={() => setResetKey((key) => key + 1)}
        submitLabel={t("save")}
        pendingLabel={t("saving")}
        isPending={updateReportType.isPending}
        errorMessage={updateReportType.isError ? updateReportType.error.message : undefined}
        disabled={!!reportType.archivedAt}
      />

      {archiveReportType.isError ? (
        <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
          {archiveReportType.error.message}
        </p>
      ) : null}

      {reportType.archivedAt ? (
        <Button
          type="button"
          variant="outline"
          disabled={restoreReportType.isPending}
          onClick={() => restoreReportType.mutate({ id: reportType.id }, { onSuccess: invalidate })}
        >
          {restoreReportType.isPending ? t("restoring") : t("restore")}
        </Button>
      ) : (
        <Button
          type="button"
          variant="destructive"
          disabled={archiveReportType.isPending}
          onClick={() => archiveReportType.mutate({ id: reportType.id }, { onSuccess: invalidate })}
        >
          {archiveReportType.isPending ? t("archiving") : t("archive")}
        </Button>
      )}
    </div>
  );
}
