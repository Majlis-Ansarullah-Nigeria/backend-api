import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { formatBytes } from "./brand";
import { SettingsScreen } from "./SettingsScreen";

const useGetOrganizationSettings = vi.fn();
const useGetSystemHealth = vi.fn();
const updateMutate = vi.fn();
const uploadMutate = vi.fn();
const retryMutate = vi.fn();

vi.mock("@/lib/api/generated/settings/settings", () => ({
  useGetOrganizationSettings: (...args: unknown[]) => useGetOrganizationSettings(...args),
  useGetSystemHealth: (...args: unknown[]) => useGetSystemHealth(...args),
  useGetTimeZoneOptions: () => ({
    data: [
      { id: "Africa/Lagos", label: "Africa/Lagos (UTC+1)" },
      { id: "Europe/London", label: "Europe/London (UTC)" },
    ],
  }),
  useUpdateOrganizationSettings: () => ({ mutate: updateMutate, isPending: false }),
  useUploadLogo: () => ({ mutate: uploadMutate, isPending: false }),
  useRemoveLogo: () => ({ mutate: vi.fn(), isPending: false }),
  useRetryFailedNotifications: () => ({ mutate: retryMutate, isPending: false }),
  getGetOrganizationSettingsQueryKey: () => ["settings"],
  getGetSystemHealthQueryKey: () => ["health"],
}));

function renderWithProviders(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const SETTINGS = {
  name: "Majlis",
  hasLogo: false,
  logoVersion: null,
  brandColor: 0,
  brandPrimary: "#0B7A4B",
  brandDark: "#075C39",
  timeZone: "Africa/Lagos",
  dateFormat: 0,
  fiscalYearStartMonth: 1,
  updatedAt: null,
  canEdit: true,
};

const HEALTH = {
  jobs: { servers: 1, enqueued: 2, processing: 0, scheduled: 4, failed: 0 },
  notifications: { sentLast24Hours: 120, waitingRetry: 1, givenUp: 3, pending: 0 },
  attachmentBytes: 19_293_798,
  attachmentCount: 42,
  emailTransport: "SMTP · smtp.example.org:587 · from noreply@example.org",
};

beforeEach(() => {
  updateMutate.mockReset();
  uploadMutate.mockReset();
  retryMutate.mockReset();
  useGetOrganizationSettings.mockReturnValue({ isPending: false, isError: false, data: SETTINGS });
  useGetSystemHealth.mockReturnValue({ isPending: false, isError: false, data: HEALTH });
});

describe("SettingsScreen", () => {
  it("lets the system administrator change branding and regional settings", async () => {
    renderWithProviders(<SettingsScreen />);

    const name = screen.getByLabelText("Organization name");
    await userEvent.clear(name);
    await userEvent.type(name, "Majlis Nigeria");
    await userEvent.click(screen.getByRole("radio", { name: "Blue" }));
    await userEvent.selectOptions(screen.getByLabelText("Time zone"), "Europe/London");
    await userEvent.selectOptions(screen.getByLabelText("Date format"), "2");
    await userEvent.selectOptions(screen.getByLabelText("Fiscal year starts"), "4");
    await userEvent.click(screen.getByRole("button", { name: "Save settings" }));

    expect(updateMutate).toHaveBeenCalledWith(
      { data: { name: "Majlis Nigeria", brandColor: 2, timeZone: "Europe/London", dateFormat: 2, fiscalYearStartMonth: 4 } },
      expect.anything(),
    );
  });

  it("refuses an oversized logo before uploading and uploads a valid one", async () => {
    renderWithProviders(<SettingsScreen />);
    const input = screen.getByLabelText("Upload logo", { selector: "input" });

    await userEvent.upload(input, new File([new Uint8Array(600 * 1024)], "big.png", { type: "image/png" }));
    expect(screen.getByRole("status")).toHaveTextContent("The logo must be at most 512 KB.");
    expect(uploadMutate).not.toHaveBeenCalled();

    const logo = new File([new Uint8Array(10)], "logo.png", { type: "image/png" });
    await userEvent.upload(input, logo);
    expect(uploadMutate).toHaveBeenCalledWith({ data: { file: logo } }, expect.anything());
  });

  it("is read-only for the Secretariat", () => {
    useGetOrganizationSettings.mockReturnValue({ isPending: false, isError: false, data: { ...SETTINGS, canEdit: false } });

    renderWithProviders(<SettingsScreen />);

    expect(screen.getByLabelText("Organization name")).toBeDisabled();
    expect(screen.queryByRole("button", { name: "Save settings" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Upload logo" })).not.toBeInTheDocument();
    expect(screen.getByText("Only the system administrator can change these settings.")).toBeInTheDocument();
  });

  it("shows system health and retries notifications the dispatcher gave up on", async () => {
    renderWithProviders(<SettingsScreen />);

    const health = screen.getByRole("heading", { name: "System health" }).closest("section")!;
    expect(within(health).getByText("All running · 2 queued")).toBeInTheDocument();
    expect(within(health).getByText("120 in 24 h")).toBeInTheDocument();
    expect(within(health).getByText("1 waiting retry · 3 gave up")).toBeInTheDocument();
    expect(within(health).getByText("18.4 MB in 42 attachments")).toBeInTheDocument();
    expect(screen.getByText(HEALTH.emailTransport)).toBeInTheDocument();

    await userEvent.click(within(health).getByRole("button", { name: "Retry failed notifications" }));
    expect(retryMutate).toHaveBeenCalled();
  });

  it("flags a missing worker", () => {
    useGetSystemHealth.mockReturnValue({ isPending: false, isError: false, data: { ...HEALTH, jobs: { ...HEALTH.jobs, servers: 0 } } });

    renderWithProviders(<SettingsScreen />);

    expect(screen.getByText("Worker not running")).toBeInTheDocument();
  });
});

describe("formatBytes", () => {
  it("uses binary units", () => {
    expect(formatBytes(512)).toBe("512 B");
    expect(formatBytes(2048)).toBe("2.0 KB");
    expect(formatBytes(19_293_798)).toBe("18.4 MB");
  });
});
