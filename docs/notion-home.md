# Candidate Assessment — Modular Monolith, Kafka and AI

Private learning home. Work through one synthetic attempt, one execution trace and one checkpoint at a time. Implementation acceptance evidence and personal learning progress are separate.

Repository: https://github.com/junrillg/candidate-assessment-lab

## Start here

Run `make up`, open http://localhost:5188, then run `make sample` for a fictional end-to-end trace. Read private local credentials from `.local/credentials.json`; never paste them into this page. Keep the attempt ID/receipt for all six lessons.

[Learning guide](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/learning.md) · [Interactive walkthrough](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/follow-an-assessment.html) (download/open the HTML locally) · [Architecture and diagrams](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/architecture.md) · [Operations](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/operations.md)

## Milestone tasks

These are learner tasks. Leave unchecked until you can explain the behavior from the same trace.

- [ ] 1. Login → assignment → start. Explain why preparation does not consume time.
- [ ] 2. Written answer → server save → refresh. Explain local vs accepted work and stale revisions.
- [ ] 3. Recording → private storage → verified upload. Explain immutable selection and authenticated playback.
- [ ] 4. Submission → transaction → outbox → Kafka. Explain a receipt during broker downtime.
- [ ] 5. Job → transcript → suggestion. Explain durable retries, leases and fenced completion.
- [ ] 6. Human reviewer → final criteria → publication. Explain what the candidate can see.

## Six lessons

1. [Login, assignment and start](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/lessons/01-start.md)
2. [Save and refresh recovery](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/lessons/02-save.md)
3. [Private verified recordings](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/lessons/03-recording.md)
4. [Submission and outbox](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/lessons/04-submit.md)
5. [Durable evaluation and AI](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/lessons/05-evaluate.md)
6. [Human review and published feedback](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/lessons/06-review.md)

Each lesson explains what happened, why the pattern exists, where the code runs and how to observe it. Optional architectural detail belongs in the linked references, not before the first successful attempt.

## Saved learner checkpoint

Current lesson: not started.

Synthetic attempt ID: not recorded.

Receipt: not recorded.

What I observed: not recorded.

What I can now explain: not recorded.

Next checkpoint: lesson 1.

## After the successful journey

- [ ] Offline save and strict deadline
- [ ] Unavailable broker and repeated delivery
- [ ] Interrupted worker and stale completion
- [ ] Failed provider and reviewer retry

[Failure drill instructions](https://github.com/junrillg/candidate-assessment-lab/blob/main/docs/failure-drills.md).

## Verification evidence

The source repository contains the implementation and CI checks. Local commands write `.local/integration-evidence.json` and `.local/recovery-evidence.json`; those files are implementation evidence, not completed learner lessons. Runtime credentials, answers, audio and internal notes do not belong in Notion. AWS Singapore Terraform is an optional future deployment path; it has not been applied in this delivery.
