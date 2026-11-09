# 2. Written answer → server save → refresh recovery

**Do:** Type a fictional answer. Read the save state as it changes from local to syncing to server-saved. Refresh after server acceptance. Later, try the offline drill rather than assuming browser-local storage means server acceptance.

**What happened:** The browser persists a draft in IndexedDB immediately and debounces server saves by one second. The server checks ownership, deadline and the supplied revision while locking the attempt. A successful save increments the answer revision. A stale tab gets a conflict response containing the current server answer; the UI preserves local work for an explicit choice.

**Why:** IndexedDB protects against a normal refresh and temporary disconnection. The revision prevents one tab silently overwriting another. The server deadline remains authoritative: late local work stays visibly unsent.

**Where:** [React source](../../frontend/src/) owns browser persistence and save states. [AssessmentModule.cs](../../src/Assessments/AssessmentModule.cs) owns `SaveAnswer`; [Persistence.cs](../../src/Assessments/Persistence.cs) configures answer concurrency. The browser and server own different facts.

**Observe:** A `PUT /api/attempts/{id}/answers/{questionId}` includes `revision`. Read the new revision in the response. [The integration journey](../../tests/Integration/journey.py) sends two writes at the same revision and requires exactly one acceptance.

**Checkpoint:** Explain the difference between local-saved and server-saved, and why reconnecting after the deadline cannot make late work accepted.

[Next: recordings](03-recording.md) · [Learning home](../learning.md)
