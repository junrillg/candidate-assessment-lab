import { useEffect, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { api, json, message, setCsrfToken } from "./api";
import type {
  Assignment,
  Attempt,
  Evaluation,
  EvaluationQuestion,
  Question,
  ReviewRow,
  Session,
} from "./types";
import type { Draft } from "./drafts";
import { removeAttemptDrafts } from "./drafts";
import { useAttempt } from "./useAttempt";
import { useRecorder } from "./useRecorder";

const time = (seconds: number) =>
  `${Math.floor(seconds / 60)
    .toString()
    .padStart(2, "0")}:${(seconds % 60).toString().padStart(2, "0")}`;
const label = (value: string) => value.replaceAll("-", " ");
function Note({
  children,
  tone = "info",
}: {
  children: ReactNode;
  tone?: "info" | "error" | "success";
}) {
  return (
    <div
      className={`notice ${tone}`}
      role={tone === "error" ? "alert" : "status"}
    >
      {children}
    </div>
  );
}
function Loading() {
  return (
    <div className="loading" role="status" aria-label="Loading assessment">
      <div />
      <div />
      <div />
    </div>
  );
}
function Book() {
  return (
    <svg
      width="27"
      height="30"
      viewBox="0 0 27 30"
      fill="none"
      aria-hidden="true"
    >
      <rect
        x="3"
        y="2"
        width="21"
        height="26"
        rx="2"
        stroke="currentColor"
        strokeWidth="1.8"
      />
      <path
        d="M8 2v26M12 10h8M12 15h8M12 20h5"
        stroke="currentColor"
        strokeWidth="1.5"
      />
    </svg>
  );
}

export function App() {
  const [session, setSession] = useState<Session>();
  const [error, setError] = useState("");
  const [online, setOnline] = useState(navigator.onLine);
  const [attemptId, setAttemptId] = useState(
    new URLSearchParams(location.search).get("attempt"),
  );
  const refresh = async () => {
    const data = await api<Session>("/session");
    setCsrfToken(data.csrfToken);
    setSession(data);
    setError("");
  };
  useEffect(() => {
    void refresh().catch((e) => setError(message(e)));
    const change = () => setOnline(navigator.onLine);
    window.addEventListener("online", change);
    window.addEventListener("offline", change);
    return () => {
      window.removeEventListener("online", change);
      window.removeEventListener("offline", change);
    };
  }, []);
  const open = (id: string | null) => {
    setAttemptId(id);
    const url = new URL(location.href);
    if (id) url.searchParams.set("attempt", id);
    else url.searchParams.delete("attempt");
    history.replaceState(null, "", url);
  };
  const logout = async () => {
    try {
      if (
        !window.confirm(
          "Sign out? Unsent drafts stay on this device. Stop any recording first; an unfinished recording can be lost.",
        )
      )
        return;
      await api("/logout", json("POST", {}));
      open(null);
      await refresh();
    } catch (e) {
      setError(message(e));
    }
  };
  return (
    <>
      <a className="skip" href="#main">
        Skip to main content
      </a>
      <header className="app-header">
        <a
          className="brand"
          href="/"
          onClick={(event) => {
            event.preventDefault();
            if (!attemptId) open(null);
          }}
        >
          <Book />
          <span>Candidate Assessment</span>
        </a>
        <div className="identity">
          {session?.authenticated ? (
            <>
              <span>
                {session.name}
                <small>{session.role}</small>
              </span>
              <button className="quiet" onClick={() => void logout()}>
                Sign out
              </button>
            </>
          ) : (
            <span>Private assessment workspace</span>
          )}
        </div>
      </header>
      {!online && (
        <div className="offline" role="status">
          You’re offline. Server saves and uploads will resume when you
          reconnect. The deadline still applies.
        </div>
      )}
      {error && (
        <div className="global-error">
          <Note tone="error">
            {error}{" "}
            <button
              className="text-button"
              onClick={() => void refresh().catch((e) => setError(message(e)))}
            >
              Retry connection
            </button>
          </Note>
        </div>
      )}
      {!session ? (
        <main id="main" className="center-page">
          {error ? <h1>We couldn’t connect</h1> : <Loading />}
        </main>
      ) : !session.authenticated ? (
        <Login onLogin={refresh} />
      ) : session.role === "Reviewer" ? (
        <Reviewer attemptId={attemptId} open={open} />
      ) : attemptId ? (
        <CandidateWorkspace
          key={`${session.userId}:${attemptId}`}
          userId={session.userId!}
          attemptId={attemptId}
          back={() => open(null)}
        />
      ) : (
        <Preparation open={open} />
      )}
      <footer className="app-footer">
        <span>Fictional workplace scenarios · Human-reviewed results</span>
        <a
          href="https://github.com/junrillg/candidate-assessment-lab/tree/main/docs"
          target="_blank"
          rel="noreferrer"
        >
          Learning guide
        </a>
      </footer>
    </>
  );
}

function Login({ onLogin }: { onLogin: () => Promise<void> }) {
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      await api(
        "/login",
        json("POST", {
          email: data.get("email"),
          password: data.get("password"),
        }),
      );
      await onLogin();
    } catch (e) {
      setError(message(e));
    } finally {
      setBusy(false);
    }
  };
  return (
    <main id="main" className="login-layout">
      <section className="welcome">
        <Book />
        <h1>
          A clear space
          <br />
          to show your thinking.
        </h1>
        <p>
          Work through written and spoken scenarios, then receive feedback from
          a human reviewer.
        </p>
        <div className="welcome-rule" />
        <p className="small">
          Prepare first. Your assessment timer starts only when you choose to
          begin.
        </p>
      </section>
      <section className="login-form">
        <h2>Sign in to your workspace</h2>
        <p>Use the private credentials provided for this assessment.</p>
        <form onSubmit={(event) => void submit(event)}>
          <label htmlFor="email">Email address</label>
          <input
            id="email"
            name="email"
            type="email"
            autoComplete="username"
            required
            maxLength={254}
          />
          <label htmlFor="password">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            maxLength={1024}
          />
          {error && <Note tone="error">{error}</Note>}
          <button className="primary wide" disabled={busy}>
            {busy ? "Signing in…" : "Sign in"}
          </button>
        </form>
        <p className="small muted">
          Registration is managed by your assessment administrator. Ask them if
          you need access.
        </p>
      </section>
    </main>
  );
}

