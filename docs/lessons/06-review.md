# 6. Reviewer → final score → published feedback

**Do:** Review one question with answer/recording, transcript, rubric and AI rationale together. Confirm or override each criterion, write feedback, save, then repeat across all twelve questions. Publish only when the review is complete.

**What happened:** Evaluation saves human criterion scores with a review revision and an audit entry. Internal notes remain reviewer-only. Publication checks that every criterion of every frozen question has a valid score, aggregates in application code and freezes the published review. The candidate can then read published scores and feedback.

**Why:** Model output is a suggestion. The human reviewer owns final judgment and must be able to justify an override. An audit records decisions without making internal notes public. Revision checks prevent stale reviewer tabs from erasing newer decisions.

**Where:** [EvaluationModule.cs](../../src/Evaluation/EvaluationModule.cs) implements `SaveReviewAsync`, `PublishAsync` and candidate-filtered reads. [Persistence.cs](../../src/Evaluation/Persistence.cs) owns reviews, publications and audit. These are ordinary vertical use cases inside one module.

**Observe:** Unpublished candidate responses contain no transcript, AI rationale, human review or internal notes. After publication, candidate responses contain final criteria/feedback. `make integration` verifies a synthetic all-threes score totals 72/120 and that private notes never appear. This arithmetic is verification, not proof of hiring validity.

**Checkpoint:** Explain which person owns a published score, what is frozen, and what the candidate can see. Save your checkpoint, then start [the four failure drills](../failure-drills.md).

[Learning home](../learning.md)
