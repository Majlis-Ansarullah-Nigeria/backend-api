import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../../messages/en.json";
import { FieldTargetsTab } from "./FieldTargetsTab";

const useGetReportType = vi.fn();
const useGetFieldTargets = vi.fn();
const mutate = vi.fn();

vi.mock("@/lib/api/generated/templates/templates", () => ({
  useGetReportType: (...args: unknown[]) => useGetReportType(...args),
  useGetFieldTargets: (...args: unknown[]) => useGetFieldTargets(...args),
  useSetFieldTargets: () => ({ mutate, isPending: false, error: null }),
  getGetFieldTargetsQueryKey: (id: string) => ["getFieldTargets", id],
}));

function renderWithProviders(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const LEVELS = [
  { id: "l-zone", code: 1, name: "Zone", pluralName: "Zones", depth: 1 },
  { id: "l-dila", code: 2, name: "Dila", pluralName: "Dilaat", depth: 2 },
  { id: "l-muqam", code: 3, name: "Muqam", pluralName: "Muqamaat", depth: 3 },
];

beforeEach(() => {
  mutate.mockReset();
  useGetReportType.mockReturnValue({ isPending: false, data: { applicableLevels: [2, 3] } });
  useGetFieldTargets.mockReturnValue({
    isPending: false,
    data: [
      { fieldKey: "members", level: 2, value: 120 },
      { fieldKey: "other", level: 3, value: 9 },
    ],
  });
});

describe("FieldTargetsTab", () => {
  it("offers a target for each level the report type is filed at, prefilled for this field", () => {
    renderWithProviders(<FieldTargetsTab reportTypeId="t1" fieldKey="members" levels={LEVELS} />);

    expect(screen.queryByLabelText("Zone target")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Dila target")).toHaveValue("120");
    expect(screen.getByLabelText("Muqam target")).toHaveValue("");
  });

  it("saves every level, sending blanks as cleared targets", async () => {
    renderWithProviders(<FieldTargetsTab reportTypeId="t1" fieldKey="members" levels={LEVELS} />);

    await userEvent.clear(screen.getByLabelText("Dila target"));
    await userEvent.type(screen.getByLabelText("Muqam target"), "20");
    await userEvent.click(screen.getByRole("button", { name: "Save targets" }));

    expect(mutate).toHaveBeenCalledWith(
      {
        id: "t1",
        fieldKey: "members",
        data: { targets: [{ level: 2, value: null }, { level: 3, value: 20 }] },
      },
      expect.anything(),
    );
  });

  it("refuses negative targets", async () => {
    renderWithProviders(<FieldTargetsTab reportTypeId="t1" fieldKey="members" levels={LEVELS} />);

    await userEvent.type(screen.getByLabelText("Muqam target"), "-5");

    expect(screen.getByText("Targets must be zero or more.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save targets" })).toBeDisabled();
  });
});
