"use client";

import { useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Check, Mail, RefreshCw, Smartphone, Upload, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { useRef, useState } from "react";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import type { OrganizationSettingsDto, SystemHealth, TimeZoneOption } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetOrganizationSettingsQueryKey,
  getGetSystemHealthQueryKey,
  useGetOrganizationSettings,
  useGetSystemHealth,
  useGetTimeZoneOptions,
  useRemoveLogo,
  useRetryFailedNotifications,
  useUpdateOrganizationSettings,
  useUploadLogo,
} from "@/lib/api/generated/settings/settings";
import { BRAND_COLORS, formatBytes, LOGO_MAX_BYTES } from "@/features/admin/settings/brand";
import { createOrgFormatter, DATE_FORMAT_STYLE } from "@/lib/org/format";
import { cn } from "@/lib/utils";

const SELECT_CLASS =
  "h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40 disabled:opacity-60";

const MONTHS = Array.from({ length: 12 }, (_, index) =>
  new Intl.DateTimeFormat("en-GB", { month: "long", timeZone: "UTC" }).format(Date.UTC(2026, index, 1)),
);

/** FR-ADM-001 and FR-ADM-005: the "Settings, integrations, health" board. Only the system
 * administrator edits; the Secretariat sees the settings read-only and can retry notifications. */
export function SettingsScreen() {
  const t = useTranslations("Settings");
  const settings = useGetOrganizationSettings<OrganizationSettingsDto, ApiError>();

  if (settings.isPending) {
    return <div className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</div>;
  }

  if (settings.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => settings.refetch()} />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink sm:text-3xl">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{settings.data.canEdit ? t("subtitle") : t("subtitleReadOnly")}</p>
      </div>
      {/* Keyed so the form starts from the saved values again after every save. */}
      <SettingsForm key={settings.data.updatedAt ?? "default"} settings={settings.data} />
      <div className="grid gap-6 lg:grid-cols-2">
        <HealthCards />
      </div>
    </div>
  );
}

