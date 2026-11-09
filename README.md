# Candidate Assessment

A timed assessment lab built as a **.NET modular monolith with background processing**, React, PostgreSQL and Kafka. One backend release contains Access, Assessments and Evaluation. Kafka carries processing IDs; answers and audio remain private.

The fictional assessment contains eight written questions, four spoken questions and a twenty-minute deadline. A second candidate has a thirty-minute assignment. A reviewer applies anchored criteria, checks AI suggestions and publishes the final feedback.

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

The modules use EF Core directly. There is no mediator framework, generic repository, event sourcing, saga or separate read database. [Architecture and boundaries](docs/architecture.md) · [Diagrams and editable Mermaid](docs/diagrams/).

## Check and recover

```sh
make help
make test          # module, architecture, adapter and draft checks
make verify        # builds, Terraform validation and real API integration
make recovery      # real provider outage + interrupted worker recovery
make diagrams      # render the four Mermaid sources to SVG
```

The isolated browser suite uses generated microphone audio, never your physical microphone:

```sh
cd frontend
npx playwright install --with-deps chromium
npm run test:browser
```

[Delivery and verification record](docs/TASK.md) distinguishes implemented behavior, verified acceptance and remaining limits. [Failure drills](docs/failure-drills.md) follow the successful journey. [Operations](docs/operations.md) covers configuration, health, explicit migrations, backups, restore, rollback, deletion and the thirty-day recording policy.

## Optional AWS Singapore profile

`infra/aws` provisions one ARM64 EC2 host in `ap-southeast-1`, encrypted persistent storage, and Session Manager access with no inbound security-group rules. A commit-pinned bootstrap runs the same lab and host Ollama. It is a learning deployment, not managed Kafka/PostgreSQL or a high-availability service.

```sh
make aws-init
make aws-validate
```

Read [AWS deployment instructions](infra/aws/README.md) before planning or applying. Terraform validation is not a cloud runtime test. No cloud resources were deployed for this delivery.

## Limits

An unfinished browser recording can be lost. Offline editing cannot extend the deadline. Browser storage can be blocked or exhausted; the UI warns explicitly and continues online server saves. Physical microphone quality, production capacity, cloud runtime reliability and hiring-score validity require separate evidence.
