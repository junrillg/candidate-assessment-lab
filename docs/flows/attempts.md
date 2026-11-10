# Identity and assessment attempts

Access verifies credentials with ASP.NET Core Identity and issues an HTTP-only session cookie. Candidate/reviewer roles restrict operations; candidate ownership checks restrict assignments and attempts. Sign-in and preparation do not start an assessment deadline.

Starting an assignment freezes its question and rubric versions, assigned duration, server start time and deadline. Later question changes cannot alter the attempt's rules. The seeded ordinary assignment has twenty minutes; the extended assignment has thirty.

The server clock remains authoritative throughout the attempt. At expiry, background maintenance finalizes server-accepted answers and verified selected recordings even when the browser is closed. A local draft created before expiry but synchronized afterward is not accepted as an on-time answer.

## Request and persistence boundary

The browser requests `/api/session` and `/api/assignments`, then starts the selected assignment through `/api/assignments/{id}/start`. `/api/attempts/{id}` returns `startedAt`, `deadline`, `serverTime` and the frozen questions. Sign-in and starting an attempt are handled in-process; neither needs a Kafka message.

[AccessModule.cs](../../src/Access/AccessModule.cs) owns session/login endpoints and role policies. [AssessmentModule.cs](../../src/Assessments/AssessmentModule.cs) owns attempt start and state rules. [Seed.cs](../../src/Assessments/Seed.cs) supplies the fictional questions and assignments. All run inside the same .NET host.

[Answer saving](answer-saving.md) · [System overview](../how-it-works.md)
