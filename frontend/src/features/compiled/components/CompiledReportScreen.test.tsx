import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { CompiledReportScreen } from "./CompiledReportScreen";

const useGetCompiledReport = vi.fn();
const mutate = vi.fn();
const getCompiledReportPdf = vi.fn();
const getCompiledReportWord = vi.fn();
const downloadBlob = vi.fn();

vi.mock("@/lib/api/generated/compiled-reports/compiled-reports", () => ({
  useGetCompiledReport: (...args: unknown[]) => useGetCompiledReport(...args),
  useUpdateCompiledNarrative: () => ({ mutate, isPending: false, error: null }),
  getCompiledReportPdf: (...args: unknown[]) => getCompiledReportPdf(...args),
  getCompiledReportWord: (...args: unknown[]) => getCompiledReportWord(...args),
  getGetCompiledReportQueryKey: (id: string) => ["getCompiledReport", id],
}));

vi.mock("@/features/organization/utils/bulk-import", () => ({
  downloadBlob: (...args: unknown[]) => downloadBlob(...args),
}));

function renderWithProviders(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const REPORT = {
  windowId: "w9",
  reportTypeId: "t1",
  reportTypeName: "Monthly Report",
  periodLabel: "September 2026",
  previousPeriodLabel: "August 2026",
  referenceNumber: "NAT-GMR-2026-09",
  compiledAt: "2026-10-22T09:00:00Z",
  canEdit: true,
  narrative: "",
  narrativeUpdatedAt: null,
  overview: {
    expected: 267,
    onTime: 120,
    late: 20,
    notReported: 127,
    acknowledged: 98,
    levels: [
      { level: 3, levelName: "Muqam", levelPluralName: "Muqamaat", submitted: 131, expected: 214 },
      { level: 2, levelName: "Dila", levelPluralName: "Dilaat", submitted: 4, expected: 38 },
    ],
  },
  sections: [
    {
      key: "membership",
      title: "Membership",
      figures: [{ fieldKey: "members", label: "Total members", type: 2, unitLabel: null, value: 3702, previous: 3650, target: 3800 }],
      highlights: [
        { kind: 0, fieldKey: "members", fieldLabel: "Total members", zoneName: "Lagos Zone", value: 1064, change: 21 },
        { kind: 2, fieldKey: "members", fieldLabel: "Total members", zoneName: "North West Zone", value: 310, change: -10 },
      ],
    },
  ],
  fields: [{ fieldKey: "members", label: "Total members", type: 2, unitLabel: null, aggregation: 1 }],
  zones: [{ unitId: "z1", name: "Lagos Zone", submitted: 49, expected: 66, values: [1064] }],
};

beforeEach(() => {
  useGetCompiledReport.mockReset();
  mutate.mockReset();
  downloadBlob.mockReset();
  getCompiledReportPdf.mockReset();
});

describe("CompiledReportScreen", () => {
  it("lays out the report: overview, sections, highlights and appendix", () => {
    useGetCompiledReport.mockReturnValue({ isPending: false, isError: false, data: REPORT });

    renderWithProviders(<CompiledReportScreen windowId="w9" basePath="/secretariat/compiled" />);

    expect(screen.getByText("NAT-GMR-2026-09")).toBeInTheDocument();
    expect(screen.getByText(/Of 267 units expected to report, 120 submitted on time, 20 late and 127 did not report/)).toBeInTheDocument();
    expect(screen.getByText("131 / 214")).toBeInTheDocument();
    const membership = screen.getByRole("heading", { name: "3 · Membership" }).closest("section")!;
    const row = within(membership).getByRole("row", { name: /total members/i });
    expect(within(row).getByText("3,702")).toBeInTheDocument();
    expect(within(row).getByText("+52")).toBeInTheDocument();
    expect(screen.getByText("Total members: highest in Lagos Zone (1,064).")).toBeInTheDocument();
    expect(screen.getByText("Total members: the largest fall was North West Zone (-10, now 310).")).toBeInTheDocument();
    expect(screen.getByRole("row", { name: /lagos zone 49 \/ 66/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Compiled reports" })).toHaveAttribute("href", "/secretariat/compiled");
  });

  it("lets the Secretariat write and save the summary", async () => {
    useGetCompiledReport.mockReturnValue({ isPending: false, isError: false, data: REPORT });

    renderWithProviders(<CompiledReportScreen windowId="w9" basePath="/secretariat/compiled" />);
    await userEvent.type(screen.getByLabelText(/write the summary/i), "A strong month.");
    await userEvent.click(screen.getByRole("button", { name: "Save summary" }));

    expect(mutate).toHaveBeenCalledWith({ windowId: "w9", data: { narrative: "A strong month." } }, expect.anything());
  });

  it("shows the summary read-only to Leadership", () => {
    useGetCompiledReport.mockReturnValue({ isPending: false, isError: false, data: { ...REPORT, canEdit: false, narrative: "Written by the Secretariat." } });

    renderWithProviders(<CompiledReportScreen windowId="w9" basePath="/compiled" />);

    expect(screen.getByText("Written by the Secretariat.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save summary" })).not.toBeInTheDocument();
  });

  it("downloads the PDF and refreshes the data", async () => {
    const refetch = vi.fn();
    useGetCompiledReport.mockReturnValue({ isPending: false, isError: false, isFetching: false, data: REPORT, refetch });
    getCompiledReportPdf.mockResolvedValue(new Blob(["pdf"]));

    renderWithProviders(<CompiledReportScreen windowId="w9" basePath="/secretariat/compiled" />);
    await userEvent.click(screen.getByRole("button", { name: "Download PDF" }));
    await userEvent.click(screen.getByRole("button", { name: "Refresh data" }));

    expect(downloadBlob).toHaveBeenCalledWith(expect.any(Blob), "NAT-GMR-2026-09.pdf");
    expect(refetch).toHaveBeenCalled();
  });
});
