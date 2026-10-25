import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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

  it("signs out from the sign-out button", async () => {
    const onSignOut = vi.fn();
    renderWithIntl(<Sidebar user={user} unit={unit} onSignOut={onSignOut} />);

    await userEvent.click(screen.getByRole("button", { name: "Sign out" }));

    expect(onSignOut).toHaveBeenCalled();
  });

  it("switches to another unit the user can act as (FR-AC-002)", async () => {
    const onSelectUnit = vi.fn();
    const dila = { id: "ibafo-dila", name: "Ibafo Dila", role: "Dila Delegate" };
    renderWithIntl(<Sidebar user={user} unit={unit} unitOptions={[unit, dila]} onSelectUnit={onSelectUnit} />);

    await userEvent.click(screen.getByRole("button", { name: /acting as/i }));
    await userEvent.click(await screen.findByRole("menuitem", { name: /Ibafo Dila/ }));

    expect(onSelectUnit).toHaveBeenCalledWith(dila);
  });
});
