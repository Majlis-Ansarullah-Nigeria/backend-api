import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { ApiError } from "@/lib/api/client";
import { DepartmentScreen } from "./DepartmentScreen";

const useGetDepartmentReports = vi.fn();
vi.mock("@/lib/api/generated/department/department", () => ({
  useGetDepartmentReports: (...args: unknown[]) => useGetDepartmentReports(...args),
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

const ROW = {
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
  submittedAt: "2026-10-01T10:00:00Z",
  fields: [{ fieldKey: "income", label: "Income", value: "500" }],
};

beforeEach(() => {
  useGetDepartmentReports.mockReset();
  useGetReportTypes.mockReset();
  useGetSubmissionWindows.mockReset();
  useGetUnitLevels.mockReset();
  useGetUnits.mockReset();

  useGetReportTypes.mockReturnValue({ data: [] });
  useGetSubmissionWindows.mockReturnValue({ data: [] });
  useGetUnitLevels.mockReturnValue({ data: [] });
  useGetUnits.mockReturnValue({ data: [] });
});

describe("DepartmentScreen", () => {
  it("shows a loading message while the data loads", () => {
    useGetDepartmentReports.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<DepartmentScreen />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows a graceful empty state, not a scary error, when the caller isn't a department officer", () => {
    useGetDepartmentReports.mockReturnValue({
      isPending: false,
      isError: true,
      error: new ApiError(403, "FORBIDDEN", "Only department officers have department data access."),
      data: undefined,
    });

    renderWithProviders(<DepartmentScreen />);

    expect(screen.getByText("No department access")).toBeInTheDocument();
  });

  it("shows a real error state with retry for an unexpected failure", async () => {
    const refetch = vi.fn();
    useGetDepartmentReports.mockReturnValue({
      isPending: false,
      isError: true,
      error: new Error("network down"),
      data: undefined,
      refetch,
    });

    renderWithProviders(<DepartmentScreen />);

    expect(screen.getByText(/we couldn't load your department's data/i)).toBeInTheDocument();
  });

  it("shows an empty state when nothing matches the filter", () => {
    useGetDepartmentReports.mockReturnValue({ isPending: false, isError: false, data: [] });

    renderWithProviders(<DepartmentScreen />);

    expect(screen.getByText("Nothing here yet")).toBeInTheDocument();
  });

  it("lists each report's unit, period and only the department's own field values", () => {
    useGetDepartmentReports.mockReturnValue({ isPending: false, isError: false, data: [ROW] });

    renderWithProviders(<DepartmentScreen />);

    expect(screen.getByText(/Dila Ibafo/)).toBeInTheDocument();
    expect(screen.getByText(/Zone 3/)).toBeInTheDocument();
    expect(screen.getByText(/Dila Monthly Activities Report/)).toBeInTheDocument();
    expect(screen.getByText("Income")).toBeInTheDocument();
    expect(screen.getByText("500")).toBeInTheDocument();
  });
});
