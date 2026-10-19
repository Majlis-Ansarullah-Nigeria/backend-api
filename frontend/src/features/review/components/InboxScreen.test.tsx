import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { InboxScreen } from "./InboxScreen";

const useGetInbox = vi.fn();
const useGetInboxReportDetail = vi.fn();
const useGetSavedViews = vi.fn();
const useCreateSavedView = vi.fn();
const useDeleteSavedView = vi.fn();
const useOpenInboxReport = vi.fn();
const useAcknowledgeInboxReport = vi.fn();
const useReturnInboxReport = vi.fn();
const useSetInboxReportFlagged = vi.fn();
const useAssignInboxReport = vi.fn();
const useGetWorkload = vi.fn();
const getGetInboxQueryKey = (params?: unknown) => ["getInbox", params];
const getGetInboxReportDetailQueryKey = (id: string) => ["getInboxReportDetail", id];

// The version history panel has its own tests (ReportVersionsPanel.test.tsx).
vi.mock("@/features/history/components/ReportVersionsPanel", () => ({
  ReportVersionsPanel: () => <div>Version history panel</div>,
}));

vi.mock("@/lib/api/generated/review/review", () => ({
  useGetInbox: (...args: unknown[]) => useGetInbox(...args),
  useGetInboxReportDetail: (...args: unknown[]) => useGetInboxReportDetail(...args),
  useGetSavedViews: (...args: unknown[]) => useGetSavedViews(...args),
  useCreateSavedView: (...args: unknown[]) => useCreateSavedView(...args),
  useDeleteSavedView: (...args: unknown[]) => useDeleteSavedView(...args),
  useOpenInboxReport: (...args: unknown[]) => useOpenInboxReport(...args),
  useAcknowledgeInboxReport: (...args: unknown[]) => useAcknowledgeInboxReport(...args),
  useReturnInboxReport: (...args: unknown[]) => useReturnInboxReport(...args),
  useSetInboxReportFlagged: (...args: unknown[]) => useSetInboxReportFlagged(...args),
  useAssignInboxReport: (...args: unknown[]) => useAssignInboxReport(...args),
  useGetWorkload: (...args: unknown[]) => useGetWorkload(...args),
  getGetInboxQueryKey: (params?: unknown) => getGetInboxQueryKey(params),
  getGetInboxReportDetailQueryKey: (id: string) => getGetInboxReportDetailQueryKey(id),
}));

const useGetReportComments = vi.fn();
const useAddReportComment = vi.fn();
vi.mock("@/lib/api/generated/submissions/submissions", () => ({
  useGetReportComments: (...args: unknown[]) => useGetReportComments(...args),
  useAddReportComment: (...args: unknown[]) => useAddReportComment(...args),
  getGetReportCommentsQueryKey: (id: string) => ["getReportComments", id],
}));

const useGetMe = vi.fn();
vi.mock("@/lib/api/generated/identity/identity", () => ({
  useGetMe: (...args: unknown[]) => useGetMe(...args),
}));

const useGetReportTypes = vi.fn();
vi.mock("@/lib/api/generated/templates/templates", () => ({
  useGetReportTypes: (...args: unknown[]) => useGetReportTypes(...args),
}));

const useGetSubmissionWindows = vi.fn();
vi.mock("@/lib/api/generated/windows/windows", () => ({
  useGetSubmissionWindows: (...args: unknown[]) => useGetSubmissionWindows(...args),
}));

