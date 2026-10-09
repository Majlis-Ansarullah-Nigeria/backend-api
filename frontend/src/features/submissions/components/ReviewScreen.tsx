"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, ArrowLeft, Check, Clock, Download, Pencil, Send } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { ApiError } from "@/lib/api/client";
import type { ReportInstanceForEditing } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetMyReportsQueryKey,
  getReportReceiptPdf,
  submitReport,
  useGetReportInstanceForEditing,
  useGetReportReviewWarnings,
} from "@/lib/api/generated/submissions/submissions";
import { visibleFields } from "@/lib/form-engine/field-visibility";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";
import { downloadBlob } from "@/features/organization/utils/bulk-import";
import { formatAnswerForDisplay } from "@/features/submissions/utils/answer-display";
import { countdownText } from "@/features/submissions/utils/countdown";
import { canSubmitFromStatus } from "@/features/submissions/utils/report-status";
import { sectionProgress } from "@/features/submissions/utils/section-progress";

export function ReviewScreen({ id, unitId }: { id: string; unitId: string }) {
  const t = useTranslations("ReportReview");
  const router = useRouter();
  const queryClient = useQueryClient();
  const idempotencyKey = useRef(crypto.randomUUID());

  const query = useGetReportInstanceForEditing<ReportInstanceForEditing, ApiError>(id, { unit: unitId });
  const warningsQuery = useGetReportReviewWarnings(id, { unit: unitId }, { query: { enabled: !!query.data && canSubmitFromStatus(query.data.status) } });

  const [confirmed, setConfirmed] = useState(false);
  const [downloading, setDownloading] = useState(false);

  const submitMutation = useMutation({
    mutationFn: () =>
      submitReport(id, { confirmed: true }, { unit: unitId }, { headers: { "Idempotency-Key": idempotencyKey.current } }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: getGetMyReportsQueryKey() });
      router.push(`/reports/${id}/submitted?unit=${unitId}`);
    },
  });

  const downloadDraftPdf = async () => {
    setDownloading(true);
    try {
      const blob = (await getReportReceiptPdf(id, { unit: unitId })) as unknown as Blob;
      downloadBlob(blob, "draft-report.pdf");
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

  if (!canSubmitFromStatus(data.status)) {
    return (
      <div className="flex flex-col gap-4 p-4 sm:p-6">
        <p className="text-sm text-ink-muted">{t("notReviewable")}</p>
        <Link href={`/reports/${id}/edit?unit=${unitId}`}>
          <Button type="button" variant="outline">
            {t("backToForm")}
          </Button>
        </Link>
      </div>
    );
  }

  const sections = data.schema.sections;
  const unitLevel = Number(data.unitLevel);
  const progress = sectionProgress(sections, unitLevel, data.answers as Record<string, unknown>);
  const warnings = warningsQuery.data?.warnings ?? [];
  const submitError = submitMutation.error instanceof ApiError ? submitMutation.error : null;

  return (
    <div className="flex flex-col gap-4 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <Link
            href={`/reports/${id}/edit?unit=${unitId}`}
            aria-label={t("backToForm")}
            className="mt-1 flex size-9 shrink-0 items-center justify-center rounded-md border border-line-strong hover:bg-ground"
          >
            <ArrowLeft className="size-4" aria-hidden="true" />
          </Link>
          <div>
            <h1 className="text-xl font-bold text-ink sm:text-2xl">{t("title")}</h1>
            <p className="mt-1 text-sm text-ink-muted">
              {data.reportTypeName} · {data.periodLabel} · {data.unitName}
            </p>
          </div>
        </div>

        <Button type="button" variant="outline" onClick={() => void downloadDraftPdf()} disabled={downloading}>
          <Download className="size-4" aria-hidden="true" />
          {t("downloadDraftPdf")}
        </Button>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className="rounded-lg border border-line bg-surface p-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{t("sections")}</p>
          <p className="mt-1 text-lg font-bold text-ink">
            {progress.completed}/{progress.total}
          </p>
          <p className="text-sm text-ink-muted">{progress.completed === progress.total ? t("allComplete") : t("someIncomplete")}</p>
        </div>

        <div className="rounded-lg border border-line bg-surface p-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{t("checks")}</p>
          <p className="mt-1 flex items-center gap-2 text-lg font-bold text-ink">
            {warnings.length > 0 ? <AlertTriangle className="size-5 text-amber-600" aria-hidden="true" /> : <Check className="size-5 text-green-600" aria-hidden="true" />}
            {warnings.length}
          </p>
          <p className="text-sm text-ink-muted">{warnings.length === 0 ? t("noWarnings") : t("warningsToLookAt", { count: warnings.length })}</p>
        </div>

        <div className="rounded-lg border border-line bg-surface p-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{t("window")}</p>
          <p className="mt-1 flex items-center gap-2 text-lg font-bold text-ink">
            <Clock className="size-5 text-ink-muted" aria-hidden="true" />
          </p>
          <p className="text-sm text-ink-muted">{data.dueAt ? countdownText(data.dueAt) : t("noDueDate")}</p>
        </div>
      </div>

      {warnings.length > 0 ? (
        <div className="flex flex-col gap-2 rounded-lg border border-amber-200 bg-amber-50 p-4">
          {warnings.map((warning) => (
            <p key={warning} className="flex items-start gap-2 text-sm text-amber-900">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
              {warning}
            </p>
          ))}
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {sections.map((section, index) => {
          const fields = visibleFields(section.fields, unitLevel, data.answers as Record<string, unknown>).filter(
            (f) => f.type !== TEMPLATE_FIELD_TYPE.Computed,
          );

          return (
            <div key={section.key} className="rounded-lg border border-line bg-surface p-4">
              <div className="flex items-center justify-between">
                <h2 className="font-bold text-ink">
                  {index + 1} · {section.title}
                </h2>
                <Link
                  href={`/reports/${id}/edit?unit=${unitId}`}
                  className="flex items-center gap-1 text-sm font-medium text-green-700 hover:underline"
                >
                  <Pencil className="size-3.5" aria-hidden="true" />
                  {t("edit")}
                </Link>
              </div>
              <dl className="mt-3 flex flex-col gap-2">
                {fields.map((field) => (
                  <div key={field.key} className="flex items-baseline justify-between gap-3 text-sm">
                    <dt className="text-ink-muted">{field.label}</dt>
                    <dd className="text-right font-medium text-ink">
                      {formatAnswerForDisplay(field, (data.answers as Record<string, unknown>)[field.key])}
                    </dd>
                  </div>
                ))}
              </dl>
            </div>
          );
        })}
      </div>

      <div className="rounded-lg border border-line bg-surface p-4">
        <label className="flex items-start gap-3 text-sm text-ink">
          <Checkbox checked={confirmed} onCheckedChange={(checked) => setConfirmed(checked === true)} />
          {t("declaration", { unitName: data.unitName })}
        </label>
        <p className="mt-2 text-sm text-ink-muted">{t("routingNote")}</p>

        {submitError ? <p className="mt-3 text-sm text-danger">{submitError.message}</p> : null}

        <div className="mt-4 flex justify-end gap-2">
          <Link href={`/reports/${id}/edit?unit=${unitId}`}>
            <Button type="button" variant="outline">
              {t("backToForm")}
            </Button>
          </Link>
          <Button type="button" onClick={() => submitMutation.mutate()} disabled={!confirmed || submitMutation.isPending}>
            <Send className="size-4" aria-hidden="true" />
            {t("submitReport")}
          </Button>
        </div>
      </div>
    </div>
  );
}
