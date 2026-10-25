"use client";

import { PowerOff } from "lucide-react";
import { useTranslations } from "next-intl";
import { isFeatureDisabledError } from "@/features/admin/hooks/useFeatureFlags";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState, type ErrorStateProps } from "@/components/shared/ErrorState";

/** An ErrorState that explains a switched-off module (FR-ADM-004's FEATURE_DISABLED) instead of
 * offering a retry that can only fail again. */
export function FeatureAwareErrorState({ error, ...props }: ErrorStateProps & { error: unknown }) {
  const t = useTranslations("Shell");
  if (isFeatureDisabledError(error)) {
    return <EmptyState icon={PowerOff} title={t("featureDisabledTitle")} description={t("featureDisabledDescription")} className={props.className} />;
  }
  return <ErrorState {...props} />;
}
