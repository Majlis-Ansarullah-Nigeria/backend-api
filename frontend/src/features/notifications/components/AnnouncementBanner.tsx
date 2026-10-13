"use client";

import { Megaphone, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { useQueryClient } from "@tanstack/react-query";
import type { ApiError } from "@/lib/api/client";
import type { NotificationSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetMyBannerNotificationQueryKey,
  getGetUnreadNotificationCountQueryKey,
  useGetMyBannerNotification,
  useMarkNotificationRead,
} from "@/lib/api/generated/notifications/notifications";

/** FR-NOT-006's "Banner on home" — the latest unread announcement flagged to show here.
 * Dismissing it is the same action as marking it read in the notification centre (M8.2), so
 * there's no separate dismiss state to track. */
export function AnnouncementBanner() {
  const t = useTranslations("HeadHome");
  const queryClient = useQueryClient();
  const banner = useGetMyBannerNotification<NotificationSummary | null, ApiError>();
  const markRead = useMarkNotificationRead<ApiError>();

  if (!banner.data) {
    return null;
  }

  const dismiss = () => {
    markRead.mutate(
      { id: banner.data!.id },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: getGetMyBannerNotificationQueryKey() });
          queryClient.invalidateQueries({ queryKey: getGetUnreadNotificationCountQueryKey() });
        },
      },
    );
  };

  return (
    <div className="flex items-start gap-3 rounded-lg bg-mint p-4">
      <Megaphone className="mt-0.5 size-5 shrink-0 text-green-700" aria-hidden="true" />
      <div className="flex-1">
        <p className="font-bold text-green-700">{banner.data.subject}</p>
        <p className="mt-0.5 text-sm text-green-700/90">{banner.data.body}</p>
      </div>
      <button
        type="button"
        aria-label={t("dismissAnnouncement")}
        onClick={dismiss}
        className="shrink-0 rounded-md p-1 text-green-700 hover:bg-green-700/10"
      >
        <X className="size-4" aria-hidden="true" />
      </button>
    </div>
  );
}
