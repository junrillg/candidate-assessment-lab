import type { Answer } from "./types";

export type Draft = Answer & {
  key: string;
  baseKey?: string;
  dirty: boolean;
  blob?: Blob;
  uploadId?: string;
  uploadedId?: string;
  localUpdatedAt: number;
};
let database: Promise<IDBDatabase> | undefined;
function open(): Promise<IDBDatabase> {
  return (database ??= new Promise((resolve, reject) => {
    const request = indexedDB.open("candidate-assessment", 1);
    request.onupgradeneeded = () =>
      request.result.createObjectStore("drafts", { keyPath: "key" });
    request.onerror = () => {
      database = undefined;
      reject(
        new Error(
          "Device storage is unavailable. Keep this page open and copy your response before leaving.",
        ),
      );
    };
    request.onsuccess = () => resolve(request.result);
  }));
}
export function draftKey(
  userId: string,
  attemptId: string,
  questionId: string,
) {
  return `${userId}:${attemptId}:${questionId}`;
}
export async function readDraft(key: string): Promise<Draft | undefined> {
  const db = await open();
  return new Promise((resolve, reject) => {
    const request = db
      .transaction("drafts")
      .objectStore("drafts")
      .get(sessionStorage.getItem(key) ?? key);
    request.onsuccess = () => resolve(request.result as Draft | undefined);
    request.onerror = () =>
      reject(new Error("Your saved device draft could not be opened."));
  });
}
export async function writeDraft(draft: Draft): Promise<void> {
  const db = await open();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction("drafts", "readwrite");
    transaction.objectStore("drafts").put(draft);
    transaction.oncomplete = () => {
      try {
        if (draft.baseKey) sessionStorage.setItem(draft.baseKey, draft.key);
        resolve();
      } catch {
        reject(
          new Error(
            "Device recovery storage is unavailable. Keep this page open.",
          ),
        );
      }
    };
    transaction.onerror = transaction.onabort = () =>
      reject(
        new Error(
          "Device storage is full or unavailable. Copy your response before leaving.",
        ),
      );
  });
}
export function mergeDraft(
  key: string,
  server: Answer,
  local: Draft | undefined,
): Draft {
  // A dirty draft keeps the revision it edited, so reconnect detects a stale tab.
  return local?.dirty || local?.blob
    ? local
    : { ...server, key, dirty: false, localUpdatedAt: 0 };
}
export function acceptedDraft(
  sent: Draft,
  latest: Draft,
  response: Answer,
): Draft {
  return {
    ...latest,
    revision: response.revision,
    dirty: latest.localUpdatedAt !== sent.localUpdatedAt,
  };
}
export async function removeAttemptDrafts(
  userId: string,
  attemptId: string,
): Promise<void> {
  const prefix = `${userId}:${attemptId}:`;
  const db = await open();
  await new Promise<void>((resolve, reject) => {
    const transaction = db.transaction("drafts", "readwrite");
    const cursor = transaction
      .objectStore("drafts")
      .openCursor(IDBKeyRange.bound(prefix, prefix + "\uffff"));
    cursor.onsuccess = () => {
      const entry = cursor.result;
      if (entry) {
        entry.delete();
        entry.continue();
      }
    };
    transaction.oncomplete = () => resolve();
    transaction.onerror = transaction.onabort = () =>
      reject(
        new Error(
          "Server deletion was accepted, but device drafts could not be cleared. Clear this site’s browser storage.",
        ),
      );
  });
  for (const key of Object.keys(sessionStorage))
    if (key.startsWith(prefix)) sessionStorage.removeItem(key);
}
