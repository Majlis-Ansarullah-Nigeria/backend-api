import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { InboxDetailPanel } from "./InboxDetailPanel";

const useGetInboxReportDetail = vi.fn();
const useOpenInboxReport = vi.fn();
const useAcknowledgeInboxReport = vi.fn();
const useReturnInboxReport = vi.fn();
const useSetInboxReportFlagged = vi.fn();
const useAssignInboxReport = vi.fn();
const useGetWorkload = vi.fn();

vi.mock("@/lib/api/generated/review/review", () => ({
  useGetInboxReportDetail: (...args: unknown[]) => useGetInboxReportDetail(...args),
  useOpenInboxReport: (...args: unknown[]) => useOpenInboxReport(...args),
  useAcknowledgeInboxReport: (...args: unknown[]) => useAcknowledgeInboxReport(...args),
  useReturnInboxReport: (...args: unknown[]) => useReturnInboxReport(...args),
  useSetInboxReportFlagged: (...args: unknown[]) => useSetInboxReportFlagged(...args),
  useAssignInboxReport: (...args: unknown[]) => useAssignInboxReport(...args),
  useGetWorkload: (...args: unknown[]) => useGetWorkload(...args),
  getGetInboxQueryKey: () => ["getInbox"],
  getGetInboxReportDetailQueryKey: (id: string) => ["getInboxReportDetail", id],
}));