function Preparation({ open }: { open: (id: string) => void }) {
  const [assignments, setAssignments] = useState<Assignment[]>();
  const [selected, setSelected] = useState("");
  const [manualOnly, setManualOnly] = useState(false);
  const [consent, setConsent] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    void api<Assignment[]>("/assignments")
      .then((data) => {
        setAssignments(data);
        setSelected(data[0]?.id ?? "");
      })
      .catch((e) => setError(message(e)));
  }, []);
  const assignment = assignments?.find((x) => x.id === selected);
  const begin = async () => {
    if (!assignment) return;
    setBusy(true);
    setError("");
    try {
      const data = await api<Attempt>(
        `/assignments/${assignment.id}/start`,
        json("POST", { manualOnly }),
      );
      open(data.id);
    } catch (e) {
      setError(message(e));
    } finally {
      setBusy(false);
    }
  };
  return (
    <main id="main" className="page">
      <div className="page-heading">
        <div>
          <h1>Before you begin</h1>
          <p>Settle in, try the tools, and start when you’re ready.</p>
        </div>
        <span className="status-pill">Timer not started</span>
      </div>
      {error && <Note tone="error">{error}</Note>}
      {!assignments ? (
        <Loading />
      ) : assignments.length === 0 ? (
        <Note>
          No assignments yet. Your administrator will provide one when it’s
          ready.
        </Note>
      ) : (
        <div className="prep-grid">
          <section className="instructions">
            <h2>Your assessment</h2>
            {assignments.length > 1 && (
              <>
                <label htmlFor="assignment">Assignment</label>
                <select
                  id="assignment"
                  value={selected}
                  onChange={(e) => setSelected(e.target.value)}
                >
                  {assignments.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.title} · {a.durationMinutes} minutes
                    </option>
                  ))}
                </select>
              </>
            )}
            <h3>{assignment?.title}</h3>
            <dl className="assessment-facts">
              <div>
                <dt>Written</dt>
                <dd>8 questions</dd>
              </div>
              <div>
                <dt>Spoken</dt>
                <dd>4 questions</dd>
              </div>
              <div>
                <dt>Duration</dt>
                <dd>{assignment?.durationMinutes} minutes</dd>
              </div>
            </dl>
            <h3>How it works</h3>
            <ul className="instruction-list">
              <li>
                Move freely between questions and flag anything you want to
                revisit.
              </li>
              <li>
                Written drafts save on this device, then sync to the server.
                Only a server-saved response is accepted.
              </li>
              <li>
                Record up to two minutes per oral answer. Completed recordings
                save locally before upload. An unfinished recording can be lost
                if the browser closes.
              </li>
              <li>
                The server deadline is strict, even offline or with the browser
                closed. It submits only accepted responses and verified,
                selected recordings.
              </li>
            </ul>
            <h3>Recording and review</h3>
            <p>
              Recordings are private and retained for 30 days. AI can transcribe
              and suggest rubric scores. A human reviewer decides the final
              scores and publishes your feedback.
            </p>
            <label className="check">
              <input
                type="checkbox"
                checked={manualOnly}
                onChange={(e) => setManualOnly(e.target.checked)}
              />
              Request manual-only review, without AI processing
            </label>
            <label className="check">
              <input
                type="checkbox"
                checked={consent}
                onChange={(e) => setConsent(e.target.checked)}
              />
              I understand recording, review and the strict deadline.
            </label>
            <button
              className="primary"
              disabled={busy || (!assignment?.attemptId && !consent)}
              onClick={() =>
                assignment?.attemptId
                  ? open(assignment.attemptId)
                  : void begin()
              }
            >
              {busy
                ? "Starting…"
                : assignment?.attemptId
                  ? "Open existing attempt"
                  : `Start ${assignment?.durationMinutes}-minute assessment`}
            </button>
            <p className="small muted">
              Starting freezes your questions, rubrics and assigned duration.
            </p>
          </section>
          <aside className="practice">
            <h2>Try the tools</h2>
            <p className="small muted">
              Practice stays on this page and is never submitted.
            </p>
            <label htmlFor="practice-answer">
              A teammate is blocked by a missing detail. How would you help?
            </label>
            <textarea
              id="practice-answer"
              rows={5}
              placeholder="Try writing a short response…"
            />
            <h3>Test your microphone</h3>
            <Recording key="practice" practice />
            <p className="small muted">
              Use headphones if you can. Listen back to check that your voice is
              clear.
            </p>
          </aside>
        </div>
      )}
    </main>
  );
}

