import { UsersScreen } from "@/features/admin/components/UsersScreen";

export default async function UsersPage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const { q } = await searchParams;
  return <UsersScreen key={q ?? ""} initialSearch={q} />;
}
