<p align="center">
  <img src="docs/assets/candidate-assessment-icon.svg" width="96" height="96" alt="Candidate Assessment notebook icon" />
</p>

<h1 align="center">Candidate Assessment</h1>

<p align="center"><strong>A local learning lab for timed assessments, reliable jobs and human review.</strong></p>
<p align="center">Prepare. Answer. Follow the evidence from submission to published feedback.</p>

<p align="center">
  <a href="#run-locally">Run locally</a> ·
  <a href="#screenshots">Screenshots</a> ·
  <a href="#how-it-connects">Architecture</a> ·
  <a href="docs/learning.md">Six lessons</a>
</p>

<p align="center">
  <a href="https://github.com/junrillg/candidate-assessment-lab/actions/workflows/ci.yml"><img src="https://github.com/junrillg/candidate-assessment-lab/actions/workflows/ci.yml/badge.svg" alt="Build and verification status" /></a>
</p>

A timed assessment lab built as a **.NET modular monolith with background processing**, React, PostgreSQL and Kafka. One backend release contains Access, Assessments and Evaluation. Kafka carries processing IDs; answers and audio remain private.

The fictional assessment contains eight written questions, four spoken questions and a twenty-minute deadline. A second candidate has a thirty-minute assignment. A reviewer applies anchored criteria, checks AI suggestions and publishes the final feedback.

## Screenshots

Real application screens with fictional learning data. Select an image to view it at full size.

[![Candidate workspace with question navigation, timer and answer save status](docs/assets/screenshots/candidate-workspace.png)](docs/assets/screenshots/candidate-workspace.png)

| Prepare before the timer starts | Review the evidence and rubric |
|---|---|
| [![Preparation and microphone practice](docs/assets/screenshots/preparation.png)](docs/assets/screenshots/preparation.png) | [![Reviewer workspace with evidence and rubric criteria](docs/assets/screenshots/reviewer-workspace.png)](docs/assets/screenshots/reviewer-workspace.png) |

[Capture details and provenance](docs/assets/screenshots/README.md). The [supplied Langdeck screenshot](docs/assets/langdeck-readme-reference.png) is preserved as a layout reference; the screens above are Candidate Assessment.

## How it connects

The browser talks to **one .NET application**. Access handles identity, Assessments accepts and freezes answers, and Evaluation runs processing and human review. Each module owns its PostgreSQL schema; recordings stay in private storage.

[![High-level architecture connecting the React browser, three modules in one .NET host, PostgreSQL, Kafka, private recording storage, transcription and Ollama](docs/diagrams/overview.svg)](docs/diagrams/overview.svg)

1. **Prepare and answer:** the candidate signs in, starts an assignment, and saves answers or verified recordings through the authenticated API.
2. **Submit durably:** Assessments commits the frozen submission and outbox intents in one transaction. A background dispatcher sends IDs to Kafka; answer text and audio never enter messages.
3. **Process and review:** Evaluation creates durable jobs, saves transcripts from whisper.cpp, then asks Ollama for rubric suggestions. A reviewer confirms or changes the scores and publishes feedback for the candidate. Manual-only review skips AI processing.

[Editable overview](docs/diagrams/overview.mmd) · [Detailed architecture](docs/architecture.md) · [Step-by-step submission flow](docs/diagrams/happy-path.svg) · [Interactive illustrated walkthrough](docs/follow-an-assessment.html)

## Run locally

