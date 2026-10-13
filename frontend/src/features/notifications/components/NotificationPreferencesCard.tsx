"use client";

import { useTranslations } from "next-intl";
import { useQueryClient } from "@tanstack/react-query";
import { ErrorState } from "@/components/shared/ErrorState";
import { Switch } from "@/components/ui/switch";
import type { ApiError } from "@/lib/api/client";
import type { NotificationPreferenceView, NotificationSettingsView } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetMyNotificationPreferencesQueryKey,
  getGetMyNotificationSettingsQueryKey,
  useGetMyNotificationPreferences,
  useGetMyNotificationSettings,
  useUpdateNotificationPreference,
  useUpdateNotificationSettings,
} from "@/lib/api/generated/notifications/notifications";
import { NOTIFICATION_CATEGORY_ORDER, notificationCategoryLabel } from "@/features/notifications/utils/notification-category";

export function NotificationPreferencesCard() {
  const t = useTranslations("Profile");
  const queryClient = useQueryClient();

  const preferences = useGetMyNotificationPreferences<NotificationPreferenceView[], ApiError>();
  const settings = useGetMyNotificationSettings<NotificationSettingsView, ApiError>();
  const updatePreference = useUpdateNotificationPreference();
  const updateSettings = useUpdateNotificationSettings();

  const invalidatePreferences = () =>
    queryClient.invalidateQueries({ queryKey: getGetMyNotificationPreferencesQueryKey() });
  const invalidateSettings = () =>
    queryClient.invalidateQueries({ queryKey: getGetMyNotificationSettingsQueryKey() });

  if (preferences.isPending || settings.isPending) {
    return (
      <section className="rounded-lg border border-line bg-surface p-6">
        <h2 className="text-lg font-semibold text-ink">{t("notificationsHeading")}</h2>
        <p className="mt-4 text-sm text-ink-muted">{t("notificationsLoading")}</p>
      </section>
    );
  }

  if (preferences.isError || settings.isError) {
    return (
      <section className="rounded-lg border border-line bg-surface p-6">
        <h2 className="text-lg font-semibold text-ink">{t("notificationsHeading")}</h2>
        <div className="mt-4">
          <ErrorState
            title={t("notificationsErrorTitle")}
            onRetry={() => {
              preferences.refetch();
              settings.refetch();
            }}
          />
        </div>
      </section>
    );
  }

  const sorted = preferences.data
    .slice()
    .sort((a, b) => NOTIFICATION_CATEGORY_ORDER.indexOf(a.category) - NOTIFICATION_CATEGORY_ORDER.indexOf(b.category));

  return (
    <section className="rounded-lg border border-line bg-surface p-6">
      <div className="flex items-center gap-4 pb-1.5">
        <h2 className="flex-1 text-lg font-semibold text-ink">{t("notificationsHeading")}</h2>
        <span className="w-11 text-center text-xs font-bold text-ink-muted">{t("inAppColumnLabel")}</span>
        <span className="w-11 text-center text-xs font-bold text-ink-muted">{t("emailColumnLabel")}</span>
      </div>

      {sorted.map((preference) => (
        <div key={preference.category} className="flex items-center gap-4 border-t border-line py-3">
          <span className="flex-1 text-sm font-semibold text-ink">{notificationCategoryLabel(preference.category)}</span>
          <Switch
            className="shrink-0"
            aria-label={t("inAppColumnLabel")}
            checked={preference.inAppEnabled}
            onCheckedChange={(checked) =>
              updatePreference.mutate(
                { category: preference.category, data: { inAppEnabled: checked, emailEnabled: preference.emailEnabled } },
                { onSuccess: invalidatePreferences },
              )
            }
          />
          <Switch
            className="shrink-0"
            aria-label={t("emailColumnLabel")}
            checked={preference.emailEnabled}
            onCheckedChange={(checked) =>
              updatePreference.mutate(
                { category: preference.category, data: { inAppEnabled: preference.inAppEnabled, emailEnabled: checked } },
                { onSuccess: invalidatePreferences },
              )
            }
          />
        </div>
      ))}

      <div className="flex items-center justify-between gap-4 border-t border-line py-3">
        <span className="text-sm font-semibold text-ink">{t("digestLabel")}</span>
        <Switch
          aria-label={t("digestLabel")}
          checked={settings.data.digestEnabled}
          onCheckedChange={(checked) =>
            updateSettings.mutate({ data: { digestEnabled: checked } }, { onSuccess: invalidateSettings })
          }
        />
      </div>
    </section>
  );
}
