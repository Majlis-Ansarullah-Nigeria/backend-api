"use client";

import { Mail } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";
import type { ApiError } from "@/lib/api/client";
import type { AnnouncementSummary, NotificationTemplateSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetAnnouncementsQueryKey,
  useGetAnnouncementAudienceCounts,
  useGetAnnouncements,
  useGetNotificationTemplates,
  useSendAnnouncement,
  useUpdateNotificationTemplate,
} from "@/lib/api/generated/notifications/notifications";
import { NOTIFICATION_EVENT_TYPE_ORDER, notificationEventTypeLabel } from "@/features/notifications/utils/notification-event-type";
import { ANNOUNCEMENT_AUDIENCE, ANNOUNCEMENT_AUDIENCE_ORDER, announcementAudienceLabel } from "@/features/notifications/utils/announcement-audience";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

const PLACEHOLDERS = ["{{headName}}", "{{unitName}}", "{{dueDate}}", "{{link}}"];

type AnnouncementsTabKey = "announcements" | "wording";

export function AnnouncementsScreen() {
  const t = useTranslations("Announcements");
  const [tab, setTab] = useState<AnnouncementsTabKey>("announcements");

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>

      <div className="flex gap-1 border-b border-line" role="tablist" aria-label={t("tabsLabel")}>
        <TabButton active={tab === "announcements"} onClick={() => setTab("announcements")} label={t("tabAnnouncements")} />
        <TabButton active={tab === "wording"} onClick={() => setTab("wording")} label={t("tabWording")} />
      </div>

      {tab === "announcements" ? <AnnouncementsTab /> : <EmailWordingTab />}
    </div>
  );
}

function TabButton({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        "h-10 border-b-2 px-3.5 text-sm font-bold transition-colors",
        active ? "border-green-600 text-green-700" : "border-transparent text-ink-muted hover:text-ink",
      )}
    >
      {label}
    </button>
  );
}

