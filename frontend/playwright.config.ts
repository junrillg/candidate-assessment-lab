import { defineConfig } from "@playwright/test";
export default defineConfig({
  webServer: {
    command: "python3 -m http.server 5193 --bind 127.0.0.1 --directory ../docs",
    url: "http://127.0.0.1:5193/follow-an-assessment.html",
    reuseExistingServer: !process.env.CI,
    timeout: 10000,
  },
  testDir: "./browser",
  workers: 1,
  fullyParallel: false,
  timeout: 180000,
  expect: { timeout: 15000 },
  use: {
    baseURL: process.env.LAB_URL ?? "http://127.0.0.1:5188",
    viewport: { width: 1440, height: 1000 },
    launchOptions: {
      args: [
        "--use-fake-device-for-media-stream",
        "--use-fake-ui-for-media-stream",
      ],
    },
    permissions: ["microphone"],
  },
  reporter: "list",
  outputDir: "../.local/browser-results",
});
