import { ChildReportScreen } from "@/features/observer/components/ChildReportScreen";

export default async function ChildReportPage({
  params,
}: {
  params: Promise<{ unitId: string; id: string }>;
}) {
  const { unitId, id } = await params;

  return <ChildReportScreen unitId={unitId} reportId={id} />;
}
