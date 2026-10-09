/**
 * NFR-RES-01: "Draft autosave survives a network drop: unsaved changes are queued in the browser
 * (IndexedDB) and synced on reconnect." A minimal raw-IndexedDB wrapper — no new dependency for
 * one small object store. Each pending draft is keyed by its report (window-or-instance id + unit
 * id), so at most one unsynced write per report sits queued at a time; a newer queued write always
 * replaces an older one for the same report.
 */

const DB_NAME = "hrs-offline-drafts";
const DB_VERSION = 1;
const STORE_NAME = "pending-drafts";

export interface PendingDraft {
  key: string;
  answers: unknown;
  /** The RowVersion this draft was based on — replayed as If-Match when flushed, so a genuine
   * conflict (someone else saved in the meantime) still surfaces instead of silently overwriting. */
  ifMatch: string | null;
  queuedAt: string;
}

export function draftKey(id: string, unitId: string): string {
  return `${id}:${unitId}`;
}

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(STORE_NAME)) {
        request.result.createObjectStore(STORE_NAME, { keyPath: "key" });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

/** Every call is wrapped in try/catch by its caller — a private window, blocked site data, or a
 * browser without IndexedDB support should degrade to "no offline queue," not a crash. */
export async function savePendingDraft(key: string, answers: unknown, ifMatch: string | null): Promise<void> {
  const db = await openDb();
  try {
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE_NAME, "readwrite");
      tx.objectStore(STORE_NAME).put({ key, answers, ifMatch, queuedAt: new Date().toISOString() } satisfies PendingDraft);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } finally {
    db.close();
  }
}

export async function getPendingDraft(key: string): Promise<PendingDraft | null> {
  const db = await openDb();
  try {
    return await new Promise<PendingDraft | null>((resolve, reject) => {
      const tx = db.transaction(STORE_NAME, "readonly");
      const request = tx.objectStore(STORE_NAME).get(key);
      request.onsuccess = () => resolve((request.result as PendingDraft | undefined) ?? null);
      request.onerror = () => reject(request.error);
    });
  } finally {
    db.close();
  }
}

export async function clearPendingDraft(key: string): Promise<void> {
  const db = await openDb();
  try {
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE_NAME, "readwrite");
      tx.objectStore(STORE_NAME).delete(key);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } finally {
    db.close();
  }
}
