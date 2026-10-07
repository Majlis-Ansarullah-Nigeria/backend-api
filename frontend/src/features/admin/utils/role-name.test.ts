import { describe, expect, it } from "vitest";
import { DELEGATE_SCOPE, ROLE_NAME, delegateScopeLabel, roleLabel } from "./role-name";

describe("roleLabel", () => {
  it("labels every known role", () => {
    expect(roleLabel(ROLE_NAME.UnitHead)).toBe("Unit Head");
    expect(roleLabel(ROLE_NAME.Delegate)).toBe("Delegate");
    expect(roleLabel(ROLE_NAME.Secretariat)).toBe("Secretariat");
    expect(roleLabel(ROLE_NAME.SysAdmin)).toBe("Sys Admin");
  });

  it("falls back for an unknown value", () => {
    expect(roleLabel(999)).toBe("Unknown role");
  });
});

describe("delegateScopeLabel", () => {
  it("labels both scopes", () => {
    expect(delegateScopeLabel(DELEGATE_SCOPE.DraftOnly)).toBe("Draft only");
    expect(delegateScopeLabel(DELEGATE_SCOPE.DraftAndSubmit)).toBe("Draft and submit");
  });
});
