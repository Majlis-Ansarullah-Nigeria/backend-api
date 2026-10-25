import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { ReportVersionsPanel } from "./ReportVersionsPanel";

const useGetReportRevisions = vi.fn();

vi.mock("@/lib/api/generated/history/history", () => ({
  useGetReportRevisions: (...args: unknown[]) => useGetReportRevisions(...args),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

const HISTORY = {
  reportId: "r1",
  reportTypeName: "General Monthly Report",
  periodLabel: "Sep 2026",
  unitName: "Agodi Muqam",
  revisions: [
    {
      revisionNo: 3,
      action: 2,
      createdAt: "2026-10-04T09:00:00Z",
      actorName: "Amina Bello",
      changes: [{ sectionTitle: "Membership", fieldKey: "members", label: "Total members", before: "142", after: "148" }],
    },
    { revisionNo: 2, action: 1, createdAt: "2026-10-03T09:00:00Z", actorName: "Amina Bello", changes: [] },
    { revisionNo: 1, action: 0, createdAt: "2026-10-02T09:00:00Z", actorName: null, changes: [] },
  ],
};

beforeEach(() => {
  useGetReportRevisions.mockReset();
});

describe("ReportVersionsPanel", () => {
  it("lists every version newest first with what changed in each", () => {
    useGetReportRevisions.mockReturnValue({ isPending: false, isError: false, data: HISTORY });

    renderWithIntl(<ReportVersionsPanel reportId="r1" />);

    expect(screen.getByText("Agodi Muqam · General Monthly Report · Sep 2026")).toBeInTheDocument();
    const items = screen.getAllByRole("listitem");
    expect(items.map((item) => within(item).getByText(/^Version \d/).textContent)).toEqual([
      "Version 3 · Resubmitted",
      "Version 2 · Withdrawn",
      "Version 1 · Submitted",
    ]);

    const changes = within(items[0]).getByRole("table", { name: /changes in version 3/i });
    expect(within(changes).getByRole("rowheader", { name: /total members/i })).toBeInTheDocument();
    expect(within(changes).getByText("142")).toBeInTheDocument();
    expect(within(changes).getByText("148")).toBeInTheDocument();
    expect(within(items[0]).getByText(/amina bello/i)).toBeInTheDocument();

    expect(within(items[1]).getByText(/no answers changed/i)).toBeInTheDocument();
    expect(within(items[2]).queryByText(/no answers changed/i)).not.toBeInTheDocument();
  });

  it("is a section heading in the review pane and the page heading on its own screen (NFR-ACC-01)", () => {
    useGetReportRevisions.mockReturnValue({ isPending: false, isError: false, data: HISTORY });

    const { unmount } = renderWithIntl(<ReportVersionsPanel reportId="r1" />);
    expect(screen.getByRole("heading", { level: 2, name: "Version history" })).toBeInTheDocument();
    unmount();

    renderWithIntl(<ReportVersionsPanel reportId="r1" headingLevel={1} />);
    expect(screen.getByRole("heading", { level: 1, name: "Version history" })).toBeInTheDocument();
  });

  it("shows an empty state when nothing was recorded", () => {
    useGetReportRevisions.mockReturnValue({ isPending: false, isError: false, data: { ...HISTORY, revisions: [] } });

    renderWithIntl(<ReportVersionsPanel reportId="r1" />);

    expect(screen.getByText(/no version history recorded/i)).toBeInTheDocument();
  });

  it("shows an error state with retry", async () => {
    const refetch = vi.fn();
    useGetReportRevisions.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithIntl(<ReportVersionsPanel reportId="r1" />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });
});
