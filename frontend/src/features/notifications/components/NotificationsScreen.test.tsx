import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { NotificationsScreen } from "./NotificationsScreen";

const useGetMyNotifications = vi.fn();
const useGetUnreadNotificationCount = vi.fn();
const useMarkNotificationRead = vi.fn();
const useMarkAllNotificationsRead = vi.fn();
const usePushSubscription = vi.fn();

vi.mock("@/lib/api/generated/notifications/notifications", () => ({
  useGetMyNotifications: (...args: unknown[]) => useGetMyNotifications(...args),
  useGetUnreadNotificationCount: (...args: unknown[]) => useGetUnreadNotificationCount(...args),
  useMarkNotificationRead: (...args: unknown[]) => useMarkNotificationRead(...args),
  useMarkAllNotificationsRead: (...args: unknown[]) => useMarkAllNotificationsRead(...args),
  getGetUnreadNotificationCountQueryKey: () => ["unread-count"],
}));

vi.mock("@/features/notifications/hooks/usePushSubscription", () => ({
  usePushSubscription: (...args: unknown[]) => usePushSubscription(...args),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const now = new Date();
const TODAY_ISO = now.toISOString();
const OLD_ISO = new Date(now.getTime() - 1000 * 60 * 60 * 24 * 10).toISOString();

const NOTIFICATIONS = [
  { id: "n1", eventType: 2, subject: "Overdue notice", body: "b", link: "/reports/1/edit", createdAt: TODAY_ISO, readAt: null },
  { id: "n2", eventType: 4, subject: "Acknowledged notice", body: "b", link: null, createdAt: OLD_ISO, readAt: OLD_ISO },
];

let markReadMutate: ReturnType<typeof vi.fn>;
let markAllReadMutate: ReturnType<typeof vi.fn>;

beforeEach(() => {
  useGetMyNotifications.mockReset();
  useGetUnreadNotificationCount.mockReset();
  useMarkNotificationRead.mockReset();
  useMarkAllNotificationsRead.mockReset();
  usePushSubscription.mockReset();

  markReadMutate = vi.fn();
  markAllReadMutate = vi.fn();
  useMarkNotificationRead.mockReturnValue({ mutate: markReadMutate, isPending: false });
  useMarkAllNotificationsRead.mockReturnValue({ mutate: markAllReadMutate, isPending: false });
  useGetUnreadNotificationCount.mockReturnValue({ data: 1 });
  usePushSubscription.mockReturnValue({ state: "unsubscribed", subscribe: vi.fn(), unsubscribe: vi.fn(), isSubscribing: false });
});

describe("NotificationsScreen", () => {
  it("shows a loading message while notifications load", () => {
    useGetMyNotifications.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<NotificationsScreen />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<NotificationsScreen />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("shows an empty state when there are no notifications", () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: [] });

    renderWithProviders(<NotificationsScreen />);

    expect(screen.getByText(/nothing here yet/i)).toBeInTheDocument();
  });

  it("groups notifications into Today and Earlier, newest filter first by default", () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });

    renderWithProviders(<NotificationsScreen />);

    expect(screen.getByText("Today")).toBeInTheDocument();
    expect(screen.getByText("Earlier")).toBeInTheDocument();
    expect(screen.getByText("Overdue notice")).toBeInTheDocument();
    expect(screen.getByText("Acknowledged notice")).toBeInTheDocument();
    expect(useGetMyNotifications).toHaveBeenCalledWith({ filter: 0 });
  });

  it("switching to the Unread tab re-queries with the Unread filter", async () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });

    renderWithProviders(<NotificationsScreen />);
    await userEvent.click(screen.getByRole("tab", { name: /unread/i }));

    expect(useGetMyNotifications).toHaveBeenLastCalledWith({ filter: 1 });
  });

  it("switching to the Deadlines tab re-queries with the Deadlines filter", async () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });

    renderWithProviders(<NotificationsScreen />);
    await userEvent.click(screen.getByRole("tab", { name: /deadlines/i }));

    expect(useGetMyNotifications).toHaveBeenLastCalledWith({ filter: 2 });
  });

  it("clicking an unread notification marks it read", async () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });

    renderWithProviders(<NotificationsScreen />);
    await userEvent.click(screen.getByText("Overdue notice"));

    expect(markReadMutate).toHaveBeenCalledWith({ id: "n1" }, expect.anything());
  });

  it("clicking an already-read notification does not call mark-read", async () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });

    renderWithProviders(<NotificationsScreen />);
    await userEvent.click(screen.getByText("Acknowledged notice"));

    expect(markReadMutate).not.toHaveBeenCalled();
  });

  it("mark all read is disabled when the unread count is zero", () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });
    useGetUnreadNotificationCount.mockReturnValue({ data: 0 });

    renderWithProviders(<NotificationsScreen />);

    expect(screen.getByRole("button", { name: /mark all read/i })).toBeDisabled();
  });

  it("clicking mark all read calls the mutation", async () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });

    renderWithProviders(<NotificationsScreen />);
    await userEvent.click(screen.getByRole("button", { name: /mark all read/i }));

    expect(markAllReadMutate).toHaveBeenCalled();
  });

  it("shows the push opt-in banner only when unsubscribed", () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });

    renderWithProviders(<NotificationsScreen />);
    expect(screen.getByRole("button", { name: /turn on/i })).toBeInTheDocument();
  });

  it("hides the push opt-in banner once subscribed", () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });
    usePushSubscription.mockReturnValue({ state: "subscribed", subscribe: vi.fn(), unsubscribe: vi.fn(), isSubscribing: false });

    renderWithProviders(<NotificationsScreen />);

    expect(screen.queryByRole("button", { name: /turn on/i })).not.toBeInTheDocument();
  });

  it("clicking the push banner's action calls subscribe", async () => {
    useGetMyNotifications.mockReturnValue({ isPending: false, isError: false, data: NOTIFICATIONS });
    const subscribe = vi.fn();
    usePushSubscription.mockReturnValue({ state: "unsubscribed", subscribe, unsubscribe: vi.fn(), isSubscribing: false });

    renderWithProviders(<NotificationsScreen />);
    await userEvent.click(screen.getByRole("button", { name: /turn on/i }));

    expect(subscribe).toHaveBeenCalled();
  });
});
