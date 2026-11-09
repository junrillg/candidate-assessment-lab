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
| Module boundaries and CI | Implemented and verified locally and on Linux baseline | Twelve .NET tests, eight Python checks, three draft checks; [green baseline CI](https://github.com/junrillg/candidate-assessment-lab/actions/runs/42627766681); [current CI](https://github.com/junrillg/candidate-assessment-lab/actions/workflows/ci.yml) | Published head must be green before handoff |
| Independent design finish | Four material fixes resolved; disposition ship at that scope | Persistent acceptance band, mobile state labels/accessible names, absent-recording truth, metadata below titles; screenshot evidence in `.impeccable/review/` | Shipped system recorded in DESIGN.md |
| Learning and Notion | Implemented | Six lessons, four subsequent failure drills, four Mermaid/SVG pairs, interactive illustrated walkthrough, [private Notion home](https://app.notion.com/p/41371e91a0f681cb9ba9daddbe4048cf) | Learner checkpoints remain unstarted |
| Backup and retention | Implemented; private backup verified | Quiesced database/recording/key backup, checksums, private modes, safe archive paths and corrupt-restore rejection | Live restore needs a separate empty target |
| Terraform | Both profiles validated; cloud runtime owner-deferred | Docker-local profile and AWS Singapore single-host/SSM profile; no apply | Review infrastructure/cost before future deployment |

## Evidence and practical limits

Validated on 2026-11-09 with .NET 10.0.400, Docker 29.5.2, Terraform 1.15.8, host `llama3.2:latest`, whisper.cpp base and isolated Chromium. Local evidence files are `.local/integration-evidence.json`, `.local/recovery-evidence.json` and `.local/sample-evidence.json`; the retained receipt is `.local/sample.json`. They contain synthetic identifiers/status only and are separate from learner progress. Disposable test assignments are cleaned; the two original assignments and learning sample remain.

The browser suite uses generated microphone audio. Connected-browser access was unavailable; physical microphone quality was not tested. The Impeccable detector ran once with no regex findings, but parser modules were unavailable; computed-style/selector auditing was not performed. Independent source-token contrast arithmetic and visual review were used, with the stated scope.

Unfinished browser recordings can be lost; offline work cannot extend a server deadline. Accepted audio is released from the active device draft only after server selection acknowledges it. Unsent work remains local and can be downloaded; device-storage failures are explicit. Thirty-day retention applies to live server recording bytes; backups and stale browser copies require the documented operator/device cleanup.

AWS was not applied. Live restore, production capacity, cloud runtime recovery and hiring-score validity are unverified. Evaluation extraction remains a future decision only if measured scaling, ownership or release needs justify it.
