import { test, expect } from "@playwright/test";
import path from "node:path";

test("application lifecycle explorer navigates stages and source flows on narrow screens", async ({
  page,
  request,
}) => {
  const errors: string[] = [];
  const requests: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => requests.push(request.url()));
  await page.goto("http://127.0.0.1:5193/follow-an-assessment.html");
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(
    "Application lifecycle.",
  );
  await expect(page.getByRole("checkbox")).toHaveCount(0);
  const previous = page.getByRole("button", { name: "Previous stage" });
  const next = page.getByRole("button", { name: "Next stage" });
  await expect(previous).toBeDisabled();
  const stages = [
    ["01 · Start", "Attempt initialization", "attempts.md", "browser"],
    ["02 · Save", "Answer acceptance and recovery", "answer-saving.md", "browser"],
    ["03 · Record", "Recording verification", "recordings.md", "browser"],
    ["04 · Submit", "Submission and durable dispatch", "submission.md", "background"],
    ["05 · Evaluate", "Evaluation and provider recovery", "evaluation.md", "background"],
    ["06 · Review", "Human review and publication", "review.md", "browser"],
  ];
  for (const [index, [label, title, flow, component]] of stages.entries()) {
    const stage = page
      .getByRole("navigation", { name: "Application lifecycle" })
      .getByRole("button", { name: label, exact: true });
    await stage.click();
    await expect(stage).toHaveAttribute("aria-current", "step");
    await expect(page.locator("#title")).toHaveText(title);
    await expect(page.locator("#counter")).toHaveText(`Stage ${index + 1} / 6`);
    const source = page.getByRole("link", {
      name: "Open flow details and source links",
    });
    await expect(source).toHaveAttribute("href", `flows/${flow}`);
    expect((await request.get(await source.evaluate((link) => (link as HTMLAnchorElement).href))).status()).toBe(200);
    await expect(page.locator(`#${component}`)).toHaveClass(/active/);
  }
  await expect(next).toBeDisabled();
  await previous.click();
  await expect(page.locator("#title")).toHaveText("Evaluation and provider recovery");
  await next.focus();
  await page.keyboard.press("Enter");
  await expect(page.locator("#title")).toHaveText("Human review and publication");
  await page.reload();
  await expect(page.locator("#counter")).toHaveText("Stage 1 / 6");
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole("button", { name: "04 · Submit", exact: true }).click();
  await expect(page.locator("#title")).toHaveText("Submission and durable dispatch");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await page.screenshot({
    path: path.resolve(import.meta.dirname, "../../.local/lifecycle-mobile.png"),
    fullPage: true,
  });
  expect(requests.every((url) => new URL(url).origin === "http://127.0.0.1:5193")).toBeTruthy();
  expect(errors).toEqual([]);
});
