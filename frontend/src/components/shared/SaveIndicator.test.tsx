import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SaveIndicator } from "./SaveIndicator";

describe("SaveIndicator", () => {
  it("shows the relative saved time", () => {
    render(<SaveIndicator status="saved" savedLabel="8 seconds" />);
    expect(screen.getByText("Saved 8 seconds ago")).toBeInTheDocument();
  });

  it("shows a saving state", () => {
    render(<SaveIndicator status="saving" />);
    expect(screen.getByText("Saving…")).toBeInTheDocument();
  });

  it("shows an offline state distinguishable from saved without colour", () => {
    render(<SaveIndicator status="offline" />);
    expect(screen.getByText(/offline/i)).toBeInTheDocument();
  });

  it("shows an error state", () => {
    render(<SaveIndicator status="error" />);
    expect(screen.getByText(/couldn't save/i)).toBeInTheDocument();
  });
});
