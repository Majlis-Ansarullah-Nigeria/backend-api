"use client";

import { useCallback, useEffect, useState } from "react";
import type { ApiError } from "@/lib/api/client";
import {
  useDeletePushSubscription,
  useGetVapidPublicKey,
  useSavePushSubscription,
} from "@/lib/api/generated/notifications/notifications";

export type PushSupportState = "checking" | "unsupported" | "denied" | "subscribed" | "unsubscribed";

function urlBase64ToUint8Array(base64: string): Uint8Array<ArrayBuffer> {
  const padding = "=".repeat((4 - (base64.length % 4)) % 4);
  const normalized = `${base64}${padding}`.replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(normalized);

  const output = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) {
    output[i] = raw.charCodeAt(i);
  }
  return output;
}

function isPushSupported(): boolean {
  return typeof window !== "undefined" && "serviceWorker" in navigator && "PushManager" in window;
}

/**
 * FR-NOT-001's web push opt-in. A device-level action, not a preference — the per-event/channel
 * preference matrix the Profile mockup shows (In-app/Email columns, no Push column) is a
 * separate, M8.3 concern (FR-NOT-004). The subscribe/unsubscribe round trip can't be verified in
 * this environment: it needs a real browser granting Notification permission and a live push
 * service, so this hook is covered here only down to its branching logic, with
 * navigator.serviceWorker/PushManager mocked.
 */
export function usePushSubscription() {
  const [state, setState] = useState<PushSupportState>("checking");
  const vapidKey = useGetVapidPublicKey<string, ApiError>();
  const saveSubscription = useSavePushSubscription<ApiError>();
  const deleteSubscription = useDeletePushSubscription();

  // Pure — never calls setState itself, so the effect below is the only place that does,
  // always after this function's own await, never synchronously during the effect body.
  const computeState = useCallback(async (): Promise<PushSupportState> => {
    if (!isPushSupported()) {
      return "unsupported";
    }
    if (Notification.permission === "denied") {
      return "denied";
    }
    const registration = await navigator.serviceWorker.getRegistration("/sw.js");
    const existing = await registration?.pushManager.getSubscription();
    return existing ? "subscribed" : "unsubscribed";
  }, []);

  useEffect(() => {
    let ignore = false;

    computeState()
      .then((next) => {
        if (!ignore) {
          setState(next);
        }
      })
      .catch(() => {
        if (!ignore) {
          setState("unsupported");
        }
      });

    return () => {
      ignore = true;
    };
  }, [computeState]);

  const subscribe = useCallback(async () => {
    if (!vapidKey.data) {
      return;
    }

    const permission = await Notification.requestPermission();
    if (permission !== "granted") {
      setState("denied");
      return;
    }

    const registration = await navigator.serviceWorker.register("/sw.js");
    const subscription = await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(vapidKey.data),
    });
    const json = subscription.toJSON();

    await new Promise<void>((resolve, reject) => {
      saveSubscription.mutate(
        { data: { endpoint: json.endpoint ?? "", p256Dh: json.keys?.p256dh ?? "", auth: json.keys?.auth ?? "" } },
        { onSuccess: () => resolve(), onError: (error) => reject(error as Error) },
      );
    });

    setState("subscribed");
  }, [vapidKey.data, saveSubscription]);

  const unsubscribe = useCallback(async () => {
    const registration = await navigator.serviceWorker.getRegistration("/sw.js");
    const subscription = await registration?.pushManager.getSubscription();

    if (subscription) {
      const endpoint = subscription.endpoint;
      await subscription.unsubscribe();
      deleteSubscription.mutate({ params: { endpoint } });
    }

    setState("unsubscribed");
  }, [deleteSubscription]);

  return { state, subscribe, unsubscribe, isSubscribing: saveSubscription.isPending };
}
