"use client";

import { useQueryClient } from "@tanstack/react-query";
import { MessageSquarePlus } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api/client";
import type { CommentThread } from "@/lib/api/generated/hRSApiV1.schemas";
import { getGetReportCommentsQueryKey, useAddReportComment, useGetReportComments } from "@/lib/api/generated/submissions/submissions";

export interface CommentableField {
  fieldKey: string;
  label: string;
}

export function CommentsPanel({ reportId, fields }: { reportId: string; fields: CommentableField[] }) {
  const t = useTranslations("Inbox");
  const queryClient = useQueryClient();
  const [isStartingThread, setIsStartingThread] = useState(false);
  const [newThreadFieldKey, setNewThreadFieldKey] = useState("");
  const [newThreadBody, setNewThreadBody] = useState("");
  const [replyBodyByRootId, setReplyBodyByRootId] = useState<Record<string, string>>({});

  const threads = useGetReportComments<CommentThread[], ApiError>(reportId);
  const addComment = useAddReportComment();

  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetReportCommentsQueryKey(reportId) });

  const startThread = () => {
    if (!newThreadFieldKey || !newThreadBody.trim()) {
      return;
    }
    addComment.mutate(
      { id: reportId, data: { fieldKey: newThreadFieldKey, parentId: null, body: newThreadBody.trim() } },
      {
        onSuccess: () => {
          setIsStartingThread(false);
          setNewThreadFieldKey("");
          setNewThreadBody("");
          invalidate();
        },
      },
    );
  };

  const sendReply = (rootId: string) => {
    const body = (replyBodyByRootId[rootId] ?? "").trim();
    if (!body) {
      return;
    }
    addComment.mutate(
      { id: reportId, data: { fieldKey: null, parentId: rootId, body } },
      {
        onSuccess: () => {
          setReplyBodyByRootId((current) => ({ ...current, [rootId]: "" }));
          invalidate();
        },
      },
    );
  };

  if (threads.isPending) {
    return <p className="text-sm text-ink-muted">{t("loading")}</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h3 className="font-bold text-ink">{t("commentsTitle")}</h3>
        {!isStartingThread ? (
          <Button type="button" variant="outline" size="sm" onClick={() => setIsStartingThread(true)}>
            <MessageSquarePlus className="size-4" aria-hidden="true" />
            {t("startThread")}
          </Button>
        ) : null}
      </div>

      {isStartingThread ? (
        <div className="flex flex-col gap-2 rounded-lg border border-line bg-surface p-4">
          <Label htmlFor="comment-field">{t("commentFieldLabel")}</Label>
          <select
            id="comment-field"
            value={newThreadFieldKey}
            onChange={(event) => setNewThreadFieldKey(event.target.value)}
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {fields.map((field) => (
              <option key={field.fieldKey} value={field.fieldKey}>
                {field.label}
              </option>
            ))}
          </select>
          <Textarea
            autoFocus
            value={newThreadBody}
            onChange={(event) => setNewThreadBody(event.target.value)}
            placeholder={t("commentBodyPlaceholder")}
            rows={3}
          />
          {addComment.error instanceof ApiError ? <p className="text-sm text-danger">{addComment.error.message}</p> : null}
          <div className="flex gap-2">
            <Button type="button" size="sm" onClick={startThread} disabled={!newThreadFieldKey || !newThreadBody.trim() || addComment.isPending}>
              {t("postComment")}
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => setIsStartingThread(false)}>
              {t("cancel")}
            </Button>
          </div>
        </div>
      ) : null}

      {(threads.data ?? []).length === 0 && !isStartingThread ? (
        <p className="text-sm text-ink-muted">{t("noComments")}</p>
      ) : (
        (threads.data ?? []).map((thread) => (
          <div key={thread.root.id} className="rounded-lg border border-line bg-surface p-4">
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{thread.fieldLabel}</p>

            <CommentBubble authorName={thread.root.authorName} body={thread.root.body} createdAt={thread.root.createdAt} />

            {thread.replies.map((reply) => (
              <div key={reply.id} className="ml-4 mt-2 border-l border-line pl-3">
                <CommentBubble authorName={reply.authorName} body={reply.body} createdAt={reply.createdAt} />
              </div>
            ))}

            <div className="mt-3 flex items-center gap-2">
              <Textarea
                value={replyBodyByRootId[thread.root.id] ?? ""}
                onChange={(event) => setReplyBodyByRootId((current) => ({ ...current, [thread.root.id]: event.target.value }))}
                placeholder={t("replyPlaceholder")}
                rows={1}
                className="min-h-9"
              />
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => sendReply(thread.root.id)}
                disabled={!(replyBodyByRootId[thread.root.id] ?? "").trim() || addComment.isPending}
              >
                {t("sendReply")}
              </Button>
            </div>
          </div>
        ))
      )}
    </div>
  );
}

function CommentBubble({ authorName, body, createdAt }: { authorName: string; body: string; createdAt: string }) {
  return (
    <div className="mt-2">
      <p className="text-sm">
        <span className="font-semibold text-ink">{authorName}</span>{" "}
        <span className="text-xs text-ink-muted">{new Date(createdAt).toLocaleString()}</span>
      </p>
      <p className="mt-0.5 text-sm text-ink">{body}</p>
    </div>
  );
}
