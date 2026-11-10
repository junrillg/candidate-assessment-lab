# Human review and publication

Evaluation stores reviewer criterion scores separately from AI suggestions. The reviewer assesses the submitted answer or recording against the frozen rubric, can confirm or override suggested scores, and records candidate feedback and optional private notes.

Saving a question review includes a revision and creates an audit entry. Revision checks prevent stale reviewer tabs from overwriting newer decisions. Internal notes remain reviewer-only.

Publication requires a valid human score for every criterion of every frozen question. Application code aggregates the scores and freezes the published review. The candidate can then read final criterion scores and feedback. Publication is immutable; it does not expose internal notes.

## Access and verification

[EvaluationModule.cs](../../src/Evaluation/EvaluationModule.cs) owns review saves, publication and candidate-filtered reads. [Persistence.cs](../../src/Evaluation/Persistence.cs) owns reviews, publications and audit. These operations remain inside the Evaluation module in the same backend host.

Unpublished candidate responses exclude transcripts, AI rationale, human review and internal notes. After publication, candidate responses include final scores and feedback. [Integration checks](../../tests/Integration/journey.py) verify that synthetic all-threes scoring totals 72/120 and private notes never appear. Correct arithmetic and access checks do not establish hiring-score validity.

[Operational failure scenarios](../failure-drills.md) · [System overview](../how-it-works.md)
