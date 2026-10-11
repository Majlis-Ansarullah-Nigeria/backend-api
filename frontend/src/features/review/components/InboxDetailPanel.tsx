"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Check, Flag, Lock, Undo2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api/client";
import type { InboxReportDetail } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetInboxQueryKey,
  getGetInboxReportDetailQueryKey,
  useAcknowledgeInboxReport,
  useGetInboxReportDetail,
  useOpenInboxReport,
  useReturnInboxReport,
  useSetInboxReportFlagged,
} from "@/lib/api/generated/review/review";
import { REPORT_INSTANCE_STATUS, reportInstanceStatusChipVariant } from "@/features/submissions/utils/report-status";

// A report can still be acknowledged/returned from Submitted (the handlers auto-open it first)
// or InReview; once Returned or Acknowledged, only Flag remains available.
const ACTIONABLE_STATUSES = new Set<number>([REPORT_INSTANCE_STATUS.Submitted, REPORT_INSTANCE_STATUS.InReview]);

export function InboxDetailPanel({ reportId }: { reportId: string }) {
  const t = useTranslations("Inbox");
  const queryClient = useQueryClient();
  const [isReturning, setIsReturning] = useState(false);
  const [returnComment, setReturnComment] = useState("");

  const query = useGetInboxReportDetail<InboxReportDetail, ApiError>(reportId);
  const openMutation = useOpenInboxReport();
  const acknowledgeMutation = useAcknowledgeInboxReport();
  const returnMutation = useReturnInboxReport();
  const flagMutation = useSetInboxReportFlagged();

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: getGetInboxQueryKey() });
    queryClient.invalidateQueries({ queryKey: getGetInboxReportDetailQueryKey(reportId) });
  };

  // FR-REV-002's "mark as read" — viewing a report IS reading it (ReportInstance.Open's own doc
  // comment: there's no separate IsRead flag), so this fires automatically rather than needing its
  // own button. Idempotent on the server, and harmless to fire again if reportId hasn't changed.
  useEffect(() => {
    openMutation.mutate({ id: reportId }, { onSuccess: invalidate });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reportId]);

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
  const canAct = ACTIONABLE_STATUSES.has(Number(data.status));

  const submitReturn = () => {
    if (!returnComment.trim()) {
      return;
    }
    returnMutation.mutate(
      { id: reportId, data: { comment: returnComment.trim() } },
      {
        onSuccess: () => {
          setIsReturning(false);
          setReturnComment("");
          invalidate();
        },
      },
    );
  };

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
          {data.isFlagged ? <StatusChip status="overdue" label={t("flaggedChip")} /> : null}
        </div>

        {data.submittedAt ? (
          <p className="mt-2 text-xs text-ink-muted">{t("submittedMeta", { date: new Date(data.submittedAt).toLocaleString() })}</p>
        ) : null}

        {data.returnComment ? (
          <div className="mt-3 rounded-md bg-status-returned-bg p-3 text-sm text-status-returned-fg">
            <p className="font-semibold">{t("returnCommentLabel")}</p>
            <p className="mt-1">{data.returnComment}</p>
          </div>
        ) : null}

        <div className="mt-4 flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => flagMutation.mutate({ id: reportId, data: { isFlagged: !data.isFlagged } }, { onSuccess: invalidate })}
            disabled={flagMutation.isPending}
          >
            <Flag className="size-4" aria-hidden="true" />
            {data.isFlagged ? t("unflagButton") : t("flagButton")}
          </Button>

          {canAct ? (
            <Button
              type="button"
              size="sm"
              onClick={() => acknowledgeMutation.mutate({ id: reportId }, { onSuccess: invalidate })}
              disabled={acknowledgeMutation.isPending}
            >
              <Check className="size-4" aria-hidden="true" />
              {t("acknowledgeButton")}
            </Button>
          ) : null}

          {canAct && !isReturning ? (
            <Button type="button" variant="outline" size="sm" onClick={() => setIsReturning(true)}>
              <Undo2 className="size-4" aria-hidden="true" />
              {t("returnButton")}
            </Button>
          ) : null}
        </div>

        {acknowledgeMutation.error instanceof ApiError ? (
          <p className="mt-2 text-sm text-danger">{acknowledgeMutation.error.message}</p>
        ) : null}

        {isReturning ? (
          <div className="mt-3 flex flex-col gap-2">
            <Textarea
              autoFocus
              value={returnComment}
              onChange={(event) => setReturnComment(event.target.value)}
              placeholder={t("returnCommentPlaceholder")}
              rows={3}
            />
            {returnMutation.error instanceof ApiError ? <p className="text-sm text-danger">{returnMutation.error.message}</p> : null}
            <div className="flex gap-2">
              <Button type="button" size="sm" onClick={submitReturn} disabled={!returnComment.trim() || returnMutation.isPending}>
                {t("sendReturn")}
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => {
                  setIsReturning(false);
                  setReturnComment("");
                }}
              >
                {t("cancel")}
              </Button>
            </div>
          </div>
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
