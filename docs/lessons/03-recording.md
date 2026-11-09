# 3. Recording → private storage → verified upload

**Do:** Record a short fictional oral answer, stop it and play it back. Check upload state before selecting it as your answer. A completed recording is persisted locally before upload; an unfinished recording can be lost if the browser is interrupted.

**What happened:** The browser creates a completed audio blob and a stable upload ID. The authenticated API accepts a whole immutable file, checks ownership, size, declared type, actual audio format and decoded duration, then records the stored-byte hash. Repeating the same upload ID is safe only for the same content/question/type. Answer selection references a verified immutable recording version.

**Why:** A filename or browser Content-Type cannot prove a usable two-minute audio recording. An upload retry must not create multiple ambiguous versions. Private storage should never become a public bucket URL.

**Where:** [RecordingStorage.cs](../../src/Assessments/RecordingStorage.cs) validates and stores bytes. [AssessmentModule.cs](../../src/Assessments/AssessmentModule.cs) checks the attempt and maps upload/playback. [Compose](../../compose.yaml) gives the host a private recording volume. FFprobe inspects streams and FFmpeg measures actual decoded audio.

**Observe:** The upload response returns a recording ID and size. Authenticated playback returns the stored bytes; a different candidate receives no access. The integration journey verifies byte-identical playback and repeated stable upload IDs. Recordings are removed after thirty days from upload under [the retention policy](../operations.md).

**Checkpoint:** Explain the difference between recording completion, upload verification and selection for submission.

[Next: submit and outbox](04-submit.md) · [Learning home](../learning.md)
