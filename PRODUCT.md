# Candidate Assessment

<!-- impeccable:product-schema 1 -->

## Platform
web

## Stack
React and TypeScript; .NET 10 modular monolith; PostgreSQL; Kafka; local private recording storage; host Ollama and local transcription. Confirmed by the implementation plan.

## Users
Candidates completing a timed workplace assessment and reviewers evaluating submitted evidence. The repository also teaches a developer one execution trace at a time.

## Product Purpose
Complete one seeded assessment from private login to published human feedback, preserving accepted work through failures.

## Operating Context
Eight written questions, four oral questions, twenty minutes. One extended-duration assignment. Preparation, written practice and microphone testing do not start the timer. Fictional workplace scenarios and anchored rubrics.

## Capabilities and Constraints
Three modules: Access, Assessments, Evaluation. One backend host and database. Strict server deadline; optimistic concurrency; local IndexedDB drafts; immutable recordings and submission; transactional outbox to Kafka; durable idempotent jobs with fenced leases; transcript persisted before AI scoring. Manual-only review is available. AI suggestions never become final scores without a reviewer. Private credentials, no public registration. Recording retention: thirty days. No paid cloud deployment in this delivery.

## Brand Commitments
Title: Candidate Assessment. Clear, direct, calm language. Real statuses, no invented proof or commercial claims.

## Evidence on Hand
The user-approved implementation plan. All seeded people and workplace scenarios are synthetic. No hiring-score validity or production-scale evidence exists.

## Product Principles
- The server decides acceptance and expiry.
- Failure preserves evidence and leads to recoverable review.
- Human decisions and AI suggestions remain distinct.
- Teach through one attempt and one checkpoint at a time.

## Accessibility & Inclusion
Keyboard access, visible focus, accessible contrast, responsive layout, reduced motion, extended duration and manual-only review.