function AnnouncementsTab() {
  const fmt = useOrgFormat();
  const t = useTranslations("Announcements");
  const queryClient = useQueryClient();

  const audienceCounts = useGetAnnouncementAudienceCounts();
  const history = useGetAnnouncements<AnnouncementSummary[], ApiError>();
  const sendAnnouncement = useSendAnnouncement<ApiError>();

  const [title, setTitle] = useState("");
  const [message, setMessage] = useState("");
  const [audience, setAudience] = useState<number>(ANNOUNCEMENT_AUDIENCE.AllHeads);
  const [showAsBanner, setShowAsBanner] = useState(true);
  const [sendEmail, setSendEmail] = useState(true);

  const send = () => {
    sendAnnouncement.mutate(
      { data: { title: title.trim(), message: message.trim(), audience, showAsBanner, sendEmail } },
      {
        onSuccess: () => {
          setTitle("");
          setMessage("");
          queryClient.invalidateQueries({ queryKey: getGetAnnouncementsQueryKey() });
        },
      },
    );
  };

  const countsByAudience = new Map((audienceCounts.data ?? []).map((c) => [c.audience, c.recipientCount]));

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_340px]">
      <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-4 sm:p-6">
        <h2 className="font-bold text-ink">{t("newAnnouncementHeading")}</h2>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="announcement-title">{t("titleLabel")}</Label>
          <Input id="announcement-title" value={title} onChange={(event) => setTitle(event.target.value)} />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="announcement-message">{t("messageLabel")}</Label>
          <Textarea id="announcement-message" value={message} onChange={(event) => setMessage(event.target.value)} rows={5} />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="announcement-audience">{t("sendToLabel")}</Label>
          <select
            id="announcement-audience"
            value={audience}
            onChange={(event) => setAudience(Number(event.target.value))}
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            {ANNOUNCEMENT_AUDIENCE_ORDER.map((value) => (
              <option key={value} value={value}>
                {announcementAudienceLabel(value)}
                {countsByAudience.has(value) ? ` (${countsByAudience.get(value)})` : ""}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-2">
          <span className="text-sm font-bold text-ink">{t("showAsLabel")}</span>
          <div className="flex flex-wrap gap-4">
            <label className="flex items-center gap-2">
              <Checkbox checked={showAsBanner} onCheckedChange={(checked) => setShowAsBanner(checked === true)} />
              <span className="text-sm text-ink">{t("showAsBannerOption")}</span>
            </label>
            <label className="flex items-center gap-2">
              <Checkbox checked={sendEmail} onCheckedChange={(checked) => setSendEmail(checked === true)} />
              <span className="text-sm text-ink">{t("showAsEmailOption")}</span>
            </label>
          </div>
        </div>

        {sendAnnouncement.error ? <p className="text-sm text-danger">{sendAnnouncement.error.message}</p> : null}

        <div>
          <Button
            type="button"
            onClick={send}
            disabled={sendAnnouncement.isPending || !title.trim() || !message.trim()}
          >
            {t("sendButton")}
          </Button>
        </div>
      </div>

      <div className="rounded-lg border border-line bg-surface p-4 sm:p-6">
        <h2 className="mb-3 font-bold text-ink">{t("sentHeading")}</h2>
        {history.isPending ? (
          <p className="text-sm text-ink-muted">{t("loading")}</p>
        ) : history.isError ? (
          <ErrorState title={t("sentErrorTitle")} onRetry={() => history.refetch()} />
        ) : history.data.length === 0 ? (
          <EmptyState icon={Mail} title={t("emptyTitle")} description={t("emptyDescription")} />
        ) : (
          <ul className="flex flex-col divide-y divide-line">
            {history.data.map((announcement) => (
              <li key={announcement.id} className="flex flex-col gap-1 py-3 first:pt-0 last:pb-0">
                <span className="text-sm font-bold text-ink">{announcement.title}</span>
                <span className="text-xs font-medium text-ink-muted">
                  {fmt.dayMonth(announcement.createdAt)}
                  {" · "}
                  {announcementAudienceLabel(announcement.audience)}
                  {" · "}
                  {t("deliveredCount", { count: Number(announcement.deliveredCount) })}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function EmailWordingTab() {
  const t = useTranslations("Announcements");
  const templates = useGetNotificationTemplates<NotificationTemplateSummary[], ApiError>();
  const updateTemplate = useUpdateNotificationTemplate();

  const [selectedId, setSelectedId] = useState<string | null>(null);

  const sorted = (templates.data ?? [])
    .slice()
    .sort((a, b) => NOTIFICATION_EVENT_TYPE_ORDER.indexOf(a.eventType) - NOTIFICATION_EVENT_TYPE_ORDER.indexOf(b.eventType));
  // Derived during render, not in an effect: "no selection yet" always means "the first one",
  // so there's nothing to synchronize after the fact.
  const selected = sorted.find((item) => item.id === selectedId) ?? sorted[0] ?? null;

  if (templates.isPending) {
    return <p className="text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (templates.isError) {
    return <ErrorState title={t("loadErrorTitle")} onRetry={() => templates.refetch()} />;
  }

  return (
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
                  onClick={() => setSelectedId(template.id)}
                  className={cn(
                    "block w-full border-b border-line px-4 py-3 text-left text-sm last:border-0 hover:bg-ground",
                    selectedId === template.id ? "bg-mint font-semibold text-green-700" : "text-ink",
                  )}
                >
                  {notificationEventTypeLabel(template.eventType)}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>{selected ? <TemplateEditor key={selected.id} template={selected} updateTemplate={updateTemplate} t={t} /> : null}</div>
    </div>
  );
}

function TemplateEditor({
  template,
  updateTemplate,
  t,
}: {
  template: NotificationTemplateSummary;
  updateTemplate: ReturnType<typeof useUpdateNotificationTemplate>;
  t: ReturnType<typeof useTranslations<"Announcements">>;
}) {
  // Keyed by template.id in the parent, so this state is only ever initialized once per
  // template — switching selection remounts rather than needing an effect to resynchronize.
  const [subject, setSubject] = useState(template.subject);
  const [body, setBody] = useState(template.body);
  const [savedAt, setSavedAt] = useState<number | null>(null);

  const save = () => {
    updateTemplate.mutate(
      { id: template.id, data: { subject, body } },
      { onSuccess: () => setSavedAt(Date.now()) },
    );
  };

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-4 sm:p-6">
      <h2 className="font-bold text-ink">{notificationEventTypeLabel(template.eventType)}</h2>

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
        <Button type="button" onClick={save} disabled={updateTemplate.isPending || !subject.trim() || !body.trim()}>
          {t("saveButton")}
        </Button>
        {savedAt ? <span className="text-sm text-ink-muted">{t("savedConfirmation")}</span> : null}
      </div>
    </div>
  );
}
