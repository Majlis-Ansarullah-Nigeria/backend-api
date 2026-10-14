import { ExportStatusScreen } from "@/features/exports/components/ExportStatusScreen";

export default async function ExportStatusPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  return <ExportStatusScreen id={id} />;
}
