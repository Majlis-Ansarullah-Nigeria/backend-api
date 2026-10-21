import { redirect } from "next/navigation";

// Compliance is the only dashboard so far; indicator dashboards land in M10.2.
export default function DashboardsPage() {
  redirect("/secretariat/dashboards/compliance");
}
