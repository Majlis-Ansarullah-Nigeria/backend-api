"use client";

import { ArrowLeft } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { ErrorState } from "@/components/shared/ErrorState";
import type { ApiError } from "@/lib/api/client";
import type { WorkloadView } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetWorkload } from "@/lib/api/generated/review/review";

export function WorkloadScreen() {
  const t = useTranslations("Workload");
  const query = useGetWorkload<WorkloadView, ApiError>();

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex items-start gap-3">
        <Link
          href="/secretariat/inbox"
          aria-label={t("backToInbox")}
          className="mt-1 flex size-9 shrink-0 items-center justify-center rounded-md border border-line-strong hover:bg-ground"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
        </Link>
        <div>
          <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
          <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
        </div>
      </div>

      {query.isPending ? (
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      ) : query.isError ? (
        <ErrorState title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <div className="rounded-lg border border-line-strong bg-ground p-4">
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{t("unassignedLabel")}</p>
            <p className="mt-1 text-3xl font-bold text-ink">{query.data.unassignedCount}</p>
          </div>
          {query.data.staff.map((staff) => (
            <div key={staff.userId} className="rounded-lg border border-line bg-surface p-4">
              <p className="font-medium text-ink">{staff.name}</p>
              <p className="mt-1 text-3xl font-bold text-ink">{staff.assignedCount}</p>
              <p className="text-xs text-ink-muted">{t("assignedCountLabel")}</p>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
