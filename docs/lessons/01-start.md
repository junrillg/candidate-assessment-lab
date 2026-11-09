# 1. Login → assignment → start

**Do:** Log in as a candidate with private credentials. Open an assignment, try the written practice and microphone playback, then start when ready. Watch the deadline appear only after start. `make sample` creates a separate fictional trace if you want to inspect an already submitted example.

**What happened:** Identity verifies the password and issues an HTTP-only session cookie. The assignment belongs to that candidate. Start freezes question/rubric versions and assigned duration, and records the server start/deadline. The extended assignment has thirty minutes; the ordinary assignment has twenty.

**Why:** Authentication answers who you are. Authorization answers which assignment you may open. Freezing the assessment prevents later rubric edits from changing the rules halfway through. Preparation should not consume assessment time.

**Where:** [AccessModule.cs](../../src/Access/AccessModule.cs) maps session/login and role policies. [AssessmentModule.cs](../../src/Assessments/AssessmentModule.cs) handles `StartAttempt`; [Seed.cs](../../src/Assessments/Seed.cs) contains fictional questions and assignments. This is all inside one .NET host.

**Observe:** Browser network requests show `/api/session`, `/api/assignments`, then `/api/assignments/{id}/start`. No Kafka message is needed to log in or start. `/api/attempts/{id}` returns `startedAt`, `deadline`, `serverTime` and frozen questions.

**Checkpoint:** Explain why opening the preparation page is different from starting the attempt. Save the attempt ID in your private learner checkpoint, without a password or answer body.

[Next: save and recover](02-save.md) · [Learning home](../learning.md)
