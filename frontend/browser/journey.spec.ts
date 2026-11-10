import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { readFileSync, mkdirSync } from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "../..");
const credentials = JSON.parse(
  readFileSync(path.join(root, ".local/credentials.json"), "utf8"),
).users;
const shots = path.join(root, ".impeccable/review");
let assignment: { assignmentId: string; title: string };
async function login(page: Page, user: (typeof credentials)[number]) {
  await page.goto("/");
  await page.getByLabel("Email address").fill(user.email);
  await page.getByLabel("Password", { exact: true }).fill(user.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
}
async function screenshot(page: Page, name: string) {
  await page.evaluate(() => {
    (document.activeElement as HTMLElement | null)?.blur();
    window.scrollTo(0, 0);
  });
  await page.waitForFunction(() => window.scrollY === 0);
  await page.screenshot({ path: path.join(shots, name), fullPage: true });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
}
test.beforeAll(() => {
  mkdirSync(shots, { recursive: true });
  assignment = JSON.parse(
    execFileSync(
      "python3",
      ["tests/Integration/journey.py", "browser-fixture"],
      { cwd: root, encoding: "utf8" },
    ),
  );
});
test("candidate recording, draft recovery/conflict, offline messaging, review and published feedback", async ({
  page,
  context,
  browser,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/");
  await screenshot(page, "login-desktop.png");
  await page.keyboard.press("Tab");
  await expect(
    page.getByRole("link", { name: "Skip to main content" }),
  ).toBeFocused();
  await login(page, credentials[0]);
  await expect(
    page.getByRole("heading", { name: "Before you begin" }),
  ).toBeVisible();
  const select = page.getByLabel("Assignment", { exact: true });
  if (await select.count()) await select.selectOption(assignment.assignmentId);
  await page
    .getByLabel("A teammate is blocked")
    .fill("I would clarify the missing detail together.");
  await expect(page.getByText("Timer not started")).toBeVisible();
  await screenshot(page, "preparation-desktop.png");
  await page.getByLabel("Request manual-only review").check();
  await page.getByLabel("I understand recording").check();
  await page
    .getByRole("button", { name: "Start 20-minute assessment" })
    .click();
  await expect(page.getByRole("timer")).toBeVisible();
  const attemptId = new URL(page.url()).searchParams.get("attempt")!;
  const response = page.getByLabel("Your response", { exact: true });
  await response.fill(
    "I would clarify the problem, agree the next step and verify the outcome.",
  );
  await expect(page.getByText("Server saved", { exact: true })).toBeVisible();
  await page.reload();
  await expect(response).toHaveValue(
    "I would clarify the problem, agree the next step and verify the outcome.",
  );
  await screenshot(page, "desktop.png");
  await page.getByLabel('Flag for review',{exact:true}).check();
  await expect(page.getByText('Server saved',{exact:true})).toBeVisible();
  await page.evaluate(()=>window.scrollTo(0,document.body.scrollHeight));
  await expect(page.locator('.toolbar-save')).toBeInViewport();
  await expect(page.locator('.toolbar-save')).toHaveText('All changes server saved');
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator('.mobile-question-state').filter({hasText:'Answered · flagged'})).toBeVisible();
  await screenshot(page, "mobile.png");
  await page.setViewportSize({ width: 1440, height: 1000 });
  await context.setOffline(true);
  await response.fill("Offline work must remain local until acknowledged.");
  await expect(
    page.getByText("You’re offline.", { exact: false }).first(),
  ).toBeVisible();
  await expect(page.getByText("Save failed · retry needed")).toBeVisible();
  await screenshot(page, "offline-desktop.png");
  await context.setOffline(false);
  await expect(page.getByText("Server saved", { exact: true })).toBeVisible();
  await expect(
    page.getByText("You are offline.", { exact: false }),
  ).toHaveCount(0);
  // Two independent documents keep their recovery records while server revisions reject a stale write.
  const stale = await context.newPage();
  await stale.goto(`/?attempt=${attemptId}`);
  await expect(stale.getByLabel("Your response", { exact: true })).toHaveValue(
    "Offline work must remain local until acknowledged.",
  );
  await response.fill("The first tab accepts a newer server version.");
  await expect(page.getByText("Server saved", { exact: true })).toBeVisible();
  await stale
    .getByLabel("Your response", { exact: true })
    .fill("A separate stale-tab draft stays recoverable.");
  await expect(
    stale.getByRole("heading", { name: "Choose which response to keep" }),
  ).toBeVisible();
  await stale.reload();
  await expect(stale.getByLabel("Your response", { exact: true })).toHaveValue(
    "A separate stale-tab draft stays recoverable.",
  );
  await expect(
    stale.getByRole("button", { name: "Keep my local response" }),
  ).toBeVisible();
  await screenshot(stale, "conflict-desktop.png");
  await stale.getByRole("button", { name: "Use server response" }).click();
  await expect(stale.getByLabel("Your response", { exact: true })).toHaveValue(
    "The first tab accepts a newer server version.",
  );
  await stale.close();
  const oral = page
    .getByRole("navigation", { name: "Assessment questions" })
    .getByRole("button", { name: /Question 9:/ });
  await oral.click();
  await page.evaluate(async (id) => {
    const session = await fetch("/api/session").then((r) => r.json());
    const attempt = await fetch(`/api/attempts/${id}`).then((r) => r.json());
    const question = attempt.questions.find(
      (q: { number: number }) => q.number === 9,
    );
    const response = await fetch(`/api/attempts/${id}/answers/${question.id}`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": session.csrfToken,
      },
      body: JSON.stringify({
        revision: 0,
        text: "A concurrent tab saved this version.",
        flagged: false,
      }),
    });
    if (!response.ok)
      throw new Error("Concurrent recording-selection fixture failed");
  }, attemptId);
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(
    page.getByText("Recording in progress.", { exact: false }),
  ).toBeVisible();
  await page.waitForTimeout(1700);
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Choose which response to keep" }),
  ).toBeVisible();
  await expect(
    page.getByText("Completed on this device.", { exact: false }),
  ).toBeVisible();
  await page.reload();
  await page
    .getByRole("navigation", { name: "Assessment questions" })
    .getByRole("button", { name: /Question 9:/ })
    .click();
  await expect(
    page.getByLabel("Your selected recording playback"),
  ).toBeVisible();
  await page.getByRole("button", { name: "Keep my local response" }).click();
  await expect(
    page.getByText("Verified recording selected on the server."),
  ).toBeVisible();
  const audio = page.getByLabel("Your selected recording playback");
  await expect(audio).toBeVisible();
  await audio.evaluate((element: HTMLAudioElement) => element.play());
  await expect
    .poll(() =>
      audio.evaluate((element: HTMLAudioElement) => element.currentTime),
    )
    .toBeGreaterThan(0);
  await screenshot(page, "recording-desktop.png");
  await page
    .getByRole("button", { name: "Review answers", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Review your responses" }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Submit assessment", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Assessment received" }),
  ).toBeVisible();
  await screenshot(page, "receipt-desktop.png");
  const reviewerContext = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  const reviewer = await reviewerContext.newPage();
  await login(reviewer, credentials[2]);
  await expect(
    reviewer.getByRole("heading", { name: "Review workspace" }),
  ).toBeVisible();
  await reviewer
    .getByRole("button", { name: new RegExp(assignment.title) })
    .click();
  for (let number = 1; number <= 12; number++) {
    await reviewer
      .getByRole("navigation", { name: "Review questions" })
      .getByRole("button", { name: new RegExp(`Question ${number}:`) })
      .click();
    for (const field of await reviewer.getByRole("combobox").all())
      await field.selectOption("3");
    await reviewer
      .getByLabel("Feedback for the candidate", { exact: true })
      .fill("Clear next steps; make the evidence more specific.");
    await reviewer
      .getByLabel("Internal notes", { exact: false })
      .fill("Private synthetic reviewer note.");
    await reviewer
      .getByRole("button", { name: "Save question review", exact: true })
      .click();
    await expect(
      reviewer.getByText("Review saved.", { exact: true }),
    ).toBeVisible();
  }
  await screenshot(reviewer, "reviewer-desktop.png");
  await expect(reviewer.getByText('No recording was submitted for this question.',{exact:false})).toBeVisible();
  await reviewer.setViewportSize({ width: 390, height: 844 });
  await screenshot(reviewer, "reviewer-mobile.png");
  await reviewer.setViewportSize({ width: 1440, height: 1000 });
  await reviewer
    .getByLabel("Overall feedback for the candidate")
    .fill(
      "You explained a workable approach. Connect each decision to concrete evidence.",
    );
  await reviewer
    .getByRole("button", { name: "Publish feedback", exact: true })
    .click();
  await expect(
    reviewer.getByRole("button", { name: "Review published", exact: true }),
  ).toBeVisible();
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Your published feedback" }),
  ).toBeVisible();
  await expect(page.getByText("Private synthetic reviewer note.")).toHaveCount(
    0,
  );
  await screenshot(page, "published-desktop.png");
  expect(errors).toEqual([]);
  // Remove only this browser test's disposable fixture after capturing its evidence.
  await page.evaluate(async (id) => {
    const session = await fetch("/api/session").then((r) => r.json());
    const response = await fetch(`/api/attempts/${id}`, {
      method: "DELETE",
      headers: { "X-CSRF-TOKEN": session.csrfToken },
    });
    if (response.status !== 202)
      throw new Error("Synthetic attempt cleanup failed");
  }, attemptId);
  await reviewerContext.close();
});

