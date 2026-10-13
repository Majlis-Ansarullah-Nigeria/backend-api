"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import type { ApiError } from "@/lib/api/client";
import {
  getGetUnreadNotificationCountQueryKey,
  useGetUnreadNotificationCount,
} from "@/lib/api/generated/notifications/notifications";

/**
 * FR-NOT-001's live badge. The hub connection carries no payload of its own — it's a Redis
 * backplane signal (the outbox dispatcher runs in the Worker, which never serves the connection
 * itself; see architecture.md's "/hubs/*" routing and HRS.Infrastructure/Notifications) that just
 * tells this tab "re-ask the API", keeping the realtime channel a thin nudge over the one real
 * source of truth.
 */
export function useNotificationBadge(): number {
  const queryClient = useQueryClient();
  const query = useGetUnreadNotificationCount<number, ApiError>();

  useEffect(() => {
    let cancelled = false;
    let connection: import("@microsoft/signalr").HubConnection | undefined;

    // Dynamically imported so @microsoft/signalr (~150 KB unminified) never lands in every
    // page's first-load bundle via AppShell — it loads as its own chunk once the app is
    // interactive, well after the budget in frontend/CLAUDE.md is measured.
    import("@microsoft/signalr").then(({ HubConnectionBuilder, LogLevel }) => {
      if (cancelled) {
        return;
      }

      connection = new HubConnectionBuilder()
        .withUrl("/hubs/notifications", { withCredentials: true })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on("unreadCountChanged", () => {
        queryClient.invalidateQueries({ queryKey: getGetUnreadNotificationCountQueryKey() });
      });

      connection.start().catch(() => {
        // Best-effort: the badge still shows the initial fetched count; a connection failure
        // (offline, proxy hiccup) just means it won't update live until the next remount/refetch.
      });
    });

    return () => {
      cancelled = true;
      connection?.stop().catch(() => {});
    };
  }, [queryClient]);

  return query.data ?? 0;
}
