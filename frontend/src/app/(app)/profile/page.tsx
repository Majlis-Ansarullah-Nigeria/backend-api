import { getTranslations } from "next-intl/server";
import { NotificationPreferencesCard } from "@/features/notifications/components/NotificationPreferencesCard";
import { SignedInDevicesCard } from "@/features/profile/components/SignedInDevicesCard";

export default async function ProfilePage() {
  const t = await getTranslations("Profile");

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-6 px-4 py-8 md:px-8">
      <h1 className="text-2xl font-semibold text-ink">{t("title")}</h1>

      {/*
        The sessions card ships in M1.5 (FR-IAM-006); the notification preferences/digest card
        ships here in M8.3 (FR-NOT-004). The rest of the mockup — profile edit, delegates, the
        two-step sign-in toggle and password change — belongs to other milestones (M2.4, M1.6)
        and isn't built here.
      */}
      <NotificationPreferencesCard />
      <SignedInDevicesCard />
    </div>
  );
}
