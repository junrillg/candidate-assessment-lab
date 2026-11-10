# Submission and the transactional outbox

A submission receipt confirms that accepted evidence has been committed. It does not mean evaluation or human review has finished. Repeating submission returns the same durable receipt, and submitted evidence cannot be edited.

One Assessments database transaction freezes per-question evidence and writes processing-intent outbox records. A background dispatcher later publishes metadata to Kafka. If publication is interrupted, it may repeat; Evaluation deduplicates by intent ID and saves a durable job before acknowledging delivery.

The outbox closes the gap between accepting a submission and sending work to a broker that may be unavailable. Snapshot and intent are committed together, so a broker outage delays processing without discarding the receipt. Consumer idempotency makes repeat delivery safe without a cross-module transaction.

## Delivery boundary

Kafka messages carry intent, attempt, question and correlation IDs, never text or audio. Evaluation reads frozen evidence through the Assessments public contract. Each module persists only its own data.

[AssessmentModule.cs](../../src/Assessments/AssessmentModule.cs) owns finalization. [AssessmentMaintenance.cs](../../src/Assessments/AssessmentMaintenance.cs) dispatches the outbox. [Evaluation background processing](../../src/Evaluation/BackgroundProcessing.cs) owns durable intake.

[The API integration checks](../../tests/Integration/journey.py) briefly stop this lab's broker, require the receipt and unpublished intents to survive, then check delivery and duplicates after restart. [Submission sequence](../diagrams/happy-path.svg) · [Editable Mermaid](../diagrams/happy-path.mmd)

[Evaluation jobs](evaluation.md) · [System overview](../how-it-works.md)
