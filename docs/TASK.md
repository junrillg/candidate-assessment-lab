# Delivery and verification record

Outcome: Candidate Assessment runs locally as one React frontend and one .NET host with Access, Assessments and Evaluation assemblies; PostgreSQL, Kafka, private recording storage, whisper.cpp and host Ollama support it. Existing sibling projects were left unchanged.

Scope exclusions: microservices, recruiting-platform expansion, paid cloud deployment, production-scale or hiring-score-validity claims. Stop condition: the local flow and targeted failure checks pass, the independent UI fixes close, shipped design is documented, and the published source passes CI.

| Requirement | Status | Evidence | Next action |
|---|---|---|---|
| Identity, roles, CSRF, private credentials | Implemented and verified | Real integration authentication/ownership/role/CSRF checks; credentials ignored and mode 0600 | Use private local accounts |
| Attempts, revision saves, strict expiry | Implemented and verified | Concurrent stale write rejected, refresh recovery, expiry without browser, late writes rejected | Complete a seeded assignment |
| Immutable recordings and submission | Implemented and verified | Whole-file IDs, byte-identical private playback, media validation, repeated durable receipts | Wait for server acceptance |
| Outbox and Kafka | Implemented and verified | Broker outage preserves twelve intents; duplicate deliveries create one job per intent | Follow the same attempt ID |
| Durable jobs and AI adapters | Implemented and verified | Five real recovery checks: terminal retries, manual retry, restarted worker, old-fence update zero, real Whisper/Ollama | Inspect provider metadata and evidence |
| Review and published feedback | Implemented and verified | Full-criteria/concurrency gates, audit, immutable publication, candidate redaction | Human scores remain separate from AI |
| Full AI-assisted sample | Implemented and verified | Four transcripts, nine validated suggestions, three invalid outputs safely routed to review; all twelve synthetic reviews published 72/120 | Open the retained sample receipt |
| Frontend and mobile states | Implemented and verified | Three isolated Chromium journeys; MediaRecorder/playback, offline/refresh/conflicts, local-recording selection conflict, blocked storage, publication, saved walkthrough checkpoints | Physical microphone quality remains device-specific |
| Module boundaries and CI | Implemented and verified locally and on Linux baseline | Twelve .NET tests, sixteen Python checks, three draft checks and seven offline Azure mock plans; [green baseline CI](https://github.com/junrillg/candidate-assessment-lab/actions/runs/42627766681); [current CI](https://github.com/junrillg/candidate-assessment-lab/actions/workflows/ci.yml) | Published head must be green before handoff |
| Independent design finish | Four material fixes resolved; disposition ship at that scope | Persistent acceptance band, mobile state labels/accessible names, absent-recording truth, metadata below titles; screenshot evidence in `.impeccable/review/` | Shipped system recorded in DESIGN.md |
| Learning and Notion | Implemented | Six lessons, four subsequent failure drills, six Mermaid/SVG pairs, interactive illustrated walkthrough, [private Notion home](https://app.notion.com/p/41371e91a0f681cb9ba9daddbe4048cf) | Learner checkpoints remain unstarted |
| Backup and retention | Implemented; private backup verified | Quiesced database/recording/key backup, checksums, private modes, safe archive paths and corrupt-restore rejection | Live restore needs a separate empty target |
| Terraform | Docker-local, Azure and AWS validated; cloud runtime owner-deferred | Azure Singapore preferred, AWS alternative; AzureRM 4.81.0, seven account-offline mock plans, bootstrap safety tests; no apply | Review provider inputs, access, cost and retained-data recovery before deployment |

## Evidence and practical limits

Validated on 2026-11-09 with .NET 10.0.400, Docker 29.5.2, Terraform 1.15.8, host `llama3.2:latest`, whisper.cpp base and isolated Chromium. Local evidence files are `.local/integration-evidence.json`, `.local/recovery-evidence.json` and `.local/sample-evidence.json`; the retained receipt is `.local/sample.json`. They contain synthetic identifiers/status only and are separate from learner progress. Disposable test assignments are cleaned; the two original assignments and learning sample remain.

The browser suite uses generated microphone audio. Connected-browser access was unavailable; physical microphone quality was not tested. The Impeccable detector ran once with no regex findings, but parser modules were unavailable; computed-style/selector auditing was not performed. Independent source-token contrast arithmetic and visual review were used, with the stated scope.

Unfinished browser recordings can be lost; offline work cannot extend a server deadline. Accepted audio is released from the active device draft only after server selection acknowledges it. Unsent work remains local and can be downloaded; device-storage failures are explicit. Thirty-day retention applies to live server recording bytes; backups and stale browser copies require the documented operator/device cleanup.

Neither Azure nor AWS was applied. Live restore, production capacity, cloud runtime recovery and hiring-score validity are unverified. Evaluation extraction remains a future decision only if measured scaling, ownership or release needs justify it.


## Azure follow-up

Requested outcome: add a runnable future Azure profile alongside AWS, make Azure the preferred cloud path, update repository/Notion docs, and add editable Mermaid plus rendered Azure deployment diagram. Local learning remains primary. Exclusions: cloud deployment, paid resource creation, managed-service migration, application/UI changes. Implemented: AzureRM 4.81.0 profile, explicit Azure Make/CLI commands and CI validation, protected data disk/resource group, commit-pinned Ubuntu bootstrap and SSH tunnel. Verified: format/init/validate, seven mock-provider plan cases, eight new Python bootstrap tests (including nine refusal subcases), script syntax, readable SVG render, source topology and Markdown links. Thirteen Python integration-unit tests pass; the three existing transcription checks remain unchanged. Independent scoped review closed the zstd, mounted-disk persistence and explicit no-format-while-mounted findings. Repository and private Notion docs now prefer Azure while retaining AWS. The release gate is green CI on the published head; unchanged application browser/recovery checks were reused locally.

## README visual overview follow-up

Outcome: a repository landing page with the existing notebook identity, real candidate/reviewer screenshots and an immediately visible high-level connection/flow diagram, using the supplied Langdeck screenshot as a presentation reference. Scope: README and documentation assets only; no application behavior, new infrastructure or borrowed branding. Stop condition: the supplied image is preserved unchanged, screenshots show real fictional lab data without secrets, the diagram agrees with source, local/image links resolve, and the reviewed assets are published to the repository.

| Requirement | Status | Evidence | Next action |
|---|---|---|---|
| Supplied image retained | Verified | `docs/assets/langdeck-readme-reference.png`, labeled as reference; SHA-256 matches the attachment | None |
| Notebook icon and README header | Verified | Editable notebook SVG based on shipped mark; GitHub GFM render accepts centered header and CI badge | None |
| Real application screenshots | Verified | Four byte-identical 1440px real captures, reviewed visually; fictional data only; provenance in `docs/assets/screenshots/README.md` | None |
| High-level architecture and flow | Verified | `overview.mmd` and readable 1241×934 SVG; source and independent checks confirm runtime/module wiring | None |
| Publication and verification | Verified and published | 50 relative links/assets resolve; reference identity, PNG dimensions, SVG XML, GitHub GFM render and diff checks pass; independent scoped review clear; remote README/assets checked at delivery | None |

This follow-up changes documentation and assets only. Existing application tests were not rerun locally; the unchanged app behavior is covered by the earlier delivery evidence and the repository CI workflow.