Install Docker Compose, .NET 10, Node 24, Python 3, FFmpeg and Terraform. Install and start [Ollama](https://ollama.com/), then:

```sh
ollama pull llama3.2:latest
make up
```

Open **[http://localhost:5188](http://localhost:5188)**. `make configure` generates private credentials for two candidates and one reviewer in `.local/credentials.json` (mode 0600, ignored by Git). Open that file locally to sign in. Registration is disabled. Never commit or paste those passwords into learning notes.

The first start builds the application and transcription runtime, downloads a checksum-verified Whisper base model, explicitly migrates all three schemas, and seeds the fictional assessment. Preparation and microphone practice do not start the timer.

`make down` preserves the database, broker, recordings, session keys and downloaded model. No command in the default local path deploys paid cloud infrastructure.

## Follow one attempt

1. Sign in as a candidate, choose an assignment and try the practice tools.
2. Start the timer, write an answer, wait for **Server saved**, then refresh.
3. Record a spoken answer, stop, play it back and wait for verified server selection.
4. Review the answers and submit. Keep the durable receipt.
5. Sign in as the reviewer. Inspect the evidence, transcript, rubric and AI rationale.
6. Save every criterion score and publish feedback. The candidate can return to the receipt to read it.

Manual-only review is available before starting. AI suggestions are never final scores. Failed processing requests human review and never assigns an automatic zero.

Use `make sample` to create one reusable synthetic trace for learning. [Six lessons](docs/learning.md) · [Interactive illustrated walkthrough](docs/follow-an-assessment.html) · [Private Notion learning home](https://app.notion.com/p/41371e91a0f681cb9ba9daddbe4048cf). To open the interactive walkthrough locally, serve `docs/` with `python3 -m http.server 5193 --directory docs` and visit `/follow-an-assessment.html` on that port.

## What to observe

- A response first persists in IndexedDB, then synchronizes after a one-second debounce. Device and server acceptance have distinct statuses. Separate tab drafts survive a server revision conflict.
- The database clock controls expiry. A background loop freezes only accepted answers and verified selected recordings, even when the browser is closed. Late local work is visibly unsent and can be downloaded.
- Submission freezes evidence and writes an outbox intent per question in the same transaction. Broker downtime delays processing while the receipt remains durable.
- Evaluation stores each intent once, then claims persisted leases with fenced completion. Transcription is saved before scoring. Timeouts, capped retries and terminal needs-review states are durable.
- A reviewer’s scores, feedback, private notes and audit records are separate from AI suggestions. Publication is immutable and only published feedback reaches the candidate.

The modules use EF Core directly. There is no mediator framework, generic repository, event sourcing, saga or separate read database. [Architecture and boundaries](docs/architecture.md) · [Diagrams and editable Mermaid](docs/diagrams/) · [Shipped UI design system](DESIGN.md).

## Check and recover

```sh
make help
make test          # module, architecture, adapter and draft checks
make verify        # builds, Terraform validation and real API integration
make recovery      # real provider outage + interrupted worker recovery
make diagrams      # render editable Mermaid sources to SVG
```

The isolated browser suite uses generated microphone audio, never your physical microphone:

```sh
cd frontend
npx playwright install --with-deps chromium
npm run test:browser
```

[Delivery and verification record](docs/TASK.md) distinguishes implemented behavior, verified acceptance and remaining limits. [Failure drills](docs/failure-drills.md) follow the successful journey. [Operations](docs/operations.md) covers configuration, health, explicit migrations, backups, restore, rollback, deletion and the thirty-day recording policy.

## Future cloud profiles

Azure is the preferred future cloud deployment. Local Compose remains the learning path, and the AWS profile stays available as an alternative. Both cloud profiles run the same application, PostgreSQL, Kafka, transcription and host Ollama on one VM; they are learning deployments with one failure domain.

| Profile | Singapore region | Operator access |
|---|---|---|
| [Azure](infra/azure/README.md) — preferred | `southeastasia` | SSH public key, port 22 restricted to an operator IPv4 `/32`, local tunnel to the app |
| [AWS](infra/aws/README.md) — alternative | `ap-southeast-1` | Session Manager with no security-group ingress |

The Azure profile uses an Ubuntu VM, encrypted managed storage with a protected data disk, and a commit-pinned checkout. Application, database and broker ports remain bound to loopback. See the [Azure deployment diagram](docs/diagrams/azure.svg) and its [editable Mermaid](docs/diagrams/azure.mmd).

```sh
make azure-init
make azure-validate
# Retained alternative:
make aws-init
make aws-validate
```

Read the provider-specific instructions for required inputs, access, costs and data retention before planning or applying. Terraform validation is not a cloud runtime test. Neither cloud profile has been deployed for this delivery.

## Limits

An unfinished browser recording can be lost. Offline editing cannot extend the deadline. Browser storage can be blocked or exhausted; the UI warns explicitly and continues online server saves. Physical microphone quality, production capacity, cloud runtime reliability and hiring-score validity require separate evidence.
