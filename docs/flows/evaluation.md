# Durable evaluation jobs

Evaluation creates jobs idempotently from processing-intent IDs. A bounded background worker claims a persisted lease and increments its fence. The lease permits recovery after interruption; the fence prevents an older worker from overwriting a job reclaimed by another worker.

For an AI-assisted spoken response, the worker transcribes the selected audio and persists the transcript before asking Ollama for rubric scores. Separating these stages avoids repeating transcription after an interrupted scoring request. Manual-only attempts skip AI processing; written answers do not require transcription.

Provider output must contain exactly the frozen rubric criteria, integer scores from zero through five and evidence grounded in the answer or transcript. Provider, model, prompt and rubric versions are retained. Application code aggregates validated criterion scores, and AI suggestions remain separate from human review.

Retries use durable schedules, provider timeouts and capped backoff. A terminal processing failure requests human attention and never assigns an automatic zero. The reviewer can retry eligible failed processing or apply the rubric directly to the accepted evidence.

## Source and recovery evidence

[BackgroundProcessing.cs](../../src/Evaluation/BackgroundProcessing.cs) owns durable intake, claims, fencing and retries. [Providers.cs](../../src/Evaluation/Providers.cs) owns adapter calls and output validation. [The transcription runtime](../../infra/transcription/server.py) runs FFmpeg and whisper.cpp.

The reviewer sees per-question status, transcripts, suggestions and provider errors. Candidate reads exclude private evaluation details before publication. [Recovery checks](../../tests/Integration/recovery.py) interrupt a synthetic provider request, verify reclamation with a higher fence and continue through real local transcription/scoring. [Recovery diagram](../diagrams/recovery.svg) · [Editable Mermaid](../diagrams/recovery.mmd)

[Human review](review.md) · [System overview](../how-it-works.md)
