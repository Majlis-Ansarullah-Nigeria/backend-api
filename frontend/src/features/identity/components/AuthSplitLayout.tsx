import { Bell, Download, PenLine } from "lucide-react";
import { getTranslations } from "next-intl/server";
import type { ReactNode } from "react";
import { BrandMark } from "@/components/shared/BrandMark";

export async function AuthSplitLayout({ children }: { children: ReactNode }) {
  const t = await getTranslations("Login");

  return (
    <div className="flex min-h-dvh">
      <div className="relative hidden w-1/2 shrink-0 flex-col justify-between overflow-hidden bg-forest-900 px-10 py-10 text-white lg:flex xl:px-16">
        <div className="flex items-center gap-3">
          <BrandMark />
          <div className="min-w-0">
            <p className="truncate text-sm font-bold leading-tight">{t("appName")}</p>
            <p className="truncate text-xs text-white/60">{t("orgPlaceholder")}</p>
          </div>
        </div>

        <div className="flex flex-col gap-5">
          <span className="inline-flex w-fit items-center rounded-full bg-lime/20 px-3 py-1 text-xs font-semibold text-lime">
            {t("windowBadge")}
          </span>
          <h1 className="text-4xl font-black leading-tight xl:text-5xl">{t("tagline")}</h1>
          <p className="max-w-md text-white/70">{t("taglineDescription")}</p>
        </div>

        <div className="grid grid-cols-3 gap-3">
          <FeatureCard icon={PenLine} label={t("featureAutosave")} />
          <FeatureCard icon={Bell} label={t("featureReminders")} />
          <FeatureCard icon={Download} label={t("featureExports")} />
        </div>
      </div>

      <div className="flex w-full flex-1 flex-col items-center justify-center bg-ground px-4 py-10 lg:w-1/2">
        <div className="w-full max-w-md">{children}</div>
      </div>
    </div>
  );
}

function FeatureCard({ icon: Icon, label }: { icon: typeof PenLine; label: string }) {
  return (
    <div className="flex flex-col gap-2 rounded-lg bg-white/5 p-3">
      <Icon className="size-5 text-lime" aria-hidden="true" />
      <p className="text-xs font-semibold text-white">{label}</p>
    </div>
  );
}
