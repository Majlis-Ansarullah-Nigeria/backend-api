"use client";

import { useQueryClient } from "@tanstack/react-query";
import { BarChart3, Check, Flag, Lock, Undo2 } from "lucide-react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api/client";
import type { InboxReportDetail, WorkloadView } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetInboxQueryKey,
  getGetInboxReportDetailQueryKey,
  useAcknowledgeInboxReport,
  useAssignInboxReport,
  useGetInboxReportDetail,
  useGetWorkload,
  useOpenInboxReport,
  useReturnInboxReport,
  useSetInboxReportFlagged,
} from "@/lib/api/generated/review/review";
import { REPORT_INSTANCE_STATUS, reportInstanceStatusChipVariant } from "@/features/submissions/utils/report-status";
import { ReportVersionsPanel } from "@/features/history/components/ReportVersionsPanel";
import { CommentsPanel } from "./CommentsPanel";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

// A report can still be acknowledged/returned from Submitted (the handlers auto-open it first)
// or InReview; once Returned or Acknowledged, only Flag remains available.
const ACTIONABLE_STATUSES = new Set<number>([REPORT_INSTANCE_STATUS.Submitted, REPORT_INSTANCE_STATUS.InReview]);

export function InboxDetailPanel({
  reportId,
  compareWith,
}: {
  reportId: string;
  /** The report's unit and type, for the "Compare periods" link (FR-HIS-005). */
  compareWith?: { unitId: string; reportTypeId: string; windowId: string };
}) {
  const fmt = useOrgFormat();
  const t = useTranslations("Inbox");
  const queryClient = useQueryClient();
  const [isReturning, setIsReturning] = useState(false);
  const [returnComment, setReturnComment] = useState("");

  const query = useGetInboxReportDetail<InboxReportDetail, ApiError>(reportId);
  const workload = useGetWorkload<WorkloadView, ApiError>();
  const openMutation = useOpenInboxReport();
  const acknowledgeMutation = useAcknowledgeInboxReport();
  const returnMutation = useReturnInboxReport();
  const flagMutation = useSetInboxReportFlagged();
  const assignMutation = useAssignInboxReport();

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
          <p className="mt-2 text-xs text-ink-muted">{t("submittedMeta", { date: fmt.dateTime(data.submittedAt) })}</p>
        ) : null}

        {data.returnComment ? (
          <div className="mt-3 rounded-md bg-status-returned-bg p-3 text-sm text-status-returned-fg">
            <p className="font-semibold">{t("returnCommentLabel")}</p>
            <p className="mt-1">{data.returnComment}</p>
          </div>
        ) : null}

        <div className="mt-4 flex flex-col gap-1.5">
          <Label htmlFor="inbox-assign-to">{t("assignToLabel")}</Label>
          <select
            id="inbox-assign-to"
            value={data.assignedToUserId ?? ""}
            onChange={(event) =>
              assignMutation.mutate(
                { id: reportId, data: { assignedToUserId: event.target.value || null } },
                { onSuccess: invalidate },
              )
            }
            disabled={assignMutation.isPending}
            className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("unassigned")}</option>
            {(workload.data?.staff ?? []).map((staff) => (
              <option key={staff.userId} value={staff.userId}>
                {staff.name}
              </option>
            ))}
          </select>
        </div>

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
              <div key={field.fieldKey} className="flex items-baseline justify-between gap-3 text-sm">
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

      <ReportVersionsPanel reportId={reportId} />

      {compareWith ? (
        <Link
          href={`/history/compare?unitId=${compareWith.unitId}&reportTypeId=${compareWith.reportTypeId}&periodB=${compareWith.windowId}`}
          className="inline-flex items-center gap-2 self-start text-sm font-semibold text-green-700 underline-offset-2 hover:underline"
        >
          <BarChart3 className="size-4" aria-hidden="true" />
          {t("comparePeriodsLink")}
        </Link>
      ) : null}

      <CommentsPanel
        reportId={reportId}
        fields={data.receipt.sections.flatMap((section) => section.fields.map((field) => ({ fieldKey: field.fieldKey, label: field.label })))}
      />
    </div>
  );
}
