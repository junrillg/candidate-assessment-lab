# Four failure drills

Finish one successful published review first. Use fictional work and a separate synthetic assignment. These drills affect only the dedicated `candidate-assessment-lab` Compose project. Keep accepted work and learner checkpoints separate from automated verification evidence.

## 1. Offline save

In a candidate written answer, disconnect the browser with developer tools. Type a distinctive fictional sentence. The UI must say local-saved/failed or offline, rather than server-saved. Refresh and inspect the local draft. Reconnect before the server deadline and wait for acknowledgment. For the strict-cutoff case, reconnect after expiry and keep the late work visibly unsent.

Explain: IndexedDB recovery protects browser-local work; only the server can accept an answer before the deadline. An uncompleted recording can still be lost on interruption. Do not use this drill on a real candidate's work.

## 2. Unavailable broker

```sh
docker compose --env-file .local/lab.env stop kafka
# Submit the synthetic attempt in the UI and keep its receipt.
docker compose --env-file .local/lab.env start kafka
```

The submission should stay accepted while processing waits. The durable outbox retries delivery after restart. `make integration` automates this case and duplicate delivery. Explain why receipt and processing completion mean different things.

## 3. Interrupted worker

`make recovery` pauses the local transcriber during a synthetic claimed job, stops the app, expires only that test job's lease, then restarts the app and transcriber. It requires a higher fence on recovery and explicitly checks that an old-fence completion affects zero rows.

The lease expiry is advanced by isolated test setup to avoid waiting five minutes. The application has no public test endpoint or shortened production lease. Explain why cancelling an old process is insufficient without a durable fence.

## 4. Failed provider

`make recovery` first stops only this project's transcription container, submits an oral synthetic answer and waits through the persisted retry schedule. After four failures the question must need human review with no automatic score. The drill restarts transcription and exercises reviewer retry, real Whisper transcription and host Ollama scoring.

Host Ollama is never stopped by the drill because other projects may use it. Local providers must be installed and available for the successful recovery portion. A provider still failing after restart is visible evidence to investigate, not a reason to bypass review or invent a result.

When done, record one sentence per drill: the accepted fact, the failed dependency, the durable recovery state and the observation that proved recovery.
