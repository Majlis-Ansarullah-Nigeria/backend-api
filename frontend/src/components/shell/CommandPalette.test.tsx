import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { renderWithIntl } from "@/test/renderWithIntl";
import { CommandPalette } from "./CommandPalette";

const push = vi.fn();
const useGetMe = vi.fn();
const useGlobalSearch = vi.fn();

vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));
vi.mock("@/lib/api/generated/identity/identity", () => ({ useGetMe: (...args: unknown[]) => useGetMe(...args) }));
vi.mock("@/lib/api/generated/search/search", () => ({ useGlobalSearch: (...args: unknown[]) => useGlobalSearch(...args) }));
vi.mock("@/lib/api/generated/settings/settings", () => ({ useGetFeatureFlags: () => ({ data: { canEdit: false, flags: [] } }) }));

const RESULTS = {
  units: [{ id: "u3", name: "Zone 3", code: "Z03", levelName: "Zone", isObserved: false }],
  users: [],
  reports: [
    {
      instanceId: "r1",
      unitId: "u3",
      unitName: "Zone 3",
      reportTypeName: "General Monthly Report",
      periodLabel: "September 2026",
      referenceNumber: null,
      status: 2,
      access: 0,
    },
  ],
  reportTypes: [],
};

beforeAll(() => {
  // jsdom has no modal <dialog> support.
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.setAttribute("open", "");
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.removeAttribute("open");
  };
});

beforeEach(() => {
  push.mockReset();
  useGetMe.mockReturnValue({ data: { roleAssignments: [{ role: 2 }] } });
  useGlobalSearch.mockImplementation((params: { q: string }) => ({ data: params.q.length >= 2 ? RESULTS : undefined, isFetching: false }));
});

describe("CommandPalette", () => {
  it("lists the caller's quick actions and opens one", async () => {
    const onClose = vi.fn();
    renderWithIntl(<CommandPalette open onClose={onClose} />);

    expect(screen.getByRole("option", { name: "Open inbox" })).toHaveAttribute("aria-selected", "true");
    expect(screen.queryByRole("option", { name: "Go to my reports" })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("option", { name: "Create report type" }));

    expect(push).toHaveBeenCalledWith("/secretariat/report-types?new=1");
    expect(onClose).toHaveBeenCalled();
  });

  it("searches after a pause and opens a result with the keyboard", async () => {
    renderWithIntl(<CommandPalette open onClose={vi.fn()} />);

    await userEvent.type(screen.getByRole("combobox", { name: "Search units, reports, people" }), "zone 3");

    expect(await screen.findByRole("option", { name: /Open General Monthly Report · September 2026/ })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: /Go to Zone 3/ })).toBeInTheDocument();
    expect(useGlobalSearch).toHaveBeenLastCalledWith({ q: "zone 3" }, expect.anything());

    // No action matches "zone 3": the report comes first, so one step down is the unit.
    await userEvent.keyboard("{ArrowDown}{Enter}");
    expect(push).toHaveBeenCalledWith("/secretariat/organization?unit=u3");
  });

  it("says when nothing matches", async () => {
    useGlobalSearch.mockReturnValue({ data: { units: [], users: [], reports: [], reportTypes: [] }, isFetching: false });
    renderWithIntl(<CommandPalette open onClose={vi.fn()} />);

    await userEvent.type(screen.getByRole("combobox"), "qqqq");

    await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent("Nothing matches."));
  });
});