test("blocked device storage still permits an acknowledged online server save", async ({
  page,
  context,
}) => {
  const fixture = JSON.parse(
    execFileSync(
      "python3",
      ["tests/Integration/journey.py", "browser-fixture"],
      { cwd: root, encoding: "utf8" },
    ),
  );
  await context.addInitScript(() => {
    Object.defineProperty(window, "indexedDB", {
      value: {
        open() {
          throw new Error("Device storage is blocked.");
        },
      },
    });
  });
  await login(page, credentials[0]);
  await page
    .getByLabel("Assignment", { exact: true })
    .selectOption(fixture.assignmentId);
  await page.getByLabel("I understand recording").check();
  await page.getByLabel("Request manual-only review").check();
  await page
    .getByRole("button", { name: "Start 20-minute assessment" })
    .click();
  await page
    .getByLabel("Your response", { exact: true })
    .fill("An online save remains possible when local backup is blocked.");
  await expect(page.getByText("Server saved", { exact: true })).toBeVisible();
  await expect(
    page.getByText("Local backup is not guaranteed.", { exact: false }),
  ).toBeVisible();
  const saved = await page.evaluate(async () => {
    const id = new URL(location.href).searchParams.get("attempt");
    const attempt = await fetch(`/api/attempts/${id}`).then((r) => r.json());
    const session = await fetch("/api/session").then((r) => r.json());
    await fetch(`/api/attempts/${id}`, {
      method: "DELETE",
      headers: { "X-CSRF-TOKEN": session.csrfToken },
    });
    return attempt.answers.find((a: { text: string }) =>
      a.text.startsWith("An online save"),
    );
  });
  expect(saved.revision).toBe(1);
});
