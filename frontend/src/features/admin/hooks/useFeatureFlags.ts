"use client";

import type { FeatureFlagsView } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetFeatureFlags } from "@/lib/api/generated/settings/settings";
import { ApiError } from "@/lib/api/client";

/** FR-ADM-004's switchable modules — mirrors the backend FeatureModule enum. */
export const FEATURE_MODULE = {
  Dashboards: 0,
  CompiledReports: 1,
  ComparisonAndVersions: 2,
  WebPush: 3,
} as const;

export type FeatureModuleValue = (typeof FEATURE_MODULE)[keyof typeof FEATURE_MODULE];

export const FEATURE_DISABLED = "FEATURE_DISABLED";

export function isFeatureDisabledError(error: unknown): boolean {
  return error instanceof ApiError && error.code === FEATURE_DISABLED;
}

/**
 * Which modules are on. Navigation only — the API refuses a switched-off module on every request.
 * Until the flags load (or if they fail to), every module counts as on: a link that briefly shows
 * just meets the API's refusal, whereas hiding everything would flicker on every page.
 */
export function useFeatureFlags() {
  const query = useGetFeatureFlags<FeatureFlagsView>({ query: { staleTime: 5 * 60 * 1000 } });
  const flags = query.data?.flags ?? [];
  const isEnabled = (module: FeatureModuleValue) => flags.find((flag) => Number(flag.module) === module)?.enabled ?? true;
  return { isEnabled, canEdit: query.data?.canEdit ?? false, flags, query };
}
