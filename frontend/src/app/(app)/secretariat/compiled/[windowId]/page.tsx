import { CompiledReportScreen } from "@/features/compiled/components/CompiledReportScreen";

export default async function CompiledReportPage({ params }: { params: Promise<{ windowId: string }> }) {
  const { windowId } = await params;
  return <CompiledReportScreen windowId={windowId} basePath="/secretariat/compiled" />;
}
