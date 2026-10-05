import { screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { renderWithIntl } from "@/test/renderWithIntl";
import { BottomTabs } from "./BottomTabs";

vi.mock("next/navigation", () => ({
  usePathname: () => "/reports",
}));

describe("BottomTabs", () => {
  it("renders all five tabs with short labels", () => {
    renderWithIntl(<BottomTabs />);

    expect(screen.getByRole("link", { name: "Home" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Reports" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "History" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Alerts" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Me" })).toBeInTheDocument();
  });

  it("marks the tab matching the current route as active", () => {
    renderWithIntl(<BottomTabs />);

    expect(screen.getByRole("link", { name: "Reports" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "Home" })).not.toHaveAttribute("aria-current");
  });
});
