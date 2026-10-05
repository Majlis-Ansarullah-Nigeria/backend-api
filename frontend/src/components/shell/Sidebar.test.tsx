import { screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { renderWithIntl } from "@/test/renderWithIntl";
import { Sidebar } from "./Sidebar";

vi.mock("next/navigation", () => ({
  usePathname: () => "/home",
}));

describe("Sidebar", () => {
  const user = { name: "Abdulrahman Adeyemi", initials: "AA" };
  const unit = { id: "agodi-muqam", name: "Agodi Muqam", role: "Muqam Head" };

  it("marks the active route with aria-current and the others not", () => {
    renderWithIntl(<Sidebar user={user} unit={unit} />);

    expect(screen.getByRole("link", { name: /home/i })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: /my reports/i })).not.toHaveAttribute("aria-current");
  });

  it("shows the unit switcher with the current unit and role", () => {
    renderWithIntl(<Sidebar user={user} unit={unit} />);

    expect(screen.getByText("Agodi Muqam")).toBeInTheDocument();
    expect(screen.getByText("Muqam Head")).toBeInTheDocument();
  });

  it("shows the signed-in user", () => {
    renderWithIntl(<Sidebar user={user} unit={unit} />);
    expect(screen.getByText("Abdulrahman Adeyemi")).toBeInTheDocument();
  });
});
