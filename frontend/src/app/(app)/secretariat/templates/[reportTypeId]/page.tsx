import { TemplateBuilderScreen } from "@/features/templates/components/builder/TemplateBuilderScreen";

export default async function TemplateBuilderPage({ params }: { params: Promise<{ reportTypeId: string }> }) {
  const { reportTypeId } = await params;

  return <TemplateBuilderScreen reportTypeId={reportTypeId} />;
}
