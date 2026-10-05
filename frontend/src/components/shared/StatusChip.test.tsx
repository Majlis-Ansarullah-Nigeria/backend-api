import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { StatusChip } from "./StatusChip";

describe("StatusChip", () => {
  it("renders the default label and an icon for each status", () => {
    render(<StatusChip status="acknowledged" />);
    expect(screen.getByText("Acknowledged")).toBeInTheDocument();
  });

  it("renders an svg icon alongside the text (never colour alone)", () => {
    const { container } = render(<StatusChip status="overdue" />);
    expect(container.querySelector("svg")).toBeInTheDocument();
    expect(screen.getByText("Overdue")).toBeInTheDocument();
  });

  it("allows overriding the label", () => {
    render(<StatusChip status="late" label="2 days late" />);
    expect(screen.getByText("2 days late")).toBeInTheDocument();
    expect(screen.queryByText("Submitted late")).not.toBeInTheDocument();
  });
});