function Recording({
  draft,
  attemptId,
  questionId,
  onComplete,
  disabled = false,
  practice = false,
  onRecording,
}: {
  draft?: Draft;
  attemptId?: string;
  questionId?: string;
  onComplete?: (blob: Blob) => void | Promise<void>;
  disabled?: boolean;
  practice?: boolean;
  onRecording?: (value: boolean) => void;
}) {
  const [local, setLocal] = useState<Blob>();
  const [source, setSource] = useState("");
  const recorder = useRecorder((blob) => {
    if (practice) setLocal(blob);
    else return onComplete?.(blob);
  });
  useEffect(() => {
    onRecording?.(recorder.recording || recorder.busy);
    return () => onRecording?.(false);
  }, [recorder.recording, recorder.busy, onRecording]);
  useEffect(() => {
    const blob = local ?? draft?.blob;
    if (blob) {
      const url = URL.createObjectURL(blob);
      setSource(url);
      return () => URL.revokeObjectURL(url);
    }
    setSource(
      draft?.recordingId
        ? `/api/attempts/${attemptId}/recordings/${draft.recordingId}`
        : "",
    );
  }, [local, draft?.blob, draft?.recordingId, attemptId]);
  return (
    <div className="recording">
      <div className="record-controls">
        {recorder.recording ? (
          <button className="danger" onClick={recorder.stop}>
            Stop recording
          </button>
        ) : (
          <button
            className="secondary"
            disabled={disabled || recorder.busy}
            onClick={() => void recorder.start()}
          >
            {recorder.busy
              ? "Opening microphone…"
              : source
                ? "Record a replacement"
                : "Start recording"}
          </button>
        )}
        <span
          className={recorder.recording ? "record-time active" : "record-time"}
        >
          {time(recorder.seconds)} / 02:00
        </span>
      </div>
      {recorder.recording && (
        <p role="status">
          Recording in progress. Stop to save this recording before leaving this
          question.
        </p>
      )}
      {source && (
        <>
          <audio
            controls
            src={source}
            aria-label={
              practice
                ? "Practice recording playback"
                : "Your selected recording playback"
            }
          />
          <p className="small">
            {practice
              ? "Practice playback"
              : draft?.blob
                ? "Completed on this device. Upload and server selection still pending."
                : "Verified recording selected on the server."}
          </p>
        </>
      )}
      {!source && !recorder.recording && (
        <p className="small muted">
          No recording yet. Microphone access is requested when you start.
        </p>
      )}
      {recorder.error && <Note tone="error">{recorder.error}</Note>}
    </div>
  );
}

