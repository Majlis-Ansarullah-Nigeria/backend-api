import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../../messages/en.json";
import { TemplateBuilderScreen } from "./TemplateBuilderScreen";

const useGetReportType = vi.fn();
const useGetTemplateVersions = vi.fn();
const useGetTemplateVersion = vi.fn();
const updateTemplateSchemaMutate = vi.fn();
const publishTemplateVersionMutate = vi.fn();
const useGetUnitLevels = vi.fn();
const useGetOrganizationTree = vi.fn();

vi.mock("@/lib/api/generated/templates/templates", () => ({
  useGetReportType: (...args: unknown[]) => useGetReportType(...args),
  useGetTemplateVersions: (...args: unknown[]) => useGetTemplateVersions(...args),
  useGetTemplateVersion: (...args: unknown[]) => useGetTemplateVersion(...args),
  useUpdateTemplateSchema: () => ({ mutate: updateTemplateSchemaMutate, isPending: false, isError: false }),
  usePublishTemplateVersion: () => ({ mutate: publishTemplateVersionMutate, isPending: false, isError: false }),
  getGetTemplateVersionsQueryKey: (id: string) => ["getTemplateVersions", id],
}));

vi.mock("@/lib/api/generated/organization/organization", () => ({
  useGetUnitLevels: (...args: unknown[]) => useGetUnitLevels(...args),
  useGetOrganizationTree: (...args: unknown[]) => useGetOrganizationTree(...args),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const LEVELS = [
  { id: "l1", code: 1, name: "Zone", pluralName: "Zones", depth: 1 },
  { id: "l3", code: 3, name: "Muqam", pluralName: "Muqamaat", depth: 3 },
];

const REPORT_TYPE = {
  id: "rt1",
  name: "General Monthly Report",
  description: null,
  category: 0,
  frequency: 0,
  applicableLevels: [3],
  recipientLevel: 0,
  observerDepth: 1,
  allowAttachments: true,
  allowNilReport: false,
  createdAt: "2026-01-01T00:00:00Z",
  archivedAt: null,
};

const DRAFT_SUMMARY = {
  id: "v1",
  reportTypeId: "rt1",
  versionNumber: 1,
  status: 0,
  createdAt: "2026-01-01T00:00:00Z",
  publishedAt: null,
  retiredAt: null,
};

const EMPTY_SCHEMA_DETAIL = {
  id: "v1",
  reportTypeId: "rt1",
  versionNumber: 1,
  status: 0,
  schema: { sections: [] },
  createdAt: "2026-01-01T00:00:00Z",
  publishedAt: null,
  retiredAt: null,
};

beforeEach(() => {
  useGetReportType.mockReset();
  useGetTemplateVersions.mockReset();
  useGetTemplateVersion.mockReset();
  updateTemplateSchemaMutate.mockReset();
  publishTemplateVersionMutate.mockReset();
  useGetUnitLevels.mockReset();
  useGetOrganizationTree.mockReset();

  useGetReportType.mockReturnValue({ isPending: false, isError: false, data: REPORT_TYPE, refetch: vi.fn() });
  useGetUnitLevels.mockReturnValue({ isPending: false, isError: false, data: LEVELS, refetch: vi.fn() });
  useGetOrganizationTree.mockReturnValue({ isPending: false, isError: false, data: [], refetch: vi.fn() });
  useGetTemplateVersion.mockReturnValue({ isPending: false, isError: false, data: undefined, refetch: vi.fn() });
});

describe("TemplateBuilderScreen", () => {
  it("shows a loading message while the report type loads", () => {
    useGetReportType.mockReturnValue({ isPending: true, isError: false, data: undefined, refetch: vi.fn() });
    useGetTemplateVersions.mockReturnValue({ isPending: true, isError: false, data: undefined, refetch: vi.fn() });

    renderWithProviders(<TemplateBuilderScreen reportTypeId="rt1" />);

    expect(screen.getByText(/loading template/i)).toBeInTheDocument();
  });

  it("shows 'nothing published yet' when there are no versions", () => {
    useGetTemplateVersions.mockReturnValue({ isPending: false, isError: false, data: [], refetch: vi.fn() });

    renderWithProviders(<TemplateBuilderScreen reportTypeId="rt1" />);

    expect(screen.getByText(/nothing published yet/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /publish v/i })).not.toBeInTheDocument();
  });

  it("shows the draft badge and a publish button when a draft exists", () => {
    useGetTemplateVersions.mockReturnValue({ isPending: false, isError: false, data: [DRAFT_SUMMARY], refetch: vi.fn() });
    useGetTemplateVersion.mockReturnValue({ isPending: false, isError: false, data: EMPTY_SCHEMA_DETAIL, refetch: vi.fn() });

    renderWithProviders(<TemplateBuilderScreen reportTypeId="rt1" />);

    expect(screen.getByText(/draft v1/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /publish v1/i })).toBeInTheDocument();
  });

  it("publishes the draft when the header button is clicked", async () => {
    useGetTemplateVersions.mockReturnValue({ isPending: false, isError: false, data: [DRAFT_SUMMARY], refetch: vi.fn() });
    useGetTemplateVersion.mockReturnValue({ isPending: false, isError: false, data: EMPTY_SCHEMA_DETAIL, refetch: vi.fn() });
    const user = userEvent.setup();

    renderWithProviders(<TemplateBuilderScreen reportTypeId="rt1" />);
    await user.click(screen.getByRole("button", { name: /publish v1/i }));

    expect(publishTemplateVersionMutate).toHaveBeenCalledWith(
      { reportTypeId: "rt1", versionId: "v1" },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });

  it("adds a field from the palette, selects it, and edits its label", async () => {
    useGetTemplateVersions.mockReturnValue({ isPending: false, isError: false, data: [] });
    const user = userEvent.setup();

    renderWithProviders(<TemplateBuilderScreen reportTypeId="rt1" />);

    await user.click(screen.getByRole("button", { name: /add section/i }));
    await user.click(screen.getByRole("button", { name: /^short text$/i }));

    expect(screen.getByText("New field")).toBeInTheDocument();

    const labelInput = await screen.findByLabelText(/^label$/i);
    await user.clear(labelInput);
    await user.type(labelInput, "Full name");

    expect(await screen.findByText("Full name")).toBeInTheDocument();
  });

  it("switches to the Preview tab and renders the schema via the form engine", async () => {
    useGetTemplateVersions.mockReturnValue({ isPending: false, isError: false, data: [DRAFT_SUMMARY] });
    useGetTemplateVersion.mockReturnValue({
      isPending: false,
      isError: false,
      data: {
        ...EMPTY_SCHEMA_DETAIL,
        schema: { sections: [{ key: "s1", title: "Membership", fields: [] }] },
      },
    });
    const user = userEvent.setup();

    renderWithProviders(<TemplateBuilderScreen reportTypeId="rt1" />);
    await user.click(screen.getByRole("tab", { name: /preview/i }));

    expect(screen.getByText("Membership")).toBeInTheDocument();
  });

  it("switches to the Versions tab and shows the version list", async () => {
    useGetTemplateVersions.mockReturnValue({ isPending: false, isError: false, data: [DRAFT_SUMMARY] });
    useGetTemplateVersion.mockReturnValue({ isPending: false, isError: false, data: EMPTY_SCHEMA_DETAIL });
    const user = userEvent.setup();

    renderWithProviders(<TemplateBuilderScreen reportTypeId="rt1" />);
    await user.click(screen.getByRole("tab", { name: /^versions$/i }));

    await waitFor(() => expect(screen.getAllByText(/v1/i).length).toBeGreaterThan(0));
  });
});
