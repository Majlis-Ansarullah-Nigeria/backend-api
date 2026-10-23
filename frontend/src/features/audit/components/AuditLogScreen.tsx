"use client";

import { Download, ShieldCheck } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import type { AuditEventPage, AuditEventRow, AuditFilterOptions } from "@/lib/api/generated/hRSApiV1.schemas";
import { exportAuditEventsCsv, useGetAuditEvents, useGetAuditFilterOptions } from "@/lib/api/generated/audit/audit";
import { downloadBlob } from "@/features/organization/utils/bulk-import";
import { auditTone, DATE_RANGES, initials, jsonLines, rangeStart } from "@/features/audit/utils/audit";
import { cn } from "@/lib/utils";

const ORG_TIME_ZONE = "Africa/Lagos";
const WHEN = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit", timeZone: ORG_TIME_ZONE });
const WHEN_FULL = new Intl.DateTimeFormat("en-GB", {
  day: "numeric",
  month: "short",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  timeZone: ORG_TIME_ZONE,
});

const SELECT_CLASS =
  "h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40";

const TONE_CLASSES: Record<string, string> = {
  submitted: "bg-status-submitted-bg text-status-submitted-fg",
  acknowledged: "bg-status-acknowledged-bg text-status-acknowledged-fg",
  returned: "bg-status-returned-bg text-status-returned-fg",
  late: "bg-status-late-bg text-status-late-fg",
  "in-review": "bg-status-in-review-bg text-status-in-review-fg",
  draft: "bg-status-draft-bg text-status-draft-fg",
  upcoming: "bg-status-upcoming-bg text-status-upcoming-fg",
};

interface Filters {
  action?: string;
  actorUserId?: string;
  unitId?: string;
  days: number | null;
}

const PAGE_SIZE = 50;

