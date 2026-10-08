"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { ChevronDown, ChevronRight } from "lucide-react";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import type { ApiError } from "@/lib/api/client";
import type {
  TemplateVersionDetail,
  TemplateVersionSummary,
  TemplateVersionStatus,
  UnitLevelSummary,
} from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetTemplateVersionsQueryKey,
  useGetTemplateVersion,
  useGetTemplateVersions,
  usePublishTemplateVersion,
} from "@/lib/api/generated/templates/templates";
import { SectionForm } from "@/lib/form-engine/components/SectionForm";

const TEMPLATE_VERSION_STATUS = { Draft: 0, Published: 1, Retired: 2 } as const;

const STATUS_STYLES: Record<number, string> = {
  [TEMPLATE_VERSION_STATUS.Draft]: "bg-status-upcoming-bg text-status-upcoming-fg",
  [TEMPLATE_VERSION_STATUS.Published]: "bg-status-acknowledged-bg text-status-acknowledged-fg",
  [TEMPLATE_VERSION_STATUS.Retired]: "bg-status-missed-bg text-status-missed-fg",
};

export function VersionsTab({ reportTypeId, levels }: { reportTypeId: string; levels: UnitLevelSummary[] }) {
  const t = useTranslations("TemplateBuilder");
  const queryClient = useQueryClient();
  const versions = useGetTemplateVersions<TemplateVersionSummary[], ApiError>(reportTypeId);
  const publishVersion = usePublishTemplateVersion<ApiError>();
  const [expandedVersionId, setExpandedVersionId] = useState<string | null>(null);

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
      {sorted.map((version) => {
        const expanded = expandedVersionId === version.id;

        return (
          <div key={version.id} className="rounded-md border border-line bg-surface">
            <div className="flex items-center justify-between gap-3 p-3">
              <button
                type="button"
                onClick={() => setExpandedVersionId(expanded ? null : version.id)}
                className="flex items-center gap-2 text-left"
                aria-expanded={expanded}
              >
                {expanded ? (
                  <ChevronDown className="size-4 text-ink-muted" aria-hidden="true" />
                ) : (
                  <ChevronRight className="size-4 text-ink-muted" aria-hidden="true" />
                )}
                <span className="font-mono text-sm font-medium text-ink">v{version.versionNumber}</span>
                <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[version.status] ?? ""}`}>
                  {statusLabel(version.status, t)}
                </span>
              </button>
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
            {expanded ? <VersionSections reportTypeId={reportTypeId} versionId={version.id} levels={levels} /> : null}
          </div>
        );
      })}
    </div>
  );
}

function VersionSections({
  reportTypeId,
  versionId,
  levels,
}: {
  reportTypeId: string;
  versionId: string;
  levels: UnitLevelSummary[];
}) {
  const t = useTranslations("TemplateBuilder");
  const detail = useGetTemplateVersion<TemplateVersionDetail, ApiError>(reportTypeId, versionId);
  const [levelCode, setLevelCode] = useState(() => Number(levels[0]?.code ?? 0));
  const form = useForm<Record<string, unknown>>();

  if (detail.isPending) {
    return <p className="border-t border-line p-3 text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (detail.isError) {
    return (
      <div className="border-t border-line p-3">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => detail.refetch()} />
      </div>
    );
  }

  const schema = detail.data.schema;

  return (
    <div className="flex flex-col gap-4 border-t border-line p-4">
      <div className="flex flex-col gap-1.5">
        <label
          htmlFor={`version-preview-level-${versionId}`}
          className="text-xs font-semibold uppercase tracking-wide text-ink-muted"
        >
          {t("previewAsLevelLabel")}
        </label>
        <select
          id={`version-preview-level-${versionId}`}
          value={levelCode}
          onChange={(event) => setLevelCode(Number(event.target.value))}
          className="h-11 w-fit rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
        >
          {levels.map((level) => (
            <option key={level.id} value={level.code}>
              {level.name}
            </option>
          ))}
        </select>
      </div>

      {schema.sections.length === 0 ? (
        <p className="text-sm text-ink-muted">{t("noSectionsYet")}</p>
      ) : (
        <div className="flex flex-col gap-6">
          {schema.sections.map((section) => (
            <div key={section.key} className="flex flex-col gap-4">
              <h3 className="text-base font-bold text-ink">{section.title}</h3>
              <SectionForm section={section} unitLevel={levelCode} form={form} />
            </div>
          ))}
        </div>
      )}
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