const useGetReportComments = vi.fn();
const useAddReportComment = vi.fn();
vi.mock("@/lib/api/generated/submissions/submissions", () => ({
  useGetReportComments: (...args: unknown[]) => useGetReportComments(...args),
  useAddReportComment: (...args: unknown[]) => useAddReportComment(...args),
  getGetReportCommentsQueryKey: (id: string) => ["getReportComments", id],
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const BASE_DETAIL = {
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
};

let openMutate: ReturnType<typeof vi.fn>;
let acknowledgeMutate: ReturnType<typeof vi.fn>;
let returnMutate: ReturnType<typeof vi.fn>;
let flagMutate: ReturnType<typeof vi.fn>;
let assignMutate: ReturnType<typeof vi.fn>;

beforeEach(() => {
  useGetInboxReportDetail.mockReset();
  useOpenInboxReport.mockReset();
  useAcknowledgeInboxReport.mockReset();
  useReturnInboxReport.mockReset();
  useSetInboxReportFlagged.mockReset();
  useAssignInboxReport.mockReset();
  useGetWorkload.mockReset();
  useGetReportComments.mockReset();
  useAddReportComment.mockReset();

  openMutate = vi.fn();
  acknowledgeMutate = vi.fn();
  returnMutate = vi.fn();
  flagMutate = vi.fn();
  assignMutate = vi.fn();

  useOpenInboxReport.mockReturnValue({ mutate: openMutate, isPending: false });
  useAcknowledgeInboxReport.mockReturnValue({ mutate: acknowledgeMutate, isPending: false, error: null });
  useReturnInboxReport.mockReturnValue({ mutate: returnMutate, isPending: false, error: null });
  useSetInboxReportFlagged.mockReturnValue({ mutate: flagMutate, isPending: false });
  useAssignInboxReport.mockReturnValue({ mutate: assignMutate, isPending: false });
  useGetWorkload.mockReturnValue({
    data: { staff: [{ userId: "staff1", name: "Amina Bello", assignedCount: 2 }], unassignedCount: 1 },
  });
  useGetReportComments.mockReturnValue({ isPending: false, data: [] });
  useAddReportComment.mockReturnValue({ mutate: vi.fn(), isPending: false, error: null });
});

describe("InboxDetailPanel", () => {
  it("fires the open (mark as read) mutation as soon as a report is viewed", () => {
    useGetInboxReportDetail.mockReturnValue({ isPending: false, isError: false, data: { ...BASE_DETAIL, status: 2 } });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);

    expect(openMutate).toHaveBeenCalledWith({ id: "r1" }, expect.anything());
  });

  it("shows Acknowledge and Return for a Submitted or InReview report", () => {
    useGetInboxReportDetail.mockReturnValue({ isPending: false, isError: false, data: { ...BASE_DETAIL, status: 3 } });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);

    expect(screen.getByRole("button", { name: /acknowledge/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /return for revision/i })).toBeInTheDocument();
  });

  it("hides Acknowledge and Return once a report is already Acknowledged", () => {
    useGetInboxReportDetail.mockReturnValue({ isPending: false, isError: false, data: { ...BASE_DETAIL, status: 5 } });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);

    expect(screen.queryByRole("button", { name: /acknowledge/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /return for revision/i })).not.toBeInTheDocument();
    // Flag stays available regardless of status.
    expect(screen.getByRole("button", { name: /flag/i })).toBeInTheDocument();
  });

  it("clicking Acknowledge calls the mutation with the report id", async () => {
    useGetInboxReportDetail.mockReturnValue({ isPending: false, isError: false, data: { ...BASE_DETAIL, status: 3 } });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);
    await userEvent.click(screen.getByRole("button", { name: /acknowledge/i }));

    expect(acknowledgeMutate).toHaveBeenCalledWith({ id: "r1" }, expect.anything());
  });

  it("Return requires a non-empty comment before sending", async () => {
    useGetInboxReportDetail.mockReturnValue({ isPending: false, isError: false, data: { ...BASE_DETAIL, status: 3 } });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);
    await userEvent.click(screen.getByRole("button", { name: /return for revision/i }));

    const sendButton = screen.getByRole("button", { name: /send/i });
    expect(sendButton).toBeDisabled();

    await userEvent.type(screen.getByPlaceholderText(/explain what needs to change/i), "Please fix the figures");
    expect(sendButton).toBeEnabled();

    await userEvent.click(sendButton);
    expect(returnMutate).toHaveBeenCalledWith({ id: "r1", data: { comment: "Please fix the figures" } }, expect.anything());
  });

  it("toggling Flag calls the mutation with the opposite of the current isFlagged value", async () => {
    useGetInboxReportDetail.mockReturnValue({ isPending: false, isError: false, data: { ...BASE_DETAIL, status: 3, isFlagged: false } });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);
    await userEvent.click(screen.getByRole("button", { name: /^flag$/i }));

    expect(flagMutate).toHaveBeenCalledWith({ id: "r1", data: { isFlagged: true } }, expect.anything());
  });

  it("shows the return comment when the report was returned", () => {
    useGetInboxReportDetail.mockReturnValue({
      isPending: false,
      isError: false,
      data: { ...BASE_DETAIL, status: 4, returnComment: "Please double-check the figures." },
    });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);

    expect(screen.getByText("Please double-check the figures.")).toBeInTheDocument();
  });

  it("lists Secretariat staff from the workload view in the Assign to dropdown", () => {
    useGetInboxReportDetail.mockReturnValue({ isPending: false, isError: false, data: { ...BASE_DETAIL, status: 3 } });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);

    const select = screen.getByLabelText("Assign to") as HTMLSelectElement;
    expect(within(select).getByText("Amina Bello")).toBeInTheDocument();
  });

  it("picking a staff member calls the assign mutation with that user's id", async () => {
    useGetInboxReportDetail.mockReturnValue({ isPending: false, isError: false, data: { ...BASE_DETAIL, status: 3 } });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);
    await userEvent.selectOptions(screen.getByLabelText("Assign to"), "staff1");

    expect(assignMutate).toHaveBeenCalledWith({ id: "r1", data: { assignedToUserId: "staff1" } }, expect.anything());
  });

  it("renders the comments panel with the receipt's fields available for a new thread", () => {
    useGetInboxReportDetail.mockReturnValue({ isPending: false, isError: false, data: { ...BASE_DETAIL, status: 3 } });

    renderWithProviders(<InboxDetailPanel reportId="r1" />);

    expect(screen.getByText("Comments")).toBeInTheDocument();
    expect(screen.getByText("No comments yet.")).toBeInTheDocument();
  });
});
