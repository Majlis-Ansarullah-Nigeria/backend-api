"use client";

import { Mail } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import type { ApiError } from "@/lib/api/client";
import type { NotificationTemplateSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetNotificationTemplates, useUpdateNotificationTemplate } from "@/lib/api/generated/notifications/notifications";
import { NOTIFICATION_EVENT_TYPE_ORDER, notificationEventTypeLabel } from "@/features/notifications/utils/notification-event-type";

const PLACEHOLDERS = ["{{headName}}", "{{unitName}}", "{{dueDate}}", "{{link}}"];

export function AnnouncementsScreen() {
  const t = useTranslations("Announcements");
  const templates = useGetNotificationTemplates<NotificationTemplateSummary[], ApiError>();
  const updateTemplate = useUpdateNotificationTemplate();

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [savedAt, setSavedAt] = useState<number | null>(null);

  const sorted = (templates.data ?? [])
    .slice()
    .sort((a, b) => NOTIFICATION_EVENT_TYPE_ORDER.indexOf(a.eventType) - NOTIFICATION_EVENT_TYPE_ORDER.indexOf(b.eventType));
  const selected = sorted.find((t) => t.id === selectedId) ?? null;

  useEffect(() => {
    if (!selectedId && sorted.length > 0) {
      setSelectedId(sorted[0]!.id);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sorted.length]);

  useEffect(() => {
    if (selected) {
      setSubject(selected.subject);
      setBody(selected.body);
      setSavedAt(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected?.id]);

  const selectTemplate = (id: string) => {
    setSelectedId(id);
  };

  const save = () => {
    if (!selected) {
      return;
    }
    updateTemplate.mutate(
      { id: selected.id, data: { subject, body } },
      { onSuccess: () => setSavedAt(Date.now()) },
    );
  };

  if (templates.isPending) {
    return <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (templates.isError) {
    return (
      <div className="p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => templates.refetch()} />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
        <div className="rounded-lg border border-line bg-surface">
          {sorted.length === 0 ? (
            <div className="p-2">
              <EmptyState icon={Mail} title={t("emptyTitle")} description={t("emptyDescription")} />
            </div>
          ) : (
            <ul className="flex flex-col">
              {sorted.map((template) => (
                <li key={template.id}>
                  <button
                    type="button"
                    onClick={() => selectTemplate(template.id)}
                    className={`block w-full border-b border-line px-4 py-3 text-left text-sm last:border-0 hover:bg-ground ${
                      selectedId === template.id ? "bg-mint font-semibold text-green-700" : "text-ink"
                    }`}
                  >
                    {notificationEventTypeLabel(template.eventType)}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div>
          {selected ? (
            <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-4 sm:p-6">
              <h2 className="font-bold text-ink">{notificationEventTypeLabel(selected.eventType)}</h2>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="template-subject">{t("subjectLabel")}</Label>
                <Input id="template-subject" value={subject} onChange={(event) => setSubject(event.target.value)} />
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="template-body">{t("bodyLabel")}</Label>
                <Textarea id="template-body" value={body} onChange={(event) => setBody(event.target.value)} rows={8} />
              </div>

              <div className="rounded-md bg-ground p-3 text-xs text-ink-muted">
                <p className="font-semibold text-ink">{t("placeholdersLabel")}</p>
                <p className="mt-1 font-mono">{PLACEHOLDERS.join("  ")}</p>
              </div>

              {updateTemplate.error ? <p className="text-sm text-danger">{(updateTemplate.error as ApiError).message}</p> : null}

              <div className="flex items-center gap-3">
                <Button
                  type="button"
                  onClick={save}
                  disabled={updateTemplate.isPending || !subject.trim() || !body.trim()}
                >
                  {t("saveButton")}
                </Button>
                {savedAt ? <span className="text-sm text-ink-muted">{t("savedConfirmation")}</span> : null}
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
