import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { AnnouncementBanner } from "./AnnouncementBanner";

const useGetMyBannerNotification = vi.fn();
const useMarkNotificationRead = vi.fn();

vi.mock("@/lib/api/generated/notifications/notifications", () => ({
  useGetMyBannerNotification: (...args: unknown[]) => useGetMyBannerNotification(...args),
  useMarkNotificationRead: (...args: unknown[]) => useMarkNotificationRead(...args),
  getGetMyBannerNotificationQueryKey: () => ["banner"],
  getGetUnreadNotificationCountQueryKey: () => ["unread-count"],
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

let markReadMutate: ReturnType<typeof vi.fn>;

beforeEach(() => {
  useGetMyBannerNotification.mockReset();
  useMarkNotificationRead.mockReset();
  markReadMutate = vi.fn();
  useMarkNotificationRead.mockReturnValue({ mutate: markReadMutate, isPending: false });
});

describe("AnnouncementBanner", () => {
  it("renders nothing when there is no active banner", () => {
    useGetMyBannerNotification.mockReturnValue({ data: null });

    const { container } = renderWithProviders(<AnnouncementBanner />);

    expect(container).toBeEmptyDOMElement();
  });

  it("shows the banner's subject and body when one exists", () => {
    useGetMyBannerNotification.mockReturnValue({
      data: { id: "n1", subject: "September reports are open", body: "Please submit by the 5th.", link: null },
    });

    renderWithProviders(<AnnouncementBanner />);

    expect(screen.getByText("September reports are open")).toBeInTheDocument();
    expect(screen.getByText("Please submit by the 5th.")).toBeInTheDocument();
  });

  it("dismissing the banner marks the notification read", async () => {
    useGetMyBannerNotification.mockReturnValue({
      data: { id: "n1", subject: "Title", body: "Body", link: null },
    });

    renderWithProviders(<AnnouncementBanner />);
    await userEvent.click(screen.getByRole("button", { name: /dismiss announcement/i }));

    expect(markReadMutate).toHaveBeenCalledWith({ id: "n1" }, expect.anything());
  });
});
