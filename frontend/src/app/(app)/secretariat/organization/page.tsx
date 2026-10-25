import { OrganizationScreen } from "@/features/organization/components/OrganizationScreen";

export default async function OrganizationPage({ searchParams }: { searchParams: Promise<{ unit?: string }> }) {
  const { unit } = await searchParams;
  // Keyed so arriving from the search palette while already here re-selects the unit.
  return <OrganizationScreen key={unit ?? ""} initialUnitId={unit} />;
}
