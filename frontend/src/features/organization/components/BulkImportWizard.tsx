"use client";

import { Check, Download, Upload } from "lucide-react";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import type { ApiError } from "@/lib/api/client";
import type { BulkImportCommitResult, BulkImportSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getBulkImportTemplate,
  useCommitBulkImport,
  usePreviewBulkImport,
} from "@/lib/api/generated/organization/organization";
import { BulkImportColumns } from "@/features/organization/utils/bulk-import-columns";
import { downloadBlob } from "@/features/organization/utils/bulk-import";
import { BulkImportRowsTable } from "./BulkImportRowsTable";
import { BulkImportStepper, type BulkImportStep } from "./BulkImportStepper";

export function BulkImportWizard() {
  const t = useTranslations("Organization.import");
  const router = useRouter();

  const [step, setStep] = useState<BulkImportStep>("upload");
  const [file, setFile] = useState<File | null>(null);
  const [summary, setSummary] = useState<BulkImportSummary | null>(null);
  const [commitResult, setCommitResult] = useState<BulkImportCommitResult | null>(null);
  const [isDownloadingTemplate, setIsDownloadingTemplate] = useState(false);

  const preview = usePreviewBulkImport<ApiError>();
  const commit = useCommitBulkImport<ApiError>();

  const handleDownloadTemplate = async () => {
    setIsDownloadingTemplate(true);
    try {
      const blob = (await getBulkImportTemplate()) as Blob;
      downloadBlob(blob, "units_and_heads_template.xlsx");
    } finally {
      setIsDownloadingTemplate(false);
    }
  };

  const handleFileSelected = (selected: File) => {
    setFile(selected);
    preview.mutate(
      { data: { file: selected } },
      {
        onSuccess: (result) => {
          setSummary(result);
          setStep("columns");
        },
      },
    );
  };

  const handleImport = () => {
    if (!file) {
      return;
    }

    commit.mutate(
      { data: { file } },
      {
        onSuccess: (result) => {
          setCommitResult(result);
          setStep("done");
        },
      },
    );
  };

  const handleReupload = () => {
    setFile(null);
    setSummary(null);
    setStep("upload");
    preview.reset();
  };

  const totalRows = summary ? Number(summary.totalRows) : 0;
  const errorCount = summary ? Number(summary.errorCount) : 0;
  const importableCount = totalRows - errorCount;

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-xs text-ink-muted">{t("breadcrumb")}</p>
          <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
          {file ? (
            <p className="mt-1 text-sm text-ink-muted">
              {file.name} · {t("rowCount", { count: totalRows })}
            </p>
          ) : null}
        </div>
        <Button variant="outline" onClick={handleDownloadTemplate} disabled={isDownloadingTemplate}>
          <Download aria-hidden="true" />
          {t("downloadTemplate")}
        </Button>
      </div>

      <BulkImportStepper current={step} />

      {step === "upload" ? (
        <UploadStep onFileSelected={handleFileSelected} isPending={preview.isPending} error={preview.error ?? null} />
      ) : null}

      {step === "columns" ? <ColumnsStep onBack={handleReupload} onContinue={() => setStep("rows")} /> : null}

      {step === "rows" && summary ? (
        <div className="flex flex-col gap-4">
          <BulkImportRowsTable summary={summary} />
          <p className="rounded-md bg-status-upcoming-bg px-4 py-3 text-sm text-status-upcoming-fg">{t("dryRunNotice")}</p>
          {commit.isError ? (
            <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
              {commit.error.message}
            </p>
          ) : null}
          <div className="flex flex-wrap items-center justify-between gap-3">
            <Button variant="outline" onClick={() => setStep("columns")}>
              {t("back")}
            </Button>
            <div className="flex gap-2">
              <Button variant="outline" onClick={handleReupload}>
                {t("reupload")}
              </Button>
              <Button onClick={handleImport} disabled={commit.isPending || importableCount === 0}>
                {commit.isPending ? t("importing") : t("importButton", { count: importableCount, errors: errorCount })}
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {step === "done" && commitResult ? (
        <DoneStep
          result={commitResult}
          onViewOrganization={() => router.push("/secretariat/organization")}
          onImportAnother={handleReupload}
        />
      ) : null}
    </div>
  );
}

function UploadStep({
  onFileSelected,
  isPending,
  error,
}: {
  onFileSelected: (file: File) => void;
  isPending: boolean;
  error: ApiError | null;
}) {
  const t = useTranslations("Organization.import");
  const inputRef = useRef<HTMLInputElement>(null);

  return (
    <div className="flex flex-col items-center gap-4 rounded-lg border border-dashed border-line-strong bg-surface p-10 text-center">
      <span className="flex size-14 items-center justify-center rounded-full bg-mint text-green-600">
        <Upload className="size-6" aria-hidden="true" />
      </span>
      <div>
        <p className="font-medium text-ink">{t("uploadPrompt")}</p>
        <p className="mt-1 text-sm text-ink-muted">{t("uploadHint")}</p>
      </div>
      <input
        ref={inputRef}
        type="file"
        accept=".xlsx,.csv"
        className="sr-only"
        disabled={isPending}
        onChange={(event) => {
          const selected = event.target.files?.[0];
          if (selected) {
            onFileSelected(selected);
          }
          event.target.value = "";
        }}
      />
      <Button onClick={() => inputRef.current?.click()} disabled={isPending}>
        {isPending ? t("checking") : t("chooseFile")}
      </Button>
      {error ? (
        <p role="alert" className="max-w-md text-sm text-danger">
          {error.message}
        </p>
      ) : null}
    </div>
  );
}

function ColumnsStep({ onBack, onContinue }: { onBack: () => void; onContinue: () => void }) {
  const t = useTranslations("Organization.import");

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-6">
      <div>
        <h2 className="text-lg font-semibold text-ink">{t("columnsHeading")}</h2>
        <p className="mt-1 text-sm text-ink-muted">{t("columnsDescription")}</p>
      </div>
      <ul className="grid grid-cols-2 gap-x-6 gap-y-2 sm:grid-cols-3">
        {BulkImportColumns.map((column) => (
          <li key={column} className="flex items-center gap-2 text-sm text-ink">
            <Check className="size-4 shrink-0 text-green-600" aria-hidden="true" />
            {column}
          </li>
        ))}
      </ul>
      <div className="flex justify-between">
        <Button variant="outline" onClick={onBack}>
          {t("back")}
        </Button>
        <Button onClick={onContinue}>{t("continue")}</Button>
      </div>
    </div>
  );
}

function DoneStep({
  result,
  onViewOrganization,
  onImportAnother,
}: {
  result: BulkImportCommitResult;
  onViewOrganization: () => void;
  onImportAnother: () => void;
}) {
  const t = useTranslations("Organization.import");

  return (
    <div className="flex flex-col items-center gap-4 rounded-lg border border-line bg-surface p-10 text-center">
      <span className="flex size-14 items-center justify-center rounded-full bg-mint text-green-600">
        <Check className="size-6" aria-hidden="true" />
      </span>
      <div>
        <h2 className="text-lg font-semibold text-ink">{t("doneHeading")}</h2>
        <p className="mt-1 max-w-md text-sm text-ink-muted">
          {t("doneSummary", {
            created: Number(result.createdCount),
            updated: Number(result.updatedCount),
            invited: Number(result.invitedCount),
            skipped: Number(result.skippedErrorCount),
          })}
        </p>
      </div>
      <div className="flex gap-2">
        <Button variant="outline" onClick={onImportAnother}>
          {t("importAnother")}
        </Button>
        <Button onClick={onViewOrganization}>{t("viewOrganization")}</Button>
      </div>
    </div>
  );
}
