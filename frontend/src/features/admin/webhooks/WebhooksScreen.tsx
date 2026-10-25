"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Copy, Plus, RefreshCw, Send, Trash2, Zap } from "lucide-react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { ApiError } from "@/lib/api/client";
import type { WebhookDeliveryDto, WebhookEndpointDto, WebhookSecretResult } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetWebhookDeliveriesQueryKey,
  getGetWebhooksQueryKey,
  useCreateWebhook,
  useDeleteWebhook,
  useGetWebhookDeliveries,
  useGetWebhooks,
  useRotateWebhookSecret,
  useSendTestWebhook,
  useUpdateWebhook,
} from "@/lib/api/generated/settings/settings";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";
import { cn } from "@/lib/utils";
import { isHttpsUrl, SUBSCRIBABLE_EVENTS, WEBHOOK_DELIVERY_STATUS, WEBHOOK_EVENT, webhookEventName } from "./webhook-events";

/** FR-ADM-003: the system administrator's webhook endpoints — signed deliveries of report events
 * to other systems, with a test send, secret rotation and a delivery log. */
export function WebhooksScreen() {
  const t = useTranslations("Webhooks");
  const webhooks = useGetWebhooks<WebhookEndpointDto[], ApiError>({ query: { retry: false } });
  const [editing, setEditing] = useState<WebhookEndpointDto | "new" | null>(null);
  const [secret, setSecret] = useState<{ url: string; value: string } | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const header = (
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <Link href="/admin/settings" className="text-sm text-ink-muted hover:text-ink">
          {t("backToSettings")}
        </Link>
        <h1 className="mt-1 text-2xl font-bold text-ink sm:text-3xl">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>
      {webhooks.isSuccess && editing === null ? (
        <Button type="button" onClick={() => setEditing("new")}>
          <Plus aria-hidden="true" />
          {t("addAction")}
        </Button>
      ) : null}
    </div>
  );

  if (webhooks.isPending) {
    return (
      <div className="flex flex-col gap-6 p-4 sm:p-6">
        {header}
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      </div>
    );
  }

  if (webhooks.isError) {
    return (
      <div className="flex flex-col gap-6 p-4 sm:p-6">
        {header}
        {webhooks.error.status === 403 ? (
          <EmptyState icon={Zap} title={t("forbiddenTitle")} description={t("forbiddenDescription")} />
        ) : (
          <ErrorState title={t("loadErrorTitle")} onRetry={() => webhooks.refetch()} />
        )}
      </div>
    );
  }

  const endpoints = webhooks.data;
  const selected = endpoints.find((e) => e.id === selectedId) ?? null;

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      {header}

      {secret ? <SecretNotice url={secret.url} secret={secret.value} onDismiss={() => setSecret(null)} /> : null}

      {editing !== null ? (
        <WebhookForm
          key={editing === "new" ? "new" : editing.id}
          endpoint={editing === "new" ? null : editing}
          onCancel={() => setEditing(null)}
          onSaved={(result) => {
            setEditing(null);
            if (result.secret) {
              setSecret({ url: result.endpoint.url, value: result.secret });
            }
          }}
        />
      ) : null}

      {endpoints.length === 0 && editing === null ? (
        <EmptyState icon={Zap} title={t("emptyTitle")} description={t("emptyDescription")} />
      ) : (
        <ul className="flex flex-col gap-3">
          {endpoints.map((endpoint) => (
            <EndpointCard
              key={endpoint.id}
              endpoint={endpoint}
              expanded={selectedId === endpoint.id}
              onToggle={() => setSelectedId((id) => (id === endpoint.id ? null : endpoint.id))}
              onEdit={() => setEditing(endpoint)}
              onSecret={(value) => setSecret({ url: endpoint.url, value })}
            />
          ))}
        </ul>
      )}

      {selected ? <DeliveryLog endpoint={selected} /> : null}
    </div>
  );
}