/** FR-AUD-002: the "Audit log" board — read-only, filtered, exportable to CSV. */
export function AuditLogScreen() {
  const t = useTranslations("Audit");
  const [filters, setFilters] = useState<Filters>({ days: 30 });
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  // Fixed per filter change so the query key stays stable between renders.
  const [from, setFrom] = useState<string | undefined>(() => rangeStart(30));
  const params = { action: filters.action, actorUserId: filters.actorUserId, unitId: filters.unitId, from };

  const options = useGetAuditFilterOptions<AuditFilterOptions, ApiError>();
  const events = useGetAuditEvents<AuditEventPage, ApiError>({ ...params, page, pageSize: PAGE_SIZE });

  const update = (patch: Partial<Filters>) => {
    const next = { ...filters, ...patch };
    setFilters(next);
    if ("days" in patch) {
      setFrom(rangeStart(next.days));
    }
    setPage(1);
    setSelectedId(null);
  };

  const exportCsv = async () => {
    setExporting(true);
    setExportError(null);
    try {
      const blob = (await exportAuditEventsCsv(params)) as unknown as Blob;
      downloadBlob(blob, "audit-log.csv");
    } catch (error) {
      setExportError(error instanceof ApiError ? error.message : t("exportFailed"));
    } finally {
      setExporting(false);
    }
  };

  const rows = events.data?.rows ?? [];
  const total = events.data ? Number(events.data.totalCount) : 0;
  const selected = rows.find((row) => row.id === selectedId) ?? null;

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink sm:text-3xl">{t("title")}</h1>
          <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
        </div>
        <Button type="button" variant="outline" onClick={exportCsv} disabled={exporting}>
          <Download aria-hidden="true" />
          {t("exportCsv")}
        </Button>
      </div>
      {exportError ? <p className="text-sm text-danger">{exportError}</p> : null}

      <div className="flex flex-wrap items-end gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="audit-action">{t("actionLabel")}</Label>
          <select id="audit-action" value={filters.action ?? ""} onChange={(event) => update({ action: event.target.value || undefined })} className={SELECT_CLASS}>
            <option value="">{t("allActions")}</option>
            {(options.data?.actions ?? []).map((action) => (
              <option key={action} value={action}>
                {action}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="audit-person">{t("personLabel")}</Label>
          <select id="audit-person" value={filters.actorUserId ?? ""} onChange={(event) => update({ actorUserId: event.target.value || undefined })} className={SELECT_CLASS}>
            <option value="">{t("anyone")}</option>
            {(options.data?.people ?? []).map((person) => (
              <option key={person.userId} value={person.userId}>
                {person.name}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="audit-unit">{t("unitLabel")}</Label>
          <select id="audit-unit" value={filters.unitId ?? ""} onChange={(event) => update({ unitId: event.target.value || undefined })} className={SELECT_CLASS}>
            <option value="">{t("allUnits")}</option>
            {(options.data?.units ?? []).map((unit) => (
              <option key={unit.unitId} value={unit.unitId}>
                {"  ".repeat(Number(unit.depth))}
                {unit.name}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="audit-date">{t("dateLabel")}</Label>
          <select
            id="audit-date"
            value={filters.days ?? ""}
            onChange={(event) => update({ days: event.target.value === "" ? null : Number(event.target.value) })}
            className={SELECT_CLASS}
          >
            {DATE_RANGES.map((days) => (
              <option key={days} value={days}>
                {t("lastDays", { days })}
              </option>
            ))}
            <option value="">{t("allTime")}</option>
          </select>
        </div>
      </div>

      {events.isPending ? (
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      ) : events.isError ? (
        <ErrorState title={events.error.status === 403 ? t("forbiddenTitle") : t("loadErrorTitle")} onRetry={() => events.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={ShieldCheck} title={t("emptyTitle")} description={t("emptyDescription")} />
      ) : (
        <>
          <div className="overflow-x-auto rounded-lg border border-line bg-surface">
            <table className="w-full min-w-[820px] border-collapse text-sm">
              <thead>
                <tr className="border-b border-line text-left text-xs font-bold uppercase tracking-wide text-ink-muted">
                  <th scope="col" className="px-4 py-3">{t("columnWhen")}</th>
                  <th scope="col" className="px-4 py-3">{t("columnWho")}</th>
                  <th scope="col" className="px-4 py-3">{t("columnAction")}</th>
                  <th scope="col" className="px-4 py-3">{t("columnDetails")}</th>
                  <th scope="col" className="px-4 py-3">{t("columnIp")}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <AuditRow key={row.id} row={row} selected={row.id === selectedId} onSelect={() => setSelectedId(row.id === selectedId ? null : row.id)} />
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex items-center justify-between">
            <span className="text-sm text-ink-muted">{t("showingCount", { shown: rows.length, total })}</span>
            <div className="flex gap-2">
              <Button type="button" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                {t("previousPage")}
              </Button>
              <Button type="button" variant="outline" disabled={page * PAGE_SIZE >= total} onClick={() => setPage((p) => p + 1)}>
                {t("nextPage")}
              </Button>
            </div>
          </div>

          {selected ? <AuditDetails row={selected} /> : null}
        </>
      )}
    </div>
  );
}

function AuditRow({ row, selected, onSelect }: { row: AuditEventRow; selected: boolean; onSelect: () => void }) {
  const t = useTranslations("Audit");
  return (
    <tr className={cn("border-b border-line last:border-0", selected && "bg-mint/50")}>
      <td className="whitespace-nowrap px-4 py-3.5 text-ink-muted">{WHEN.format(new Date(row.occurredAt))}</td>
      <td className="px-4 py-3.5">
        <span className="flex items-center gap-2">
          <span className="flex size-8 items-center justify-center rounded-full bg-mint text-xs font-bold text-green-700" aria-hidden="true">
            {initials(row.actorName)}
          </span>
          <span className="font-semibold text-ink">{row.actorName}</span>
        </span>
      </td>
      <td className="px-4 py-3.5">
        <span className={cn("rounded-sm px-2 py-1 text-xs font-semibold", TONE_CLASSES[auditTone(row.action)])}>{row.action}</span>
      </td>
      <td className="px-4 py-3.5 text-ink">
        <button type="button" onClick={onSelect} aria-expanded={selected} aria-label={t("detailsFor", { summary: row.summary })} className="text-left hover:underline">
          {row.summary}
        </button>
      </td>
      <td className="px-4 py-3.5 font-mono text-xs text-ink">{row.ipAddress ?? "—"}</td>
    </tr>
  );
}

function AuditDetails({ row }: { row: AuditEventRow }) {
  const t = useTranslations("Audit");
  const before = jsonLines(row.beforeJson);
  const after = jsonLines(row.afterJson);
  return (
    <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="audit-details-title">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="audit-details-title" className="text-lg font-bold text-ink">
          {t("detailsTitle", { action: row.action })}
        </h2>
        <p className="text-sm text-ink-muted">{t("detailsWhen", { when: WHEN_FULL.format(new Date(row.occurredAt)) })}</p>
      </div>
      {before.length === 0 && after.length === 0 ? (
        <p className="mt-3 text-sm text-ink-muted">{t("noChanges")}</p>
      ) : (
        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <ValueBox title={t("before")} lines={before} className="bg-status-returned-bg" titleClass="text-status-returned-fg" />
          <ValueBox title={t("after")} lines={after} className="bg-status-acknowledged-bg" titleClass="text-status-acknowledged-fg" />
        </div>
      )}
      {row.userAgent ? <p className="mt-3 text-xs text-ink-muted">{t("userAgent", { agent: row.userAgent })}</p> : null}
    </section>
  );
}

function ValueBox({ title, lines, className, titleClass }: { title: string; lines: string[]; className: string; titleClass: string }) {
  const t = useTranslations("Audit");
  return (
    <div className={cn("rounded-md p-4 font-mono text-sm text-ink", className)}>
      <p className={cn("font-bold", titleClass)}>{title}</p>
      {lines.length === 0 ? (
        <p className="text-ink-muted">{t("nothing")}</p>
      ) : (
        lines.map((line, index) => (
          <p key={index} className="break-all">
            {line}
          </p>
        ))
      )}
    </div>
  );
}
