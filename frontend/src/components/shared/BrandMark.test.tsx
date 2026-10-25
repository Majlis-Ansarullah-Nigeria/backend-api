import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { BrandMark, DEFAULT_LOGO_SRC } from "./BrandMark";

describe("BrandMark", () => {
  it("shows the organization's built-in logo until one is uploaded", () => {
    const { container } = render(<BrandMark />);
    const img = container.querySelector("img")!;
    expect(img).toHaveAttribute("src", DEFAULT_LOGO_SRC);
    // Decorative: the organization's name is right beside it.
    expect(img).toHaveAttribute("alt", "");
  });

  it("shows the logo uploaded in Settings instead (FR-ADM-001)", () => {
    const { container } = render(<BrandMark logoUrl="/api/v1/settings/logo?v=3" />);
    expect(container.querySelector("img")).toHaveAttribute("src", "/api/v1/settings/logo?v=3");
  });
});
