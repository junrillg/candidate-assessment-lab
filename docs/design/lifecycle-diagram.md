# Lifecycle architecture: scope and coverage

The [integrated system diagram](../diagrams/overview.svg) follows one assessment from preparation to published feedback. It combines the user journey with the modules, evidence, delivery and providers involved. The [deployment view](../diagrams/system-architecture.svg) supplies exact process placement and ports; the [component view](../diagrams/module-architecture.svg) supplies internal dependencies.

## Composition contract

The primary figure uses a white artboard, dark title tabs, dashed **lifecycle categories**, soft functional colors, short labels, recognizable technology marks and consistent action/artifact pictograms. Written and oral work branch and converge; manual-only and failed processing reach human review. Numbers establish reading order, not a total ordering of concurrent requests. Ownership labels and the runtime foundation distinguish phases from deployed services.

The design must retain these visible categories:

1. **Preparation:** private accounts, assignment, sign-in, practice and the explicit start boundary.
2. **Live assessment:** written and spoken capture, device persistence, server acceptance, conflict/offline outcomes and the deadline.
3. **Durable processing:** frozen evidence and receipt, transactional outbox, Kafka intake, leased jobs, provider branches and retained results.
4. **Human review and feedback:** reviewer access, evidence and rubric, human scoring, publication, candidate feedback, deletion and recording retention.

| Shipped composition | Value |
|---|---|
| Editable artboard | 1440 × 2750; white, portrait reading order |
| Hierarchy | Four lifecycle frames, nested device/server/processing areas, runtime foundation |
| Type | System sans; 43px title, 27px phase titles, 24px node names, 20px captions |
| Semantic color | Blue for device/start work, teal for accepted/human outcomes, violet for optional AI, amber for alternate/recovery handling |
| Symbols | Original action/artifact pictograms; embedded official technology marks with unchanged vendor colors |
| Navigation | Six numbered milestones, directed branches, full-size SVG and keyboard-scrollable local HTML |

Detailed deployment remains available alongside this overview. Lifecycle frames do not imply network isolation, security walls or separate services. All Access, Assessments and Evaluation operations and background loops run in **one .NET process**. One PostgreSQL connection/user serves three code-owned schemas. Recordings are private files reached through the authenticated API. Kafka and whisper.cpp run in separate Compose containers; Ollama runs on the host.

## Coverage ledger and text equivalent

This is the declared scope of completeness: material user actions, accepted evidence transitions, background handoffs and alternative outcomes. It is not a list of every HTTP request, framework callback or validation error. Only `ProcessingIntent` is a broker message; other arrows describe actions, calls, persistence or state transitions.

Each row must have a visible diagram anchor. Source links explain the behavior rather than claiming that drawing a branch constitutes a new runtime test.

