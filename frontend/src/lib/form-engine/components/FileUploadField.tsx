import { Paperclip, Trash2, Upload } from "lucide-react";
import { useTranslations } from "next-intl";
import { useId, useRef, useState } from "react";
import { Controller, type UseFormReturn } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api/client";
import type { AttachmentSummary, TemplateField } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  completeAttachmentUpload,
  deleteAttachment,
  presignAttachmentUpload,
} from "@/lib/api/generated/submissions/submissions";
import { compressImageIfWorthwhile } from "@/features/submissions/utils/compress-image";

const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024;

function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  const kb = bytes / 1024;
  if (kb < 1024) {
    return `${kb.toFixed(0)} KB`;
  }
  return `${(kb / 1024).toFixed(1)} MB`;
}

interface InFlightUpload {
  key: string;
  fileName: string;
  status: "uploading" | "error";
  errorMessage?: string;
}

export function FileUploadField({
  field,
  form,
  uploadContext,
}: {
  field: TemplateField;
  form: UseFormReturn<Record<string, unknown>>;
  uploadContext?: { reportId: string; unitId: string };
}) {
  const t = useTranslations("FormEngine");
  const inputId = useId();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [inFlight, setInFlight] = useState<InFlightUpload[]>([]);

  if (!uploadContext) {
    return (
      <div className="flex items-center gap-2 rounded-md border border-dashed border-line-strong bg-ground px-3 py-3 text-sm text-ink-muted">
        <Upload className="size-4 shrink-0" aria-hidden="true" />
        {t("fileUploadPreviewOnly")}
      </div>
    );
  }

  const { reportId, unitId } = uploadContext;

  return (
    <Controller
      control={form.control}
      name={field.key as never}
      defaultValue={[] as never}
      render={({ field: controller }) => {
        const attachments: AttachmentSummary[] = Array.isArray(controller.value) ? controller.value : [];

        const uploadOne = async (file: File) => {
          const key = `${file.name}:${file.size}:${crypto.randomUUID()}`;
          setInFlight((prev) => [...prev, { key, fileName: file.name, status: "uploading" }]);

          try {
            if (file.size > MAX_FILE_SIZE_BYTES) {
              throw new Error(t("fileTooLarge"));
            }

            const compressed = await compressImageIfWorthwhile(file);

            const presigned = await presignAttachmentUpload(reportId, {
              fieldKey: field.key,
              fileName: compressed.name,
              contentType: compressed.type,
              sizeBytes: compressed.size,
            }, { unit: unitId });

            const putResponse = await fetch(presigned.uploadUrl, {
              method: "PUT",
              body: compressed,
              headers: { "Content-Type": compressed.type },
            });
            if (!putResponse.ok) {
              throw new Error(t("uploadFailed"));
            }

            const summary = await completeAttachmentUpload(reportId, presigned.attachmentId, { unit: unitId });

            controller.onChange([...attachments, summary]);
            setInFlight((prev) => prev.filter((item) => item.key !== key));
          } catch (error) {
            const message = error instanceof ApiError ? error.message : error instanceof Error ? error.message : t("uploadFailed");
            setInFlight((prev) => prev.map((item) => (item.key === key ? { ...item, status: "error", errorMessage: message } : item)));
          }
        };

        const onFilesSelected = (files: FileList | null) => {
          if (!files) {
            return;
          }
          for (const file of Array.from(files)) {
            void uploadOne(file);
          }
          if (fileInputRef.current) {
            fileInputRef.current.value = "";
          }
        };

        const removeAttachment = async (attachment: AttachmentSummary) => {
          controller.onChange(attachments.filter((a) => a.id !== attachment.id));
          try {
            await deleteAttachment(reportId, attachment.id, { unit: unitId });
          } catch {
            // The row is already gone from the form; a failed cleanup call just leaves an orphaned
            // object in storage — not worth re-adding the row and confusing the user over.
          }
        };

        const dismissError = (key: string) => setInFlight((prev) => prev.filter((item) => item.key !== key));

        return (
          <div className="flex flex-col gap-2">
            {attachments.map((attachment) => (
              <div key={attachment.id} className="flex items-center gap-2 rounded-md border border-line bg-surface px-3 py-2 text-sm">
                <Paperclip className="size-4 shrink-0 text-ink-muted" aria-hidden="true" />
                <span className="flex-1 truncate text-ink">{attachment.fileName}</span>
                <span className="shrink-0 text-xs text-ink-muted">{formatBytes(Number(attachment.sizeBytes))}</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  aria-label={t("removeFile", { name: attachment.fileName })}
                  onClick={() => void removeAttachment(attachment)}
                >
                  <Trash2 className="size-4" aria-hidden="true" />
                </Button>
              </div>
            ))}

            {inFlight.map((item) => (
              <div
                key={item.key}
                className={`flex items-center gap-2 rounded-md border px-3 py-2 text-sm ${
                  item.status === "error" ? "border-danger/40 bg-danger/5 text-danger" : "border-line bg-ground text-ink-muted"
                }`}
              >
                <span className="flex-1 truncate">{item.status === "error" ? (item.errorMessage ?? t("uploadFailed")) : t("uploading", { name: item.fileName })}</span>
                {item.status === "error" ? (
                  <Button type="button" variant="ghost" size="sm" onClick={() => dismissError(item.key)}>
                    {t("dismiss")}
                  </Button>
                ) : null}
              </div>
            ))}

            <label
              htmlFor={inputId}
              className="flex cursor-pointer items-center justify-center gap-2 rounded-md border border-dashed border-line-strong px-3 py-3 text-sm font-medium text-green-700 hover:bg-mint"
            >
              <Upload className="size-4 shrink-0" aria-hidden="true" />
              {t("addFiles")}
              <input
                ref={fileInputRef}
                id={inputId}
                type="file"
                multiple
                accept="image/jpeg,image/png,image/webp,image/gif,application/pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx"
                className="sr-only"
                onChange={(event) => onFilesSelected(event.target.files)}
              />
            </label>
          </div>
        );
      }}
    />
  );
}
