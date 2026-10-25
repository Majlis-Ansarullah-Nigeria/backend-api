import { ReportTypesScreen } from "@/features/templates/components/ReportTypesScreen";

export default async function ReportTypesPage({ searchParams }: { searchParams: Promise<{ new?: string }> }) {
  const { new: startCreating } = await searchParams;
  return <ReportTypesScreen key={startCreating ?? ""} startCreating={startCreating === "1"} />;
}
