import test from "node:test";
import assert from "node:assert/strict";
import { mergeDraft, acceptedDraft, draftKey } from "./drafts";
import type { Draft } from "./drafts";
const server = {
  questionId: "question",
  revision: 4,
  text: "server accepted",
  flagged: false,
};
const local: Draft = {
  ...server,
  key: "key",
  revision: 3,
  text: "unsent",
  dirty: true,
  localUpdatedAt: 1,
};
test("reconnect retains the stale revision and local response for explicit conflict resolution", () => {
  assert.equal(mergeDraft("key", server, local).revision, 3);
  assert.equal(mergeDraft("key", server, local).text, "unsent");
  assert.equal(
    mergeDraft("key", server, { ...local, dirty: false }).text,
    "server accepted",
  );
});
test("an edit made while saving is still dirty and advances to accepted server revision", () => {
  const next = acceptedDraft(
    local,
    { ...local, text: "newer", localUpdatedAt: 2 },
    { ...server, revision: 5 },
  );
  assert.equal(next.dirty, true);
  assert.equal(next.text, "newer");
  assert.equal(next.revision, 5);
  assert.equal(acceptedDraft(local, local, server).dirty, false);
});
test("draft keys isolate people, attempts and questions", () => {
  assert.notEqual(draftKey("a", "x", "q"), draftKey("b", "x", "q"));
  assert.notEqual(draftKey("a", "x", "q"), draftKey("a", "y", "q"));
});
