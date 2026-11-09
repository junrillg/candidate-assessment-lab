# Operate the local lab

Prerequisites: Docker with Compose, Python 3, .NET 10 SDK, Node 24/npm, Terraform, FFmpeg/ffprobe for native tests, and host Ollama. The containerized application includes its own media tooling. Use at least enough Docker memory for PostgreSQL, Kafka and CPU transcription. All commands run from the repository root.

```sh
ollama pull llama3.2:latest
make up
```

Open `http://localhost:5188`. `make up` generates private configuration, builds one application release, starts dependencies, explicitly migrates all three module schemas, seeds fictional identities and starts the host. It serializes heavy build/check commands. The first transcription start downloads and checksum-verifies the Whisper base model. `make status` shows readiness. `make down` preserves every data volume.

Open `.local/credentials.json` locally for two candidate accounts and one reviewer. These generated passwords and `.local/lab.env` are mode 0600 and ignored by Git. The application has no public registration. Seeding is repeatable and never prints passwords. Candidate 1 has a twenty-minute assignment; candidate 2 has thirty minutes. The scenarios and people are fictional.

Docker binds application/DB/Kafka host ports to loopback. Recording storage and session keys have dedicated private volumes; audio is served only through ownership-checked endpoints. Transcription has no published host port. Logs report identifiers/status/error types, never candidate bodies, audio, passwords or raw provider responses. Local HTTP runs on localhost; a future public endpoint requires TLS and corresponding cookie/proxy configuration.

## Commands and configuration

`make help` lists the entry points. `make migrate` and `make seed` are explicit one-shot commands; normal host startup does neither. `make sample` creates or reopens one synthetic learning trace without consuming a seeded assessment. It uploads fictional speech and submits for real AI assistance; a reviewer publishes the outcome.

The generated `.local/lab.env` supplies PostgreSQL's password and `OLLAMA_MODEL`. Compose wires `ConnectionStrings__Database`, `Kafka__BootstrapServers`, `Kafka__Topic`, `Storage__Root`, `DataProtection__Path`, `Providers__OllamaUrl`, `Providers__Model`, `Providers__TranscriptionUrl` and `Seed__CredentialsPath`. `make validate` checks private file permissions, credential shape and Compose configuration; the host validates required configuration and provider URL shapes.

Readiness `/health/ready` checks database access and required module tables. Liveness `/health/live` checks the HTTP host. Broker/provider failure does not reject already accepted draft work or erase submission intent; inspect question processing status and reviewer retry controls.

## Editable diagrams

Run `cd frontend && npm ci && npx playwright install chromium` once to install the same isolated browser used by the browser checks, then run `make diagrams` from the repository root. The official pinned Mermaid CLI is fetched into npm's tool cache and reuses that Chromium. Rendering runs serially and writes SVGs beside their editable `.mmd` sources. It does not add a frontend runtime dependency.

## Backup and restore

```sh
make backup
# Read the printed private .local/backups/<timestamp> path.
```

Backup temporarily stops only this project's app, dumps PostgreSQL, archives private recordings and session keys, copies private seed credentials and writes checksums. It restarts an app that was running. Keep the backup private: it contains real evidence if real users have used the lab. Database and recording files remain consistent because the application is quiesced. Kafka contains metadata only and is recoverable from the durable database outbox/jobs.

Restore **only into an empty, stopped lab**, with PostgreSQL running and an application image already built:

```sh
make configure
# Start this new lab's PostgreSQL, but do not migrate/seed or start app.
docker compose --env-file .local/lab.env up -d --wait postgres
make restore BACKUP=/absolute/path/to/private/backup
# Then start dependencies and app without reseeding over the restored database.
docker compose --env-file .local/lab.env up -d kafka transcription app
```

Restore checks all checksums, rejects unsafe archive paths, refuses application tables or existing recording/key files, and imports SQL in one transaction. It never overwrites accepted work. Verify a receipt, published feedback and authenticated private playback after restore. A failed restore may require a fresh empty target before retry; the script does not erase partial storage to force success.

## Retention and deletion

The documented recording policy is **thirty days from upload**. The background cleanup loop removes live recording bytes after the cutoff and marks the reference purged; an unavailable recording must go to human handling. It does not silently replace missing evidence with zero. A completed local recording blob is cleared only after the server acknowledges its selected answer. Upload failures, conflicts and unsent oral work retain the blob for recovery/playback/download. Stale-tab recovery copies may remain until browser storage is cleared or the attempt is deleted; there is no automatic thirty-day client purge. Deleting an attempt clears that user's current attempt drafts and session pointers. Clear browser data on shared devices.

Deleting an attempt immediately revokes access. A durable tombstone drives retryable file, answer, snapshot/outbox, transcript, suggestion, review and audit cleanup across the modules. Minimal tombstone metadata remains to prevent resurrection and repeated use of a deleted assignment. Seeded assignment metadata/Identity accounts remain separate. Cleanup is retried after process/storage failure. Unreferenced recording files are swept after one hour.

Backups are outside the live cleanup loop. Apply the same thirty-day recording policy to backup copies deliberately; delete expired private archives and any exported recordings. Retaining an old backup can retain deleted evidence. No automated command destroys your backup copies.

## Safe update and rollback

Take a backup, record the current Git commit/image tag, stop the app, review migrations, then rebuild and run the coordinated migration command before starting the new release. Normal `make up` performs that order. Do not run destructive volume removal to fix startup. `make down` is the normal shutdown.

A previous binary can be restarted only if its schema contract is compatible. Migration `Down` methods are not an operational data rollback. Restore a verified private backup into a fresh empty target when an incompatible database change must be reversed. Keep the prior environment until the restored receipt/media and review flow pass.

The optional [Docker-local Terraform](../infra/local/README.md) invokes this same lifecycle. The future [AWS Singapore profile](../infra/aws/README.md) is separate and unapplied.
