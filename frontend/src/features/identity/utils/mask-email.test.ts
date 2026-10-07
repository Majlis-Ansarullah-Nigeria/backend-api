import { describe, expect, it } from "vitest";
import { maskEmail } from "./mask-email";

describe("maskEmail", () => {
  it("keeps the first 5 characters of the local part", () => {
    expect(maskEmail("a.adeyemi@example.org")).toBe("a.ade…@example.org");
  });

  it("keeps the whole local part when it's shorter than 5 characters", () => {
    expect(maskEmail("ab@example.org")).toBe("ab…@example.org");
  });

  it("returns the input unchanged when there's no @", () => {
    expect(maskEmail("not-an-email")).toBe("not-an-email");
  });
});
