import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { AnnouncementsScreen } from "./AnnouncementsScreen";

const useGetNotificationTemplates = vi.fn();
const useUpdateNotificationTemplate = vi.fn();
const useGetAnnouncementAudienceCounts = vi.fn();
const useGetAnnouncements = vi.fn();
const useSendAnnouncement = vi.fn();

vi.mock("@/lib/api/generated/notifications/notifications", () => ({
  useGetNotificationTemplates: (...args: unknown[]) => useGetNotificationTemplates(...args),
  useUpdateNotificationTemplate: (...args: unknown[]) => useUpdateNotificationTemplate(...args),
  useGetAnnouncementAudienceCounts: (...args: unknown[]) => useGetAnnouncementAudienceCounts(...args),
  useGetAnnouncements: (...args: unknown[]) => useGetAnnouncements(...args),
  useSendAnnouncement: (...args: unknown[]) => useSendAnnouncement(...args),
  getGetAnnouncementsQueryKey: () => ["announcements"],
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const TEMPLATES = [
  { id: "t1", eventType: 2, subject: "Overdue: {{unitName}}", body: "Hi {{headName}}, overdue.", updatedAt: "2026-01-01T00:00:00Z" },
  { id: "t2", eventType: 0, subject: "Opened: {{unitName}}", body: "Hi {{headName}}, opened.", updatedAt: "2026-01-01T00:00:00Z" },
];

const SENT_ANNOUNCEMENTS = [
  {
    id: "a1",
    title: "September reports are open",
    message: "Please submit by the 5th.",
    audience: 0,
    showAsBanner: true,
    sendEmail: true,
    deliveredCount: 214,
    createdAt: "2026-10-01T00:00:00Z",
  },
];

let updateTemplateMutate: ReturnType<typeof vi.fn>;
let sendAnnouncementMutate: ReturnType<typeof vi.fn>;

async function switchToEmailWordingTab() {
  await userEvent.click(screen.getByRole("tab", { name: /email wording/i }));
}

beforeEach(() => {
  useGetNotificationTemplates.mockReset();
  useUpdateNotificationTemplate.mockReset();
  useGetAnnouncementAudienceCounts.mockReset();
  useGetAnnouncements.mockReset();
  useSendAnnouncement.mockReset();

  updateTemplateMutate = vi.fn();
  sendAnnouncementMutate = vi.fn();
  useUpdateNotificationTemplate.mockReturnValue({ mutate: updateTemplateMutate, isPending: false, error: null });
  useSendAnnouncement.mockReturnValue({ mutate: sendAnnouncementMutate, isPending: false, error: null });
  useGetAnnouncementAudienceCounts.mockReturnValue({ data: [{ audience: 0, recipientCount: 214 }, { audience: 3, recipientCount: 180 }] });
  useGetAnnouncements.mockReturnValue({ isPending: false, isError: false, data: SENT_ANNOUNCEMENTS, refetch: vi.fn() });
  useGetNotificationTemplates.mockReturnValue({ isPending: false, isError: false, data: TEMPLATES });
});

describe("AnnouncementsScreen — Announcements tab (default)", () => {
  it("shows the sent announcements history", () => {
    renderWithProviders(<AnnouncementsScreen />);

    expect(screen.getByText("September reports are open")).toBeInTheDocument();
    expect(screen.getByText(/214 delivered/i)).toBeInTheDocument();
  });

  it("shows an empty state when nothing has been sent yet", () => {
    useGetAnnouncements.mockReturnValue({ isPending: false, isError: false, data: [], refetch: vi.fn() });

    renderWithProviders(<AnnouncementsScreen />);

    expect(screen.getByText(/nothing here yet/i)).toBeInTheDocument();
  });

  it("sends an announcement with the composed fields and resets the form on success", async () => {
    sendAnnouncementMutate.mockImplementation((_vars, options) => options?.onSuccess?.());
    renderWithProviders(<AnnouncementsScreen />);

    await userEvent.type(screen.getByLabelText("Title"), "Youth Camp form is now open");
    await userEvent.type(screen.getByLabelText("Message"), "Submit attendance by Friday.");
    await userEvent.click(screen.getByRole("button", { name: /send announcement/i }));

    expect(sendAnnouncementMutate).toHaveBeenCalledWith(
      {
        data: {
          title: "Youth Camp form is now open",
          message: "Submit attendance by Friday.",
          audience: 0,
          showAsBanner: true,
          sendEmail: true,
        },
      },
      expect.anything(),
    );
    expect(screen.getByLabelText("Title")).toHaveValue("");
  });

  it("send announcement is disabled until both title and message are filled in", async () => {
    renderWithProviders(<AnnouncementsScreen />);

    expect(screen.getByRole("button", { name: /send announcement/i })).toBeDisabled();

    await userEvent.type(screen.getByLabelText("Title"), "Title only");
    expect(screen.getByRole("button", { name: /send announcement/i })).toBeDisabled();
  });

  it("unchecking Show as Email sends sendEmail: false", async () => {
    renderWithProviders(<AnnouncementsScreen />);

    await userEvent.type(screen.getByLabelText("Title"), "Title");
    await userEvent.type(screen.getByLabelText("Message"), "Message");
    await userEvent.click(screen.getByRole("checkbox", { name: /^email$/i }));
    await userEvent.click(screen.getByRole("button", { name: /send announcement/i }));

    expect(sendAnnouncementMutate).toHaveBeenCalledWith(
      expect.objectContaining({ data: expect.objectContaining({ sendEmail: false }) }),
      expect.anything(),
    );
  });
});

describe("AnnouncementsScreen — Email wording tab", () => {
  it("shows a loading message while templates load", async () => {
    useGetNotificationTemplates.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<AnnouncementsScreen />);
    await switchToEmailWordingTab();

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetNotificationTemplates.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<AnnouncementsScreen />);
    await switchToEmailWordingTab();
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("lists templates in FR-NOT-002's event order, not the data's own order, and auto-selects the first", async () => {
    renderWithProviders(<AnnouncementsScreen />);
    await switchToEmailWordingTab();

    const buttons = screen.getAllByRole("button").map((b) => b.textContent);
    expect(buttons.indexOf("Window opened")).toBeLessThan(buttons.indexOf("Overdue"));
    expect(screen.getByDisplayValue("Opened: {{unitName}}")).toBeInTheDocument();
  });

  it("selecting a different template loads its own subject and body", async () => {
    renderWithProviders(<AnnouncementsScreen />);
    await switchToEmailWordingTab();
    await userEvent.click(screen.getByRole("button", { name: "Overdue" }));

    expect(screen.getByDisplayValue("Overdue: {{unitName}}")).toBeInTheDocument();
  });

  it("saving calls the update mutation with the edited subject and body", async () => {
    renderWithProviders(<AnnouncementsScreen />);
    await switchToEmailWordingTab();
    const bodyField = screen.getByLabelText("Body");
    await userEvent.clear(bodyField);
    await userEvent.type(bodyField, "New body text.");
    await userEvent.click(screen.getByRole("button", { name: /^save$/i }));

    expect(updateTemplateMutate).toHaveBeenCalledWith(
      { id: "t2", data: { subject: "Opened: {{unitName}}", body: "New body text." } },
      expect.anything(),
    );
  });
});
