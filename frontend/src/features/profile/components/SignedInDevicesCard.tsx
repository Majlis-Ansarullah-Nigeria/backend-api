"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Laptop, Smartphone } from "lucide-react";
import { useTranslations } from "next-intl";
import { ErrorState } from "@/components/shared/ErrorState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  getGetSessionsQueryKey,
  useGetSessions,
  useRevokeOtherSessions,
  useRevokeSession,
} from "@/lib/api/generated/identity/identity";
import type { SessionSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import type { ApiError } from "@/lib/api/client";
import { formatDeviceLabel, formatRelativeTime, isMobileUserAgent } from "@/features/profile/utils/format-session";

export function SignedInDevicesCard() {
  const t = useTranslations("Profile");
  const queryClient = useQueryClient();
  const sessions = useGetSessions<SessionSummary[], ApiError>();
  const revokeSession = useRevokeSession<ApiError>();
  const revokeOtherSessions = useRevokeOtherSessions<ApiError>();

  const invalidateSessions = () =>
    queryClient.invalidateQueries({ queryKey: getGetSessionsQueryKey() });

  if (sessions.isPending) {
    return (
      <section className="rounded-lg border border-line bg-surface p-6">
        <h2 className="text-lg font-semibold text-ink">{t("sessionsHeading")}</h2>
        <p className="mt-4 text-sm text-ink-muted">{t("sessionsLoading")}</p>
      </section>
    );
  }

  if (sessions.isError) {
    return (
      <section className="rounded-lg border border-line bg-surface p-6">
        <h2 className="text-lg font-semibold text-ink">{t("sessionsHeading")}</h2>
        <div className="mt-4">
          <ErrorState
            title={t("sessionsErrorTitle")}
            description={t("sessionsErrorDescription")}
            onRetry={() => sessions.refetch()}
          />
        </div>
      </section>
    );
  }

  const hasOtherSessions = sessions.data.some((session) => !session.isCurrent);

  return (
    <section className="rounded-lg border border-line bg-surface p-6">
      <div className="flex items-center justify-between gap-4">
        <h2 className="text-lg font-semibold text-ink">{t("sessionsHeading")}</h2>
        {hasOtherSessions ? (
          <Button
            variant="destructive"
            size="sm"
            disabled={revokeOtherSessions.isPending}
            onClick={() => revokeOtherSessions.mutate(undefined, { onSuccess: invalidateSessions })}
          >
            {t("signOutOthers")}
          </Button>
        ) : null}
      </div>

      <ul className="mt-4 flex flex-col divide-y divide-line">
        {sessions.data.map((session) => {
          const Icon = isMobileUserAgent(session.userAgent) ? Smartphone : Laptop;

          return (
            <li key={session.id} className="flex items-center gap-3 py-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-ground text-ink-muted">
                <Icon className="size-5" aria-hidden="true" />
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-ink">
                  {formatDeviceLabel(session.userAgent, t("unknownDevice"))}
                </p>
                <p className="truncate text-xs text-ink-muted">
                  {session.isCurrent ? t("thisDevice") : formatRelativeTime(session.createdAt)}
                </p>
              </div>
              {session.isCurrent ? (
                <Badge>{t("current")}</Badge>
              ) : (
                <Button
                  variant="destructive"
                  size="sm"
                  disabled={revokeSession.isPending}
                  onClick={() =>
                    revokeSession.mutate({ sessionId: session.id }, { onSuccess: invalidateSessions })
                  }
                >
                  {t("signOut")}
                </Button>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
