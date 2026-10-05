import type { ReactNode } from "react";
import { AppShell } from "@/components/shell/AppShell";

// Session-driven user/unit/notification data lands with auth (M1) and
// role assignments (M1.4/M4). This is placeholder data for the shell only.
const PLACEHOLDER_USER = { name: "Abdulrahman Adeyemi", initials: "AA" };
const PLACEHOLDER_UNIT = { id: "agodi-muqam", name: "Agodi Muqam", role: "Muqam Head" };

export default function AppLayout({ children }: { children: ReactNode }) {
  return (
    <AppShell user={PLACEHOLDER_USER} unit={PLACEHOLDER_UNIT} notificationCount={3}>
      {children}
    </AppShell>
  );
}
