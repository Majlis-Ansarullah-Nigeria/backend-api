"use client";

import { AlertTriangle, ChevronRight, Clock, FileEdit, FileText, Inbox, LayoutGrid, Megaphone, Plus, Undo2, Users } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { ApiError } from "@/lib/api/client";
import type { OverviewWindowEvent, SecretariatOverview } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetSecretariatOverview } from "@/lib/api/generated/dashboards/dashboards";
import { isOnTrack, percentOf, WINDOW_EVENT_KIND } from "@/features/dashboards/utils/compliance";
import { cn } from "@/lib/utils";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";
import { createOrgFormatter, DEFAULT_ORG_FORMAT, type OrgFormatter } from "@/lib/org/format";
import { FEATURE_MODULE, useFeatureFlags } from "@/features/admin/hooks/useFeatureFlags";

const DEFAULT_FORMAT = createOrgFormatter(DEFAULT_ORG_FORMAT);

/** Morning / afternoon / evening by the organization's clock, not the browser's. */
export function greetingKey(now: Date, fmt: OrgFormatter = DEFAULT_FORMAT): "greetingMorning" | "greetingAfternoon" | "greetingEvening" {
  const hour = fmt.hour(now);
  return hour < 12 ? "greetingMorning" : hour < 17 ? "greetingAfternoon" : "greetingEvening";
}

/** "today" / "tomorrow" / "on 5 Oct", by calendar day in the organization's time zone. */
export function relativeDueDay(dueAt: Date, now: Date, fmt: OrgFormatter = DEFAULT_FORMAT): "today" | "tomorrow" | "date" {
  const tomorrow = new Date(now.getTime() + 24 * 60 * 60 * 1000);
  const due = fmt.dayKey(dueAt);
  return due === fmt.dayKey(now) ? "today" : due === fmt.dayKey(tomorrow) ? "tomorrow" : "date";
}

