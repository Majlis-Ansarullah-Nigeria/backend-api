import { getTranslations } from "next-intl/server";
import type { ReactNode } from "react";
import { BrandMark } from "@/components/shared/BrandMark";

export default async function InviteLayout({ children }: { children: ReactNode }) {
  const t = await getTranslations("AcceptInvitation");

  return (
    <div className="flex min-h-dvh flex-col items-center bg-ground px-4 py-10">
      <div className="flex w-full max-w-lg items-center gap-3 pb-6">
        <BrandMark className="size-9" />
        <p className="text-base font-bold text-ink">{t("appName")}</p>
      </div>

      <main className="w-full max-w-lg rounded-lg border border-line bg-surface p-6 shadow-sm sm:p-8">
        {children}
      </main>
    </div>
  );
}