function CandidateWorkspace({
  userId,
  attemptId,
  back,
}: {
  userId: string;
  attemptId: string;
  back: () => void;
}) {
  const work = useAttempt(userId, attemptId);
  const { attempt, drafts, states, remaining } = work;
  const [index, setIndex] = useState(0);
  const [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [recording, setRecording] = useState(false);
  const question = attempt?.questions[index];
  const draft = question && drafts[question.id];
  useEffect(() => {
    if (attempt && !review) document.getElementById("question-title")?.focus();
  }, [index, review, !!attempt]);
  const locked = attempt?.status === "submitted" || remaining === 0;
  const dirty = Object.values(drafts).filter((x) => x.dirty || x.blob).length;
  const answered =
    attempt?.questions.filter((q) => {
      const d = drafts[q.id];
      return q.kind === "oral"
        ? !!d?.recordingId || !!d?.blob
        : !!d?.text.trim();
    }).length ?? 0;
  const submit = async () => {
    setBusy(true);
    work.setError("");
    try {
      if (!(await work.flush())) {
        work.setError(
          "Some responses have not reached the server. Resolve conflicts or retry saves before submitting.",
        );
        return;
      }
      await api(`/attempts/${attemptId}/submit`, json("POST", {}));
      await work.reload();
      setReview(false);
    } catch (e) {
      work.setError(message(e));
    } finally {
      setBusy(false);
    }
  };
  if (!attempt)
    return (
      <main id="main" className="page">
        {work.error ? (
          <Note tone="error">
            {work.error} <button onClick={back}>Back to assignments</button>
          </Note>
        ) : (
          <Loading />
        )}
      </main>
    );
  if (attempt.status === "submitted")
    return (
      <Receipt
        userId={userId}
        attempt={attempt}
        dirty={dirty}
        drafts={drafts}
        back={back}
      />
    );
  const navigate = (next: number) => {
    if (recording) {
      work.setError(
        "Stop recording before changing questions so the completed file can be saved.",
      );
      return;
    }
    setIndex(next);
    setReview(false);
  };
  const state = draft ? states[draft.questionId] : "server-saved";
  return (
    <main id="main" className="work-page">
      <div className="work-toolbar">
        <div>
          <strong>{attempt.title}</strong>
          <span className="small muted">
            {answered} of {attempt.questions.length} answered ·{" "}
            {attempt.manualOnly
              ? "Manual review"
              : "AI assistance and human review"}
          </span>
        </div>
        <div className="work-actions">
          <div
            className={`timer ${remaining < 120 ? "urgent" : ""}`}
            role="timer"
            aria-label={`${time(remaining)} remaining`}
          >
            <span>Time remaining</span>
            <strong>{time(remaining)}</strong>
          </div>
          <button
            className="primary"
            disabled={recording}
            onClick={() => setReview(!review)}
          >
            {review ? "Continue answering" : "Review answers"}
          </button>
        </div>
      </div>
      {work.storageError && (
        <Note tone="error">
          {work.storageError} Online server saves can still work. Local backup
          is not guaranteed.
        </Note>
      )}
      {work.error && <Note tone="error">{work.error}</Note>}
      {locked && (
        <Note>
          The deadline has passed. The server is finalizing accepted responses.
          Any late local work stays visibly unsent.
        </Note>
      )}
      <div className="workspace">
        <nav className="question-nav" aria-label="Assessment questions">
          <h2>Questions</h2>
          <p className="small muted">Select any question</p>
          {["written", "oral"].map((kind) => (
            <div className="question-group" key={kind}>
              <h3>
                {kind === "written" ? "Written responses" : "Spoken responses"}
              </h3>
              {attempt.questions
                .filter((q) => q.kind === kind)
                .map((q) => {
                  const d = drafts[q.id];
                  const hasAnswer =
                    q.kind === "oral"
                      ? !!d?.recordingId || !!d?.blob
                      : !!d?.text.trim();
                  return (
                    <button
                      key={q.id}
                      aria-label={`Question ${q.number}: ${q.title}`}
                      aria-current={
                        !review && q.id === question?.id ? "step" : undefined
                      }
                      onClick={() => navigate(attempt.questions.indexOf(q))}
                    >
                      <span
                        className={`question-number ${hasAnswer ? "filled" : ""}`}
                      >
                        {q.number}
                      </span>
                      <span>
                        {q.title}
                        <small>
                          {d?.flagged
                            ? "Flagged for review"
                            : hasAnswer
                              ? d?.dirty
                                ? "Unsent changes"
                                : "Answered"
                              : "Not answered"}
                        </small>
                      </span>
                    </button>
                  );
                })}
            </div>
          ))}
        </nav>
        <section className="response-sheet">
          {review ? (
            <>
              <h1>Review your responses</h1>
              <p>
                Check missing answers and upload status before submitting.
                Submission freezes the server-accepted versions.
              </p>
              <div className="review-summary">
                {attempt.questions.map((q, i) => {
                  const d = drafts[q.id];
                  const answered =
                    q.kind === "oral" ? !!d?.recordingId : !!d?.text.trim();
                  return (
                    <button
                      key={q.id}
                      className="review-row"
                      onClick={() => navigate(i)}
                    >
                      <span>
                        {q.number}. {q.title}
                      </span>
                      <span>
                        {states[q.id] === "conflict"
                          ? "Conflict to resolve"
                          : d?.blob
                            ? "Recording upload pending"
                            : d?.dirty
                              ? "Save pending"
                              : answered
                                ? "Server saved"
                                : "Unanswered"}
                        {d?.flagged ? " · Flagged" : ""}
                      </span>
                    </button>
                  );
                })}
              </div>
              {dirty > 0 && (
                <Note>
                  {dirty} response{dirty === 1 ? " has" : "s have"} unsent work.
                  Submission will first try to save it.
                </Note>
              )}
              <p className="small">
                Unanswered questions can be submitted. Processing failures go to
                human review and never become automatic zero scores.
              </p>
              <button
                className="primary"
                disabled={busy || locked}
                onClick={() => void submit()}
              >
                {busy ? "Saving and submitting…" : "Submit assessment"}
              </button>
              <button className="quiet" onClick={() => setReview(false)}>
                Continue answering
              </button>
            </>
          ) : question && draft ? (
            <>
              <div className="question-heading">
                <span className="small muted">
                  Question {question.number} of {attempt.questions.length} ·{" "}
                  {question.kind === "written"
                    ? "Written response"
                    : "Spoken response"}
                </span>
                <label className="check flag">
                  <input
                    type="checkbox"
                    checked={draft.flagged}
                    disabled={locked}
                    onChange={(e) =>
                      work.edit(question.id, { flagged: e.target.checked })
                    }
                  />
                  Flag for review
                </label>
              </div>
              <h1 id="question-title" tabIndex={-1}>
                {question.title}
              </h1>
              <p className="prompt">{question.prompt}</p>
              <details className="rubric">
                <summary>What the reviewer will look for</summary>
                {question.rubric.map((c) => (
                  <div key={c.id}>
                    <strong>{c.label}</strong>
                    <ol start={0}>
                      {c.anchors.map((a, i) => (
                        <li key={i}>{a}</li>
                      ))}
                    </ol>
                  </div>
                ))}
              </details>
              <div className="answer-label">
                <label htmlFor="answer">
                  {question.kind === "written"
                    ? "Your response"
                    : "Optional written notes"}
                </label>
                <span className={`save-state ${state}`} role="status">
                  {state === "server-saved"
                    ? "Server saved"
                    : state === "saving-locally"
                      ? "Saving on device…"
                      : state === "local-saved"
                        ? "Saved on this device"
                        : state === "syncing"
                          ? "Syncing to server…"
                          : state === "conflict"
                            ? "Conflicting server version"
                            : state === "unsent"
                              ? "Unsent · deadline passed"
                              : "Save failed · retry needed"}
                </span>
              </div>
              <textarea
                id="answer"
                className="answer-area"
                value={draft.text}
                maxLength={20000}
                readOnly={locked}
                placeholder="Share your thinking here…"
                onChange={(e) =>
                  work.edit(question.id, { text: e.target.value })
                }
              />
              <div className="answer-meta">
                <span>
                  {draft.text.length.toLocaleString()} / 20,000 characters
                </span>
                <span>Revision {draft.revision} accepted</span>
              </div>
              {question.kind === "oral" && (
                <>
                  <h2>Your recording</h2>
                  <Recording
                    key={question.id}
                    draft={draft}
                    attemptId={attemptId}
                    questionId={question.id}
                    disabled={locked}
                    onRecording={setRecording}
                    onComplete={(blob) =>
                      work.edit(question.id, {
                        blob,
                        uploadId: crypto.randomUUID(),
                      })
                    }
                  />
                </>
              )}
              {state === "conflict" && (
                <div className="conflict">
                  <h2>Choose which response to keep</h2>
                  <p>
                    Another tab saved a newer server version. Your local
                    response is still shown above. Keeping it will deliberately
                    replace the latest server response.
                  </p>
                  <button
                    className="secondary"
                    disabled={locked}
                    onClick={() => void work.resolveConflict(question.id, true)}
                  >
                    Keep my local response
                  </button>
                  <button
                    className="quiet"
                    onClick={() =>
                      void work.resolveConflict(question.id, false)
                    }
                  >
                    Use server response
                  </button>
                </div>
              )}
              {state === "failed" && (
                <button
                  className="secondary"
                  disabled={locked}
                  onClick={() => void work.sync(question.id)}
                >
                  Retry save and upload
                </button>
              )}
              <div className="question-bottom">
                <button
                  className="secondary"
                  disabled={index === 0 || recording}
                  onClick={() => navigate(index - 1)}
                >
                  Previous question
                </button>
                <button
                  className="secondary"
                  disabled={index === attempt.questions.length - 1 || recording}
                  onClick={() => navigate(index + 1)}
                >
                  Next question
                </button>
              </div>
            </>
          ) : (
            <Loading />
          )}
        </section>
      </div>
    </main>
  );
}

function Receipt({
  userId,
  attempt,
  dirty,
  drafts,
  back,
}: {
  userId: string;
  attempt: Attempt;
  dirty: number;
  drafts: Record<string, Draft>;
  back: () => void;
}) {
  const [evaluation, setEvaluation] = useState<Evaluation>();
  const [error, setError] = useState("");
  const [deleting, setDeleting] = useState(false);
  useEffect(() => {
    let live = true;
    const load = () =>
      api<Evaluation>(`/evaluation/${attempt.id}`)
        .then((data) => {
          if (live) {
            setEvaluation(data);
            setError("");
          }
        })
        .catch((e) => {
          if (live) setError(message(e));
        });
    void load();
    const timer = setInterval(() => void load(), 5000);
    return () => {
      live = false;
      clearInterval(timer);
    };
  }, [attempt.id]);
  const remove = async () => {
    if (
      !window.confirm(
        "Delete this attempt and request removal of its answers, recordings and review? This cannot be undone.",
      )
    )
      return;
    setDeleting(true);
    try {
      await api(`/attempts/${attempt.id}`, { method: "DELETE" });
      await removeAttemptDrafts(userId, attempt.id);
      back();
    } catch (e) {
      setError(message(e));
      setDeleting(false);
    }
  };
  return (
    <main id="main" className="receipt page">
      <div className="receipt-heading">
        <span className="receipt-check" aria-hidden="true">
          <svg viewBox="0 0 24 24" width="26" fill="none">
            <path d="m5 12 4 4L19 6" stroke="currentColor" strokeWidth="2" />
          </svg>
        </span>
        <h1>Assessment received</h1>
        <p>Your accepted responses are frozen and ready for review.</p>
      </div>
      <dl className="receipt-details">
        <div>
          <dt>Receipt</dt>
          <dd className="receipt-id">{attempt.receipt}</dd>
        </div>
        <div>
          <dt>Review route</dt>
          <dd>
            {attempt.manualOnly
              ? "Manual-only review"
              : "AI assistance, then human review"}
          </dd>
        </div>
        <div>
          <dt>Current status</dt>
          <dd>{label(evaluation?.status ?? "processing")}</dd>
        </div>
      </dl>
      {dirty > 0 && (
        <Note tone="error">
          {dirty} local response{dirty === 1 ? " was" : "s were"} not accepted
          before submission. This work is unsent and was not included.
          <button
            className="text-button"
            onClick={() => {
              const blob = new Blob(
                [
                  JSON.stringify(
                    Object.values(drafts)
                      .filter((d) => d.dirty || d.blob)
                      .map(({ questionId, text, flagged }) => ({
                        questionId,
                        text,
                        flagged,
                      })),
                    null,
                    2,
                  ),
                ],
                { type: "application/json" },
              );
              const url = URL.createObjectURL(blob);
              const a = document.createElement("a");
              a.href = url;
              a.download = "unsent-responses.json";
              a.click();
              URL.revokeObjectURL(url);
            }}
          >
            Download unsent written work
          </button>
        </Note>
      )}
      {Object.values(drafts).filter(d => d.dirty || d.blob).map(d => (
        <UnsentResponse key={d.questionId} draft={d} question={attempt.questions.find(q => q.id === d.questionId)!}/>
      ))}
      {error && <Note tone="error">{error}</Note>}
      {evaluation?.publishedAt ? (
        <section className="published">
          <h2>Your published feedback</h2>
          <p className="final-score">
            {evaluation.total}{" "}
            <span>of {evaluation.maximum} rubric points</span>
          </p>
          <p>{evaluation.feedback}</p>
          {evaluation.questions.map((q) => (
            <div className="published-question" key={q.questionId}>
              <h3>
                {attempt.questions.find((x) => x.id === q.questionId)?.title}
              </h3>
              <p>{q.review?.feedback}</p>
              {q.review?.criteria?.map((c) => (
                <span className="score-chip" key={c.criterionId}>
                  {
                    attempt.questions
                      .find((x) => x.id === q.questionId)
                      ?.rubric.find((x) => x.id === c.criterionId)?.label
                  }
                  : {c.score}/5
                </span>
              ))}
            </div>
          ))}
        </section>
      ) : (
        <section>
          <h2>What happens next</h2>
          <p>
            {attempt.manualOnly
              ? "Your reviewer will assess your responses directly."
              : "Processing prepares transcripts and rubric suggestions. A reviewer confirms or changes the scores before publishing your feedback."}{" "}
            You can return to this receipt at any time.
          </p>
          <div className="processing-list">
            {evaluation?.questions.map((q) => (
              <div key={q.questionId}>
                <span>
                  {attempt.questions.find((x) => x.id === q.questionId)?.title}
                </span>
                <span>{label(q.status)}</span>
              </div>
            ))}
          </div>
        </section>
      )}
      <div className="receipt-actions">
        <button className="secondary" onClick={back}>
          Back to assignments
        </button>
        <button
          className="quiet destructive"
          disabled={deleting}
          onClick={() => void remove()}
        >
          {deleting ? "Requesting deletion…" : "Delete attempt"}
        </button>
      </div>
    </main>
  );
}

function UnsentResponse({draft,question}:{draft:Draft;question:Question}) {
  const [source,setSource]=useState('');
  useEffect(()=>{
    if(!draft.blob)return;
    const url=URL.createObjectURL(draft.blob);setSource(url);
    return()=>URL.revokeObjectURL(url);
  },[draft.blob]);
  return <details className="unsent-response"><summary>Unsent local work: {question.title}</summary>
    <p className="submitted-answer">{draft.text||'No written notes.'}</p>
    {source&&<><audio controls src={source} aria-label={`Unsent recording for ${question.title}`}/><p><a href={source} download={`unsent-question-${question.number}.${draft.blob?.type.includes('mp4')?'mp4':draft.blob?.type.includes('ogg')?'ogg':'webm'}`}>Download unsent recording</a></p></>}
  </details>;
}

function Reviewer({
  attemptId,
  open,
}: {
  attemptId: string | null;
  open: (id: string | null) => void;
}) {
  const [rows, setRows] = useState<ReviewRow[]>();
  const [error, setError] = useState("");
  const [filter, setFilter] = useState("all");
  useEffect(() => {
    if (attemptId) return;
    const load = () =>
      api<ReviewRow[]>("/reviews")
        .then(setRows)
        .catch((e) => setError(message(e)));
    void load();
    const timer = setInterval(() => void load(), 8000);
    return () => clearInterval(timer);
  }, [attemptId]);
  if (attemptId)
    return (
      <ReviewWorkspace
        key={attemptId}
        attemptId={attemptId}
        back={() => open(null)}
      />
    );
  const visible = rows?.filter((r) => filter === "all" || r.status === filter);
  return (
    <main id="main" className="page">
      <div className="page-heading">
        <div>
          <h1>Review workspace</h1>
          <p>Read the evidence. Apply the rubric. Publish your judgment.</p>
        </div>
        <label className="filter">
          Show
          <select value={filter} onChange={(e) => setFilter(e.target.value)}>
            <option value="all">All attempts</option>
            {[...new Set(rows?.map((r) => r.status))].map((s) => (
              <option key={s} value={s}>
                {label(s)}
              </option>
            ))}
          </select>
        </label>
      </div>
      {error && <Note tone="error">{error}</Note>}
      {!rows ? (
        <Loading />
      ) : visible?.length === 0 ? (
        <div className="empty">
          <h2>No attempts in this view</h2>
          <p>
            Submitted assessments will appear here with their processing and
            review status.
          </p>
        </div>
      ) : (
        <div className="queue">
          <div className="queue-head">
            <span>Assessment / candidate</span>
            <span>Submitted</span>
            <span>Status</span>
            <span />
          </div>
          {visible?.map((row) => (
            <button
              key={row.attemptId}
              className="queue-row"
              onClick={() => open(row.attemptId)}
            >
              <span>
                <strong>{row.title}</strong>
                <small>Candidate {row.candidateId.slice(0, 8)}</small>
              </span>
              <span>{new Date(row.submittedAt).toLocaleString()}</span>
              <span className="status-pill">{label(row.status)}</span>
              <span>Open review</span>
            </button>
          ))}
        </div>
      )}
    </main>
  );
}

function ReviewWorkspace({
  attemptId,
  back,
}: {
  attemptId: string;
  back: () => void;
}) {
  const [attempt, setAttempt] = useState<Attempt>();
  const [evaluation, setEvaluation] = useState<Evaluation>();
  const [index, setIndex] = useState(0);
  const [error, setError] = useState("");
  const [feedback, setFeedback] = useState("");
  const [busy, setBusy] = useState(false);
  const [audit, setAudit] =
    useState<{ id: string; action: string; at: string }[]>();
  const [reviewDirty, setReviewDirty] = useState(false);
  const [reviewSaving, setReviewSaving] = useState(false);
  const [overallDirty, setOverallDirty] = useState(false);
  const load = async () => {
    const [a, e] = await Promise.all([
      api<Attempt>(`/attempts/${attemptId}`),
      api<Evaluation>(`/evaluation/${attemptId}`),
    ]);
    setAttempt(a);
    setEvaluation(e);
    if (e.publishedAt) {
      setFeedback(e.feedback ?? "");
      setOverallDirty(false);
    }
  };
  useEffect(() => {
    void load().catch((e) => setError(message(e)));
  }, [attemptId]);
  useEffect(() => {
    const beforeLeave = (event: BeforeUnloadEvent) => {
      if (overallDirty) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    window.addEventListener("beforeunload", beforeLeave);
    return () => window.removeEventListener("beforeunload", beforeLeave);
  }, [overallDirty]);
  const publish = async () => {
    if (reviewDirty || reviewSaving) return;
    setBusy(true);
    setError("");
    try {
      await api(
        `/reviews/${attemptId}/publish`,
        json("POST", { feedback, revision: evaluation?.revision ?? 0 }),
      );
      await load();
    } catch (e) {
      setError(message(e));
    } finally {
      setBusy(false);
    }
  };
  if (!attempt || !evaluation)
    return (
      <main id="main" className="page">
        {error ? (
          <Note tone="error">
            {error} <button onClick={back}>Back to queue</button>
          </Note>
        ) : (
          <Loading />
        )}
      </main>
    );
  const question = attempt.questions[index];
  const answer = attempt.answers.find((x) => x.questionId === question.id)!;
  const job = evaluation.questions.find((x) => x.questionId === question.id);
  const completed = evaluation.questions.filter((q) => q.review).length;
  const mayLeave = (includeOverall = false) =>
    !reviewSaving &&
    (!(reviewDirty || (includeOverall && overallDirty)) ||
      window.confirm(
        "Discard unsaved review changes? Save the question or publish overall feedback to keep them.",
      ));
  return (
    <main id="main" className="work-page">
      <div className="work-toolbar">
        <div>
          <button
            className="text-button"
            onClick={() => {
              if (mayLeave(true)) back();
            }}
          >
            Back to review queue
          </button>
          <strong>{attempt.title}</strong>
          <span className="small muted">
            {completed} of {attempt.questions.length} questions reviewed ·{" "}
            {label(evaluation.status)}
          </span>
        </div>
        <button
          className="secondary"
          disabled={reviewDirty || overallDirty || reviewSaving}
          onClick={() => {
            if (mayLeave()) void load().catch((e) => setError(message(e)));
          }}
        >
          Refresh processing status
        </button>
      </div>
      {error && <Note tone="error">{error}</Note>}
      <div className="review-workspace">
        <nav className="question-nav" aria-label="Review questions">
          <h2>Questions</h2>
          {attempt.questions.map((q, i) => (
            <button
              key={q.id}
              aria-label={`Question ${q.number}: ${q.title}`}
              aria-current={index === i ? "step" : undefined}
              onClick={() => {
                if (mayLeave()) setIndex(i);
              }}
            >
              <span className="question-number">{q.number}</span>
              <span>
                {q.title}
                <small>
                  {evaluation.questions.find((j) => j.questionId === q.id)
                    ?.review
                    ? "Reviewed"
                    : label(
                        evaluation.questions.find((j) => j.questionId === q.id)
                          ?.status ?? "processing",
                      )}
                </small>
              </span>
            </button>
          ))}
        </nav>
        <section className="review-evidence">
          <span className="small muted">
            Question {question.number} · {question.kind} response
          </span>
          <h1>{question.title}</h1>
          <p>{question.prompt}</p>
          <h2>Submitted evidence</h2>
          <div className="submitted-answer">
            {answer.text || "No written response was submitted."}
          </div>
          {answer.recordingId && (
            <audio
              controls
              src={`/api/attempts/${attemptId}/recordings/${answer.recordingId}`}
              aria-label="Submitted recording playback"
            />
          )}
          {question.kind === "oral" && (
            <>
              <h3>Transcript</h3>
              <p className="transcript">
                {job?.transcript ??
                  (attempt.manualOnly
                    ? "Manual-only review selected. Listen to the recording directly."
                    : "Transcript is not available yet. Listen directly or retry a failed job.")}
              </p>
            </>
          )}
          {job?.suggestion ? (
            <>
              <h2>AI suggestion</h2>
              <p className="small muted">
                A suggestion to check against the evidence. Your scores decide
                the result.
              </p>
              {job.suggestion.criteria.map((c) => (
                <div className="ai-criterion" key={c.criterionId}>
                  <strong>
                    {question.rubric.find((r) => r.id === c.criterionId)?.label}{" "}
                    · {c.score}/5
                  </strong>
                  <blockquote>{c.evidence}</blockquote>
                  <p>{c.rationale}</p>
                </div>
              ))}
              <p className="small muted">
                {job.suggestion.provider} · {job.suggestion.model} · prompt{" "}
                {job.suggestion.promptVersion} · rubric {question.version}
              </p>
            </>
          ) : (
            <Note>
              {job?.error
                ? `Processing needs attention: ${label(job.error)}. You can still review the submitted evidence.`
                : "No AI suggestion is available. Apply the rubric to the evidence directly."}
            </Note>
          )}
          {!attempt.manualOnly && job?.status === "needs-review" && (
            <button
              className="secondary"
              disabled={!!evaluation.publishedAt || busy}
              onClick={async () => {
                setBusy(true);
                try {
                  await api(
                    `/reviews/${attemptId}/questions/${question.id}/retry`,
                    json("POST", {}),
                  );
                  await load();
                } catch (e) {
                  setError(message(e));
                } finally {
                  setBusy(false);
                }
              }}
            >
              Retry failed processing
            </button>
          )}
        </section>
        <aside className="scoring-panel">
          <QuestionReview
            key={`${question.id}:${job?.review?.revision ?? 0}`}
            question={question}
            job={job}
            attemptId={attemptId}
            published={!!evaluation.publishedAt}
            onSaved={load}
            onError={setError}
            onDirty={setReviewDirty}
            onBusy={setReviewSaving}
          />
          <section className="publish-panel">
            <h2>Publish completed review</h2>
            <p className="small">
              All {attempt.questions.length} questions need reviewed criterion
              scores. Internal notes stay private.
            </p>
            <label htmlFor="overall-feedback">
              Overall feedback for the candidate
            </label>
            <textarea
              id="overall-feedback"
              rows={4}
              value={feedback}
              maxLength={4000}
              disabled={!!evaluation.publishedAt}
              onChange={(e) => {
                setFeedback(e.target.value);
                setOverallDirty(true);
              }}
            />
            <button
              className="primary wide"
              disabled={
                reviewDirty ||
                reviewSaving ||
                completed !== attempt.questions.length ||
                busy ||
                !!evaluation.publishedAt
              }
              onClick={() => void publish()}
            >
              {evaluation.publishedAt
                ? "Review published"
                : busy
                  ? "Publishing…"
                  : "Publish feedback"}
            </button>
            {evaluation.publishedAt && (
              <p className="small">
                Published reviews are immutable. Final score: {evaluation.total}
                /{evaluation.maximum}.
              </p>
            )}
            <button
              className="text-button"
              onClick={() =>
                void api<typeof audit>(`/reviews/${attemptId}/audit`)
                  .then(setAudit)
                  .catch((e) => setError(message(e)))
              }
            >
              Show review audit
            </button>
            {audit && (
              <ol className="audit-list">
                {audit.map((a) => (
                  <li key={a.id}>
                    {label(a.action)}
                    <small>{new Date(a.at).toLocaleString()}</small>
                  </li>
                ))}
              </ol>
            )}
          </section>
        </aside>
      </div>
    </main>
  );
}

function QuestionReview({
  question,
  job,
  attemptId,
  published,
  onSaved,
  onError,
  onDirty,
  onBusy,
}: {
  question: Question;
  job?: EvaluationQuestion;
  attemptId: string;
  published: boolean;
  onSaved: () => Promise<void>;
  onError: (error: string) => void;
  onDirty: (dirty: boolean) => void;
  onBusy: (busy: boolean) => void;
}) {
  const [scores, setScores] = useState<Record<string, string>>(
    Object.fromEntries(
      question.rubric.map((c) => [
        c.id,
        String(
          job?.review?.criteria.find((s) => s.criterionId === c.id)?.score ??
            "",
        ),
      ]),
    ),
  );
  const [feedback, setFeedback] = useState(job?.review?.feedback ?? "");
  const [notes, setNotes] = useState(job?.review?.internalNotes ?? "");
  const [busy, setBusy] = useState(false);
  const [changed, setChanged] = useState(false);
  useEffect(() => {
    onDirty(changed);
    return () => onDirty(false);
  }, [changed, onDirty]);
  useEffect(() => {
    onBusy(busy);
    return () => onBusy(false);
  }, [busy, onBusy]);
  useEffect(() => {
    const beforeLeave = (event: BeforeUnloadEvent) => {
      if (changed) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    window.addEventListener("beforeunload", beforeLeave);
    return () => window.removeEventListener("beforeunload", beforeLeave);
  }, [changed]);
  const save = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    onError("");
    try {
      await api(
        `/reviews/${attemptId}/questions/${question.id}`,
        json("PUT", {
          criteria: question.rubric.map((c) => ({
            criterionId: c.id,
            score: Number(scores[c.id]),
          })),
          feedback,
          internalNotes: notes,
          revision: job?.review?.revision ?? 0,
        }),
      );
      setChanged(false);
      await onSaved();
    } catch (e) {
      onError(message(e));
    } finally {
      setBusy(false);
    }
  };
  return (
    <form onSubmit={(event) => void save(event)}>
      <h2>Reviewer scores</h2>
      {question.rubric.map((c) => (
        <fieldset key={c.id} disabled={published || busy}>
          <legend>{c.label}</legend>
          <select
            aria-label={`${c.label} score`}
            value={scores[c.id]}
            required
            onChange={(e) => {
              setScores({ ...scores, [c.id]: e.target.value });
              setChanged(true);
            }}
          >
            <option value="">Select a score</option>
            {c.anchors.map((anchor, i) => (
              <option key={i} value={i}>
                {i} — {anchor}
              </option>
            ))}
          </select>
          <details>
            <summary>Read score anchors</summary>
            <ol start={0}>
              {c.anchors.map((a, i) => (
                <li key={i}>{a}</li>
              ))}
            </ol>
          </details>
        </fieldset>
      ))}
      <label htmlFor="question-feedback">Feedback for the candidate</label>
      <textarea
        id="question-feedback"
        value={feedback}
        maxLength={4000}
        rows={4}
        disabled={published || busy}
        onChange={(e) => {
          setFeedback(e.target.value);
          setChanged(true);
        }}
      />
      <label htmlFor="internal-notes">
        Internal notes <span className="muted">(reviewers only)</span>
      </label>
      <textarea
        id="internal-notes"
        value={notes}
        maxLength={4000}
        rows={3}
        disabled={published || busy}
        onChange={(e) => {
          setNotes(e.target.value);
          setChanged(true);
        }}
      />
      <p className="small muted">
        {changed
          ? "Unsaved review changes. Save before changing questions."
          : job?.review
            ? "Review saved."
            : "Select every criterion score, then save this question."}
      </p>
      <button className="primary wide" disabled={published || busy}>
        {busy
          ? "Saving review…"
          : published
            ? "Published · read only"
            : "Save question review"}
      </button>
    </form>
  );
}
