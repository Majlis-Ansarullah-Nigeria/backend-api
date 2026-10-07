import type { UserListItem } from "@/lib/api/generated/hRSApiV1.schemas";

// Matches backend/src/HRS.Application/Features/Admin/Users/GetUsersQuery.cs's enum order.
export const USER_STATUS = {
  Active: 0,
  Invited: 1,
  Deactivated: 2,
} as const;

export type UserStatusTab = "all" | "active" | "invited" | "deactivated";

export function filterUsersByTab(users: UserListItem[], tab: UserStatusTab): UserListItem[] {
  switch (tab) {
    case "active":
      return users.filter((u) => u.status === USER_STATUS.Active);
    case "invited":
      return users.filter((u) => u.status === USER_STATUS.Invited);
    case "deactivated":
      return users.filter((u) => u.status === USER_STATUS.Deactivated);
    default:
      return users;
  }
}

export function searchUsers(users: UserListItem[], search: string): UserListItem[] {
  const needle = search.trim().toLowerCase();
  if (needle === "") {
    return users;
  }

  return users.filter(
    (u) =>
      u.name.toLowerCase().includes(needle) ||
      u.email.toLowerCase().includes(needle) ||
      u.roleAssignments.some((a) => a.unitName.toLowerCase().includes(needle)),
  );
}

export function statusLabel(status: number): string {
  switch (status) {
    case USER_STATUS.Active:
      return "Active";
    case USER_STATUS.Invited:
      return "Invited";
    case USER_STATUS.Deactivated:
      return "Deactivated";
    default:
      return "Unknown";
  }
}
