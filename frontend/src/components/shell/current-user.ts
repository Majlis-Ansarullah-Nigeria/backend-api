import { ROLE_NAME } from "@/features/admin/utils/role-name";
import type { MeProfile, RoleAssignmentSummary } from "@/lib/api/generated/hRSApiV1.schemas";

/** "Amina Bello" → "AB"; one name → its first two letters. */
export function initials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) {
    return "";
  }
  if (words.length === 1) {
    return words[0].slice(0, 2).toUpperCase();
  }
  return (words[0][0] + words[words.length - 1][0]).toUpperCase();
}

/** The assignment the user is acting as: the one they chose (FR-AC-002), else their first. */
export function activeAssignment(me: MeProfile | undefined): RoleAssignmentSummary | undefined {
  if (!me) {
    return undefined;
  }
  return me.roleAssignments.find((a) => a.id === me.activeRoleAssignmentId) ?? me.roleAssignments[0];
}

export type RoleLabelKey = "unitHead" | "delegate" | "secretariat" | "leadership" | "deptOfficer" | "sysAdmin";

/** Which Shell.roles message labels an assignment; head and delegate labels take the unit's level
 * name ("Muqam Head"), which is organization data, never hard-coded. */
export function roleLabelKey(assignment: RoleAssignmentSummary): RoleLabelKey {
  switch (Number(assignment.role)) {
    case ROLE_NAME.UnitHead:
      return "unitHead";
    case ROLE_NAME.Delegate:
      return "delegate";
    case ROLE_NAME.Secretariat:
      return "secretariat";
    case ROLE_NAME.Leadership:
      return "leadership";
    case ROLE_NAME.DeptOfficer:
      return "deptOfficer";
    default:
      return "sysAdmin";
  }
}