const useGetUnitLevels = vi.fn();
const useGetUnits = vi.fn();
vi.mock("@/lib/api/generated/organization/organization", () => ({
  useGetUnitLevels: (...args: unknown[]) => useGetUnitLevels(...args),
  useGetUnits: (...args: unknown[]) => useGetUnits(...args),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const INBOX_ITEM = {
  reportId: "r1",
  reportTypeId: "rt1",
  reportTypeName: "Dila Monthly Activities Report",
  unitId: "u1",
  unitName: "Dila Ibafo",
  unitLevel: 2,
  zoneUnitId: "z1",
  zoneName: "Zone 3",
  windowId: "w1",
  periodLabel: "September 2026",
  status: 2,
  isLate: false,
  isFlagged: false,
  submittedAt: "2026-10-01T10:00:00Z",
  referenceNumber: "GMR-2026-09-Z03D12",
};

beforeEach(() => {
  useGetInbox.mockReset();
  useGetInboxReportDetail.mockReset();
  useGetSavedViews.mockReset();
  useCreateSavedView.mockReset();
  useDeleteSavedView.mockReset();
  useOpenInboxReport.mockReset();
  useAcknowledgeInboxReport.mockReset();
  useReturnInboxReport.mockReset();
  useSetInboxReportFlagged.mockReset();
  useAssignInboxReport.mockReset();
  useGetWorkload.mockReset();
  useGetReportComments.mockReset();
  useAddReportComment.mockReset();
  useGetMe.mockReset();
  useGetReportTypes.mockReset();
  useGetSubmissionWindows.mockReset();
  useGetUnitLevels.mockReset();
  useGetUnits.mockReset();

  useGetReportTypes.mockReturnValue({ data: [] });
  useGetSubmissionWindows.mockReturnValue({ data: [] });
  useGetUnitLevels.mockReturnValue({ data: [] });
  useGetUnits.mockReturnValue({ data: [] });
  useGetSavedViews.mockReturnValue({ data: [], refetch: vi.fn() });
  useCreateSavedView.mockReturnValue({ mutate: vi.fn(), isPending: false });
  useDeleteSavedView.mockReturnValue({ mutate: vi.fn() });
  useOpenInboxReport.mockReturnValue({ mutate: vi.fn(), isPending: false });
  useAcknowledgeInboxReport.mockReturnValue({ mutate: vi.fn(), isPending: false, error: null });
  useReturnInboxReport.mockReturnValue({ mutate: vi.fn(), isPending: false, error: null });
  useSetInboxReportFlagged.mockReturnValue({ mutate: vi.fn(), isPending: false });
  useAssignInboxReport.mockReturnValue({ mutate: vi.fn(), isPending: false });
  useGetWorkload.mockReturnValue({ data: { staff: [], unassignedCount: 0 } });
  useGetReportComments.mockReturnValue({ isPending: false, data: [] });
  useAddReportComment.mockReturnValue({ mutate: vi.fn(), isPending: false, error: null });
  useGetMe.mockReturnValue({ data: { id: "me1", name: "Me", email: "me@example.org" } });
});

describe("InboxScreen", () => {
  it("shows a loading message while the inbox loads", () => {
    useGetInbox.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<InboxScreen />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetInbox.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<InboxScreen />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("shows an empty state when nothing matches the filter", () => {
    useGetInbox.mockReturnValue({ isPending: false, isError: false, data: [] });

    renderWithProviders(<InboxScreen />);

    expect(screen.getByText(/nothing in the inbox/i)).toBeInTheDocument();
  });

  it("lists inbox items and shows their status and unit/zone", () => {
    useGetInbox.mockReturnValue({ isPending: false, isError: false, data: [INBOX_ITEM] });

    renderWithProviders(<InboxScreen />);

    expect(screen.getByText("Dila Monthly Activities Report")).toBeInTheDocument();
    expect(screen.getByText(/Dila Ibafo/)).toBeInTheDocument();
    expect(screen.getByText(/Zone 3/)).toBeInTheDocument();
  });

  it("selecting a row shows the read-only report content with no download/export action", async () => {
    useGetInbox.mockReturnValue({ isPending: false, isError: false, data: [INBOX_ITEM] });
    useGetInboxReportDetail.mockReturnValue({
      isPending: false,
      isError: false,
      data: {
        status: 2,
        isLate: false,
        isFlagged: false,
        assignedToUserId: null,
        assignedToName: null,
        submittedAt: "2026-10-01T10:00:00Z",
        returnComment: null,
        receipt: {
          reportTypeName: "Dila Monthly Activities Report",
          periodLabel: "September 2026",
          unitName: "Dila Ibafo",
          referenceNumber: "GMR-2026-09-Z03D12",
          generatedAt: "2026-10-01T10:00:00Z",
          sections: [{ title: "Umumi", fields: [{ fieldKey: "tajnid", label: "Tajnid", value: "120", isMasked: false }] }],
        },
      },
    });

    renderWithProviders(<InboxScreen />);
    await userEvent.click(screen.getByText("Dila Monthly Activities Report"));

    expect(screen.getByText("Umumi")).toBeInTheDocument();
    expect(screen.getByText("Tajnid")).toBeInTheDocument();
    // Acknowledge/Return/Flag/Assign/Comment (M7.2/M7.3) are in scope now; download (M9) isn't.
    expect(screen.queryByRole("button", { name: /download/i })).not.toBeInTheDocument();
  });

  it("switching to the Assigned to me tab filters by the current user's id", async () => {
    useGetInbox.mockReturnValue({ isPending: false, isError: false, data: [] });

    renderWithProviders(<InboxScreen />);
    await userEvent.click(screen.getByRole("tab", { name: "Assigned to me" }));

    const lastCall = useGetInbox.mock.calls.at(-1);
    expect(lastCall?.[0]).toMatchObject({ assignedToUserId: "me1", isFlagged: undefined, status: undefined });
  });

  it("shows the assigned staff member's name in the list", () => {
    useGetInbox.mockReturnValue({
      isPending: false,
      isError: false,
      data: [{ ...INBOX_ITEM, assignedToUserId: "staff1", assignedToName: "Amina Bello" }],
    });

    renderWithProviders(<InboxScreen />);

    expect(screen.getByText("Amina Bello")).toBeInTheDocument();
  });

  it("switching to the Flagged tab filters by isFlagged instead of status", async () => {
    useGetInbox.mockReturnValue({ isPending: false, isError: false, data: [] });

    renderWithProviders(<InboxScreen />);
    await userEvent.click(screen.getByRole("tab", { name: "Flagged" }));

    const lastCall = useGetInbox.mock.calls.at(-1);
    expect(lastCall?.[0]).toMatchObject({ isFlagged: true, status: undefined });
  });

  it("flagged and late items show their chips in the list", () => {
    useGetInbox.mockReturnValue({
      isPending: false,
      isError: false,
      data: [{ ...INBOX_ITEM, isLate: true, isFlagged: true }],
    });

    renderWithProviders(<InboxScreen />);

    const row = screen.getByText("Dila Monthly Activities Report").closest("tr");
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText("Late")).toBeInTheDocument();
    expect(within(row as HTMLElement).getByText("Flagged")).toBeInTheDocument();
  });
});