function SecretNotice({ url, secret, onDismiss }: { url: string; secret: string; onDismiss: () => void }) {
  const t = useTranslations("Webhooks");
  const [copied, setCopied] = useState(false);
  return (
    <section role="status" className="flex flex-col gap-3 rounded-lg border border-green-600 bg-mint p-4">
      <p className="font-semibold text-ink">{t("secretTitle")}</p>
      <p className="text-sm text-ink-muted">{t("secretDescription", { url })}</p>
      <div className="flex flex-wrap items-center gap-2">
        <code className="min-w-0 flex-1 break-all rounded-sm border border-line bg-surface px-3 py-2 font-mono text-sm text-ink">{secret}</code>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => {
            void navigator.clipboard?.writeText(secret).then(() => setCopied(true));
          }}
        >
          <Copy aria-hidden="true" />
          {copied ? t("copied") : t("copy")}
        </Button>
        <Button type="button" size="sm" onClick={onDismiss}>
          {t("secretDone")}
        </Button>
      </div>
    </section>
  );
}

function WebhookForm({
  endpoint,
  onCancel,
  onSaved,
}: {
  endpoint: WebhookEndpointDto | null;
  onCancel: () => void;
  onSaved: (result: { endpoint: WebhookEndpointDto; secret?: string }) => void;
}) {
  const t = useTranslations("Webhooks");
  const queryClient = useQueryClient();
  const create = useCreateWebhook();
  const update = useUpdateWebhook();
  const [url, setUrl] = useState(endpoint?.url ?? "");
  const [events, setEvents] = useState<number[]>(endpoint ? endpoint.events.map(Number) : [WEBHOOK_EVENT.ReportSubmitted]);
  const [isActive, setIsActive] = useState(endpoint?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const saving = create.isPending || update.isPending;

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    if (!isHttpsUrl(url.trim())) {
      setError(t("urlInvalid"));
      return;
    }
    if (events.length === 0) {
      setError(t("eventsRequired"));
      return;
    }
    setError(null);
    const data = { url: url.trim(), events, isActive };
    const done = () => queryClient.invalidateQueries({ queryKey: getGetWebhooksQueryKey() });
    const failed = (err: unknown) => setError(err instanceof ApiError ? err.message : t("saveError"));
    if (endpoint) {
      update.mutate(
        { id: endpoint.id, data },
        { onSuccess: (saved) => { done(); onSaved({ endpoint: saved as WebhookEndpointDto }); }, onError: failed },
      );
    } else {
      create.mutate(
        { data },
        {
          onSuccess: (result) => {
            done();
            const created = result as WebhookSecretResult;
            onSaved({ endpoint: created.endpoint, secret: created.secret });
          },
          onError: failed,
        },
      );
    }
  };

  return (
    <form onSubmit={submit} className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="webhook-form-title">
      <h2 id="webhook-form-title" className="text-lg font-bold text-ink">
        {endpoint ? t("editTitle") : t("addTitle")}
      </h2>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="webhook-url">{t("urlLabel")}</Label>
        <Input id="webhook-url" type="url" inputMode="url" placeholder="https://" value={url} onChange={(e) => setUrl(e.target.value)} />
        <p className="text-sm text-ink-muted">{t("urlHint")}</p>
      </div>
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 text-sm font-medium text-ink">{t("eventsLabel")}</legend>
        {SUBSCRIBABLE_EVENTS.map((value) => (
          <label key={value} className="flex items-center gap-3 text-sm text-ink">
            <Checkbox
              checked={events.includes(value)}
              onCheckedChange={(checked) => setEvents((current) => (checked ? [...current, value] : current.filter((e) => e !== value)))}
            />
            <span>
              <code className="font-mono">{webhookEventName(value)}</code>
              <span className="ml-2 text-ink-muted">{t(value === WEBHOOK_EVENT.ReportSubmitted ? "eventSubmitted" : "eventAcknowledged")}</span>
            </span>
          </label>
        ))}
      </fieldset>
      {endpoint ? (
        <div className="flex items-center gap-3">
          <Switch id="webhook-active" checked={isActive} onCheckedChange={setIsActive} />
          <Label htmlFor="webhook-active">{t("activeLabel")}</Label>
        </div>
      ) : null}
      {error ? (
        <p role="alert" className="text-sm text-danger">
          {error}
        </p>
      ) : null}
      <div className="flex flex-wrap gap-3">
        <Button type="submit" disabled={saving}>
          {endpoint ? t("saveAction") : t("createAction")}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={saving}>
          {t("cancelAction")}
        </Button>
      </div>
    </form>
  );
}

