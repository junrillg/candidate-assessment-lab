# Recording storage and access

A completed browser recording is persisted locally before upload. An unfinished recording can be lost if the browser is interrupted. Completion, upload verification and selection for submission are distinct states.

Uploads use a stable ID and transfer a whole immutable file. The authenticated API checks ownership, size, declared type, actual audio format and decoded duration, then records the stored-byte hash. Repeating an upload ID is safe only for the same content, question and type. Answer selection references a verified recording version; submission freezes that selection.

File extensions and browser Content-Type declarations cannot prove that audio is playable or within the two-minute limit. FFprobe inspects streams and FFmpeg measures decoded audio. Stored recordings are private: playback passes through authenticated, ownership-checked endpoints rather than a public storage URL.

## Storage and verification

[RecordingStorage.cs](../../src/Assessments/RecordingStorage.cs) validates and stores bytes. [AssessmentModule.cs](../../src/Assessments/AssessmentModule.cs) checks attempt access and exposes upload/playback. [Compose](../../compose.yaml) mounts a dedicated private recording volume.

The upload response includes a recording ID and size. [Integration checks](../../tests/Integration/journey.py) cover byte-identical playback, stable upload IDs and denial to a different candidate. The [retention policy](../operations.md) removes live server recordings thirty days after upload; backup and browser copies require their documented cleanup.

[Submission](submission.md) · [System overview](../how-it-works.md)
