import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { OrganizationScreen } from "./OrganizationScreen";

function renderWithIntl(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const useGetOrganizationTree = vi.fn();
const useGetUnitLevels = vi.fn();
const useGetUnit = vi.fn();

vi.mock("@/lib/api/generated/organization/organization", () => ({
  useGetOrganizationTree: (...args: unknown[]) => useGetOrganizationTree(...args),
  useGetUnitLevels: (...args: unknown[]) => useGetUnitLevels(...args),
  useGetUnit: (...args: unknown[]) => useGetUnit(...args),
  useCreateUnit: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useUpdateUnit: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useArchiveUnit: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useRestoreUnit: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  getGetOrganizationTreeQueryKey: () => ["getOrganizationTree"],
  getGetUnitQueryKey: (id: string) => ["getUnit", id],
}));

const LEVELS = [
  { id: "l0", code: 0, name: "National", pluralName: "National", depth: 0 },
  { id: "l1", code: 1, name: "Zone", pluralName: "Zones", depth: 1 },
  { id: "l3", code: 3, name: "Muqam", pluralName: "Muqamaat", depth: 3 },
];

const NATIONAL = {
  id: "national",
  code: "NAT",
  displayCode: "NAT",
  name: "National",
  levelId: "l0",
  levelCode: 0,
  parentId: null,
  path: "nat",
  archivedAt: null,
  headName: null,
  hasVacantHead: true,
};

const ZONE = {
  id: "zone",
  code: "Z01",
  displayCode: "Z01",
  name: "Lagos Zone",
  levelId: "l1",
  levelCode: 1,
  parentId: "national",
  path: "nat.z01",
  archivedAt: null,
  headName: "O. Balogun",
  hasVacantHead: false,
};

const MUQAM = {
  id: "muqam",
  code: "M01",
  displayCode: "Z01-M01",
  name: "Iwo Road Muqam",
  levelId: "l3",
  levelCode: 3,
  parentId: "zone",
  path: "nat.z01.m01",
  archivedAt: null,
  headName: null,
  hasVacantHead: true,
};

beforeEach(() => {
  useGetOrganizationTree.mockReset();
  useGetUnitLevels.mockReset();
  useGetUnit.mockReset();
  useGetUnitLevels.mockReturnValue({ isPending: false, isError: false, data: LEVELS });
  useGetUnit.mockReturnValue({ isPending: true, isError: false, data: undefined });
});

describe("OrganizationScreen", () => {
  it("shows a loading message while the tree is pending", () => {
    useGetOrganizationTree.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithIntl(<OrganizationScreen />);

    expect(screen.getByText(/loading the organization/i)).toBeInTheDocument();
  });

  it("shows an error state with retry when the tree fails to load", async () => {
    const refetch = vi.fn();
    useGetOrganizationTree.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithIntl(<OrganizationScreen />);
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("shows the level-count summary and auto-expands the root", () => {
    useGetOrganizationTree.mockReturnValue({
      isPending: false,
      isError: false,
      data: [NATIONAL, ZONE, MUQAM],
    });

    renderWithIntl(<OrganizationScreen />);

    expect(screen.getByText("1 zones · 1 muqamaat")).toBeInTheDocument();
    // The root auto-expands on load, so its direct child is visible without a click.
    expect(screen.getByText("Lagos Zone")).toBeInTheDocument();
    // The Muqam is two levels deep and stays collapsed until Lagos Zone is expanded.
    expect(screen.queryByText("Iwo Road Muqam")).not.toBeInTheDocument();
  });

  it("filters the tree by search while keeping ancestors visible", async () => {
    useGetOrganizationTree.mockReturnValue({
      isPending: false,
      isError: false,
      data: [NATIONAL, ZONE, MUQAM],
    });
    const user = userEvent.setup();

    renderWithIntl(<OrganizationScreen />);
    await user.type(screen.getByRole("searchbox", { name: /find a zone/i }), "Iwo Road");

    expect(screen.getByText("Iwo Road Muqam")).toBeInTheDocument();
    expect(screen.getByText("Lagos Zone")).toBeInTheDocument();
  });

  it("shows the vacant-heads count on its filter tab", () => {
    useGetOrganizationTree.mockReturnValue({
      isPending: false,
      isError: false,
      data: [NATIONAL, ZONE, MUQAM],
    });

    renderWithIntl(<OrganizationScreen />);

    // National and the Muqam both have no active head; Lagos Zone does.
    expect(screen.getByRole("tab", { name: /vacant heads · 2/i })).toBeInTheDocument();
  });
});
