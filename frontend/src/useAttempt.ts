import { useEffect, useRef, useState } from "react";
import { api, ApiError, json, message } from "./api";
import {
  acceptedDraft,
  draftKey,
  mergeDraft,
  readDraft,
  writeDraft,
} from "./drafts";
import type { Draft } from "./drafts";
import type { Answer, Attempt } from "./types";

export type SaveState =
  | "server-saved"
  | "saving-locally"
  | "local-saved"
  | "syncing"
  | "failed"
  | "conflict"
  | "unsent";
export function useAttempt(userId: string, attemptId: string) {
  const [attempt, setAttempt] = useState<Attempt>();
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [states, setStates] = useState<Record<string, SaveState>>({});
  const [error, setError] = useState("");
  const [storageError, setStorageError] = useState("");
  const [remaining, setRemaining] = useState(0);
  const current = useRef<Record<string, Draft>>({});
  const savedStates = useRef<Record<string, SaveState>>({});
  const active = useRef<Attempt | undefined>(undefined);
  const timers = useRef<Record<string, ReturnType<typeof setTimeout>>>({});
  const pending = useRef<Record<string, Promise<boolean> | undefined>>({});
  const stored = useRef<Record<string, Promise<void>>>({});
  const baseline = useRef({ time: 0, remaining: 0 });
  const mounted = useRef(true);
  const setState = (id: string, state: SaveState) => {
    savedStates.current = { ...savedStates.current, [id]: state };
    if (mounted.current) setStates(savedStates.current);
  };
  const update = (id: string, draft: Draft) => {
    current.current = { ...current.current, [id]: draft };
    if (mounted.current) setDrafts(current.current);
  };
  const persist = (id: string, draft: Draft) => {
    // Serial writes prevent a slower previous save from replacing the newest device draft.
    const write = (stored.current[id] ?? Promise.resolve())
      .catch(() => {})
      .then(() => writeDraft(draft));
    stored.current[id] = write;
    return write;
  };
  const openAttempt = async () => {
    const requested = performance.now();
    const data = await api<Attempt>(`/attempts/${attemptId}`);
    active.current = data;
    setAttempt(data);
    baseline.current = {
      time: performance.now(),
      remaining: Math.max(
        0,
        new Date(data.deadline).getTime() -
          new Date(data.serverTime).getTime() -
          (performance.now() - requested),
      ),
    };
    setRemaining(Math.ceil(baseline.current.remaining / 1000));
    return data;
  };
  const expired = () =>
    !active.current ||
    active.current.status !== "active" ||
    performance.now() - baseline.current.time >= baseline.current.remaining;

  async function sync(id: string): Promise<boolean> {
    clearTimeout(timers.current[id]);
    if (pending.current[id]) return pending.current[id]!;
    if (savedStates.current[id] === "conflict") return false;
    if (expired()) {
      if (current.current[id]?.dirty) setState(id, "unsent");
      return false;
    }
    const sent = current.current[id];
    if (!sent?.dirty && !sent?.blob) return true;
    const work = (async () => {
      try {
        await stored.current[id]?.catch((reason) =>
          setStorageError(message(reason)),
        );
        if (!navigator.onLine)
          throw new Error(
            "You are offline. Your response is saved on this device and has not reached the server.",
          );
        setState(id, "syncing");
        let recordingId = sent.recordingId;
        if (sent.blob && sent.uploadId) {
          const upload = await api<{ id: string }>(
            `/attempts/${attemptId}/recordings/${id}/${sent.uploadId}`,
            {
              method: "POST",
              headers: { "Content-Type": sent.blob.type.split(";")[0] },
              body: sent.blob,
            },
          );
          recordingId = upload.id;
          const latest = current.current[id];
          if (latest.uploadId === sent.uploadId) {
            const uploaded = { ...latest, uploadedId: upload.id };
            update(id, uploaded);
            await persist(id, uploaded).catch((reason) =>
              setStorageError(message(reason)),
            );
          }
        }
        const response = await api<Answer>(
          `/attempts/${attemptId}/answers/${id}`,
          json("PUT", {
            revision: sent.revision,
            text: sent.text,
            flagged: sent.flagged,
            recordingId,
          }),
        );
        const latest = current.current[id];
        const accepted = acceptedDraft(sent, latest, response);
        if (latest.uploadId === sent.uploadId) {
          accepted.recordingId = recordingId;
          accepted.blob = undefined;
        }
        update(id, accepted);
        await persist(id, accepted).catch((reason) =>
          setStorageError(message(reason)),
        );
        setState(id, accepted.dirty ? "local-saved" : "server-saved");
        if (
          !Object.values(savedStates.current).some(
            (state) => state === "failed" || state === "conflict",
          )
        )
          setError("");
        return !accepted.dirty;
      } catch (reason) {
        setState(
          id,
          reason instanceof ApiError && reason.status === 409
            ? "conflict"
            : expired()
              ? "unsent"
              : "failed",
        );
        if (mounted.current) setError(message(reason));
        return false;
      } finally {
        pending.current[id] = undefined;
      }
    })();
    pending.current[id] = work;
    const complete = await work;
    if (current.current[id]?.dirty && savedStates.current[id] === "local-saved")
      timers.current[id] = setTimeout(() => void sync(id), 1000);
    return complete;
  }

  function edit(id: string, patch: Partial<Draft>) {
    const draft = {
      ...current.current[id],
      ...patch,
      dirty: true,
      localUpdatedAt: Math.max(
        Date.now(),
        current.current[id].localUpdatedAt + 1,
      ),
    };
    update(id, draft);
    const conflicted = savedStates.current[id] === "conflict";
    setState(
      id,
      conflicted ? "conflict" : expired() ? "unsent" : "saving-locally",
    );
    return persist(id, draft)
      .then(() => {
        if (
          current.current[id].localUpdatedAt === draft.localUpdatedAt &&
          !conflicted &&
          !expired()
        )
          setState(id, "local-saved");
      })
      .catch((reason) => {
        setStorageError(message(reason));
        if (!conflicted) setState(id, "failed");
      })
      .finally(() => {
        if (!expired() && !conflicted) {
          clearTimeout(timers.current[id]);
          timers.current[id] = setTimeout(() => void sync(id), 1000);
        }
      });
  }

  async function resolveConflict(id: string, keepLocal: boolean) {
    try {
      const data = await openAttempt();
      const server = data.answers.find((x) => x.questionId === id)!;
      const local = current.current[id];
      const resolved = keepLocal
        ? {
            ...local,
            revision: server.revision,
            dirty: true,
            localUpdatedAt: Date.now(),
          }
        : {
            ...server,
            key: local.key,
            dirty: false,
            localUpdatedAt: Date.now(),
          };
      update(id, resolved);
      await persist(id, resolved).catch((reason) =>
        setStorageError(message(reason)),
      );
      setState(
        id,
        expired() && resolved.dirty
          ? "unsent"
          : resolved.dirty
            ? "local-saved"
            : "server-saved",
      );
      if (keepLocal && !expired()) await sync(id);
      setError("");
    } catch (reason) {
      setError(message(reason));
    }
  }

  async function flush(): Promise<boolean> {
    // Each question finishes its in-flight request before any newer edit is flushed.
    for (const id of Object.keys(current.current)) {
      if (pending.current[id]) await pending.current[id];
      if (current.current[id].dirty || current.current[id].blob) {
        if (!(await sync(id))) return false;
      }
    }
    return true;
  }

  useEffect(() => {
    mounted.current = true;
    void (async () => {
      try {
        const data = await openAttempt();
        for (const answer of data.answers) {
          const key = draftKey(userId, attemptId, answer.questionId);
          let cached: Draft | undefined;
          try {
            cached = await readDraft(key);
          } catch (reason) {
            setError(message(reason));
          }
          const draft = {
            ...mergeDraft(key, answer, cached),
            baseKey: key,
            key: `${key}:draft:${crypto.randomUUID()}`,
          };
          update(answer.questionId, draft);
          setState(
            answer.questionId,
            draft.dirty || draft.blob
              ? expired()
                ? "unsent"
                : "local-saved"
              : "server-saved",
          );
          if ((draft.dirty || draft.blob) && !expired())
            timers.current[answer.questionId] = setTimeout(
              () => void sync(answer.questionId),
              1000,
            );
        }
      } catch (reason) {
        setError(message(reason));
      }
    })();
    const clock = setInterval(() => {
      const left = Math.max(
        0,
        Math.ceil(
          (baseline.current.remaining -
            (performance.now() - baseline.current.time)) /
            1000,
        ),
      );
      setRemaining(left);
      if (active.current?.status === "active" && left === 0) {
        for (const [id, draft] of Object.entries(current.current))
          if (draft.dirty || draft.blob) setState(id, "unsent");
        void openAttempt().catch(() => {});
      }
    }, 1000);
    const reconnect = () => {
      for (const id of Object.keys(current.current))
        if (current.current[id].dirty || current.current[id].blob)
          void sync(id);
    };
    const beforeLeave = (event: BeforeUnloadEvent) => {
      if (Object.values(current.current).some((x) => x.dirty || x.blob)) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    window.addEventListener("online", reconnect);
    window.addEventListener("beforeunload", beforeLeave);
    return () => {
      mounted.current = false;
      clearInterval(clock);
      Object.values(timers.current).forEach(clearTimeout);
      window.removeEventListener("online", reconnect);
      window.removeEventListener("beforeunload", beforeLeave);
    };
    // The component is keyed by attempt/user; refs keep asynchronous saves current without resetting the workspace.
  }, [userId, attemptId]);

  return {
    attempt,
    drafts,
    states,
    error,
    storageError,
    remaining,
    edit,
    sync,
    resolveConflict,
    flush,
    reload: openAttempt,
    setError,
  };
}
