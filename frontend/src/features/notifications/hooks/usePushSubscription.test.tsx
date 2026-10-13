import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { usePushSubscription } from "./usePushSubscription";

const useGetVapidPublicKey = vi.fn();
const useSavePushSubscription = vi.fn();
const useDeletePushSubscription = vi.fn();

vi.mock("@/lib/api/generated/notifications/notifications", () => ({
  useGetVapidPublicKey: (...args: unknown[]) => useGetVapidPublicKey(...args),
  useSavePushSubscription: (...args: unknown[]) => useSavePushSubscription(...args),
  useDeletePushSubscription: (...args: unknown[]) => useDeletePushSubscription(...args),
}));

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient();
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

let saveMutate: ReturnType<typeof vi.fn>;
let deleteMutate: ReturnType<typeof vi.fn>;
let originalServiceWorker: unknown;
let originalNotification: unknown;

beforeEach(() => {
  useGetVapidPublicKey.mockReset();
  useSavePushSubscription.mockReset();
  useDeletePushSubscription.mockReset();

  saveMutate = vi.fn((_vars, options) => options?.onSuccess?.());
  deleteMutate = vi.fn();
  useGetVapidPublicKey.mockReturnValue({ data: "QUJD" }); // base64url("ABC")
  useSavePushSubscription.mockReturnValue({ mutate: saveMutate, isPending: false });
  useDeletePushSubscription.mockReturnValue({ mutate: deleteMutate, isPending: false });

  originalServiceWorker = Object.getOwnPropertyDescriptor(window.navigator, "serviceWorker");
  originalNotification = (window as unknown as { Notification?: unknown }).Notification;
});

afterEach(() => {
  if (originalServiceWorker) {
    Object.defineProperty(window.navigator, "serviceWorker", originalServiceWorker as PropertyDescriptor);
  }
  (window as unknown as { Notification?: unknown }).Notification = originalNotification;
  vi.unstubAllGlobals();
});

function stubServiceWorker(overrides: Partial<ServiceWorkerContainer> = {}) {
  Object.defineProperty(window.navigator, "serviceWorker", {
    configurable: true,
    value: {
      getRegistration: vi.fn().mockResolvedValue(undefined),
      register: vi.fn(),
      ...overrides,
    },
  });
}

function stubNotification(permission: NotificationPermission, requestPermission = vi.fn()) {
  (window as unknown as { Notification: unknown }).Notification = { permission, requestPermission };
  vi.stubGlobal("Notification", { permission, requestPermission });
}

describe("usePushSubscription", () => {
  it("reports unsupported when the browser has no PushManager", async () => {
    Object.defineProperty(window.navigator, "serviceWorker", { configurable: true, value: undefined });
    vi.stubGlobal("PushManager", undefined);

    const { result } = renderHook(() => usePushSubscription(), { wrapper });

    await waitFor(() => expect(result.current.state).toBe("unsupported"));
  });

  it("reports denied when Notification permission was already denied", async () => {
    vi.stubGlobal("PushManager", class {});
    stubServiceWorker();
    stubNotification("denied");

    const { result } = renderHook(() => usePushSubscription(), { wrapper });

    await waitFor(() => expect(result.current.state).toBe("denied"));
  });

  it("reports subscribed when an active subscription already exists", async () => {
    vi.stubGlobal("PushManager", class {});
    stubServiceWorker({
      getRegistration: vi.fn().mockResolvedValue({
        pushManager: { getSubscription: vi.fn().mockResolvedValue({ endpoint: "https://push.example/abc" }) },
      }),
    });
    stubNotification("default");

    const { result } = renderHook(() => usePushSubscription(), { wrapper });

    await waitFor(() => expect(result.current.state).toBe("subscribed"));
  });

  it("reports unsubscribed when no active subscription exists", async () => {
    vi.stubGlobal("PushManager", class {});
    stubServiceWorker({
      getRegistration: vi.fn().mockResolvedValue({ pushManager: { getSubscription: vi.fn().mockResolvedValue(undefined) } }),
    });
    stubNotification("default");

    const { result } = renderHook(() => usePushSubscription(), { wrapper });

    await waitFor(() => expect(result.current.state).toBe("unsubscribed"));
  });

  it("subscribe() requests permission, subscribes, and saves the subscription", async () => {
    vi.stubGlobal("PushManager", class {});
    const subscribe = vi.fn().mockResolvedValue({
      toJSON: () => ({ endpoint: "https://push.example/xyz", keys: { p256dh: "p256dh-key", auth: "auth-key" } }),
    });
    stubServiceWorker({
      getRegistration: vi.fn().mockResolvedValue(undefined),
      register: vi.fn().mockResolvedValue({ pushManager: { subscribe } }),
    });
    stubNotification("default", vi.fn().mockResolvedValue("granted"));

    const { result } = renderHook(() => usePushSubscription(), { wrapper });
    await waitFor(() => expect(result.current.state).toBe("unsubscribed"));

    await result.current.subscribe();

    expect(subscribe).toHaveBeenCalledWith(expect.objectContaining({ userVisibleOnly: true }));
    expect(saveMutate).toHaveBeenCalledWith(
      { data: { endpoint: "https://push.example/xyz", p256Dh: "p256dh-key", auth: "auth-key" } },
      expect.anything(),
    );
    await waitFor(() => expect(result.current.state).toBe("subscribed"));
  });

  it("subscribe() stops at denied permission without subscribing", async () => {
    vi.stubGlobal("PushManager", class {});
    const subscribe = vi.fn();
    stubServiceWorker({
      getRegistration: vi.fn().mockResolvedValue(undefined),
      register: vi.fn().mockResolvedValue({ pushManager: { subscribe } }),
    });
    stubNotification("default", vi.fn().mockResolvedValue("denied"));

    const { result } = renderHook(() => usePushSubscription(), { wrapper });
    await waitFor(() => expect(result.current.state).toBe("unsubscribed"));

    await result.current.subscribe();

    expect(subscribe).not.toHaveBeenCalled();
    await waitFor(() => expect(result.current.state).toBe("denied"));
  });

  it("unsubscribe() removes the existing subscription and deletes it server-side", async () => {
    vi.stubGlobal("PushManager", class {});
    const unsubscribe = vi.fn().mockResolvedValue(true);
    stubServiceWorker({
      getRegistration: vi.fn().mockResolvedValue({
        pushManager: { getSubscription: vi.fn().mockResolvedValue({ endpoint: "https://push.example/abc", unsubscribe }) },
      }),
    });
    stubNotification("default");

    const { result } = renderHook(() => usePushSubscription(), { wrapper });
    await waitFor(() => expect(result.current.state).toBe("subscribed"));

    await result.current.unsubscribe();

    expect(unsubscribe).toHaveBeenCalled();
    expect(deleteMutate).toHaveBeenCalledWith({ params: { endpoint: "https://push.example/abc" } });
    await waitFor(() => expect(result.current.state).toBe("unsubscribed"));
  });
});
