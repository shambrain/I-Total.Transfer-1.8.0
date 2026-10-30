/*
 * Local browser end-to-end test for the wireless sender.
 *
 * It starts the real receiver through tools/DeviceDrill (`wireless --auto-approve`), opens the ticket URL
 * in a real Chromium-based browser with Playwright, pairs, selects a file, sends it, and checks both the
 * page receipt and the bytes on disk. This is a local browser integration test, not a physical iPhone or
 * Android run.
 *
 * Usage:
 *   npm --prefix tests/sender install
 *   node tests/sender/browser-e2e.mjs <DeviceDrill.exe> [chrome-or-edge.exe]
 *
 * The browser defaults to Chrome, then Edge. Auto-approval is used so the test does not need a human at
 * the approval inbox; the approval gate itself is covered by the C# receiver tests.
 */
import { spawn } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import assert from "node:assert/strict";
import { chromium } from "playwright-core";

const drill = process.argv[2];
const browserPath = process.argv[3] || findBrowser();
if (!drill) {
  console.error("usage: node browser-e2e.mjs <DeviceDrill.exe> [chrome-or-edge.exe]");
  process.exit(2);
}
if (!browserPath || !fs.existsSync(browserPath)) {
  console.error("No Chromium-based browser found; pass its executable path as the second argument.");
  process.exit(2);
}

function findBrowser() {
  const candidates = [
    "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
    "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
    "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
    "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe"
  ];
  return candidates.find((candidate) => fs.existsSync(candidate));
}

const work = fs.mkdtempSync(path.join(os.tmpdir(), "itotal-sender-e2e-"));
const destination = path.join(work, "destination");
const report = path.join(work, "pairing.json");
const filePath = path.join(work, "IMG_E2E_0001.HEIC");
const bytes = Buffer.alloc(64 * 1024 + 17);
for (let i = 0; i < bytes.length; i++) bytes[i] = (i * 31 + 7) % 251;
fs.writeFileSync(filePath, bytes);

function delay(ms) { return new Promise((resolve) => setTimeout(resolve, ms)); }

async function waitForFile(file, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (fs.existsSync(file) && fs.statSync(file).size > 0) return;
    await delay(100);
  }
  throw new Error("Timed out waiting for " + file);
}

const child = spawn(drill, ["wireless", "--dest", destination, "--auto-approve", "--report", report, "--timeout", "90"], {
  stdio: ["ignore", "pipe", "inherit"]
});
let childOutput = "";
child.stdout.on("data", (chunk) => { childOutput += chunk.toString(); });

let browser;
try {
  await waitForFile(report, 20000);
  const pairing = JSON.parse(fs.readFileSync(report, "utf8"));
  assert.ok(pairing.TicketUrl && pairing.Secret, "pairing report is missing the ticket URL or secret");
  console.log("Receiver:", pairing.Url, "auto-approve:", pairing.AutoApprove);

  browser = await chromium.launch({ executablePath: browserPath, headless: true });
  const page = await browser.newPage();
  page.on("console", (message) => console.log("page:", message.type(), message.text()));

  await page.goto(pairing.TicketUrl, { waitUntil: "domcontentloaded" });
  assert.equal(await page.title(), "I-Total Transfer - send files");
  // The page reads the single-use secret from the ticket URL and removes it from the address bar.
  assert.ok(!page.url().includes("token="), "the pairing token must be removed from the address bar");

  await page.click("#pairButton");
  await page.waitForSelector("#sendSection:not([hidden])", { timeout: 15000 });
  console.log("Paired:", (await page.textContent("#pairStatus")).trim());

  await page.setInputFiles("#files", filePath);
  await page.waitForFunction(() => {
    const button = document.querySelector("#sendButton");
    return button && !button.disabled;
  }, null, { timeout: 10000 });

  await page.click("#sendButton");
  await page.waitForFunction(() => document.querySelector("#results li.ok"), null, { timeout: 60000 });

  const receipt = (await page.textContent("#results li.ok")).trim();
  const summary = (await page.textContent("#summary")).trim();
  console.log("Receipt:", receipt);
  console.log("Summary:", summary);

  const stored = path.join(destination, "DCIM", "IMG_E2E_0001.HEIC");
  assert.ok(fs.existsSync(stored), "the receiver did not store the file at DCIM/IMG_E2E_0001.HEIC");
  assert.deepEqual(fs.readFileSync(stored), bytes, "stored bytes differ from the selected file");
  assert.ok(receipt.includes("SHA-256 verified"), "the page receipt does not claim SHA-256 verification");
  assert.ok(summary.includes("Only the files you selected were sent"), "the summary must not imply a library transfer");
  assert.ok(summary.includes("Sent 1 of 1"), "the summary must count the selected file");

  // A second send of the same name must be stored beside the first, never overwrite it.
  await page.click("#sendButton");
  await page.waitForFunction(() => document.querySelector("#results li.ok"), null, { timeout: 60000 });
  const secondReceipt = (await page.textContent("#results li.ok")).trim();
  const renamed = path.join(destination, "DCIM", "IMG_E2E_0001 (1).HEIC");
  assert.ok(secondReceipt.includes("(1).HEIC"), "the second copy must be renamed: " + secondReceipt);
  assert.ok(fs.existsSync(renamed), "the renamed copy is missing");
  assert.deepEqual(fs.readFileSync(renamed), bytes, "the renamed copy differs from the selected file");
  assert.deepEqual(fs.readFileSync(stored), bytes, "the first copy was overwritten");
  console.log("Collision:", secondReceipt);
  console.log("PASS browser sender: paired, uploaded, receipt verified, collision renamed, bytes match");
} finally {
  if (browser) await browser.close().catch(() => {});
  if (!child.killed) child.kill();
  await delay(300);
  if (childOutput) console.log(childOutput.trim().split(/\r?\n/).slice(-12).join("\n"));
  fs.rmSync(work, { recursive: true, force: true });
}
