import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";
import { ReviewScreen } from "./ReviewScreen";

const useGetReportInstanceForEditing = vi.fn();
const useGetReportReviewWarnings = vi.fn();
const submitReport = vi.fn();
const getReportReceiptPdf = vi.fn();
const push = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("@/lib/api/generated/submissions/submissions", () => ({
  useGetReportInstanceForEditing: (...args: unknown[]) => useGetReportInstanceForEditing(...args),
  useGetReportReviewWarnings: (...args: unknown[]) => useGetReportReviewWarnings(...args),
  submitReport: (...args: unknown[]) => submitReport(...args),
  getReportReceiptPdf: (...args: unknown[]) => getReportReceiptPdf(...args),
  getGetMyReportsQueryKey: () => ["getMyReports"],
}));

vi.mock("@/features/organization/utils/bulk-import", () => ({
  downloadBlob: vi.fn(),
}));

const SECTION = {
  key: "membership",
  title: "Membership",
  fields: [
    { key: "total_members", type: TEMPLATE_FIELD_TYPE.Number, label: "Total members", helpText: null, required: true, min: null, max: null, regex: null, defaultValue: null, unitLabel: null, confidential: false, owningDepartmentId: null, visibleForLevels: null, showIf: null, aggregation: 0, computedExpression: null, options: null, columns: null },
  ],
};

function baseReport(overrides: Record<string, unknown> = {}) {
  return {
    id: "report-1",
    windowId: "window-1",
    unitId: "unit-1",
    unitName: "Agodi Muqam",
    reportTypeId: "rt-1",
    reportTypeName: "General Monthly Report",
    periodLabel: "September 2026",
    templateVersionId: "tv-1",
    templateVersionNumber: 1,
    schema: { sections: [SECTION] },
    unitLevel: 3,
    answers: { total_members: "148" },
    status: 1, // Draft
    canSave: true,
    opensAt: "2026-09-01T00:00:00Z",
    dueAt: "2026-10-05T23:59:00Z",
    closesAt: "2026-10-10T23:59:00Z",
    revisionDeadline: null,
    rowVersion: "5",
    referenceNumber: null,
    submittedAt: null,
    ...overrides,
  };
}

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  useGetReportInstanceForEditing.mockReset();
  useGetReportReviewWarnings.mockReset();
  submitReport.mockReset();
  getReportReceiptPdf.mockReset();
  push.mockReset();
  useGetReportReviewWarnings.mockReturnValue({ data: { warnings: [] } });
});

describe("ReviewScreen", () => {
  it("shows a loading message while the report loads", () => {
    useGetReportInstanceForEditing.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<ReviewScreen id="report-1" unitId="unit-1" />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows a not-reviewable message for an already-submitted report", () => {
    useGetReportInstanceForEditing.mockReturnValue({ isPending: false, isError: false, data: baseReport({ status: 2 }) });

    renderWithProviders(<ReviewScreen id="report-1" unitId="unit-1" />);

    expect(screen.getByText(/isn't ready to review/i)).toBeInTheDocument();
  });

  it("renders the section summary with formatted answers", () => {
    useGetReportInstanceForEditing.mockReturnValue({ isPending: false, isError: false, data: baseReport() });

    renderWithProviders(<ReviewScreen id="report-1" unitId="unit-1" />);

    expect(screen.getByText("Total members")).toBeInTheDocument();
    expect(screen.getByText("148")).toBeInTheDocument();
  });

  it("shows warnings when the backend returns any", () => {
    useGetReportInstanceForEditing.mockReturnValue({ isPending: false, isError: false, data: baseReport() });
    useGetReportReviewWarnings.mockReturnValue({ data: { warnings: ["Total members is 60% higher than last submission (90)."] } });

    renderWithProviders(<ReviewScreen id="report-1" unitId="unit-1" />);

    expect(screen.getByText(/60% higher than last submission/i)).toBeInTheDocument();
  });

  it("keeps Submit disabled until the declaration is checked", async () => {
    const user = userEvent.setup();
    useGetReportInstanceForEditing.mockReturnValue({ isPending: false, isError: false, data: baseReport() });

    renderWithProviders(<ReviewScreen id="report-1" unitId="unit-1" />);

    const submitButton = screen.getByRole("button", { name: /submit report/i });
    expect(submitButton).toBeDisabled();

    await user.click(screen.getByRole("checkbox"));
    expect(submitButton).not.toBeDisabled();
  });

  it("submits with an Idempotency-Key header and navigates to the submitted screen on success", async () => {
    const user = userEvent.setup();
    useGetReportInstanceForEditing.mockReturnValue({ isPending: false, isError: false, data: baseReport() });
    submitReport.mockResolvedValue({ id: "report-1", referenceNumber: "GMR-2026-09-ABC", submittedAt: "2026-10-04T10:42:00Z", isLate: false });

    renderWithProviders(<ReviewScreen id="report-1" unitId="unit-1" />);

    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /submit report/i }));

    await waitFor(() => expect(push).toHaveBeenCalledWith("/reports/report-1/submitted?unit=unit-1"));

    expect(submitReport).toHaveBeenCalledWith(
      "report-1",
      { confirmed: true },
      { unit: "unit-1" },
      expect.objectContaining({ headers: expect.objectContaining({ "Idempotency-Key": expect.any(String) }) }),
    );
  });
});