function SettingsForm({ settings }: { settings: OrganizationSettingsDto }) {
  const t = useTranslations("Settings");
  const queryClient = useQueryClient();
  const timeZones = useGetTimeZoneOptions<TimeZoneOption[], ApiError>();
  const update = useUpdateOrganizationSettings();
  const upload = useUploadLogo();
  const remove = useRemoveLogo();
  const fileInput = useRef<HTMLInputElement>(null);
  const readOnly = !settings.canEdit;

  const [name, setName] = useState(settings.name);
  const [brandColor, setBrandColor] = useState(Number(settings.brandColor));
  const [timeZone, setTimeZone] = useState(settings.timeZone);
  const [dateFormat, setDateFormat] = useState(Number(settings.dateFormat));
  const [fiscalMonth, setFiscalMonth] = useState(Number(settings.fiscalYearStartMonth));
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);

  const refresh = () => queryClient.invalidateQueries({ queryKey: getGetOrganizationSettingsQueryKey() });
  const fail = (error: unknown) => setMessage({ kind: "error", text: error instanceof ApiError ? error.message : t("saveFailed") });

  const save = () => {
    setMessage(null);
    update.mutate(
      { data: { name: name.trim(), brandColor, timeZone, dateFormat, fiscalYearStartMonth: fiscalMonth } },
      { onSuccess: () => { setMessage({ kind: "ok", text: t("saved") }); refresh(); }, onError: fail },
    );
  };

  const onLogoChosen = (file: File | undefined) => {
    setMessage(null);
    if (!file) return;
    if (file.size > LOGO_MAX_BYTES) {
      setMessage({ kind: "error", text: t("logoTooLarge") });
      return;
    }
    upload.mutate({ data: { file } }, { onSuccess: refresh, onError: fail });
  };

  const sample = new Date();
  const timeZoneOptions = timeZones.data ?? [{ id: settings.timeZone, label: settings.timeZone }];

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <section className="flex flex-col gap-5 rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="settings-branding">
        <h2 id="settings-branding" className="text-lg font-bold text-ink">
          {t("brandingTitle")}
        </h2>
        <div className="flex flex-wrap items-center gap-4">
          <div className="flex size-20 items-center justify-center overflow-hidden rounded-md border border-dashed border-line-strong bg-ground">
            {settings.hasLogo ? (
              // eslint-disable-next-line @next/next/no-img-element -- served by the API
              <img src={`/api/v1/settings/logo?v=${settings.logoVersion ?? ""}`} alt={t("logoAlt")} className="max-h-full max-w-full object-contain" />
            ) : (
              <span className="text-xs text-ink-muted">{t("noLogo")}</span>
            )}
          </div>
          {readOnly ? null : (
            <div className="flex flex-col gap-1.5">
              <div className="flex gap-2">
                <Button type="button" variant="outline" onClick={() => fileInput.current?.click()} disabled={upload.isPending}>
                  <Upload aria-hidden="true" />
                  {t("uploadLogo")}
                </Button>
                {settings.hasLogo ? (
                  <Button type="button" variant="ghost" onClick={() => remove.mutate(undefined, { onSuccess: refresh, onError: fail })} disabled={remove.isPending}>
                    <X aria-hidden="true" />
                    {t("removeLogo")}
                  </Button>
                ) : null}
              </div>
              <p className="text-xs text-ink-muted">{t("logoHelp")}</p>
              <input
                ref={fileInput}
                type="file"
                accept="image/png,image/svg+xml"
                className="sr-only"
                aria-label={t("uploadLogo")}
                onChange={(event) => {
                  onLogoChosen(event.target.files?.[0]);
                  event.target.value = "";
                }}
              />
            </div>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="settings-name">{t("nameLabel")}</Label>
          <Input id="settings-name" value={name} onChange={(event) => setName(event.target.value)} maxLength={120} disabled={readOnly} />
        </div>

        <fieldset className="flex flex-col gap-2">
          <legend className="text-xs font-bold uppercase tracking-wide text-ink-muted">{t("brandColourLabel")}</legend>
          <div className="flex flex-wrap gap-3">
            {BRAND_COLORS.map((color) => (
              <label key={color.key} className="cursor-pointer">
                <input
                  type="radio"
                  name="brand-colour"
                  value={color.value}
                  checked={brandColor === color.value}
                  onChange={() => setBrandColor(color.value)}
                  disabled={readOnly}
                  className="peer sr-only"
                />
                <span
                  className="flex size-12 items-center justify-center rounded-md ring-offset-2 peer-checked:ring-2 peer-checked:ring-ink peer-focus-visible:outline peer-focus-visible:outline-2 peer-focus-visible:outline-green-600"
                  style={{ backgroundColor: color.primary }}
                >
                  {brandColor === color.value ? <Check className="size-5 text-white" aria-hidden="true" /> : null}
                </span>
                <span className="sr-only">{t(`colour_${color.key}`)}</span>
              </label>
            ))}
          </div>
        </fieldset>
      </section>

      <section className="flex flex-col gap-5 rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="settings-regional">
        <h2 id="settings-regional" className="text-lg font-bold text-ink">
          {t("regionalTitle")}
        </h2>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="settings-timezone">{t("timeZoneLabel")}</Label>
            <select id="settings-timezone" value={timeZone} onChange={(event) => setTimeZone(event.target.value)} disabled={readOnly} className={SELECT_CLASS}>
              {timeZoneOptions.map((zone) => (
                <option key={zone.id} value={zone.id}>
                  {zone.label}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="settings-dateformat">{t("dateFormatLabel")}</Label>
            <select id="settings-dateformat" value={dateFormat} onChange={(event) => setDateFormat(Number(event.target.value))} disabled={readOnly} className={SELECT_CLASS}>
              {Object.values(DATE_FORMAT_STYLE).map((style) => (
                <option key={style} value={style}>
                  {createOrgFormatter({ timeZone, dateFormat: style }).date(sample)}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="settings-fiscal">{t("fiscalYearLabel")}</Label>
            <select id="settings-fiscal" value={fiscalMonth} onChange={(event) => setFiscalMonth(Number(event.target.value))} disabled={readOnly} className={SELECT_CLASS}>
              {MONTHS.map((month, index) => (
                <option key={month} value={index + 1}>
                  {month}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="settings-language">{t("languageLabel")}</Label>
            <select id="settings-language" value="en" disabled className={SELECT_CLASS} aria-describedby="settings-language-note">
              <option value="en">English</option>
            </select>
            <p id="settings-language-note" className="text-xs text-ink-muted">
              {t("languageNote")}
            </p>
          </div>
        </div>
      </section>

      <div className="flex flex-wrap items-center gap-3 lg:col-span-2">
        {readOnly ? (
          <p className="text-sm text-ink-muted">{t("readOnlyNote")}</p>
        ) : (
          <Button type="button" onClick={save} disabled={update.isPending || name.trim().length === 0}>
            {t("save")}
          </Button>
        )}
        {message ? (
          <p role="status" className={cn("text-sm", message.kind === "ok" ? "text-green-700" : "text-danger")}>
            {message.text}
          </p>
        ) : null}
      </div>
    </div>
  );
}

function HealthCards() {
  const t = useTranslations("Settings");
  const queryClient = useQueryClient();
  const health = useGetSystemHealth<SystemHealth, ApiError>();
  const retry = useRetryFailedNotifications();
  const [retried, setRetried] = useState<number | null>(null);

  if (health.isPending) {
    return <p className="text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (health.isError) {
    return health.error.status === 403 ? null : <ErrorState title={t("healthErrorTitle")} onRetry={() => health.refetch()} />;
  }

  const data = health.data;
  const jobs = data.jobs;
  const failedNotifications = Number(data.notifications.waitingRetry) + Number(data.notifications.givenUp);
  const givenUp = Number(data.notifications.givenUp);

  return (
    <>
      <section className="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="settings-integrations">
        <h2 id="settings-integrations" className="text-lg font-bold text-ink">
          {t("integrationsTitle")}
        </h2>
        <IntegrationRow icon={Mail} title={t("emailProvider")} detail={data.emailTransport} badge={t("configured")} />
        <IntegrationRow icon={Smartphone} title={t("smsWhatsapp")} detail={t("smsDetail")} badge={t("later")} muted />
      </section>

      <section className="flex flex-col gap-1 rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="settings-health">
        <h2 id="settings-health" className="mb-2 text-lg font-bold text-ink">
          {t("healthTitle")}
        </h2>
        <HealthRow
          ok={jobs != null && Number(jobs.servers) > 0 && Number(jobs.failed) === 0}
          label={t("backgroundJobs")}
          value={
            jobs == null
              ? t("jobsUnknown")
              : Number(jobs.servers) === 0
                ? t("jobsNoWorker")
                : Number(jobs.failed) > 0
                  ? t("jobsFailed", { count: Number(jobs.failed) })
                  : t("jobsRunning", { queued: Number(jobs.enqueued) })
          }
        />
        <HealthRow ok label={t("notificationsSent")} value={t("sentLast24h", { count: Number(data.notifications.sentLast24Hours) })} />
        <HealthRow
          ok={failedNotifications === 0}
          label={t("failedNotifications")}
          value={failedNotifications === 0 ? t("noneFailed") : t("failedDetail", { waiting: Number(data.notifications.waitingRetry), givenUp })}
        />
        <HealthRow ok label={t("fileStorage")} value={t("storageUsed", { size: formatBytes(Number(data.attachmentBytes)), count: Number(data.attachmentCount) })} />
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <Button
            type="button"
            variant="outline"
            disabled={givenUp === 0 || retry.isPending}
            onClick={() =>
              retry.mutate(undefined, {
                onSuccess: (result) => {
                  setRetried(Number(result.retried));
                  queryClient.invalidateQueries({ queryKey: getGetSystemHealthQueryKey() });
                },
              })
            }
          >
            <RefreshCw aria-hidden="true" />
            {t("retryFailed")}
          </Button>
          {retried != null ? (
            <p role="status" className="text-sm text-green-700">
              {t("retried", { count: retried })}
            </p>
          ) : null}
        </div>
      </section>
    </>
  );
}

function IntegrationRow({
  icon: Icon,
  title,
  detail,
  badge,
  muted = false,
}: {
  icon: typeof Mail;
  title: string;
  detail: string;
  badge: string;
  muted?: boolean;
}) {
  return (
    <div className="flex items-center gap-3 rounded-md border border-line p-3">
      <span className="flex size-10 items-center justify-center rounded-md bg-ground" aria-hidden="true">
        <Icon className="size-5 text-green-700" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-semibold text-ink">{title}</p>
        <p className="truncate text-sm text-ink-muted">{detail}</p>
      </div>
      <span className={cn("rounded-sm px-2 py-1 text-xs font-semibold", muted ? "bg-ground text-ink-muted" : "bg-status-acknowledged-bg text-status-acknowledged-fg")}>
        {badge}
      </span>
    </div>
  );
}

function HealthRow({ ok, label, value }: { ok: boolean; label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3 border-b border-line py-3 last:border-0">
      <span className="flex items-center gap-2 font-medium text-ink">
        {ok ? <Check className="size-4 text-green-600" aria-hidden="true" /> : <AlertTriangle className="size-4 text-status-late-fg" aria-hidden="true" />}
        {label}
      </span>
      <span className={cn("text-right text-sm font-semibold", ok ? "text-green-700" : "text-status-late-fg")}>{value}</span>
    </div>
  );
}