function EndpointCard({
  endpoint,
  expanded,
  onToggle,
  onEdit,
  onSecret,
}: {
  endpoint: WebhookEndpointDto;
  expanded: boolean;
  onToggle: () => void;
  onEdit: () => void;
  onSecret: (secret: string) => void;
}) {
  const t = useTranslations("Webhooks");
  const fmt = useOrgFormat();
  const queryClient = useQueryClient();
  const remove = useDeleteWebhook();
  const rotate = useRotateWebhookSecret();
  const test = useSendTestWebhook();
  const [status, setStatus] = useState<{ kind: "ok" | "error"; text: string } | null>(null);
  // Rotating breaks the receiver until it has the new secret, and deleting can't be undone: both
  // ask once more, inline.
  const [confirming, setConfirming] = useState<"rotate" | "delete" | null>(null);
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: getGetWebhooksQueryKey() });
    queryClient.invalidateQueries({ queryKey: getGetWebhookDeliveriesQueryKey(endpoint.id) });
  };
  const failed = () => setStatus({ kind: "error", text: t("actionError") });
  const failedCount = Number(endpoint.failed);

  return (
    <li className="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="break-all font-mono text-sm font-semibold text-ink">{endpoint.url}</p>
          <p className="mt-1 text-sm text-ink-muted">
            {endpoint.events.map((e) => webhookEventName(Number(e))).join(", ")} · {t("secretHint", { hint: endpoint.secretHint })}
          </p>
        </div>
        <span
          className={cn(
            "rounded-sm px-2 py-1 text-xs font-semibold",
            endpoint.isActive ? "bg-status-acknowledged-bg text-status-acknowledged-fg" : "bg-ground text-ink-muted",
          )}
        >
          {endpoint.isActive ? t("active") : t("paused")}
        </span>
      </div>
      <p className="text-sm text-ink-muted">
        {t("stats", { delivered: Number(endpoint.deliveredLast24Hours), pending: Number(endpoint.pending) })}
        {failedCount > 0 ? <span className="ml-1 font-semibold text-status-late-fg">{t("failedCount", { count: failedCount })}</span> : null}
        {endpoint.lastDeliveryAt ? <span className="ml-1">· {t("lastDelivery", { when: fmt.dayMonthTime(endpoint.lastDeliveryAt) })}</span> : null}
      </p>
      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={!endpoint.isActive || test.isPending}
          onClick={() =>
            test.mutate({ id: endpoint.id }, { onSuccess: () => { refresh(); setStatus({ kind: "ok", text: t("testQueued") }); }, onError: failed })
          }
        >
          <Send aria-hidden="true" />
          {t("testAction")}
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={onEdit}>
          {t("editAction")}
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={rotate.isPending}
          onClick={() => setConfirming("rotate")}
        >
          <RefreshCw aria-hidden="true" />
          {t("rotateAction")}
        </Button>
        <Button type="button" variant="outline" size="sm" aria-expanded={expanded} onClick={onToggle}>
          {expanded ? t("hideDeliveries") : t("showDeliveries")}
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={remove.isPending}
          onClick={() => setConfirming("delete")}
        >
          <Trash2 aria-hidden="true" />
          {t("deleteAction")}
        </Button>
      </div>
      {confirming ? (
        <div role="alertdialog" aria-labelledby={`confirm-${endpoint.id}`} className="flex flex-wrap items-center gap-3 rounded-md border border-line-strong bg-ground p-3">
          <p id={`confirm-${endpoint.id}`} className="flex-1 text-sm text-ink">
            {confirming === "rotate" ? t("rotateConfirm") : t("deleteConfirm")}
          </p>
          <Button
            type="button"
            size="sm"
            variant={confirming === "delete" ? "destructive" : "primary"}
            onClick={() => {
              const action = confirming;
              setConfirming(null);
              if (action === "rotate") {
                rotate.mutate(
                  { id: endpoint.id },
                  { onSuccess: (result) => { refresh(); onSecret((result as WebhookSecretResult).secret); }, onError: failed },
                );
              } else {
                remove.mutate({ id: endpoint.id }, { onSuccess: refresh, onError: failed });
              }
            }}
          >
            {confirming === "rotate" ? t("rotateConfirmAction") : t("deleteConfirmAction")}
          </Button>
          <Button type="button" size="sm" variant="outline" onClick={() => setConfirming(null)}>
            {t("cancelAction")}
          </Button>
        </div>
      ) : null}
      {status ? (
        <p role="status" className={cn("text-sm", status.kind === "ok" ? "text-green-700" : "text-danger")}>
          {status.text}
        </p>
      ) : null}
    </li>
  );
}

