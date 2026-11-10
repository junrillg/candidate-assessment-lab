# One application, three business modules

Candidate Assessment is one .NET host with HTTP endpoints and `BackgroundService` loops. React is built into the host. PostgreSQL, Kafka, recording storage, transcription and Ollama support that application; they are not business microservices.

## Start with the shape

![Local system map: browser, one backend and supporting infrastructure](diagrams/overview.svg)

[Open the readable offline map](diagrams/overview.html) · [Edit the Mermaid model](diagrams/overview.mmd)

Read left to right: people use React, React calls one .NET application, and that application owns access to the stores. Background work uses the same host. The two AI runtimes and Kafka support it. They do not split the business application into microservices.

## Then follow one attempt

![Seven handoffs from starting an attempt to published human feedback](diagrams/journey.svg)

[Open the readable offline journey](diagrams/journey.html) · [Edit the Mermaid model](diagrams/journey.mmd) · [Lifecycle and design notes](how-it-works.md)

The blue submission stage is the transaction boundary: frozen evidence and outbox intent commit together before the durable receipt returns. Kafka carries processing identifiers; Evaluation reads the actual evidence through Assessments' public contract. For oral answers, the transcript is persisted before requesting a suggestion. The reviewer supplies the final scores and publishes feedback.

Written answers skip transcription. Manual-only attempts and terminal provider failures go to human review without an automatic zero. Only published feedback reaches the candidate.

## Look inside the backend when you need more detail

![Module boundaries](diagrams/modules.svg)

[Edit module Mermaid](diagrams/modules.mmd) · [Detailed runtime diagram](diagrams/runtime.svg) · [Runtime source](diagrams/runtime.mmd)

Each module has its own assembly, internal EF context, schema and migration history. Access owns Identity and sessions. Assessments owns assignments, attempts, answers, recording references, frozen submission evidence and publication intent. Evaluation owns durable jobs, transcripts, AI suggestions, human scores and audit. Evaluation reads immutable assessment evidence through `IAssessmentReader`; Assessments has no Evaluation dependency. The host composes modules.

EF Core is used directly in use cases. There is no mediator framework, generic repository, event sourcing, saga, separate read database or cross-module transaction. [Architecture tests](../tests/Integration/test_architecture.py) enforce project-reference direction, internal contexts and SQL schema ownership.

## Reliability in one trace

![Successful journey](diagrams/happy-path.svg)

[Edit Mermaid](diagrams/happy-path.mmd)

Start freezes question/rubric versions and duration. A draft save requires the server revision. Submission locks the attempt and commits immutable per-question evidence and an outbox row in one transaction. The dispatcher can publish more than once; Kafka consumer deduplication uses the processing-intent ID and commits offsets only after durable handling. Messages contain IDs and correlation metadata, never answer bodies or audio.

Evaluation claims bounded work through persisted leases and increments a fence. Completion checks the same fence and unexpired lease. Transcript is committed before scoring, so an interrupted scoring request can reuse transcription. Provider calls have timeouts and bounded retries; terminal failure means needs review, never zero. Application code validates rubric criteria/evidence and aggregates human criterion scores. Only a reviewer can publish.

![Recovery](diagrams/recovery.svg)

[Edit Mermaid](diagrams/recovery.mmd)

## Explicit limits

The local deployment uses a single Kafka broker and PostgreSQL instance. It establishes neither production scale nor hiring-score validity. [Docker-local Terraform](../infra/local/README.md) remains the default deployment path.

The preferred future cloud path is [Azure Southeast Asia (Singapore)](../infra/azure/README.md): one Ubuntu VM, the same Compose application and host Ollama, retained managed storage, and an SSH tunnel restricted to one operator address. The [AWS Singapore profile](../infra/aws/README.md) remains an alternative, using ARM64 EC2 and Session Manager. These profiles preserve the modular monolith; neither replaces PostgreSQL, Kafka or the AI adapters with a managed service. Cloud runtime reliability remains unverified because neither profile has been applied.

![Azure deployment profile](diagrams/azure.svg)

[Edit Azure Mermaid](diagrams/azure.mmd) · [Azure deployment instructions](../infra/azure/README.md) · [All deployment profiles](../infra/cloud/README.md)

Extract Evaluation into a separate host only after measuring independent scaling, ownership or release needs. Its module boundary makes that a future decision; no microservice implementation is included.

Primary background reading: [Microsoft service boundary tradeoffs](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/microservice-application-design), [Fowler: Monolith First](https://martinfowler.com/bliki/MonolithFirst.html), [AWS transactional outbox](https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/transactional-outbox.html), [ASP.NET Core hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services), [whisper.cpp](https://github.com/ggml-org/whisper.cpp).
