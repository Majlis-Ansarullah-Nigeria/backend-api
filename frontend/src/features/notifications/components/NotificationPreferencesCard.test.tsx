import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { NotificationPreferencesCard } from "./NotificationPreferencesCard";

const useGetMyNotificationPreferences = vi.fn();
const useGetMyNotificationSettings = vi.fn();
const useUpdateNotificationPreference = vi.fn();
const useUpdateNotificationSettings = vi.fn();

vi.mock("@/lib/api/generated/notifications/notifications", () => ({
  useGetMyNotificationPreferences: (...args: unknown[]) => useGetMyNotificationPreferences(...args),
  useGetMyNotificationSettings: (...args: unknown[]) => useGetMyNotificationSettings(...args),
  useUpdateNotificationPreference: (...args: unknown[]) => useUpdateNotificationPreference(...args),
  useUpdateNotificationSettings: (...args: unknown[]) => useUpdateNotificationSettings(...args),
  getGetMyNotificationPreferencesQueryKey: () => ["preferences"],
  getGetMyNotificationSettingsQueryKey: () => ["settings"],
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const PREFERENCES = [
  { category: 0, inAppEnabled: true, emailEnabled: true },
  { category: 1, inAppEnabled: true, emailEnabled: true },
  { category: 2, inAppEnabled: true, emailEnabled: true },
  { category: 3, inAppEnabled: true, emailEnabled: false },
  { category: 4, inAppEnabled: true, emailEnabled: true },
];

let updatePreferenceMutate: ReturnType<typeof vi.fn>;
let updateSettingsMutate: ReturnType<typeof vi.fn>;

beforeEach(() => {
  useGetMyNotificationPreferences.mockReset();
  useGetMyNotificationSettings.mockReset();
  useUpdateNotificationPreference.mockReset();
  useUpdateNotificationSettings.mockReset();

  updatePreferenceMutate = vi.fn();
  updateSettingsMutate = vi.fn();
  useUpdateNotificationPreference.mockReturnValue({ mutate: updatePreferenceMutate, isPending: false });
  useUpdateNotificationSettings.mockReturnValue({ mutate: updateSettingsMutate, isPending: false });
  useGetMyNotificationSettings.mockReturnValue({ isPending: false, isError: false, data: { digestEnabled: false }, refetch: vi.fn() });
});

describe("NotificationPreferencesCard", () => {
  it("shows a loading message while preferences load", () => {
    useGetMyNotificationPreferences.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<NotificationPreferencesCard />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetMyNotificationPreferences.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<NotificationPreferencesCard />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("lists all 5 categories in the mockup's order", () => {
    useGetMyNotificationPreferences.mockReturnValue({ isPending: false, isError: false, data: PREFERENCES });

    renderWithProviders(<NotificationPreferencesCard />);

    const labels = screen.getAllByText(/Window opened|Deadline reminders|Report returned or acknowledged|Comments on my reports|Announcements/);
    expect(labels.map((el) => el.textContent)).toEqual([
      "Window opened",
      "Deadline reminders",
      "Report returned or acknowledged",
      "Comments on my reports",
      "Announcements",
    ]);
  });

  it("toggling a switch sends both channel values for that category", async () => {
    useGetMyNotificationPreferences.mockReturnValue({ isPending: false, isError: false, data: PREFERENCES });

    renderWithProviders(<NotificationPreferencesCard />);
    const emailSwitches = screen.getAllByRole("switch", { name: "Email" });
    // Category 3 ("Comments on my reports") is the 4th row, email currently off.
    await userEvent.click(emailSwitches[3]!);

    expect(updatePreferenceMutate).toHaveBeenCalledWith(
      { category: 3, data: { inAppEnabled: true, emailEnabled: true } },
      expect.anything(),
    );
  });

  it("toggling the digest switch updates the setting", async () => {
    useGetMyNotificationPreferences.mockReturnValue({ isPending: false, isError: false, data: PREFERENCES });

    renderWithProviders(<NotificationPreferencesCard />);
    await userEvent.click(screen.getByRole("switch", { name: /daily digest/i }));

    expect(updateSettingsMutate).toHaveBeenCalledWith({ data: { digestEnabled: true } }, expect.anything());
  });
});
