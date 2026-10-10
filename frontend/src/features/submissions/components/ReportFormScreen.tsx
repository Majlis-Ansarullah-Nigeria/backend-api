"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, ArrowRight, Check, Copy, Send, Undo2 } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useForm, useWatch, type Resolver } from "react-hook-form";
import { ErrorState } from "@/components/shared/ErrorState";
import { ReadOnlyBanner } from "@/components/shared/ReadOnlyBanner";
import { SaveIndicator, type SaveStatus } from "@/components/shared/SaveIndicator";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api/client";
import type { ReportInstanceForEditing } from "@/lib/api/generated/hRSApiV1.schemas";
import { getGetMyReportsQueryKey } from "@/lib/api/generated/submissions/submissions";
import {
  getReportPreviousAnswers,
  saveReportDraft,
  submitReport,
  useGetReportInstanceForEditing,
  withdrawReport,
} from "@/lib/api/generated/submissions/submissions";
import { buildFormSchema } from "@/lib/form-engine/field-schema";
import { TEMPLATE_FIELD_TYPE } from "@/lib/form-engine/field-types";
import { SectionForm } from "@/lib/form-engine/components/SectionForm";
import { clearPendingDraft, draftKey, getPendingDraft, savePendingDraft } from "@/lib/offline/draft-queue";
import { countdownText } from "@/features/submissions/utils/countdown";
import {
  canSubmitFromStatus,
  REPORT_INSTANCE_STATUS,
  reportInstanceStatusChipVariant,
} from "@/features/submissions/utils/report-status";
import { sectionProgress } from "@/features/submissions/utils/section-progress";

const AUTOSAVE_DELAY_MS = 10_000;

/** FR-SUB-008's "text and settings fields" — figures (Number/Currency/Percentage), dates,
 * location, attachments and structural list fields (RepeatableTable/MemberList) don't carry over
 * from one period to the next the way a description or a yes/no setting does. */
const COPYABLE_FIELD_TYPES: ReadonlySet<number> = new Set([
  TEMPLATE_FIELD_TYPE.ShortText,
  TEMPLATE_FIELD_TYPE.LongText,
  TEMPLATE_FIELD_TYPE.YesNo,
  TEMPLATE_FIELD_TYPE.SingleChoice,
  TEMPLATE_FIELD_TYPE.MultipleChoice,
  TEMPLATE_FIELD_TYPE.Dropdown,
  TEMPLATE_FIELD_TYPE.Rating,
]);

