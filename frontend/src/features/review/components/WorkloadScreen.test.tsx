import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { WorkloadScreen } from "./WorkloadScreen";

const useGetWorkload = vi.fn();

vi.mock("@/lib/api/generated/review/review", () => ({
  useGetWorkload: (...args: unknown[]) => useGetWorkload(...args),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  useGetWorkload.mockReset();
});

describe("WorkloadScreen", () => {
  it("shows a loading message while the workload loads", () => {
    useGetWorkload.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<WorkloadScreen />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetWorkload.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<WorkloadScreen />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("shows each staff member's assigned count and the unassigned count", () => {
    useGetWorkload.mockReturnValue({
      isPending: false,
      isError: false,
      data: {
        staff: [
          { userId: "s1", name: "Amina Bello", assignedCount: 3 },
          { userId: "s2", name: "Chidi Okafor", assignedCount: 0 },
        ],
        unassignedCount: 2,
      },
    });

    renderWithProviders(<WorkloadScreen />);

    expect(screen.getByText("Amina Bello")).toBeInTheDocument();
    expect(screen.getByText("Chidi Okafor")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getAllByText("0")).not.toHaveLength(0);
    expect(screen.getByText("2")).toBeInTheDocument();
  });
});
