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

vi.mock("@/lib/api/generated/notifications/notifications", () => ({
  useGetNotificationTemplates: (...args: unknown[]) => useGetNotificationTemplates(...args),
  useUpdateNotificationTemplate: (...args: unknown[]) => useUpdateNotificationTemplate(...args),
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

let updateMutate: ReturnType<typeof vi.fn>;

beforeEach(() => {
  useGetNotificationTemplates.mockReset();
  useUpdateNotificationTemplate.mockReset();
  updateMutate = vi.fn();
  useUpdateNotificationTemplate.mockReturnValue({ mutate: updateMutate, isPending: false, error: null });
});

describe("AnnouncementsScreen", () => {
  it("shows a loading message while templates load", () => {
    useGetNotificationTemplates.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<AnnouncementsScreen />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetNotificationTemplates.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<AnnouncementsScreen />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("lists templates in FR-NOT-002's event order, not the data's own order, and auto-selects the first", () => {
    useGetNotificationTemplates.mockReturnValue({ isPending: false, isError: false, data: TEMPLATES });

    renderWithProviders(<AnnouncementsScreen />);

    const buttons = screen.getAllByRole("button").map((b) => b.textContent);
    expect(buttons.indexOf("Window opened")).toBeLessThan(buttons.indexOf("Overdue"));
    expect(screen.getByDisplayValue("Opened: {{unitName}}")).toBeInTheDocument();
  });

  it("selecting a different template loads its own subject and body", async () => {
    useGetNotificationTemplates.mockReturnValue({ isPending: false, isError: false, data: TEMPLATES });

    renderWithProviders(<AnnouncementsScreen />);
    await userEvent.click(screen.getByRole("button", { name: "Overdue" }));

    expect(screen.getByDisplayValue("Overdue: {{unitName}}")).toBeInTheDocument();
  });

  it("saving calls the update mutation with the edited subject and body", async () => {
    useGetNotificationTemplates.mockReturnValue({ isPending: false, isError: false, data: TEMPLATES });

    renderWithProviders(<AnnouncementsScreen />);
    const bodyField = screen.getByLabelText("Body");
    await userEvent.clear(bodyField);
    await userEvent.type(bodyField, "New body text.");
    await userEvent.click(screen.getByRole("button", { name: /^save$/i }));

    expect(updateMutate).toHaveBeenCalledWith(
      { id: "t2", data: { subject: "Opened: {{unitName}}", body: "New body text." } },
      expect.anything(),
    );
  });
});
