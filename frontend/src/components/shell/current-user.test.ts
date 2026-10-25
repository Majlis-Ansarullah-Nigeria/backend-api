import { describe, expect, it } from "vitest";
import type { MeProfile, RoleAssignmentSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { activeAssignment, initials, roleLabelKey } from "./current-user";

const assignment = (id: string, role: number): RoleAssignmentSummary => ({
  id,
  unitId: `u-${id}`,
  role,
  delegateScope: null,
  startsAt: "2026-01-01T00:00:00Z",
  endsAt: null,
  unitName: `Unit ${id}`,
  unitLevel: 3,
  unitLevelName: "Muqam",
});

const me = (activeRoleAssignmentId: string | null, roleAssignments: RoleAssignmentSummary[]): MeProfile => ({
  id: "user-1",
  name: "Amina Bello",
  email: "amina@example.org",
  activeRoleAssignmentId,
  roleAssignments,
  mfaEnabled: false,
  mfaRequiredByRole: false,
});

describe("current user", () => {
  it("makes initials from the first and last names", () => {
    expect(initials("Amina Bello")).toBe("AB");
    expect(initials("  Abdul Rahman  Adeyemi ")).toBe("AA");
    expect(initials("Secretariat")).toBe("SE");
    expect(initials("")).toBe("");
  });

  it("acts as the chosen assignment, falling back to the first", () => {
    const assignments = [assignment("a", 0), assignment("b", 2)];
    expect(activeAssignment(me("b", assignments))?.id).toBe("b");
    expect(activeAssignment(me(null, assignments))?.id).toBe("a");
    expect(activeAssignment(me("gone", assignments))?.id).toBe("a");
    expect(activeAssignment(me(null, []))).toBeUndefined();
    expect(activeAssignment(undefined)).toBeUndefined();
  });

  it("labels each role", () => {
    expect([0, 1, 2, 3, 4, 5].map((role) => roleLabelKey(assignment("x", role)))).toEqual([
      "unitHead",
      "delegate",
      "secretariat",
      "leadership",
      "deptOfficer",
      "sysAdmin",
    ]);
  });
});