| Figure anchor | Trigger, owner and transformation | Outcome / alternative | Source |
|---|---|---|---|
| `provision` | Operator migrates and seeds private accounts, questions and assignments | Candidate and reviewer access; 20-minute and extended 30-minute assignments; no registration | [Host modes](../../src/Host/Program.cs), [Identity seed](../../src/Access/AccessModule.cs), [assessment seed](../../src/Assessments/Seed.cs) |
| `access` | Browser → Access: sign in, cookie session and CSRF | Role-specific workspace; throttling/lockout on failed access | [Access](../../src/Access/AccessModule.cs) |
| `preparation` | Candidate reads rules, practices writing and microphone playback, chooses optional manual-only review | Timer not started; practice stays in memory and is not submitted | [Preparation screen](../../frontend/src/App.tsx) |
| `start` | Assessments locks the assignment and starts or resumes | Frozen questions/rubrics/duration; server-authoritative deadline | [StartAttempt](../../src/Assessments/AssessmentModule.cs) |
| `written`, `local-drafts` | Candidate types, navigates and flags; browser immediately saves device drafts | IndexedDB refresh recovery; one-second debounced server save | [Attempt state](../../frontend/src/useAttempt.ts), [draft storage](../../frontend/src/drafts.ts) |
| `server-save` | Browser → Assessments: answer PUT with server revision | Accepted answer and new revision; stale writes rejected | [Answer endpoint](../../src/Assessments/AssessmentModule.cs) |
| `recording`, `local-audio` | Candidate records up to two minutes; completed Blob and stable upload ID persist locally | Whole-file retry; unfinished capture can be lost on browser interruption | [Recorder](../../frontend/src/useRecorder.ts), [local audio](../../frontend/src/drafts.ts) |
| `upload-select` | Assessments validates ownership and stored audio bytes → immutable recording → separate answer save selects its ID | Upload alone does not select the answer's recording | [Upload and selection](../../src/Assessments/AssessmentModule.cs), [byte validation/storage](../../src/Assessments/RecordingStorage.cs) |
| `accepted-evidence` | Assessments retains accepted answers and verified selected recordings | Only this evidence can enter the frozen submission | [Finalization](../../src/Assessments/AssessmentModule.cs) |
| `unsent` | Offline, failed upload/save, revision conflict or late editing | Local work remains visibly unsent; reconnect/resolve before deadline; storage failure is explicit | [Recovery and conflict handling](../../frontend/src/useAttempt.ts), [draft errors](../../frontend/src/drafts.ts) |
| `finalize` | Candidate reviews unanswered/upload states and submits, or maintenance reaches server expiry | Pending work blocks voluntary submit; expiry finalizes even with browser closed | [Submit screen](../../frontend/src/App.tsx), [deadline loop](../../src/Assessments/AssessmentMaintenance.cs) |
| `snapshot-outbox` | One Assessments transaction freezes evidence, durable receipt and per-question outbox intents | Repeated submission returns the same receipt; no cross-module transaction | [SubmitAttempt / FinalizeAsync](../../src/Assessments/AssessmentModule.cs) |
| `dispatch`, `kafka` | Dispatcher publishes committed intents, then marks them published | IDs/correlation only, no answer/audio; unavailable broker leaves intent pending; publication can repeat | [Dispatcher](../../src/Assessments/AssessmentMaintenance.cs), [message contract](../../src/Assessments/Contracts.cs) |
| `intake` | Evaluation reads frozen evidence through `IAssessmentReader`, inserts job idempotently, then acknowledges delivery | Duplicate delivery reuses durable job; DB failure remains unacknowledged | [Kafka intake](../../src/Evaluation/BackgroundProcessing.cs) |
| `lease` | Background worker claims queued/due/reclaimable jobs with a lease and fence | Interrupted work is reclaimable; stale completion rejected | [Claim and fenced updates](../../src/Evaluation/BackgroundProcessing.cs) |
| `manual-only` | Manual-only job bypasses both providers; unanswered or missing-recording evidence also needs human handling | Still passes through outbox/Kafka/job intake; never an automatic zero | [Processing branches](../../src/Evaluation/BackgroundProcessing.cs) |
| `transcription`, `transcript` | Oral recording → whisper.cpp → validated transcript persisted before scoring | Retry reuses saved transcript; written answers bypass transcription | [Worker ordering](../../src/Evaluation/BackgroundProcessing.cs), [provider adapter](../../src/Evaluation/Providers.cs) |
| `scoring`, `suggestion` | Written evidence or saved transcript + frozen rubric → host Ollama → validated criterion scores/evidence | Versioned AI suggestion stays separate from human scores | [Output validation](../../src/Evaluation/Providers.cs) |
| `retry` | Timeout, invalid output or unavailable provider schedules durable capped retries | Fourth failure → needs review; reviewer can retry eligible failed-provider jobs before publication | [Retry scheduling](../../src/Evaluation/BackgroundProcessing.cs), [reviewer retry](../../src/Evaluation/EvaluationModule.cs) |
| `review` | Reviewer signs in, opens queue and inspects frozen answer/audio, rubric, transcript and available suggestion | May review without successful AI or completed processing; revisioned criterion scores, candidate feedback, private notes and audit | [Review endpoints](../../src/Evaluation/EvaluationModule.cs) |
| `publish` | All questions/criteria scored → application aggregates and publishes | Immutable publication with audit; repeat publication returns existing result | [PublishReview](../../src/Evaluation/EvaluationModule.cs) |
| `feedback` | Candidate polls processing status and reads published result | Scores/feedback appear only after publication; internal notes/transcripts/AI rationale stay private | [Candidate result projection](../../src/Evaluation/EvaluationModule.cs), [receipt/status screen](../../frontend/src/App.tsx) |
| `deletion`, `retention` | Candidate/reviewer deletes → access blocked immediately → retryable module/file cleanup; retention independently purges old live recording bytes | Thirty days applies to recording bytes, not all assessment data; browser copies/backups have separate cleanup obligations | [Assessment cleanup](../../src/Assessments/AssessmentMaintenance.cs), [Evaluation cleanup](../../src/Evaluation/BackgroundProcessing.cs), [retention policy](../operations.md) |
| `runtime-map` | Browser React/IndexedDB; one .NET process; supporting PostgreSQL, files, Kafka, transcription and host Ollama | No worker microservice, separate read database or managed cloud service is implied | [Composition](../../src/Host/Program.cs), [local deployment](../../compose.yaml) |

Broker-log loss is outside ordinary redelivery: published-but-not-intaken records are not recreated automatically by database restore. See [operational limits](../architecture.md#explicit-limits).

## Evidence behind the visual method

[C4 dynamic diagrams](https://c4model.com/diagrams/dynamic) support numbered interactions over a structural arrangement. [Microsoft architecture-diagram guidance](https://learn.microsoft.com/en-us/azure/well-architected/architect-role/design-diagrams) discusses layers, user journeys, state transitions and consistent visual language. These support a combined explanation; the figure does not claim strict C4 conformance or a universal industry style.

Functional pictograms describe actions and artifacts; vendor marks identify actual technologies. Those are separate visual roles. [Lucide's design principles](https://lucide.dev/contribute/icons/design-principles) and [brand-logo statement](https://lucide.dev/brand-logo-statement) make that distinction useful; our functional symbols are original SVG artwork, not downloaded Lucide assets. Vendor vectors are reused from the [verified asset inventory](vendor-icons.md), with their existing notices and approved colors. Future cloud views should use the official [Azure](https://learn.microsoft.com/en-us/azure/architecture/icons/) or [AWS](https://aws.amazon.com/architecture/icons/) service packs for services actually depicted.

SVG keeps text and geometry editable and embeds vectors without network rendering dependencies. Shape, text and line labels reinforce color meaning, following [W3C color guidance](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html). This ledger supplies the [complex image text alternative](https://www.w3.org/WAI/tutorials/images/complex/).

## Acceptance and stop condition

Verify every ledger anchor against visible content and source, including alternate branches and human publication. Inspect the exported figure at its README width and at full size; check short labels, icons, arrow endpoints, crossings, category hierarchy and vendor contrast. On mobile, provide named keyboard-accessible scrolling without document overflow. Require embedded assets, accessible naming, source/export parity, aligned Mermaid and local links. Finish with an independent coverage/visual review and fix material findings. Runtime tests remain separate evidence; a documentation redraw does not establish new application behavior.
