# Follow one assessment

Work through one synthetic attempt, one trace and one checkpoint at a time. Start with the successful journey; introduce the four failure drills only after publishing a review. Application verification evidence is separate from your learning progress.

```sh
make up
make sample
```

Keep the printed attempt ID and receipt. Open the reviewer queue using the private local reviewer credentials. [Interactive illustrated walkthrough](follow-an-assessment.html) · [Module boundaries](architecture.md) · [Operations](operations.md)

1. [Login → assignment → start](lessons/01-start.md)
2. [Written answer → server save → recovery](lessons/02-save.md)
3. [Recording → private storage → verification](lessons/03-recording.md)
4. [Submission → transaction → outbox → Kafka](lessons/04-submit.md)
5. [Job → transcript → AI suggestion](lessons/05-evaluate.md)
6. [Reviewer → final scores → feedback](lessons/06-review.md)

Afterward: [four failure drills](failure-drills.md). Optional depth lives in [architecture](architecture.md), [operations](operations.md), and the module source; it is not a prerequisite quiz before the first lesson.

## Learner checkpoints

Record understanding in the private Notion home or the walkthrough's browser-local checklist. Use the same attempt ID throughout. A checked lesson means you can explain what happened, why the pattern exists, where the code runs and how to observe it. It does not mean an implementation acceptance test passed.

| Checkpoint | What you can explain |
|---|---|
| 1 | Why preparation does not start the timer |
| 2 | Why a server revision rejects a stale tab |
| 3 | Why uploaded bytes are validated and privately served |
| 4 | Why a committed receipt survives Kafka downtime |
| 5 | Why transcript persistence and fenced completion matter |
| 6 | Why an AI suggestion is separate from a published human score |
