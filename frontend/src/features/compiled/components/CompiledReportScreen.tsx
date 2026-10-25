"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Check, ChevronRight, Download, FileText, RefreshCw } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { FeatureAwareErrorState } from "@/components/shared/FeatureAwareErrorState";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api/client";
import type { CompiledFigure, CompiledHighlight, CompiledReportView } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getCompiledReportPdf,
  getCompiledReportWord,
  getGetCompiledReportQueryKey,
  useGetCompiledReport,
  useUpdateCompiledNarrative,
} from "@/lib/api/generated/compiled-reports/compiled-reports";
import { downloadBlob } from "@/features/organization/utils/bulk-import";
import { change, changeKind, formatIndicator, formatSigned } from "@/features/dashboards/utils/indicators";
import { cn } from "@/lib/utils";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

// Matches backend/src/HRS.Application/Features/Review/Compiled/CompiledReportView.cs.
const HIGHLIGHT_KIND = { Highest: 0, LargestRise: 1, LargestFall: 2 } as const;
const MAX_NARRATIVE = 20_000;

const toNumber = (value: number | string | null | undefined): number | null => (value == null ? null : Number(value));

/** FR-REV-006: the "Compiled national report" board. */
export function CompiledReportScreen({ windowId, basePath }: { windowId: string; basePath: string }) {
  const fmt = useOrgFormat();
  const t = useTranslations("Compiled");
  const query = useGetCompiledReport<CompiledReportView, ApiError>(windowId);
  const [downloading, setDownloading] = useState<"pdf" | "word" | null>(null);

  if (query.isPending) {
    return <div className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</div>;
  }

  if (query.isError) {
    return (
      <div className="p-4 sm:p-6">
        <FeatureAwareErrorState error={query.error} title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      </div>
    );
  }

  const report = query.data;
  const figures = new Map(report.sections.flatMap((section) => section.figures).map((figure) => [figure.fieldKey, figure]));
  const highlights = report.sections.flatMap((section) => section.highlights);

  const download = async (format: "pdf" | "word") => {
    setDownloading(format);
    try {
      const blob = (format === "pdf" ? await getCompiledReportPdf(windowId) : await getCompiledReportWord(windowId)) as unknown as Blob;
      downloadBlob(blob, `${report.referenceNumber}.${format === "pdf" ? "pdf" : "docx"}`);
    } finally {
      setDownloading(null);
    }
  };

  const contents = [
    { id: "summary", label: t("sectionSummary"), done: report.narrative.trim().length > 0 },
    { id: "overview", label: t("sectionOverview"), done: Number(report.overview.expected) > 0 },
    ...report.sections.map((section) => ({
      id: `section-${section.key}`,
      label: section.title,
      done: section.figures.some((figure) => figure.value != null),
    })),
    { id: "highlights", label: t("sectionHighlights"), done: highlights.length > 0 },
    { id: "appendix", label: t("sectionAppendix"), done: report.zones.some((zone) => zone.values.some((value) => value != null)) },
  ];

  let number = 3;

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <nav aria-label={t("breadcrumbLabel")} className="flex items-center gap-2 text-sm">
        <Link href={basePath} className="text-ink-muted hover:text-ink">
          {t("listTitle")}
        </Link>
        <ChevronRight className="size-4 text-ink-muted" aria-hidden="true" />
        <span className="font-semibold text-ink" aria-current="page">
          {report.periodLabel}
        </span>
      </nav>

      <div>
        <h1 className="text-2xl font-bold text-ink sm:text-3xl">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle", { period: report.periodLabel })}</p>
      </div>

      <div className="flex flex-wrap gap-3">
        <Button type="button" variant="outline" onClick={() => query.refetch()} disabled={query.isFetching}>
          <RefreshCw aria-hidden="true" className={cn(query.isFetching && "animate-spin")} />
          {t("refreshAction")}
        </Button>
        <Button type="button" variant="outline" onClick={() => download("word")} disabled={downloading !== null}>
          <Download aria-hidden="true" />
          {t("wordAction")}
        </Button>
        <Button type="button" onClick={() => download("pdf")} disabled={downloading !== null}>
          <Download aria-hidden="true" />
          {t("pdfAction")}
        </Button>
      </div>

      <div className="grid gap-6 lg:grid-cols-[16rem_minmax(0,1fr)]">
        <nav aria-label={t("contentsLabel")} className="self-start rounded-lg border border-line bg-surface p-4 lg:sticky lg:top-4">
          <p className="text-xs font-bold uppercase tracking-wide text-ink-muted">{t("contentsLabel")}</p>
          <ul className="mt-2 flex flex-col">
            {contents.map((item) => (
              <li key={item.id}>
                <a href={`#${item.id}`} className="flex items-center justify-between gap-2 rounded-md px-2 py-2 text-sm text-ink hover:bg-mint">
                  {item.label}
                  {item.done ? <Check className="size-4 text-green-600" aria-label={t("sectionDone")} /> : null}
                </a>
              </li>
            ))}
          </ul>
        </nav>

        <article className="flex flex-col gap-8 rounded-lg border border-line bg-surface p-6 sm:p-10">
          <header className="flex items-start justify-between gap-4 border-b-2 border-green-600 pb-4">
            <div className="flex items-center gap-3">
              <span className="flex size-12 items-center justify-center rounded-md bg-forest-900 text-lime" aria-hidden="true">
                <FileText className="size-6" />
              </span>
              <div>
                <p className="font-bold text-ink">{t("organizationName")}</p>
                <p className="text-sm text-ink-muted">{t("nationalSecretariat")}</p>
              </div>
            </div>
            <p className="font-mono text-sm text-ink">{report.referenceNumber}</p>
          </header>

          <div>
            <p className="text-sm font-bold uppercase tracking-wide text-green-700">{t("reportKind", { reportType: report.reportTypeName })}</p>
            <p className="text-4xl font-bold text-ink">{report.periodLabel}</p>
          </div>

          <section id="summary" aria-labelledby="summary-title" className="flex flex-col gap-3">
            <h2 id="summary-title" className="text-xl font-bold text-ink">
              {t("numbered", { number: 1, title: t("sectionSummary") })}
            </h2>
            {report.canEdit ? (
              <NarrativeEditor windowId={windowId} initial={report.narrative} updatedAt={report.narrativeUpdatedAt} key={report.narrative} />
            ) : report.narrative ? (
              <p className="whitespace-pre-line text-ink">{report.narrative}</p>
            ) : (
              <p className="text-ink-muted">{t("noSummary")}</p>
            )}
          </section>

          <section id="overview" aria-labelledby="overview-title" className="flex flex-col gap-4">
            <h2 id="overview-title" className="text-xl font-bold text-ink">
              {t("numbered", { number: 2, title: t("sectionOverview") })}
            </h2>
            <p className="text-ink">
              {t("overviewSentence", {
                expected: Number(report.overview.expected),
                onTime: Number(report.overview.onTime),
                late: Number(report.overview.late),
                notReported: Number(report.overview.notReported),
                acknowledged: Number(report.overview.acknowledged),
                date: `${fmt.dateTime(report.compiledAt)} (${fmt.offset(report.compiledAt)})`,
              })}
            </p>
            <div className="grid gap-3 sm:grid-cols-3">
              {report.overview.levels.map((level) => (
                <div key={Number(level.level)} className="rounded-md bg-ground p-4">
                  <p className="text-xs font-bold uppercase tracking-wide text-ink-muted">{level.levelPluralName}</p>
                  <p className="mt-1 text-2xl font-bold tabular-nums text-ink">
                    {Number(level.submitted)} / {Number(level.expected)}
                  </p>
                </div>
              ))}
            </div>
          </section>

          {report.sections.map((section) => (
            <section key={section.key} id={`section-${section.key}`} aria-labelledby={`section-${section.key}-title`} className="flex flex-col gap-3">
              <h2 id={`section-${section.key}-title`} className="text-xl font-bold text-ink">
                {t("numbered", { number: number++, title: section.title })}
              </h2>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[520px] border-collapse text-sm">
                  <thead>
                    <tr className="border-b border-line text-xs font-bold uppercase tracking-wide text-ink-muted">
                      <th scope="col" className="py-2 text-left">{t("columnFigure")}</th>
                      <th scope="col" className="py-2 text-right">{report.periodLabel}</th>
                      <th scope="col" className="py-2 text-right">{report.previousPeriodLabel ?? t("columnPrevious")}</th>
                      <th scope="col" className="py-2 text-right">{t("columnChange")}</th>
                      <th scope="col" className="py-2 text-right">{t("columnTarget")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {section.figures.map((figure) => (
                      <FigureRow key={figure.fieldKey} figure={figure} />
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          ))}

          <section id="highlights" aria-labelledby="highlights-title" className="flex flex-col gap-3">
            <h2 id="highlights-title" className="text-xl font-bold text-ink">
              {t("numbered", { number: number++, title: t("sectionHighlights") })}
            </h2>
            {highlights.length === 0 ? (
              <p className="text-ink-muted">{t("noHighlights")}</p>
            ) : (
              <ul className="list-disc pl-5 text-ink">
                {highlights.map((highlight, index) => (
                  <li key={index}>
                    <HighlightText highlight={highlight} figure={figures.get(highlight.fieldKey)} />
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section id="appendix" aria-labelledby="appendix-title" className="flex flex-col gap-3">
            <h2 id="appendix-title" className="text-xl font-bold text-ink">
              {t("sectionAppendix")}
            </h2>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[520px] border-collapse text-sm">
                <thead>
                  <tr className="border-b border-line text-xs font-bold uppercase tracking-wide text-ink-muted">
                    <th scope="col" className="py-2 text-left">{t("columnZone")}</th>
                    <th scope="col" className="py-2 text-right">{t("columnSubmitted")}</th>
                    {report.fields.map((field) => (
                      <th key={field.fieldKey} scope="col" className="py-2 text-right">
                        {field.label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {report.zones.map((zone) => (
                    <tr key={zone.unitId} className="border-b border-line last:border-0">
                      <th scope="row" className="py-2 text-left font-normal text-ink">{zone.name}</th>
                      <td className="py-2 text-right tabular-nums text-ink">
                        {Number(zone.submitted)} / {Number(zone.expected)}
                      </td>
                      {report.fields.map((field, index) => (
                        <td key={field.fieldKey} className="py-2 text-right tabular-nums text-ink">
                          {formatIndicator(toNumber(zone.values[index]), Number(field.type), field.unitLabel)}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </article>
      </div>
    </div>
  );
}

function FigureRow({ figure }: { figure: CompiledFigure }) {
  const type = Number(figure.type);
  const value = toNumber(figure.value);
  const previous = toNumber(figure.previous);
  const difference = change(previous, value, type);
  const kind = changeKind(type);
  return (
    <tr className="border-b border-line last:border-0">
      <th scope="row" className="py-2 text-left font-normal text-ink">{figure.label}</th>
      <td className="py-2 text-right tabular-nums text-ink">{formatIndicator(value, type, figure.unitLabel)}</td>
      <td className="py-2 text-right tabular-nums text-ink-muted">{formatIndicator(previous, type, figure.unitLabel)}</td>
      <td className={cn("py-2 text-right font-semibold tabular-nums", difference == null || difference === 0 ? "text-ink-muted" : difference > 0 ? "text-green-700" : "text-danger")}>
        {difference == null ? "—" : `${formatSigned(difference)}${kind === "percent" ? "%" : kind === "points" ? " pts" : ""}`}
      </td>
      <td className="py-2 text-right tabular-nums text-ink">{formatIndicator(toNumber(figure.target), type, figure.unitLabel)}</td>
    </tr>
  );
}

function HighlightText({ highlight, figure }: { highlight: CompiledHighlight; figure?: CompiledFigure }) {
  const t = useTranslations("Compiled");
  const type = Number(figure?.type ?? 2);
  const value = formatIndicator(toNumber(highlight.value), type, figure?.unitLabel);
  const delta = toNumber(highlight.change);
  const changeText = delta == null ? "" : formatSigned(delta);
  switch (Number(highlight.kind)) {
    case HIGHLIGHT_KIND.LargestRise:
      return <>{t("highlightRise", { field: highlight.fieldLabel, zone: highlight.zoneName, change: changeText, value })}</>;
    case HIGHLIGHT_KIND.LargestFall:
      return <>{t("highlightFall", { field: highlight.fieldLabel, zone: highlight.zoneName, change: changeText, value })}</>;
    default:
      return <>{t("highlightHighest", { field: highlight.fieldLabel, zone: highlight.zoneName, value })}</>;
  }
}

function NarrativeEditor({ windowId, initial, updatedAt }: { windowId: string; initial: string; updatedAt?: string | null }) {
  const fmt = useOrgFormat();
  const t = useTranslations("Compiled");
  const queryClient = useQueryClient();
  const save = useUpdateCompiledNarrative();
  const [text, setText] = useState(initial);
  const dirty = text !== initial;

  return (
    <div className="flex flex-col gap-2">
      <label htmlFor="compiled-narrative" className="text-sm text-ink-muted">
        {t("narrativeHelp")}
      </label>
      <Textarea id="compiled-narrative" value={text} onChange={(event) => setText(event.target.value)} rows={6} maxLength={MAX_NARRATIVE} />
      {save.error instanceof ApiError ? <p className="text-sm text-danger">{save.error.message}</p> : null}
      <div className="flex items-center gap-3">
        <Button
          type="button"
          size="sm"
          disabled={!dirty || save.isPending}
          onClick={() =>
            save.mutate(
              { windowId, data: { narrative: text } },
              { onSuccess: () => queryClient.invalidateQueries({ queryKey: getGetCompiledReportQueryKey(windowId) }) },
            )
          }
        >
          {t("saveNarrative")}
        </Button>
        {!dirty && updatedAt ? (
          <span className="text-xs text-ink-muted">{t("narrativeSaved", { date: fmt.dateTime(updatedAt) })}</span>
        ) : null}
      </div>
    </div>
  );
}