/** The "Secretariat overview" board. */
export function SecretariatOverviewScreen() {
  const fmt = useOrgFormat();
  const t = useTranslations("Overview");
  const { isEnabled } = useFeatureFlags();
  const query = useGetSecretariatOverview<SecretariatOverview, ApiError>();
  const [levelTab, setLevelTab] = useState<number | null>(null);

  if (query.isPending) {
    return <div className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</div>;
  }

  if (query.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      </div>
    );
  }

  const data = query.data;
  const now = new Date();
  const firstName = data.userName.split(" ")[0] ?? "";
  const zoneTab = data.byZone.find((level) => Number(level.level) === levelTab) ?? data.byZone[0];
  const nextDue = data.nextDue;
  const nextDueWhen = nextDue ? relativeDueDay(new Date(nextDue.dueAt), now, fmt) : null;

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink sm:text-3xl">
            {firstName ? t(greetingKey(now, fmt), { name: firstName }) : t("greetingNoName")}
          </h1>
          {data.currentWindow ? (
            <p className="mt-1 text-sm text-ink-muted">
              {t(data.currentWindow.isClosed ? "currentWindowClosed" : "currentWindowOpen", {
                period: data.currentWindow.periodLabel,
                reportType: data.currentWindow.reportTypeName,
                date: fmt.dayMonth(data.currentWindow.opensAt),
              })}
            </p>
          ) : null}
        </div>
        <div className="flex flex-wrap gap-3">
          <Link
            href="/secretariat/windows/new"
            className="inline-flex h-11 items-center gap-2 rounded-sm border border-line-strong bg-surface px-4 text-sm font-semibold text-ink hover:bg-ground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-600"
          >
            <Plus className="size-4" aria-hidden="true" />
            {t("newAdHocAction")}
          </Link>
          <Link
            href="/secretariat/inbox"
            className="inline-flex h-11 items-center gap-2 rounded-sm bg-green-600 px-4 text-sm font-semibold text-white hover:bg-green-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-600 focus-visible:ring-offset-2"
          >
            <Inbox className="size-4" aria-hidden="true" />
            {t("openInboxAction")}
          </Link>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Tile label={t("kpiReceivedLabel")} value={Number(data.received)} detail={t("kpiReceivedDetail", { expected: Number(data.expected) })} />
        <Tile
          label={t("kpiUnreadLabel")}
          value={Number(data.unreadInInbox)}
          tone="text-status-submitted-fg"
          detail={t("kpiUnreadDetail", { count: Number(data.assignedToMe) })}
        />
        {nextDue ? (
          <Tile
            label={t(`kpiDue_${nextDueWhen}`, { levels: nextDue.levelPluralName, date: fmt.dayMonth(nextDue.dueAt) })}
            value={Number(nextDue.notSubmitted)}
            tone="text-status-late-fg"
            detail={t("kpiDueDetail")}
          />
        ) : (
          <Tile label={t("kpiDueNoneLabel")} value={null} detail={t("kpiDueNoneDetail")} />
        )}
        <Tile
          label={t("kpiVacantLabel")}
          value={Number(data.vacantHeads)}
          tone="text-danger"
          action={
            <Link href="/secretariat/organization" className="text-sm font-semibold text-green-700 underline underline-offset-2">
              {t("assignHeadsAction")}
            </Link>
          }
        />
      </div>

      {data.currentWindow ? (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)]">
          <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="overview-zones-title">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 id="overview-zones-title" className="text-lg font-bold text-ink">
                {zoneTab ? t("byZoneTitle", { level: zoneTab.levelName }) : t("byZoneTitleEmpty")}
              </h2>
              {data.byZone.length > 1 ? (
                <div role="group" aria-label={t("levelLabel")} className="flex rounded-md bg-mint/60 p-1">
                  {data.byZone.map((level) => {
                    const active = level === zoneTab;
                    return (
                      <button
                        key={Number(level.level)}
                        type="button"
                        aria-pressed={active}
                        onClick={() => setLevelTab(Number(level.level))}
                        className={cn(
                          "h-10 rounded-sm px-4 text-sm font-semibold",
                          active ? "bg-surface text-ink shadow-sm" : "text-ink-muted hover:text-ink",
                        )}
                      >
                        {level.levelName}
                      </button>
                    );
                  })}
                </div>
              ) : null}
            </div>

            {zoneTab && zoneTab.groups.length > 0 ? (
              <>
                <ul className="mt-6 flex items-end gap-3 overflow-x-auto pb-1">
                  {zoneTab.groups.map((group) => {
                    const percent = percentOf(Number(group.submitted), Number(group.expected));
                    return (
                      <li key={group.groupUnitId ?? group.name} className="flex min-w-16 flex-1 flex-col items-center gap-2">
                        <span className="text-sm font-bold tabular-nums text-ink">{percent}%</span>
                        <div
                          className="flex h-40 w-full max-w-20 items-end rounded-md bg-ground"
                          title={t("zoneBarTitle", { zone: group.name, submitted: Number(group.submitted), expected: Number(group.expected) })}
                        >
                          <div
                            className={cn("w-full rounded-md", isOnTrack(percent) ? "bg-chart-on-time" : "bg-chart-late")}
                            style={{ height: `${percent}%` }}
                          />
                        </div>
                        <span className="text-center text-sm text-ink">{group.name}</span>
                      </li>
                    );
                  })}
                </ul>
                <ul className="mt-4 flex gap-4 text-sm text-ink-muted">
                  <li className="flex items-center gap-1.5">
                    <span className="size-2.5 rounded-sm bg-chart-on-time" aria-hidden="true" />
                    {t("legendOnTrack")}
                  </li>
                  <li className="flex items-center gap-1.5">
                    <span className="size-2.5 rounded-sm bg-chart-late" aria-hidden="true" />
                    {t("legendBehind")}
                  </li>
                </ul>
              </>
            ) : (
              <p className="mt-4 text-sm text-ink-muted">{t("byZoneEmpty")}</p>
            )}
          </section>

          <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="overview-levels-title">
            <div className="flex items-center justify-between">
              <h2 id="overview-levels-title" className="text-lg font-bold text-ink">
                {t("byLevelTitle")}
              </h2>
              {isEnabled(FEATURE_MODULE.Dashboards) ? (
                <Link
                  href={`/secretariat/dashboards/compliance?reportTypeId=${data.currentWindow.reportTypeId}`}
                  className="text-sm font-semibold text-green-700 underline underline-offset-2"
                >
                  {t("detailsLink")}
                </Link>
              ) : null}
            </div>
            <ul className="mt-2 flex flex-col">
              {data.byLevel.map((level) => {
                const expected = Number(level.expected);
                const submitted = Number(level.submitted);
                return (
                  <li key={Number(level.level)} className="border-t border-line py-4 first:border-t-0">
                    <div className="flex items-baseline justify-between gap-3 text-sm">
                      <span className="font-semibold text-ink">{level.levelName}</span>
                      <span className="text-ink-muted">
                        {t.rich("levelProgress", {
                          submitted,
                          expected,
                          strong: (chunks) => <strong className="text-ink">{chunks}</strong>,
                        })}
                        {level.dueAt ? ` · ${t("levelDue", { date: fmt.dayMonth(level.dueAt) })}` : null}
                      </span>
                    </div>
                    <div
                      className="mt-2 h-2 rounded-full bg-line"
                      role="progressbar"
                      aria-label={level.levelName}
                      aria-valuemin={0}
                      aria-valuemax={expected}
                      aria-valuenow={submitted}
                    >
                      <div className="h-2 rounded-full bg-green-600" style={{ width: `${percentOf(submitted, expected)}%` }} />
                    </div>
                  </li>
                );
              })}
            </ul>
          </section>
        </div>
      ) : (
        <EmptyState icon={LayoutGrid} title={t("noWindowTitle")} description={t("noWindowDescription")} />
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="overview-upcoming-title">
          <h2 id="overview-upcoming-title" className="text-lg font-bold text-ink">
            {t("upcomingTitle")}
          </h2>
          {data.upcomingWindows.length === 0 ? (
            <p className="mt-3 text-sm text-ink-muted">{t("upcomingEmpty")}</p>
          ) : (
            <ul className="mt-2 flex flex-col">
              {data.upcomingWindows.map((event) => (
                <UpcomingItem key={`${event.windowId}-${Number(event.kind)}`} event={event} />
              ))}
            </ul>
          )}
        </section>

        <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="overview-attention-title">
          <h2 id="overview-attention-title" className="text-lg font-bold text-ink">
            {t("attentionTitle")}
          </h2>
          <ul className="mt-2 flex flex-col">
            <AttentionItem
              href="/secretariat/inbox"
              icon={<AlertTriangle className="size-5 text-danger" aria-hidden="true" />}
              label={t("attentionFlagged", { count: Number(data.flaggedReports) })}
            />
            <AttentionItem
              href="/secretariat/inbox"
              icon={<Undo2 className="size-5 text-ink-muted" aria-hidden="true" />}
              label={t("attentionReturnedPastDeadline", { count: Number(data.returnedPastRevisionDeadline) })}
            />
            <AttentionItem
              href="/secretariat/organization"
              icon={<Users className="size-5 text-ink-muted" aria-hidden="true" />}
              label={t("attentionNoHead", { count: Number(data.vacantHeads) })}
            />
          </ul>
        </section>

        <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="overview-actions-title">
          <h2 id="overview-actions-title" className="text-lg font-bold text-ink">
            {t("quickActionsTitle")}
          </h2>
          <div className="mt-4 flex flex-col gap-3">
            <QuickAction href="/secretariat/report-types" icon={<FileEdit className="size-4" aria-hidden="true" />}>
              {data.currentWindow ? t("editTemplateAction", { reportType: data.currentWindow.reportTypeName }) : t("editTemplatesAction")}
            </QuickAction>
            <QuickAction href="/secretariat/windows" icon={<Clock className="size-4" aria-hidden="true" />}>
              {t("grantExtensionAction")}
            </QuickAction>
            <QuickAction href="/secretariat/announcements" icon={<Megaphone className="size-4" aria-hidden="true" />}>
              {t("sendAnnouncementAction")}
            </QuickAction>
            {data.currentWindow && isEnabled(FEATURE_MODULE.CompiledReports) ? (
              <Link
                href={`/secretariat/compiled/${data.currentWindow.windowId}`}
                className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-lime px-4 text-sm font-semibold text-forest-900 hover:brightness-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-600"
              >
                <FileText className="size-4" aria-hidden="true" />
                {t("compileAction", { period: data.currentWindow.periodLabel })}
              </Link>
            ) : null}
          </div>
        </section>
      </div>
    </div>
  );
}

