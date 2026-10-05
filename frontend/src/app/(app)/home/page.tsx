"use client";

import { useTranslations } from "next-intl";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { ReadOnlyBanner } from "@/components/shared/ReadOnlyBanner";
import { SaveIndicator, type SaveStatus } from "@/components/shared/SaveIndicator";
import { StatusChip, type StatusChipVariant } from "@/components/shared/StatusChip";
import { CheckCircle2 } from "lucide-react";

const ALL_STATUSES: StatusChipVariant[] = [
  "not-started",
  "draft",
  "submitted",
  "in-review",
  "late",
  "returned",
  "acknowledged",
  "missed",
  "overdue",
  "upcoming",
];

const SAVE_STATUSES: SaveStatus[] = ["saving", "saved", "offline", "error"];

export default function HomePlaceholderPage() {
  const t = useTranslations("HomePlaceholder");
  const [retried, setRetried] = useState(false);

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-8 px-4 py-8 md:px-8">
      <div>
        <h1 className="text-2xl font-semibold text-ink">{t("greeting", { name: "Abdulrahman" })}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>

      <section className="flex flex-col gap-6 rounded-lg border border-line bg-surface p-6">
        <h2 className="text-lg font-semibold text-ink">{t("componentsHeading")}</h2>

        <div>
          <p className="mb-2 text-sm font-medium text-ink-muted">{t("statusChipsLabel")}</p>
          <div className="flex flex-wrap gap-2">
            {ALL_STATUSES.map((status) => (
              <StatusChip key={status} status={status} />
            ))}
          </div>
        </div>

        <div>
          <p className="mb-2 text-sm font-medium text-ink-muted">{t("saveIndicatorLabel")}</p>
          <div className="flex flex-wrap gap-4">
            {SAVE_STATUSES.map((status) => (
              <SaveIndicator key={status} status={status} savedLabel="8 seconds" />
            ))}
          </div>
        </div>

        <div>
          <p className="mb-2 text-sm font-medium text-ink-muted">{t("readOnlyLabel")}</p>
          <ReadOnlyBanner />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <p className="mb-2 text-sm font-medium text-ink-muted">{t("emptyStateLabel")}</p>
            <EmptyState
              icon={CheckCircle2}
              title={t("emptyStateTitle")}
              description={t("emptyStateDescription")}
            />
          </div>
          <div>
            <p className="mb-2 text-sm font-medium text-ink-muted">{t("errorStateLabel")}</p>
            {retried ? (
              <EmptyState
                icon={CheckCircle2}
                title={t("emptyStateTitle")}
                description={t("emptyStateDescription")}
              />
            ) : (
              <ErrorState
                title={t("errorStateTitle")}
                description={t("errorStateDescription")}
                onRetry={() => setRetried(true)}
              />
            )}
          </div>
        </div>
      </section>
    </div>
  );
}
