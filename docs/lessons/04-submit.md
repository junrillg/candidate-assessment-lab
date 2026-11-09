# 4. Submission → transaction → outbox → Kafka

**Do:** Review unanswered questions and upload states, then submit. Keep the durable receipt. Repeating submission returns that same receipt.

**What happened:** A single Assessments database transaction freezes accepted per-question evidence and inserts processing-intent outbox records. The receipt means the submission is committed. A background dispatcher later publishes metadata to Kafka; publication can repeat after interruption. Evaluation stores a durable job idempotently by intent ID before acknowledging delivery.

**Why:** Writing the snapshot and sending directly to Kafka would leave a gap if the broker failed between those actions. The outbox keeps the intent beside the accepted work. Consumer deduplication makes repeated delivery harmless. No cross-module database transaction is needed.

**Where:** [AssessmentModule.cs](../../src/Assessments/AssessmentModule.cs) owns `FinalizeAsync`; [AssessmentMaintenance.cs](../../src/Assessments/AssessmentMaintenance.cs) dispatches the outbox. [Evaluation BackgroundProcessing.cs](../../src/Evaluation/BackgroundProcessing.cs) handles durable intake. [Happy-path diagram](../diagrams/happy-path.svg) · [Editable Mermaid](../diagrams/happy-path.mmd).

**Observe:** `/submit` returns a receipt before all questions are scored. Kafka messages contain intent/attempt/question/correlation IDs, never text or audio. `make integration` stops this lab's broker briefly, requires the receipt and unpublished intents to survive, then checks delivery and duplicates after restart.

**Checkpoint:** Explain what the receipt guarantees and why it does not mean AI processing is complete.

[Next: durable evaluation](05-evaluate.md) · [Learning home](../learning.md)
