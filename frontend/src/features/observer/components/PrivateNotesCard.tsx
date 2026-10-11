"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Lock } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api/client";
import type { PrivateNoteView } from "@/lib/api/generated/hRSApiV1.schemas";
import { addPrivateNote, getGetPrivateNotesQueryKey, useGetPrivateNotes } from "@/lib/api/generated/observer/observer";

/** FR-OBS-004: a simple append-only log, visible to whoever currently heads the observing unit —
 * never the individual author (the backend scopes notes by ObserverUnitId, not AuthorUserId). */
export function PrivateNotesCard({ unitId, reportId }: { unitId: string; reportId: string }) {
  const t = useTranslations("ChildReport");
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState("");

  const notesQuery = useGetPrivateNotes<PrivateNoteView[], ApiError>(unitId, reportId);

  const addMutation = useMutation({
    mutationFn: () => addPrivateNote(unitId, reportId, { body: draft }),
    onSuccess: () => {
      setDraft("");
      queryClient.invalidateQueries({ queryKey: getGetPrivateNotesQueryKey(unitId, reportId) });
    },
  });

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <div className="flex items-center justify-between">
        <h2 className="font-bold text-ink">{t("privateNotesTitle")}</h2>
        <Lock className="size-4 text-ink-muted" aria-hidden="true" />
      </div>
      <p className="mt-1 text-sm text-ink-muted">{t("privateNotesHint")}</p>

      <div className="mt-3 flex flex-col gap-2">
        {notesQuery.data?.map((note) => (
          <div key={note.id} className="rounded-md bg-status-upcoming-bg p-3 text-sm">
            <p className="text-ink">{note.body}</p>
            <p className="mt-1 text-xs text-ink-muted">
              {note.authorName} · {new Date(note.createdAt).toLocaleDateString()}
            </p>
          </div>
        ))}
      </div>

      <div className="mt-3 flex flex-col gap-2">
        <label htmlFor="private-note-draft" className="sr-only">
          {t("addNotePlaceholder")}
        </label>
        <Textarea
          id="private-note-draft"
          rows={2}
          placeholder={t("addNotePlaceholder")}
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
        />
        {addMutation.error instanceof ApiError ? <p className="text-sm text-danger">{addMutation.error.message}</p> : null}
        <Button
          type="button"
          variant="outline"
          onClick={() => addMutation.mutate()}
          disabled={!draft.trim() || addMutation.isPending}
        >
          {t("saveNote")}
        </Button>
      </div>
    </div>
  );
}
