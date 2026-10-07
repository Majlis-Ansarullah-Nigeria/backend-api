import { describe, expect, it } from "vitest";
import type { UserListItem } from "@/lib/api/generated/hRSApiV1.schemas";
import { USER_STATUS, filterUsersByTab, searchUsers, statusLabel } from "./users";

function user(overrides: Partial<UserListItem> & { name: string }): UserListItem {
  return {
    userId: "u1",
    invitationId: null,
    email: `${overrides.name.toLowerCase()}@example.org`,
    lastLoginAt: null,
    twoFactorEnabled: false,
    status: USER_STATUS.Active,
    roleAssignments: [],
    ...overrides,
  };
}

const ACTIVE = user({ name: "Amina" });
const INVITED = user({ name: "Bola", status: USER_STATUS.Invited, userId: null, invitationId: "i1" });
const DEACTIVATED = user({ name: "Chidi", status: USER_STATUS.Deactivated });
const WITH_UNIT = user({
  name: "Dara",
  roleAssignments: [
    { id: "r1", role: 0, delegateScope: null, unitId: "un1", unitName: "Ibadan Dila", unitDisplayCode: "SW-IBD", startsAt: "2026-01-01T00:00:00Z", endsAt: null, isActive: true },
  ],
});

const ALL = [ACTIVE, INVITED, DEACTIVATED, WITH_UNIT];

describe("filterUsersByTab", () => {
  it("'all' returns everyone", () => {
    expect(filterUsersByTab(ALL, "all")).toEqual(ALL);
  });

  it("'active' returns only active accounts", () => {
    expect(filterUsersByTab(ALL, "active")).toEqual([ACTIVE, WITH_UNIT]);
  });

  it("'invited' returns only pending invitations", () => {
    expect(filterUsersByTab(ALL, "invited")).toEqual([INVITED]);
  });

  it("'deactivated' returns only deactivated accounts", () => {
    expect(filterUsersByTab(ALL, "deactivated")).toEqual([DEACTIVATED]);
  });
});

describe("searchUsers", () => {
  it("matches by name", () => {
    expect(searchUsers(ALL, "amina")).toEqual([ACTIVE]);
  });

  it("matches by email", () => {
    expect(searchUsers(ALL, "bola@example.org")).toEqual([INVITED]);
  });

  it("matches by an assigned unit's name", () => {
    expect(searchUsers(ALL, "ibadan")).toEqual([WITH_UNIT]);
  });

  it("returns everyone for an empty search", () => {
    expect(searchUsers(ALL, "  ")).toEqual(ALL);
  });
});

describe("statusLabel", () => {
  it("labels every status", () => {
    expect(statusLabel(USER_STATUS.Active)).toBe("Active");
    expect(statusLabel(USER_STATUS.Invited)).toBe("Invited");
    expect(statusLabel(USER_STATUS.Deactivated)).toBe("Deactivated");
  });
});
