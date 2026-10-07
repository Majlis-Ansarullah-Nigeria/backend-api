"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import type { ApiError } from "@/lib/api/client";
import type { TemplateVersionSummary, TemplateVersionStatus } from "@/lib/api/generated/hRSApiV1.schemas";
import { getGetTemplateVersionsQueryKey, useGetTemplateVersions, usePublishTemplateVersion } from "@/lib/api/generated/templates/templates";

const TEMPLATE_VERSION_STATUS = { Draft: 0, Published: 1, Retired: 2 } as const;

const STATUS_STYLES: Record<number, string> = {
  [TEMPLATE_VERSION_STATUS.Draft]: "bg-status-upcoming-bg text-status-upcoming-fg",
  [TEMPLATE_VERSION_STATUS.Published]: "bg-status-acknowledged-bg text-status-acknowledged-fg",
  [TEMPLATE_VERSION_STATUS.Retired]: "bg-status-missed-bg text-status-missed-fg",
};

export function VersionsTab({ reportTypeId }: { reportTypeId: string }) {
  const t = useTranslations("TemplateBuilder");
  const queryClient = useQueryClient();
  const versions = useGetTemplateVersions<TemplateVersionSummary[], ApiError>(reportTypeId);
  const publishVersion = usePublishTemplateVersion<ApiError>();

  if (versions.isPending) {
    return <p className="text-sm text-ink-muted">{t("loadingVersions")}</p>;
  }

  if (versions.isError) {
    return <ErrorState title={t("versionsErrorTitle")} onRetry={() => versions.refetch()} />;
  }

  if (versions.data.length === 0) {
    return <p className="text-sm text-ink-muted">{t("noVersionsYet")}</p>;
  }

  const sorted = [...versions.data].sort((a, b) => Number(b.versionNumber) - Number(a.versionNumber));

  return (
    <div className="flex flex-col gap-2">
      {sorted.map((version) => (
        <div key={version.id} className="flex items-center justify-between gap-3 rounded-md border border-line bg-surface p-3">
          <div className="flex items-center gap-2">
            <span className="font-mono text-sm font-medium text-ink">v{version.versionNumber}</span>
            <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[version.status] ?? ""}`}>
              {statusLabel(version.status, t)}
            </span>
          </div>
          {version.status === TEMPLATE_VERSION_STATUS.Draft ? (
            <Button
              type="button"
              size="sm"
              disabled={publishVersion.isPending}
              onClick={() =>
                publishVersion.mutate(
                  { reportTypeId, versionId: version.id },
                  { onSuccess: () => queryClient.invalidateQueries({ queryKey: getGetTemplateVersionsQueryKey(reportTypeId) }) },
                )
              }
            >
              {publishVersion.isPending ? t("publishing") : t("publishVersion", { number: version.versionNumber })}
            </Button>
          ) : null}
        </div>
      ))}
    </div>
  );
}

function statusLabel(status: TemplateVersionStatus, t: ReturnType<typeof useTranslations>): string {
  switch (status) {
    case TEMPLATE_VERSION_STATUS.Draft:
      return t("statusDraft");
    case TEMPLATE_VERSION_STATUS.Published:
      return t("statusPublished");
    case TEMPLATE_VERSION_STATUS.Retired:
      return t("statusRetired");
    default:
      return t("statusUnknown");
  }
}
