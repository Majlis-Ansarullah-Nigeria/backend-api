import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ReadOnlyBanner } from "./ReadOnlyBanner";

describe("ReadOnlyBanner", () => {
  it("renders the default observer copy", () => {
    render(<ReadOnlyBanner />);
    expect(
      screen.getByText("Read-only view. You can see this report but not act on it."),
    ).toBeInTheDocument();
  });

  it("renders custom content when provided", () => {
    render(<ReadOnlyBanner>Custom notice</ReadOnlyBanner>);
    expect(screen.getByText("Custom notice")).toBeInTheDocument();
  });
});
