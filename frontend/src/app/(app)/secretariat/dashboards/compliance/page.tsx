import { ComplianceDashboardScreen } from "@/features/dashboards/components/ComplianceDashboardScreen";

export default async function ComplianceDashboardPage({
  searchParams,
}: {
  searchParams: Promise<{ reportTypeId?: string; level?: string }>;
}) {
  const { reportTypeId, level } = await searchParams;
  const parsedLevel = level == null || level === "" || Number.isNaN(Number(level)) ? undefined : Number(level);

  return <ComplianceDashboardScreen initial={{ reportTypeId, level: parsedLevel }} />;
}
