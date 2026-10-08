"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { ArrowLeft } from "lucide-react";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import type { ApiError } from "@/lib/api/client";
import type {
  ReportTypeSummary,
  TemplateVersionDetail,
  TemplateVersionSummary,
  UnitLevelSummary,
  UnitTreeNode,
} from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetTemplateVersionsQueryKey,
  useGetReportType,
  useGetTemplateVersion,
  useGetTemplateVersions,
  usePublishTemplateVersion,
} from "@/lib/api/generated/templates/templates";
import { useGetOrganizationTree, useGetUnitLevels } from "@/lib/api/generated/organization/organization";
import { emptySchema } from "@/features/templates/utils/builder";
import { cn } from "@/lib/utils";
import { BuildTab } from "./BuildTab";
import { PreviewTab } from "./PreviewTab";
import { VersionsTab } from "./VersionsTab";

type BuilderTab = "build" | "preview" | "versions";

const TEMPLATE_VERSION_STATUS = { Draft: 0, Published: 1, Retired: 2 } as const;

export function TemplateBuilderScreen({ reportTypeId }: { reportTypeId: string }) {
  const t = useTranslations("TemplateBuilder");
  const reportType = useGetReportType<ReportTypeSummary, ApiError>(reportTypeId);
  const versions = useGetTemplateVersions<TemplateVersionSummary[], ApiError>(reportTypeId);
  const levels = useGetUnitLevels<UnitLevelSummary[], ApiError>();
  const units = useGetOrganizationTree<UnitTreeNode[], ApiError>({ includeArchived: false });

  const [tab, setTab] = useState<BuilderTab>("build");
  const queryClient = useQueryClient();
  const publishVersion = usePublishTemplateVersion<ApiError>();

  const draftSummary = versions.data?.find((v) => v.status === TEMPLATE_VERSION_STATUS.Draft);
  const publishedSummary = versions.data?.find((v) => v.status === TEMPLATE_VERSION_STATUS.Published);

  const draftDetail = useGetTemplateVersion<TemplateVersionDetail, ApiError>(reportTypeId, draftSummary?.id ?? "", {
    query: { enabled: !!draftSummary },
  });

  if (reportType.isPending || versions.isPending || levels.isPending || units.isPending) {
    return <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (reportType.isError || versions.isError || levels.isError || units.isError) {
    return (
      <div className="p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => { reportType.refetch(); versions.refetch(); levels.refetch(); units.refetch(); }} />
      </div>
    );
  }

  if (draftSummary && draftDetail.isPending) {
    return <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (draftSummary && draftDetail.isError) {
    return (
      <div className="p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => draftDetail.refetch()} />
      </div>
    );
  }

  const schema = draftDetail.data?.schema ?? emptySchema();

  return (
    <div className="flex flex-col gap-4 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <Link
            href="/secretariat/report-types"
            aria-label={t("backToReportTypes")}
            className="mt-1 flex size-9 shrink-0 items-center justify-center rounded-md border border-line-strong hover:bg-ground"
          >
            <ArrowLeft className="size-4" aria-hidden="true" />
          </Link>
          <div>
            <h1 className="text-2xl font-bold text-ink">{reportType.data.name}</h1>
            <p className="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
              {draftSummary ? (
                <span className="rounded-full bg-status-upcoming-bg px-2 py-0.5 text-xs font-medium text-status-upcoming-fg">
                  {t("draftBadge", { number: draftSummary.versionNumber })}
                </span>
              ) : null}
              {publishedSummary ? t("liveVersionNote", { number: publishedSummary.versionNumber }) : t("noLiveVersionNote")}
            </p>
          </div>
        </div>

        {draftSummary ? (
          <Button
            type="button"
            disabled={publishVersion.isPending}
            onClick={() =>
              publishVersion.mutate(
                { reportTypeId, versionId: draftSummary.id },
                { onSuccess: () => queryClient.invalidateQueries({ queryKey: getGetTemplateVersionsQueryKey(reportTypeId) }) },
              )
            }
          >
            {publishVersion.isPending ? t("publishing") : t("publishVersion", { number: draftSummary.versionNumber })}
          </Button>
        ) : null}
      </div>

      <div className="flex gap-1 border-b border-line" role="tablist" aria-label={t("builderTabsLabel")}>
        <BuilderTabButton active={tab === "build"} onClick={() => setTab("build")} label={t("buildTab")} />
        <BuilderTabButton active={tab === "preview"} onClick={() => setTab("preview")} label={t("previewTab")} />
        <BuilderTabButton active={tab === "versions"} onClick={() => setTab("versions")} label={t("versionsTab")} />
      </div>

      {tab === "build" ? (
        <BuildTab key={draftSummary?.id ?? "new"} reportTypeId={reportTypeId} initialSchema={schema} levels={levels.data} units={units.data} />
      ) : tab === "preview" ? (
        <PreviewTab schema={schema} levels={levels.data} />
      ) : (
        <VersionsTab reportTypeId={reportTypeId} levels={levels.data} />
      )}
    </div>
  );
}

function BuilderTabButton({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        "border-b-2 px-4 py-2 text-sm font-medium",
        active ? "border-green-600 text-green-700" : "border-transparent text-ink-muted hover:text-ink",
      )}
    >
      {label}
    </button>
  );
}
