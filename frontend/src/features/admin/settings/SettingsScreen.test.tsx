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
const useGetFeatureFlags = vi.fn();
const setFlagMutate = vi.fn();
const useGetWebhooks = vi.fn();

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
  useGetFeatureFlags: (...args: unknown[]) => useGetFeatureFlags(...args),
  useSetFeatureFlag: () => ({ mutate: setFlagMutate, isPending: false }),
  getGetFeatureFlagsQueryKey: () => ["flags"],
  useGetWebhooks: (...args: unknown[]) => useGetWebhooks(...args),
}));

function flags(canEdit: boolean, off: number[] = []) {
  return { isPending: false, data: { canEdit, flags: [0, 1, 2, 3].map((module) => ({ module, enabled: !off.includes(module) })) } };
}

const WEBHOOK = {
  id: "h1",
  url: "https://hooks.example.org/hrs",
  events: [0],
  isActive: true,
  secretHint: "…ab12",
  createdAt: "2026-10-20T09:00:00Z",
  deliveredLast24Hours: 3,
  pending: 0,
  failed: 0,
  lastDeliveryAt: null,
  lastDeliveryStatus: null,
};

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
  setFlagMutate.mockReset();
  useGetFeatureFlags.mockReturnValue(flags(true));
  useGetWebhooks.mockReturnValue({ isPending: false, isError: false, isSuccess: true, data: [WEBHOOK] });
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

describe("SettingsScreen modules and webhooks", () => {
  it("lets the system administrator switch a module off", async () => {
    useGetFeatureFlags.mockReturnValue(flags(true, [3]));
    renderWithProviders(<SettingsScreen />);

    const modules = screen.getByRole("heading", { name: "Modules" }).closest("section")!;
    expect(within(modules).getByRole("switch", { name: "Web push" })).not.toBeChecked();
    const compiled = within(modules).getByRole("switch", { name: "Compiled reports" });
    expect(compiled).toBeChecked();

    await userEvent.click(compiled);
    expect(setFlagMutate).toHaveBeenCalledWith({ module: 1, data: { enabled: false } }, expect.anything());
  });

  it("shows the modules read-only to anyone else", () => {
    useGetFeatureFlags.mockReturnValue(flags(false));
    renderWithProviders(<SettingsScreen />);

    const modules = screen.getByRole("heading", { name: "Modules" }).closest("section")!;
    for (const toggle of within(modules).getAllByRole("switch")) {
      expect(toggle).toBeDisabled();
    }
  });

  it("summarises webhooks with a Manage link for the system administrator", () => {
    renderWithProviders(<SettingsScreen />);

    expect(screen.getByText("report.submitted → 1 endpoint")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Manage" })).toHaveAttribute("href", "/admin/webhooks");
  });

  it("hides webhook details from the Secretariat, whom the API refuses", () => {
    useGetWebhooks.mockReturnValue({ isPending: false, isError: true, isSuccess: false, error: { status: 403 } });
    renderWithProviders(<SettingsScreen />);

    expect(screen.getByText("Managed by the system administrator")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Manage" })).not.toBeInTheDocument();
  });
});

describe("formatBytes", () => {
  it("uses binary units", () => {
    expect(formatBytes(512)).toBe("512 B");
    expect(formatBytes(2048)).toBe("2.0 KB");
    expect(formatBytes(19_293_798)).toBe("18.4 MB");
  });
});
