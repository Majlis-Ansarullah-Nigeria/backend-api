import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderWithIntl } from "@/test/renderWithIntl";
import { TopBar } from "./TopBar";

describe("TopBar", () => {
  const user = { name: "Abdulrahman Adeyemi", initials: "AA" };

  it("renders the page title", () => {
    renderWithIntl(<TopBar title="Home" user={user} />);
    expect(screen.getByText("Home")).toBeInTheDocument();
  });

  it("shows the unread notification count when greater than zero", () => {
    renderWithIntl(<TopBar title="Home" user={user} notificationCount={3} />);
    expect(screen.getByRole("button", { name: /3 unread/i })).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("omits the badge when there are no notifications", () => {
    renderWithIntl(<TopBar title="Home" user={user} notificationCount={0} />);
    expect(screen.getByRole("button", { name: "Notifications" })).toBeInTheDocument();
  });
});
