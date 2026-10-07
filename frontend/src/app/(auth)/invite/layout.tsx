import { BarChart3 } from "lucide-react";
import { getTranslations } from "next-intl/server";
import type { ReactNode } from "react";

export default async function InviteLayout({ children }: { children: ReactNode }) {
  const t = await getTranslations("AcceptInvitation");

  return (
    <div className="flex min-h-dvh flex-col items-center bg-ground px-4 py-10">
      <div className="flex w-full max-w-lg items-center gap-3 pb-6">
        <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-green-600">
          <BarChart3 className="size-5 text-lime" aria-hidden="true" />
        </span>
        <p className="text-base font-bold text-ink">{t("appName")}</p>
      </div>

      <main className="w-full max-w-lg rounded-lg border border-line bg-surface p-6 shadow-sm sm:p-8">
        {children}
      </main>
    </div>
  );
}
