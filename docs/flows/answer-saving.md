# Answer saving and recovery

The browser persists drafts immediately in IndexedDB and synchronizes after a one-second debounce. Local persistence and server acceptance are separate facts: the interface distinguishes local-saved, syncing, server-saved and failed states. Refresh can recover local work, but only server-accepted responses enter a submission.

Each save includes the answer's current revision. The server checks ownership, deadline and revision while locking the attempt. An accepted save increments the revision. A stale tab receives a conflict containing the current server answer; the browser preserves its local draft for an explicit decision instead of silently overwriting newer work.

This combines refresh and temporary-disconnection recovery with optimistic concurrency. Reconnecting cannot extend the deadline. After expiry, late local work remains visibly unsent and can be downloaded rather than presented as accepted evidence.

## Request and source

`PUT /api/attempts/{id}/answers/{questionId}` carries `revision`; the response returns the accepted revision. [The API integration checks](../../tests/Integration/journey.py) send competing writes at the same revision and require exactly one acceptance.

[Draft storage](../../frontend/src/drafts.ts) and [useAttempt.ts](../../frontend/src/useAttempt.ts) own browser recovery and synchronization. [AssessmentModule.cs](../../src/Assessments/AssessmentModule.cs) owns answer-save rules; [Persistence.cs](../../src/Assessments/Persistence.cs) owns persistence mappings.

[Recording storage](recordings.md) · [System overview](../how-it-works.md)
