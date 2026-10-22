import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { greetingKey, relativeDueDay, SecretariatOverviewScreen } from "./SecretariatOverviewScreen";

const useGetSecretariatOverview = vi.fn();

vi.mock("@/lib/api/generated/dashboards/dashboards", () => ({
  useGetSecretariatOverview: (...args: unknown[]) => useGetSecretariatOverview(...args),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

const OVERVIEW = {
  userName: "Mariam Okafor",
  currentWindow: {
    windowId: "w9",
    reportTypeId: "t1",
    reportTypeName: "General Monthly Report",
    periodLabel: "September 2026",
    opensAt: "2026-10-01T08:00:00Z",
    isClosed: false,
  },
  received: 140,
  expected: 267,
  unreadInInbox: 48,
  assignedToMe: 12,
  nextDue: { level: 3, levelPluralName: "Muqamaat", dueAt: "2099-10-05T12:00:00Z", notSubmitted: 83 },
  vacantHeads: 5,
  byZone: [
    {
      level: 3,
      levelName: "Muqam",
      groups: [
        { groupUnitId: "z1", name: "Lagos", expected: 100, submitted: 74 },
        { groupUnitId: "z2", name: "North West", expected: 100, submitted: 38 },
      ],
    },
    { level: 2, levelName: "Dila", groups: [{ groupUnitId: "z1", name: "Lagos", expected: 10, submitted: 1 }] },
  ],
  byLevel: [
    { level: 3, levelName: "Muqam", levelPluralName: "Muqamaat", dueAt: "2026-10-05T12:00:00Z", expected: 214, submitted: 131 },
    { level: 2, levelName: "Dila", levelPluralName: "Dilaat", dueAt: "2026-10-10T12:00:00Z", expected: 38, submitted: 4 },
  ],
  upcomingWindows: [
    {
      windowId: "w10",
      kind: 0,
      at: "2026-11-01T08:00:00Z",
      reportTypeName: "General Monthly Report",
      periodLabel: "Oct 2026",
      levelNames: ["Muqam", "Dila", "Zone"],
      isScheduled: true,
    },
    {
      windowId: "w11",
      kind: 1,
      at: "2026-10-12T08:00:00Z",
      reportTypeName: "Youth Camp Attendance",
      periodLabel: "2026",
      levelNames: ["Muqam"],
      isScheduled: false,
    },
  ],
  flaggedReports: 7,
  returnedPastRevisionDeadline: 4,
};

beforeEach(() => {
  useGetSecretariatOverview.mockReset();
});

describe("SecretariatOverviewScreen", () => {
  it("greets the secretary and shows the current window's KPIs", () => {
    useGetSecretariatOverview.mockReturnValue({ isPending: false, isError: false, data: OVERVIEW });

    renderWithIntl(<SecretariatOverviewScreen />);

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(/, Mariam$/);
    expect(screen.getByText("September 2026 General Monthly Report · window open since 1 Oct")).toBeInTheDocument();
    expect(screen.getByText("140")).toBeInTheDocument();
    expect(screen.getByText("of 267 expected reports")).toBeInTheDocument();
    expect(screen.getByText("12 assigned to you")).toBeInTheDocument();
    expect(screen.getByText("Muqamaat due 5 Oct")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Assign heads →" })).toHaveAttribute("href", "/secretariat/organization");
    expect(screen.getByRole("link", { name: /open inbox/i })).toHaveAttribute("href", "/secretariat/inbox");
  });

  it("switches the by-zone chart between levels", async () => {
    useGetSecretariatOverview.mockReturnValue({ isPending: false, isError: false, data: OVERVIEW });

    renderWithIntl(<SecretariatOverviewScreen />);
    expect(screen.getByRole("heading", { name: "Muqam submissions by zone" })).toBeInTheDocument();
    expect(screen.getByText("74%")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Dila" }));
    expect(screen.getByRole("heading", { name: "Dila submissions by zone" })).toBeInTheDocument();
    expect(screen.getByText("10%")).toBeInTheDocument();
  });

  it("lists levels, upcoming windows, attention items and quick actions", () => {
    useGetSecretariatOverview.mockReturnValue({ isPending: false, isError: false, data: OVERVIEW });

    renderWithIntl(<SecretariatOverviewScreen />);

    expect(screen.getByRole("progressbar", { name: "Muqam" })).toHaveAttribute("aria-valuenow", "131");
    expect(screen.getByRole("link", { name: "Details" })).toHaveAttribute("href", "/secretariat/dashboards/compliance?reportTypeId=t1");
    expect(screen.getByText("Opens · Muqam, Dila, Zone · auto-scheduled")).toBeInTheDocument();
    expect(screen.getByText("Closes · Muqam only")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /7 reports flagged by reviewers/i })).toBeInTheDocument();
    expect(screen.getByText("4 returned reports past revision date")).toBeInTheDocument();
    expect(screen.getByText("5 units with no head")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Edit General Monthly Report template" })).toHaveAttribute("href", "/secretariat/report-types");
    expect(screen.getByRole("link", { name: "Compile September 2026 report" })).toHaveAttribute("href", "/secretariat/compiled/w9");
  });

  it("shows an empty state when no window has opened, and an error state with retry", async () => {
    useGetSecretariatOverview.mockReturnValue({
      isPending: false,
      isError: false,
      data: { ...OVERVIEW, currentWindow: null, nextDue: null, byZone: [], byLevel: [] },
    });
    const { unmount } = renderWithIntl(<SecretariatOverviewScreen />);
    expect(screen.getByText("No window has opened yet")).toBeInTheDocument();
    expect(within(screen.getByText("Next due date").parentElement!).getByText("—")).toBeInTheDocument();
    unmount();

    const refetch = vi.fn();
    useGetSecretariatOverview.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });
    renderWithIntl(<SecretariatOverviewScreen />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));
    expect(refetch).toHaveBeenCalled();
  });
});

describe("overview date helpers", () => {
  it("greets by the organization's clock (Africa/Lagos, UTC+1)", () => {
    expect(greetingKey(new Date("2026-10-19T07:30:00Z"))).toBe("greetingMorning");
    expect(greetingKey(new Date("2026-10-19T11:30:00Z"))).toBe("greetingAfternoon");
    expect(greetingKey(new Date("2026-10-19T17:30:00Z"))).toBe("greetingEvening");
  });

  it("names today and tomorrow by calendar day", () => {
    const now = new Date("2026-10-19T10:00:00Z");
    expect(relativeDueDay(new Date("2026-10-19T22:00:00Z"), now)).toBe("today");
    expect(relativeDueDay(new Date("2026-10-20T09:00:00Z"), now)).toBe("tomorrow");
    expect(relativeDueDay(new Date("2026-10-25T09:00:00Z"), now)).toBe("date");
  });
});
