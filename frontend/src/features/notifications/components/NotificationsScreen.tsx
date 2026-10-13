"use client";

import { AlertTriangle, Bell, Calendar, CheckCircle, CornerUpLeft, FileText, MessageSquare, Pencil } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type { ApiError } from "@/lib/api/client";
import type { NotificationSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetUnreadNotificationCountQueryKey,
  useGetMyNotifications,
  useGetUnreadNotificationCount,
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
} from "@/lib/api/generated/notifications/notifications";
import { usePushSubscription } from "@/features/notifications/hooks/usePushSubscription";
import {
  NOTIFICATION_FILTER,
  type NotificationIconName,
  notificationEventTypeStyle,
} from "@/features/notifications/utils/notification-event-type";
import { formatNotificationTimestamp, notificationDayGroup } from "@/features/notifications/utils/format-notification-time";

const ICONS: Record<NotificationIconName, typeof Bell> = {
  calendar: Calendar,
  clock: Bell,
  "alert-triangle": AlertTriangle,
  "check-circle": CheckCircle,
  "corner-up-left": CornerUpLeft,
  "message-square": MessageSquare,
  "file-text": FileText,
  pencil: Pencil,
};

type FilterKey = "all" | "unread" | "deadlines";

const FILTER_VALUES: Record<FilterKey, number> = {
  all: NOTIFICATION_FILTER.All,
  unread: NOTIFICATION_FILTER.Unread,
  deadlines: NOTIFICATION_FILTER.Deadlines,
};

export function NotificationsScreen() {
  const t = useTranslations("Notifications");
  const queryClient = useQueryClient();

  const [filter, setFilter] = useState<FilterKey>("all");
  const unreadCount = useGetUnreadNotificationCount<number, ApiError>();
  const notifications = useGetMyNotifications<NotificationSummary[], ApiError>({ filter: FILTER_VALUES[filter] });
  const markRead = useMarkNotificationRead<ApiError>();
  const markAllRead = useMarkAllNotificationsRead<ApiError>();
  const push = usePushSubscription();

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: getGetUnreadNotificationCountQueryKey() });
    notifications.refetch();
  };

  const openNotification = (notification: NotificationSummary) => {
    if (!notification.readAt) {
      markRead.mutate({ id: notification.id }, { onSuccess: invalidate });
    }
    if (notification.link) {
      window.location.href = notification.link;
    }
  };

  const groups = (notifications.data ?? []).reduce<{ today: NotificationSummary[]; earlier: NotificationSummary[] }>(
    (acc, notification) => {
      acc[notificationDayGroup(notification.createdAt)].push(notification);
      return acc;
    },
    { today: [], earlier: [] },
  );

  return (
    <div className="mx-auto flex max-w-2xl flex-col">
      <div className="flex flex-col gap-3 border-b border-line bg-surface px-4 py-4 sm:px-6">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-bold text-ink">{t("title")}</h1>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={markAllRead.isPending || (unreadCount.data ?? 0) === 0}
            onClick={() => markAllRead.mutate(undefined, { onSuccess: invalidate })}
          >
            {t("markAllRead")}
          </Button>
        </div>

        <div className="inline-flex gap-0.5 rounded-lg bg-ground p-0.5" role="tablist" aria-label={t("tabsLabel")}>
          <FilterTab active={filter === "all"} onClick={() => setFilter("all")} label={t("tabAll")} />
          <FilterTab
            active={filter === "unread"}
            onClick={() => setFilter("unread")}
            label={t("tabUnread", { count: unreadCount.data ?? 0 })}
          />
          <FilterTab active={filter === "deadlines"} onClick={() => setFilter("deadlines")} label={t("tabDeadlines")} />
        </div>

        {push.state === "unsubscribed" ? (
          <div className="flex items-center justify-between gap-3 rounded-md bg-mint px-3 py-2">
            <p className="text-sm text-green-700">{t("pushBannerText")}</p>
            <Button type="button" size="sm" disabled={push.isSubscribing} onClick={() => push.subscribe()}>
              {t("pushBannerAction")}
            </Button>
          </div>
        ) : null}
      </div>

      <div className="flex-1">
        {notifications.isPending ? (
          <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>
        ) : notifications.isError ? (
          <div className="p-6">
            <ErrorState title={t("loadErrorTitle")} onRetry={() => notifications.refetch()} />
          </div>
        ) : (notifications.data ?? []).length === 0 ? (
          <div className="p-6">
            <EmptyState icon={Bell} title={t("emptyTitle")} description={t("emptyDescription")} />
          </div>
        ) : (
          <>
            {groups.today.length > 0 ? (
              <NotificationGroup label={t("groupToday")} items={groups.today} onOpen={openNotification} />
            ) : null}
            {groups.earlier.length > 0 ? (
              <NotificationGroup label={t("groupEarlier")} items={groups.earlier} onOpen={openNotification} />
            ) : null}
          </>
        )}
      </div>
    </div>
  );
}

function FilterTab({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        "min-h-9 flex-1 rounded-md px-3 text-sm font-semibold transition-colors",
        active ? "bg-surface text-ink shadow-sm" : "text-ink-muted hover:bg-surface/60",
      )}
    >
      {label}
    </button>
  );
}

function NotificationGroup({
  label,
  items,
  onOpen,
}: {
  label: string;
  items: NotificationSummary[];
  onOpen: (notification: NotificationSummary) => void;
}) {
  return (
    <div>
      <p className="px-4 py-2 text-xs font-bold uppercase tracking-wide text-ink-muted sm:px-6">{label}</p>
      <ul className="flex flex-col">
        {items.map((notification) => {
          const style = notificationEventTypeStyle(notification.eventType);
          const Icon = ICONS[style.icon];
          const isUnread = !notification.readAt;

          return (
            <li key={notification.id} className="border-b border-line last:border-0">
              <button
                type="button"
                onClick={() => onOpen(notification)}
                className={cn("flex w-full items-start gap-3 px-4 py-3.5 text-left hover:bg-ground sm:px-6", isUnread && "bg-ground/40")}
              >
                <span
                  className="flex size-10 shrink-0 items-center justify-center rounded-xl"
                  style={{ backgroundColor: style.background, color: style.foreground }}
                >
                  <Icon className="size-[19px]" aria-hidden="true" />
                </span>
                <div className="flex-1">
                  <p className={cn("text-sm leading-snug", isUnread ? "font-bold text-ink" : "font-medium text-ink")}>
                    {notification.subject}
                  </p>
                  <p className="mt-0.5 text-xs font-semibold text-ink-muted">{formatNotificationTimestamp(notification.createdAt)}</p>
                </div>
                {isUnread ? <span className="mt-1.5 size-2.5 shrink-0 rounded-full bg-green-600" aria-hidden="true" /> : null}
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
