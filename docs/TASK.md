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
| Frontend and mobile states | Implemented and verified | Three isolated Chromium journeys; MediaRecorder/playback, offline/refresh/conflicts, local-recording selection conflict, blocked storage, publication, lifecycle explorer navigation | Physical microphone quality remains device-specific |
| Module boundaries and CI | Implemented and verified locally and on Linux baseline | Twelve .NET tests, sixteen Python checks, three draft checks and seven offline Azure mock plans; [green baseline CI](https://github.com/junrillg/candidate-assessment-lab/actions/runs/42627766681); [current CI](https://github.com/junrillg/candidate-assessment-lab/actions/workflows/ci.yml) | Published head must be green before handoff |
| Independent design finish | Four material fixes resolved; disposition ship at that scope | Persistent acceptance band, mobile state labels/accessible names, absent-recording truth, metadata below titles; screenshot evidence in `.impeccable/review/` | Shipped system recorded in DESIGN.md |
| Technical documentation | Implemented and verified | Lifecycle and design notes, six flow explanations, failure/recovery scenarios, nine Mermaid/SVG pairs and interactive lifecycle explorer | Use the documentation index |
| Backup and retention | Implemented; private backup verified | Quiesced database/recording/key backup, checksums, private modes, safe archive paths and corrupt-restore rejection | Live restore needs a separate empty target |
| Terraform | Docker-local, Azure and AWS validated; cloud runtime owner-deferred | Azure Singapore preferred, AWS alternative; AzureRM 4.81.0, seven account-offline mock plans, bootstrap safety tests; no apply | Review provider inputs, access, cost and retained-data recovery before deployment |

## Evidence and practical limits

Validated on 2026-11-09 with .NET 10.0.400, Docker 29.5.2, Terraform 1.15.8, host `llama3.2:latest`, whisper.cpp base and isolated Chromium. Local evidence files are `.local/integration-evidence.json`, `.local/recovery-evidence.json` and `.local/sample-evidence.json`; the retained receipt is `.local/sample.json`. They contain synthetic identifiers/status only and are separate from application data. Disposable test assignments are cleaned; the two original assignments and learning sample remain.

The browser suite uses generated microphone audio. Connected-browser access was unavailable; physical microphone quality was not tested. The Impeccable detector ran once with no regex findings, but parser modules were unavailable; computed-style/selector auditing was not performed. Independent source-token contrast arithmetic and visual review were used, with the stated scope.

Unfinished browser recordings can be lost; offline work cannot extend a server deadline. Accepted audio is released from the active device draft only after server selection acknowledges it. Unsent work remains local and can be downloaded; device-storage failures are explicit. Thirty-day retention applies to live server recording bytes; backups and stale browser copies require the documented operator/device cleanup.

Neither Azure nor AWS was applied. Live restore, production capacity, cloud runtime recovery and hiring-score validity are unverified. Evaluation extraction remains a future decision only if measured scaling, ownership or release needs justify it.


## Azure follow-up

Requested outcome: add a runnable future Azure profile alongside AWS, make Azure the preferred cloud path, update repository/Notion docs, and add editable Mermaid plus rendered Azure deployment diagram. Local development remains primary. Exclusions: cloud deployment, paid resource creation, managed-service migration, application/UI changes. Implemented: AzureRM 4.81.0 profile, explicit Azure Make/CLI commands and CI validation, protected data disk/resource group, commit-pinned Ubuntu bootstrap and SSH tunnel. Verified: format/init/validate, seven mock-provider plan cases, eight new Python bootstrap tests (including nine refusal subcases), script syntax, readable SVG render, source topology and Markdown links. Thirteen Python integration-unit tests pass; the three existing transcription checks remain unchanged. Independent scoped review closed the zstd, mounted-disk persistence and explicit no-format-while-mounted findings. Repository and private Notion docs now prefer Azure while retaining AWS. The release gate is green CI on the published head; unchanged application browser/recovery checks were reused locally.

## README visual overview follow-up

Outcome: a repository landing page with the existing notebook identity, real candidate/reviewer screenshots and an immediately visible high-level connection/flow diagram. Scope: README and documentation assets only; no application behavior, new infrastructure or borrowed branding. Stop condition: screenshots show real fictional lab data without secrets, the diagram agrees with source, local/image links resolve, and the reviewed assets are published to the repository.

| Requirement | Status | Evidence | Next action |
|---|---|---|---|
| Notebook icon and README header | Verified | Editable notebook SVG based on shipped mark; GitHub GFM render accepts centered header and CI badge | None |
| Real application screenshots | Verified | Four byte-identical 1440px real captures, reviewed visually; fictional data only; provenance in `docs/assets/screenshots/README.md` | None |
| High-level architecture and flow | Verified | `overview.mmd` and readable 1241×934 SVG; source and independent checks confirm runtime/module wiring | None |
| Publication and verification | Verified and published | 50 relative links/assets resolve; PNG dimensions, SVG XML, GitHub GFM render and diff checks pass; independent scoped review clear; remote README/assets checked at delivery | None |

This follow-up changes documentation and assets only. Existing application tests were not rerun locally; the unchanged app behavior is covered by the earlier delivery evidence and the repository CI workflow.

## Architecture visualization and technical documentation

User outcome: replace the dense overview with clear, attractive diagrams, use consistent icons and established documentation practices, and present the repository as an application with technical knowledge docs. Keep the project icon, authentic screenshots, local development and existing architecture. Scope includes a footer label and the documentation explorer; business behavior, infrastructure and production claims stay unchanged.

| Requirement | Status | Evidence | Next action |
|---|---|---|---|
| Primary-source guidance | Verified | C4, arc42, Microsoft, IBM, Diátaxis and GitHub; `docs/design/diagram-notes.md` | None |
| Separate structure from journey | Verified | One-host map and numbered flow; HTML/SVG plus Mermaid models; 1280×720, opaque artboards, source-accurate labels | None |
| Product README and technical knowledge | Verified | Product workflow and quickstart; `docs/how-it-works.md` and six `docs/flows/` topics replace course pages | None |
| Repeatable diagram export | Verified | Stable HTML extraction, pixel-identical to bundled export; Mermaid fallback and four invalid-source preservation cases checked | None |
| Lifecycle explorer | Verified | Isolated browser test: stage navigation, keyboard access, six source links, no app mutation, no page overflow at 390px; course/progress controls removed | None |
| Review and publication | Verified and published | Source/visual review; offline SVG/geometry and icon bounds checks; repeatable exports; 149 local links; product README GitHub rendering; targeted lifecycle browser case and frontend build passed | None |

## Engineer-facing system design

Outcome: show the application's structural architecture for engineering review, alongside the separate business workflow. Scope: two technical diagrams and their source-grounded explanation/navigation; no runtime or infrastructure changes. Stop condition: deployment/process boundaries, interfaces/ports, module dependency direction, data ownership, persistence and reliability boundaries agree with source; both rendered figures are legible, accessible, repeatably exported and published.

| Requirement | Status | Evidence | Next action |
|---|---|---|---|
| Runtime and deployment structure | Verified | `system-architecture` HTML/SVG/Mermaid; actual workload placement, interfaces and five retained volumes match Compose | None |
| Internal components and ownership | Verified | `module-architecture` HTML/SVG/Mermaid; one process, three schemas, public reader, outbox/intake and shared credential boundaries verified | None |
| Architecture explanation and discovery | Verified | README presents system design first; architecture document adds interface/storage/ownership tables and operational constraints; stale aggregation/recovery claims corrected | None |
| Verification and publication | Verified and published | Independent engineering review clear; both 1440×960 figures pass geometry/bounds/mobile/export checks; 152 local links and GitHub rendering checked; unchanged app checks reused from green `d8d4b42` | None |


## Reusable architecture illustration

Outcome: install a global architecture-diagram skill from the shared skills repository and apply its icon-led visual grammar to the local deployment and module views. Scope: skill instructions, discovery/installation, the two structural diagrams and their design documentation. Runtime code, infrastructure and business flow remain outside this change. Stop condition: skill help/discovery and context synchronization pass; diagrams preserve verified topology, use traceable vendor marks, export repeatably and remain readable without external assets.

| Requirement | Status | Evidence | Next action |
|---|---|---|---|
| Reusable skill and global discovery | Verified and published | `architecture-diagram` in shared skills repository at `962a8a9`; Claude/Codex links resolve; validation and context synchronization 1940/1940 passed; help and local/proposed-cloud decisions independently reviewed | None |
| Vendor icons and visual grouping | Verified | Six genuine technology marks; titled execution/ownership groups; source/notice inventory; independent visual review clear | None |
| Architecture fidelity and export | Verified | Source-grounded deployment/modules preserved; byte-identical canonical export; unique SVG IDs; zero geometry/text bounds findings; light/dark 830px and contained 390px scroll inspected | None |
| Publication | Verified and published | Shared skill `962a8a9`; application figure sources/notices published together; 153 local links resolved; unchanged runtime tests reused | None |

Verification scope: documentation and skill behavior only; application code/config are unchanged and their prior acceptance evidence is reused. Offline SVG checks intentionally allow the Apache Kafka project hyperlink; it is navigation, not a loaded rendering asset. The earlier `dc4b005` CI failed while downloading Ubuntu packages from its mirror, before application execution; no application change is inferred from that network failure.

## Integrated lifecycle architecture

Outcome: make the primary diagram connect the complete scoped candidate/reviewer journey to actual modules, artifacts, stores and supporting runtimes. Update the global skill so broad architecture requests require that coverage rather than a technology inventory. Scope: shared skill, the existing overview HTML/SVG/Mermaid, README navigation and technical design documentation. Exclusions: application behavior, infrastructure changes, paid deployment, exhaustive low-level implementation events and unsupported pixel-identity claims. Stop condition: every [coverage-ledger](design/lifecycle-diagram.md) row is visible, source-accurate and readable; exported artifacts and mobile access pass; one independent review closes material findings; both repositories are published.

| Requirement | Status | Evidence | Next action |
|---|---|---|---|
| Research and explicit composition plan | Verified | Primary C4, Microsoft, vendor and W3C guidance; four categorized phases, action/artifact pictograms, named runtime ownership and dependency foundation | None |
| Shared skill defaults and global install | Verified and published | Shared skills commit `64c0407`; lifecycle default, narrow-view override and fast help reviewed; validation/context sync 1940/1940 passed; installed links resolve | None |
| Integrated overview and source-linked coverage | Verified | All 30 ledger anchors visible/present; written/oral acceptance, submission/expiry, delivery, AI bypasses/retries, publication and cleanup; one-process runtime ownership preserved | Use the overview and detail companions |
| Visual and artifact checks | Verified | 1440×2750 export, 830px light/dark inspection; zero out-of-bounds/within-node text overlaps, duplicate IDs or broken references; 390px document containment and keyboard scrolling; exact canonical export; 212 local links resolve | None |
| Independent review and publication | Verified and published | Evidence-to-review path and reviewer-retry loop corrected and independently rechecked; README and technical docs link the new figure; shared skill and application sources published together | Check repository CI for current-head application verification |

Skill help was reviewed and forward-simulated, not executed in an instrumented isolated invocation. Diagram checks do not constitute new application, microphone, provider or cloud-runtime tests. Application code/config did not change; unchanged runtime evidence is reused from green [CI on `84d6c12`](https://github.com/junrillg/candidate-assessment-lab/actions/runs/42746773485). The two external Kafka project hyperlinks are navigation, not remote rendering assets. Local render/coverage evidence is retained in `.local/lifecycle-diagram-evidence.json` and `.local/lifecycle-root-evidence.json`.