export function ReportFormScreen({ id, unitId }: { id: string; unitId: string }) {
  const t = useTranslations("ReportForm");
  const router = useRouter();
  const queryClient = useQueryClient();
  const key = draftKey(id, unitId);
  const nilSubmitIdempotencyKey = useRef(crypto.randomUUID());
  const [copyStatus, setCopyStatus] = useState<"idle" | "loading" | "none">("idle");

  const query = useGetReportInstanceForEditing<ReportInstanceForEditing, ApiError>(id, { unit: unitId });

  const [activeSectionIndex, setActiveSectionIndex] = useState(0);
  const [saveStatus, setSaveStatus] = useState<SaveStatus | null>(null);
  const [rowVersion, setRowVersion] = useState<string | null>(null);
  const [hydrated, setHydrated] = useState(false);

  // A function, not a pre-built zodResolver: showIf-gated fields' required-ness depends on the
  // CURRENT answers, so the schema has to be rebuilt from the live `values` react-hook-form hands
  // the resolver at validate time — a resolver frozen at mount (or at the last render's answers)
  // would validate every conditional field as if its trigger were never visible.
  const resolver: Resolver<Record<string, unknown>> | undefined = query.data
    ? (values, context, options) =>
        zodResolver(buildFormSchema(query.data!.schema.sections, Number(query.data!.unitLevel), values))(values, context, options)
    : undefined;

  const form = useForm<Record<string, unknown>>({ defaultValues: {}, resolver });

  const answers = useWatch({ control: form.control });

  // Seeds the form once the report loads — and prefers a still-unsynced offline draft over the
  // server's answers, since that draft represents newer work the server never received
  // (FR-SUB-003: "drafts survive closing the browser").
  useEffect(() => {
    if (!query.data || hydrated) {
      return;
    }

    let cancelled = false;
    (async () => {
      let pending: Awaited<ReturnType<typeof getPendingDraft>> = null;
      try {
        pending = await getPendingDraft(key);
      } catch {
        // No IndexedDB (private window, unsupported browser) — fall back to the server's answers.
      }

      if (cancelled) {
        return;
      }

      setRowVersion(query.data!.rowVersion);

      if (pending) {
        form.reset(pending.answers as Record<string, unknown>);
        setSaveStatus("offline");
      } else {
        form.reset(query.data!.answers as Record<string, unknown>);
      }

      setHydrated(true);
    })();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query.data, hydrated]);

  const saveMutation = useMutation({
    mutationFn: async (vars: { answers: Record<string, unknown>; ifMatch: string | null }) => {
      const fileFieldKeys = new Set(
        (query.data?.schema.sections ?? [])
          .flatMap((section) => section.fields)
          .filter((f) => f.type === TEMPLATE_FIELD_TYPE.FileUpload)
          .map((f) => f.key),
      );
      const stripped = stripFileFields(vars.answers, fileFieldKeys);
      return saveReportDraft(
        id,
        { answers: stripped },
        { unit: unitId },
        vars.ifMatch ? { headers: { "If-Match": `"${vars.ifMatch}"` } } : undefined,
      );
    },
    onSuccess: async (result) => {
      setRowVersion(result.rowVersion);
      setSaveStatus("saved");
      try {
        await clearPendingDraft(key);
      } catch {
        // Nothing queued, or no IndexedDB — fine either way.
      }
      queryClient.invalidateQueries({ queryKey: getGetMyReportsQueryKey() });
    },
    onError: async (error, vars) => {
      if (error instanceof ApiError) {
        setSaveStatus("error");
        if (error.code === "WINDOW_CLOSED" || error.code === "WINDOW_NOT_OPEN" || error.code === "REPORT_LOCKED") {
          query.refetch();
        }
        return;
      }

      // Not an ApiError — the request never reached the server (offline). Queue it.
      try {
        await savePendingDraft(key, vars.answers, rowVersion);
        setSaveStatus("offline");
      } catch {
        setSaveStatus("error");
      }
    },
  });

  const withdrawMutation = useMutation({
    mutationFn: () => withdrawReport(id, { unit: unitId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: getGetMyReportsQueryKey() });
      query.refetch();
    },
  });

  const nilSubmitMutation = useMutation({
    mutationFn: () =>
      submitReport(
        id,
        { confirmed: true, isNil: true },
        { unit: unitId },
        { headers: { "Idempotency-Key": nilSubmitIdempotencyKey.current } },
      ),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: getGetMyReportsQueryKey() });
      router.push(`/reports/${id}/submitted?unit=${unitId}`);
    },
  });

  const canSave = query.data?.canSave ?? false;

  // Refs, not deps — the debounce effect should only restart when the answers themselves change,
  // not when saveMutation/rowVersion are recreated by an unrelated render (same reasoning as
  // BuildTab.tsx's autosave).
  const latest = useRef({ saveMutation, rowVersion, canSave });
  useEffect(() => {
    latest.current = { saveMutation, rowVersion, canSave };
  });

  const isFirstRun = useRef(true);
  useEffect(() => {
    if (!hydrated) {
      return;
    }
    if (isFirstRun.current) {
      isFirstRun.current = false;
      return;
    }
    if (!latest.current.canSave) {
      return;
    }

    setSaveStatus("saving");
    const timer = setTimeout(() => {
      latest.current.saveMutation.mutate({ answers: form.getValues(), ifMatch: latest.current.rowVersion });
    }, AUTOSAVE_DELAY_MS);

    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [answers, hydrated]);

  const onBlurCapture = () => {
    if (!hydrated || !latest.current.canSave) {
      return;
    }
    setSaveStatus("saving");
    latest.current.saveMutation.mutate({ answers: form.getValues(), ifMatch: latest.current.rowVersion });
  };

  // Flush a queued offline draft as soon as the browser comes back online.
  useEffect(() => {
    const onOnline = () => {
      if (!latest.current.canSave) {
        return;
      }
      getPendingDraft(key)
        .then((pending) => {
          if (pending) {
            latest.current.saveMutation.mutate({ answers: pending.answers as Record<string, unknown>, ifMatch: pending.ifMatch });
          }
        })
        .catch(() => undefined);
    };

    window.addEventListener("online", onOnline);
    return () => window.removeEventListener("online", onOnline);
  }, [key]);

  if (query.isPending) {
    return <p className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</p>;
  }

  if (query.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      </div>
    );
  }

  const data = query.data;
  const sections = data.schema.sections;
  const activeSection = sections[activeSectionIndex];
  const progress = sectionProgress(sections, Number(data.unitLevel), (answers as Record<string, unknown>) ?? {});

  const goNext = () => setActiveSectionIndex((index) => Math.min(index + 1, sections.length - 1));
  const goBack = () => setActiveSectionIndex((index) => Math.max(index - 1, 0));

  // FR-SUB-008: prefills text/settings fields (not figures — see COPYABLE_FIELD_TYPES) from the
  // unit's last submission of the same report type, then saves immediately rather than waiting
  // for the 10s autosave timer.
  const copyFromLastPeriod = async () => {
    setCopyStatus("loading");
    try {
      const previous = await getReportPreviousAnswers(id, { unit: unitId });
      if (!previous) {
        setCopyStatus("none");
        return;
      }

      const copyableFieldKeys = new Set(
        sections.flatMap((section) => section.fields).filter((f) => COPYABLE_FIELD_TYPES.has(f.type)).map((f) => f.key),
      );
      const merged = { ...form.getValues() };
      for (const [fieldKey, value] of Object.entries(previous.answers)) {
        if (copyableFieldKeys.has(fieldKey)) {
          merged[fieldKey] = value;
        }
      }

      form.reset(merged);
      setCopyStatus("idle");
      setSaveStatus("saving");
      latest.current.saveMutation.mutate({ answers: merged, ifMatch: latest.current.rowVersion });
    } catch {
      setCopyStatus("none");
    }
  };

  return (
    <div className="flex flex-col gap-4 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <Link
            href="/reports"
            aria-label={t("backToReports")}
            className="mt-1 flex size-9 shrink-0 items-center justify-center rounded-md border border-line-strong hover:bg-ground"
          >
            <ArrowLeft className="size-4" aria-hidden="true" />
          </Link>
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-xl font-bold text-ink sm:text-2xl">
                {data.reportTypeName} · {data.periodLabel}
              </h1>
              <StatusChip status={reportInstanceStatusChipVariant(data.status)} />
            </div>
            <p className="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
              <span>{data.unitName}</span>
              {data.dueAt ? <span>· {countdownText(data.dueAt)}</span> : null}
              {saveStatus ? <SaveIndicator status={saveStatus} /> : null}
            </p>
          </div>
        </div>

        {canSubmitFromStatus(data.status) ? (
          <Link href={`/reports/${id}/review?unit=${unitId}`}>
            <Button type="button">{t("reviewAndSubmit")}</Button>
          </Link>
        ) : null}

        {data.status === REPORT_INSTANCE_STATUS.Submitted ? (
          <Button type="button" variant="outline" onClick={() => withdrawMutation.mutate()} disabled={withdrawMutation.isPending}>
            <Undo2 className="size-4" aria-hidden="true" />
            {t("withdraw")}
          </Button>
        ) : null}
      </div>

      {withdrawMutation.error instanceof ApiError ? <p className="text-sm text-danger">{withdrawMutation.error.message}</p> : null}
      {nilSubmitMutation.error instanceof ApiError ? <p className="text-sm text-danger">{nilSubmitMutation.error.message}</p> : null}

      {!canSave ? <ReadOnlyBanner /> : null}

      {canSave ? (
        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" variant="outline" size="sm" onClick={() => void copyFromLastPeriod()} disabled={copyStatus === "loading"}>
            <Copy className="size-4" aria-hidden="true" />
            {copyStatus === "loading" ? t("copyingFromLastPeriod") : t("copyFromLastPeriod")}
          </Button>
          {data.allowNilReport ? (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => nilSubmitMutation.mutate()}
              disabled={nilSubmitMutation.isPending}
            >
              <Send className="size-4" aria-hidden="true" />
              {t("noActivityThisPeriod")}
            </Button>
          ) : null}
          {copyStatus === "none" ? <span className="text-sm text-ink-muted">{t("noPreviousSubmission")}</span> : null}
        </div>
      ) : null}

      <div className="flex flex-col gap-4 lg:flex-row">
        <aside className="hidden lg:block lg:w-64 lg:shrink-0">
          <div className="sticky top-4 rounded-lg border border-line bg-surface p-4">
            <div className="flex items-center justify-between text-xs font-semibold uppercase tracking-wide text-ink-muted">
              <span>{t("sectionsLabel")}</span>
              <span>
                {progress.completed}/{progress.total}
              </span>
            </div>
            <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-ground">
              <div
                className="h-full bg-green-600 transition-all"
                style={{ width: `${progress.total === 0 ? 0 : (progress.completed / progress.total) * 100}%` }}
              />
            </div>
            <nav className="mt-4 flex flex-col gap-1">
              {sections.map((section, index) => {
                const complete =
                  index < progress.total && isComplete(sections, index, Number(data.unitLevel), (answers as Record<string, unknown>) ?? {});
                return (
                  <button
                    key={section.key}
                    type="button"
                    onClick={() => setActiveSectionIndex(index)}
                    className={`flex items-center gap-2 rounded-md px-3 py-2 text-left text-sm font-medium ${
                      index === activeSectionIndex ? "bg-mint text-green-700" : "text-ink hover:bg-ground"
                    }`}
                  >
                    {complete ? (
                      <Check className="size-4 shrink-0 text-green-600" aria-hidden="true" />
                    ) : (
                      <span className="flex size-4 shrink-0 items-center justify-center rounded-full border border-line-strong text-[10px]">
                        {index + 1}
                      </span>
                    )}
                    {section.title}
                  </button>
                );
              })}
            </nav>
          </div>
        </aside>

        <div className="flex-1">
          <div className="mb-3 flex gap-1 lg:hidden" aria-hidden="true">
            {sections.map((section, index) => (
              <span
                key={section.key}
                className={`h-1.5 flex-1 rounded-full ${index <= activeSectionIndex ? "bg-green-600" : "bg-ground"}`}
              />
            ))}
          </div>

          {activeSection ? (
            <div className="rounded-lg border border-line bg-surface p-4 sm:p-6" onBlurCapture={onBlurCapture}>
              <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
                {t("sectionOf", { number: activeSectionIndex + 1, total: sections.length })}
              </p>
              <h2 className="mt-1 text-lg font-bold text-ink">{activeSection.title}</h2>

              <fieldset disabled={!canSave} className="mt-4">
                <SectionForm
                  section={activeSection}
                  unitLevel={Number(data.unitLevel)}
                  form={form}
                  uploadContext={{ reportId: id, unitId }}
                />
              </fieldset>

              <div className="mt-6 flex justify-between gap-2">
                <Button type="button" variant="outline" onClick={goBack} disabled={activeSectionIndex === 0}>
                  {t("back")}
                </Button>
                {activeSectionIndex < sections.length - 1 ? (
                  <Button type="button" onClick={goNext}>
                    {t("nextSection", { title: sections[activeSectionIndex + 1]?.title ?? "" })}
                    <ArrowRight className="size-4" aria-hidden="true" />
                  </Button>
                ) : null}
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}

/** FileUpload fields are never part of the autosaved answers JSON — their values are managed by
 * their own presign/complete/delete endpoints (FR-SUB-005) and re-injected by the server on every
 * load from the authoritative ReportAttachment rows, so saving them here would just be redundant
 * (and could drift out of sync with what's actually been uploaded). */
function stripFileFields(answers: Record<string, unknown>, fileFieldKeys: Set<string>): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(answers)) {
    if (fileFieldKeys.has(key)) {
      continue;
    }
    result[key] = value;
  }
  return result;
}

function isComplete(
  sections: ReportInstanceForEditing["schema"]["sections"],
  index: number,
  unitLevel: number,
  answers: Record<string, unknown>,
): boolean {
  const section = sections[index];
  if (!section) {
    return false;
  }
  return sectionProgress([section], unitLevel, answers).completed === 1;
}
