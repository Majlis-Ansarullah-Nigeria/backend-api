// Matches backend/src/HRS.Domain/Access/RoleName.cs and DelegateScope.cs's enum order —
// the API serializes both as plain numbers.
export const ROLE_NAME = {
  UnitHead: 0,
  Delegate: 1,
  Secretariat: 2,
  Leadership: 3,
  DeptOfficer: 4,
  SysAdmin: 5,
} as const;

export const DELEGATE_SCOPE = {
  DraftOnly: 0,
  DraftAndSubmit: 1,
} as const;

const ROLE_LABELS: Record<number, string> = {
  [ROLE_NAME.UnitHead]: "Unit Head",
  [ROLE_NAME.Delegate]: "Delegate",
  [ROLE_NAME.Secretariat]: "Secretariat",
  [ROLE_NAME.Leadership]: "Leadership",
  [ROLE_NAME.DeptOfficer]: "Dept. Officer",
  [ROLE_NAME.SysAdmin]: "Sys Admin",
};

const DELEGATE_SCOPE_LABELS: Record<number, string> = {
  [DELEGATE_SCOPE.DraftOnly]: "Draft only",
  [DELEGATE_SCOPE.DraftAndSubmit]: "Draft and submit",
};

export function roleLabel(role: number): string {
  return ROLE_LABELS[role] ?? "Unknown role";
}

export function delegateScopeLabel(scope: number): string {
  return DELEGATE_SCOPE_LABELS[scope] ?? "Unknown scope";
}
