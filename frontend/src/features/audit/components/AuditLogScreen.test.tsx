import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { AuditLogScreen } from "./AuditLogScreen";

const useGetAuditEvents = vi.fn();
const useGetAuditFilterOptions = vi.fn();
const exportAuditEventsCsv = vi.fn();
const downloadBlob = vi.fn();

vi.mock("@/lib/api/generated/audit/audit", () => ({
  useGetAuditEvents: (...args: unknown[]) => useGetAuditEvents(...args),
  useGetAuditFilterOptions: (...args: unknown[]) => useGetAuditFilterOptions(...args),
  exportAuditEventsCsv: (...args: unknown[]) => exportAuditEventsCsv(...args),
}));

vi.mock("@/features/organization/utils/bulk-import", () => ({
  downloadBlob: (...args: unknown[]) => downloadBlob(...args),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

const row = (id: string, action: string, actorName: string, summary: string, before: string | null = null, after: string | null = null) => ({
  id,
  occurredAt: "2026-10-04T08:15:22Z",
  actorUserId: "u1",
  actorName,
  action,
  entityType: "window",
  entityId: "w1",
  unitId: null,
  unitName: null,
  summary,
  beforeJson: before,
  afterJson: after,
  ipAddress: "197.210.x.x",
  userAgent: "Firefox",
});

const PAGE = {
  rows: [
    row("e1", "report.submitted", "A. Adeyemi", "General Monthly · Sep · Agodi Muqam"),
    row("e2", "window.extended", "Mariam Okafor", "Iwo Road Muqam · due → 12 Oct", '{"dueAt":"2026-10-05"}', '{"dueAt":"2026-10-12","reason":"No head until 6 Oct"}'),
  ],
  totalCount: 2,
  page: 1,
  pageSize: 50,
};

beforeEach(() => {
  useGetAuditEvents.mockReset();
  exportAuditEventsCsv.mockReset();
  downloadBlob.mockReset();
  useGetAuditFilterOptions.mockReturnValue({
    data: {
      actions: ["report.submitted", "window.extended"],
      people: [{ userId: "u1", name: "Mariam Okafor" }],
      units: [
        { unitId: "z1", name: "South West Zone", depth: 1 },
        { unitId: "d1", name: "Ibadan Dila", depth: 2 },
      ],
    },
  });
});

describe("AuditLogScreen", () => {
  it("lists entries newest first with masked IPs and action chips", () => {
    useGetAuditEvents.mockReturnValue({ isPending: false, isError: false, data: PAGE });

    renderWithIntl(<AuditLogScreen />);

    const submitted = screen.getByRole("row", { name: /a\. adeyemi/i });
    expect(within(submitted).getByText("report.submitted")).toBeInTheDocument();
    expect(within(submitted).getByText("4 Oct, 09:15")).toBeInTheDocument();
    expect(within(submitted).getByText("197.210.x.x")).toBeInTheDocument();
    expect(within(submitted).getByText("AA")).toBeInTheDocument();
  });

  it("opens an entry's before and after values", async () => {
    useGetAuditEvents.mockReturnValue({ isPending: false, isError: false, data: PAGE });

    renderWithIntl(<AuditLogScreen />);
    await userEvent.click(screen.getByRole("button", { name: /show details: iwo road/i }));

    const details = screen.getByRole("heading", { name: "window.extended · details" }).closest("section")!;
    expect(within(details).getByText("dueAt: 2026-10-05")).toBeInTheDocument();
    expect(within(details).getByText("reason: No head until 6 Oct")).toBeInTheDocument();
    expect(within(details).getByText("Device: Firefox")).toBeInTheDocument();
  });

  it("filters by action, person, unit and date, starting from the last 30 days", async () => {
    useGetAuditEvents.mockReturnValue({ isPending: false, isError: false, data: PAGE });

    renderWithIntl(<AuditLogScreen />);
    expect(useGetAuditEvents).toHaveBeenLastCalledWith(expect.objectContaining({ from: expect.any(String), page: 1 }));

    await userEvent.selectOptions(screen.getByLabelText("Action"), "window.extended");
    await userEvent.selectOptions(screen.getByLabelText("Person"), "u1");
    await userEvent.selectOptions(screen.getByLabelText("Unit"), "z1");
    await userEvent.selectOptions(screen.getByLabelText("Date"), "");

    expect(useGetAuditEvents).toHaveBeenLastCalledWith(
      expect.objectContaining({ action: "window.extended", actorUserId: "u1", unitId: "z1", from: undefined }),
    );
  });

  it("exports the filtered log as CSV", async () => {
    useGetAuditEvents.mockReturnValue({ isPending: false, isError: false, data: PAGE });
    exportAuditEventsCsv.mockResolvedValue(new Blob(["csv"]));

    renderWithIntl(<AuditLogScreen />);
    await userEvent.click(screen.getByRole("button", { name: "Export CSV" }));

    expect(exportAuditEventsCsv).toHaveBeenCalledWith(expect.objectContaining({ from: expect.any(String) }));
    expect(downloadBlob).toHaveBeenCalledWith(expect.any(Blob), "audit-log.csv");
  });

  it("explains when the viewer isn't allowed, and when nothing matches", () => {
    useGetAuditEvents.mockReturnValue({ isPending: false, isError: true, error: { status: 403 }, data: undefined, refetch: vi.fn() });
    const { unmount } = renderWithIntl(<AuditLogScreen />);
    expect(screen.getByText(/only the secretariat and the system administrator/i)).toBeInTheDocument();
    unmount();

    useGetAuditEvents.mockReturnValue({ isPending: false, isError: false, data: { ...PAGE, rows: [], totalCount: 0 } });
    renderWithIntl(<AuditLogScreen />);
    expect(screen.getByText("Nothing recorded")).toBeInTheDocument();
  });
});
