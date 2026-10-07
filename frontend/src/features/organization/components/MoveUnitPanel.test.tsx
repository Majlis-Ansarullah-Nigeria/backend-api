import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { MoveUnitPanel } from "./MoveUnitPanel";

const moveUnitMutate = vi.fn();

vi.mock("@/lib/api/generated/organization/organization", () => ({
  useMoveUnit: () => ({ mutate: moveUnitMutate, isPending: false, isError: false }),
  getGetOrganizationTreeQueryKey: () => ["getOrganizationTree"],
  getGetUnitQueryKey: (id: string) => ["getUnit", id],
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const UNITS = [
  { id: "zone1", code: "Z01", displayCode: "Z01", name: "Lagos Zone", levelId: "l1", levelCode: 1, parentId: "national", path: "nat.z01", archivedAt: null, headName: null, hasVacantHead: true },
  { id: "zone2", code: "Z02", displayCode: "Z02", name: "Ogun Zone", levelId: "l1", levelCode: 1, parentId: "national", path: "nat.z02", archivedAt: null, headName: null, hasVacantHead: true },
  { id: "zone3-archived", code: "Z03", displayCode: "Z03", name: "Archived Zone", levelId: "l1", levelCode: 1, parentId: "national", path: "nat.z03", archivedAt: "2026-01-01T00:00:00Z", headName: null, hasVacantHead: true },
  { id: "dila1", code: "D01", displayCode: "Z01-D01", name: "Ibadan Dila", levelId: "l2", levelCode: 2, parentId: "zone1", path: "nat.z01.d01", archivedAt: null, headName: null, hasVacantHead: true },
];

beforeEach(() => {
  moveUnitMutate.mockReset();
});

describe("MoveUnitPanel", () => {
  it("shows a 'move to another parent' button", () => {
    renderWithProviders(<MoveUnitPanel unitId="dila1" unitName="Ibadan Dila" parentId="zone1" path="nat.z01.d01" units={UNITS} />);

    expect(screen.getByRole("button", { name: /move to another parent/i })).toBeInTheDocument();
  });

  it("offers only non-archived, same-level units other than the current parent as candidates", async () => {
    const user = userEvent.setup();
    renderWithProviders(<MoveUnitPanel unitId="dila1" unitName="Ibadan Dila" parentId="zone1" path="nat.z01.d01" units={UNITS} />);

    await user.click(screen.getByRole("button", { name: /move to another parent/i }));
    const options = screen.getAllByRole("option").map((option) => option.textContent);

    expect(options).toContain("Z02 — Ogun Zone");
    expect(options).not.toContain("Z01 — Lagos Zone");
    expect(options).not.toContain("Z03 — Archived Zone");
  });

  it("submits the move with the unit's id and chosen parent", async () => {
    const user = userEvent.setup();
    renderWithProviders(<MoveUnitPanel unitId="dila1" unitName="Ibadan Dila" parentId="zone1" path="nat.z01.d01" units={UNITS} />);

    await user.click(screen.getByRole("button", { name: /move to another parent/i }));
    await user.selectOptions(screen.getByLabelText(/new parent/i), "zone2");
    await user.click(screen.getByRole("button", { name: /^move$/i }));

    expect(moveUnitMutate).toHaveBeenCalledWith(
      { id: "dila1", data: { newParentId: "zone2" } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });

  it("shows a confirmation after a successful move", async () => {
    moveUnitMutate.mockImplementation((_data, options) => {
      options.onSuccess();
    });
    const user = userEvent.setup();
    renderWithProviders(<MoveUnitPanel unitId="dila1" unitName="Ibadan Dila" parentId="zone1" path="nat.z01.d01" units={UNITS} />);

    await user.click(screen.getByRole("button", { name: /move to another parent/i }));
    await user.selectOptions(screen.getByLabelText(/new parent/i), "zone2");
    await user.click(screen.getByRole("button", { name: /^move$/i }));

    expect(screen.getByText(/unit moved/i)).toBeInTheDocument();
  });
});
