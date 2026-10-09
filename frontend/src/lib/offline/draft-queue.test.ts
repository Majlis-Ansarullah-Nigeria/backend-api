import "fake-indexeddb/auto";
import { IDBFactory } from "fake-indexeddb";
import { beforeEach, describe, expect, it } from "vitest";
import { clearPendingDraft, draftKey, getPendingDraft, savePendingDraft } from "./draft-queue";

describe("draftKey", () => {
  it("combines a report id and unit id", () => {
    expect(draftKey("report-1", "unit-1")).toBe("report-1:unit-1");
  });
});

describe("draft queue", () => {
  beforeEach(() => {
    indexedDB = new IDBFactory();
  });

  it("returns null for a key that was never saved", async () => {
    expect(await getPendingDraft("missing")).toBeNull();
  });

  it("saves and reads back a pending draft", async () => {
    await savePendingDraft("report-1:unit-1", { q1: "hello" }, "5");

    const draft = await getPendingDraft("report-1:unit-1");

    expect(draft?.answers).toEqual({ q1: "hello" });
    expect(draft?.ifMatch).toBe("5");
    expect(draft?.key).toBe("report-1:unit-1");
  });

  it("overwrites an older queued draft for the same key", async () => {
    await savePendingDraft("report-1:unit-1", { q1: "first" }, "5");
    await savePendingDraft("report-1:unit-1", { q1: "second" }, "6");

    const draft = await getPendingDraft("report-1:unit-1");

    expect(draft?.answers).toEqual({ q1: "second" });
    expect(draft?.ifMatch).toBe("6");
  });

  it("clears a pending draft", async () => {
    await savePendingDraft("report-1:unit-1", { q1: "hello" }, null);

    await clearPendingDraft("report-1:unit-1");

    expect(await getPendingDraft("report-1:unit-1")).toBeNull();
  });

  it("keeps drafts for different keys independent", async () => {
    await savePendingDraft("report-1:unit-1", { q1: "a" }, null);
    await savePendingDraft("report-2:unit-1", { q1: "b" }, null);

    expect((await getPendingDraft("report-1:unit-1"))?.answers).toEqual({ q1: "a" });
    expect((await getPendingDraft("report-2:unit-1"))?.answers).toEqual({ q1: "b" });
  });
});