function DeliveryLog({ endpoint }: { endpoint: WebhookEndpointDto }) {
  const t = useTranslations("Webhooks");
  const fmt = useOrgFormat();
  const deliveries = useGetWebhookDeliveries<WebhookDeliveryDto[], ApiError>(endpoint.id);

  return (
    <section className="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="webhook-deliveries-title">
      <h2 id="webhook-deliveries-title" className="text-lg font-bold text-ink">
        {t("deliveriesTitle", { url: endpoint.url })}
      </h2>
      {deliveries.isPending ? (
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      ) : deliveries.isError ? (
        <ErrorState title={t("loadErrorTitle")} onRetry={() => deliveries.refetch()} />
      ) : deliveries.data.length === 0 ? (
        <p className="text-sm text-ink-muted">{t("deliveriesEmpty")}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-ink-muted">
              <tr>
                <th scope="col" className="py-2 pr-4 font-medium">{t("colEvent")}</th>
                <th scope="col" className="py-2 pr-4 font-medium">{t("colStatus")}</th>
                <th scope="col" className="py-2 pr-4 font-medium">{t("colAttempts")}</th>
                <th scope="col" className="py-2 pr-4 font-medium">{t("colResponse")}</th>
                <th scope="col" className="py-2 font-medium">{t("colQueued")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {deliveries.data.map((delivery) => (
                <tr key={delivery.id}>
                  <td className="py-2 pr-4 font-mono text-ink">{webhookEventName(Number(delivery.event))}</td>
                  <td className="py-2 pr-4">
                    <DeliveryStatus delivery={delivery} />
                  </td>
                  <td className="py-2 pr-4 text-ink">{Number(delivery.attempts)}</td>
                  <td className="py-2 pr-4 text-ink-muted">
                    {delivery.lastStatusCode != null ? `HTTP ${Number(delivery.lastStatusCode)}` : ""}
                    {delivery.lastError ? <span className="block max-w-xs truncate" title={delivery.lastError}>{delivery.lastError}</span> : null}
                  </td>
                  <td className="py-2 text-ink-muted">{fmt.dayMonthTime(delivery.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function DeliveryStatus({ delivery }: { delivery: WebhookDeliveryDto }) {
  const t = useTranslations("Webhooks");
  const fmt = useOrgFormat();
  const status = Number(delivery.status);
  if (status === WEBHOOK_DELIVERY_STATUS.Delivered) {
    return <span className="font-semibold text-green-700">{t("statusDelivered")}</span>;
  }
  if (status === WEBHOOK_DELIVERY_STATUS.Failed) {
    return <span className="font-semibold text-danger">{t("statusFailed")}</span>;
  }
  return (
    <span className="font-semibold text-status-late-fg">
      {Number(delivery.attempts) === 0 ? t("statusQueued") : t("statusRetrying", { when: fmt.time(delivery.nextAttemptAt) })}
    </span>
  );
}
