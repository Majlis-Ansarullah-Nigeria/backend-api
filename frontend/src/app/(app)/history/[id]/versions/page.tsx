import { ReportVersionsScreen } from "@/features/history/components/ReportVersionsScreen";

export default async function ReportVersionsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  return <ReportVersionsScreen reportId={id} />;
}
