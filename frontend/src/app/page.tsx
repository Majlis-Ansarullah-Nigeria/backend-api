import { useTranslations } from "next-intl";

export default function Home() {
  const t = useTranslations("Home");

  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-2 bg-ground px-6 text-center">
      <h1 className="text-2xl font-semibold text-ink">{t("title")}</h1>
      <p className="text-ink-muted">{t("description")}</p>
    </main>
  );
}
