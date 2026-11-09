# 5. Job → transcript → AI suggestion

**Do:** Open the synthetic attempt in the reviewer queue and inspect one oral question. Watch processing become a transcript and an AI suggestion, or a clear needs-review state if a provider fails.

**What happened:** A bounded background worker claims a persisted lease and increments its fence. It calls the local transcription adapter for selected audio, persists the transcript, then asks host Ollama to score frozen rubric criteria. Output must contain exactly the rubric criteria, integer scores from zero through five and evidence grounded in the answer/transcript. Provider/model/prompt/rubric versions are retained. Suggestions remain separate from human review.

**Why:** Persisting transcription avoids redoing expensive work after an interrupted score request. A fence and lease prevent an old worker from overwriting a reclaimed job. Durable retry schedules survive restart. Failed processing is a request for human attention, never an automatic zero.

**Where:** [BackgroundProcessing.cs](../../src/Evaluation/BackgroundProcessing.cs) owns durable intake, leases and retries. [Providers.cs](../../src/Evaluation/Providers.cs) owns the AI adapters/output validation. [Transcription server](../../infra/transcription/server.py) runs FFmpeg and whisper.cpp. [Recovery diagram](../diagrams/recovery.svg) · [Mermaid](../diagrams/recovery.mmd).

**Observe:** The reviewer sees per-question status, transcript, suggestion and provider errors; a candidate sees only appropriate processing status until publication. `make recovery` deliberately interrupts a synthetic provider request and demonstrates lease reclamation with a higher fence, followed by real local transcription/scoring.

**Checkpoint:** Explain why saving the transcript and saving the score are two steps, and what a stale worker is forbidden to do.

[Next: human review](06-review.md) · [Learning home](../learning.md)