function Tile({
  label,
  value,
  detail,
  action,
  tone = "text-ink",
}: {
  label: string;
  value: number | null;
  detail?: string;
  action?: React.ReactNode;
  tone?: string;
}) {
  return (
    <div className="rounded-lg border border-line bg-surface p-5">
      <p className="text-xs font-bold uppercase tracking-wide text-ink-muted">{label}</p>
      <p className={cn("mt-2 text-4xl font-bold tabular-nums", tone)}>{value == null ? "—" : value}</p>
      {detail ? <p className="mt-1 text-sm text-ink-muted">{detail}</p> : null}
      {action ? <div className="mt-1">{action}</div> : null}
    </div>
  );
}

function UpcomingItem({ event }: { event: OverviewWindowEvent }) {
  const fmt = useOrgFormat();
  const t = useTranslations("Overview");
  const at = new Date(event.at);
  const levels = event.levelNames.length === 1 ? t("levelOnly", { level: event.levelNames[0] }) : event.levelNames.join(", ");
  const kind = Number(event.kind) === WINDOW_EVENT_KIND.Opens ? t("eventOpens") : t("eventCloses");
  const detail = [kind, levels, event.isScheduled ? t("autoScheduled") : null].filter(Boolean).join(" · ");

  return (
    <li className="flex items-center gap-4 border-t border-line py-3 first:border-t-0">
      <div className="flex w-10 flex-col items-center">
        <span className="text-xs font-bold uppercase text-green-700">{fmt.monthShort(at)}</span>
        <span className="text-xl font-bold text-ink">{fmt.dayOfMonth(at)}</span>
      </div>
      <div>
        <p className="font-semibold text-ink">
          {event.reportTypeName} · {event.periodLabel}
        </p>
        <p className="text-sm text-ink-muted">{detail}</p>
      </div>
    </li>
  );
}

function AttentionItem({ href, icon, label }: { href: string; icon: React.ReactNode; label: string }) {
  return (
    <li className="border-t border-line first:border-t-0">
      <Link href={href} className="flex items-center gap-3 py-3 text-sm text-ink hover:text-green-700">
        {icon}
        <span className="flex-1">{label}</span>
        <ChevronRight className="size-4 text-ink-muted" aria-hidden="true" />
      </Link>
    </li>
  );
}

function QuickAction({ href, icon, children }: { href: string; icon: React.ReactNode; children: React.ReactNode }) {
  return (
    <Link
      href={href}
      className="inline-flex h-12 items-center justify-center gap-2 rounded-md border border-line-strong bg-surface px-4 text-sm font-semibold text-ink hover:bg-ground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-600"
    >
      {icon}
      {children}
    </Link>
  );
}
