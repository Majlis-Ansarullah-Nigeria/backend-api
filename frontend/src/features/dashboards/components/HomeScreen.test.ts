import { describe, expect, it } from "vitest";
import type { MeProfile } from "@/lib/api/generated/hRSApiV1.schemas";
import { isLeadershipHome } from "./HomeScreen";

const assignment = (id: string, role: number) => ({ id, unitId: "u", role, delegateScope: null, startsAt: "2026-01-01T00:00:00Z", endsAt: null });

const me = (roles: number[], active: string | null): MeProfile => ({
  id: "me",
  name: "Ibrahim Adewale",
  email: "i@example.org",
  activeRoleAssignmentId: active,
  roleAssignments: roles.map((role, index) => assignment(`a${index}`, role)),
  mfaEnabled: false,
  mfaRequiredByRole: false,
});

describe("isLeadershipHome", () => {
  it("follows the active assignment's role", () => {
    expect(isLeadershipHome(me([3, 0], "a0"))).toBe(true);
    expect(isLeadershipHome(me([3, 0], "a1"))).toBe(false);
  });

  it("without an active assignment, only when every assignment is Leadership", () => {
    expect(isLeadershipHome(me([3], null))).toBe(true);
    expect(isLeadershipHome(me([3, 0], null))).toBe(false);
    expect(isLeadershipHome(me([], null))).toBe(false);
    expect(isLeadershipHome(undefined)).toBe(false);
  });
});
