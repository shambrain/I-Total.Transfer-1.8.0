/*
 * Local browser end-to-end test for the wireless sender.
 *
 * It starts the real receiver through tools/DeviceDrill, opens the ticket URL in a real
 * Chromium-based browser with Playwright, and drives the page. Two phases:
 *
 *   1. happy path (auto-approve): pair, select a 64 KiB HEIC, upload, verify the receipt, check the
 *      destination bytes, send again and confirm the collision rename;
 *   2. cancellation (manual approval): pair, select two files, start the batch, cancel while the first
 *      file waits for approval, and confirm the page reports CANCELLED with the second file NOT
 *      ATTEMPTED and no destination file.
 *
 * This is a local browser integration test, not a physical iPhone or Android run.
 *
 * Usage:
 *   npm --prefix tests/sender install
 *   node tests/sender/browser-e2e.mjs <DeviceDrill.exe> [chrome-or-edge.exe]
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
const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function waitForFile(file, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (fs.existsSync(file) && fs.statSync(file).size > 0) return;
    await delay(100);
  }
  throw new Error("Timed out waiting for " + file);
}

async function startReceiver(phase, extraArgs, timeoutSeconds) {
  const destination = path.join(work, phase, "destination");
  const report = path.join(work, phase, "pairing.json");
  fs.mkdirSync(destination, { recursive: true });
  const child = spawn(drill, [
    "wireless", "--dest", destination, "--report", report,
    "--timeout", String(timeoutSeconds), ...extraArgs
  ], { stdio: ["ignore", "pipe", "inherit"] });
  let output = "";
  child.stdout.on("data", (chunk) => { output += chunk.toString(); });
  await waitForFile(report, 20000);
  const pairing = JSON.parse(fs.readFileSync(report, "utf8"));
  assert.ok(pairing.TicketUrl && pairing.Secret, "pairing report is missing the ticket URL or secret");
  return { child, destination, pairing, output: () => output };
}

function makeFile(name, size) {
  const filePath = path.join(work, name);
  const bytes = Buffer.alloc(size);
  for (let i = 0; i < bytes.length; i++) bytes[i] = (i * 31 + 7) % 251;
  fs.writeFileSync(filePath, bytes);
  return { filePath, bytes };
}

async function pairPage(page, pairing) {
  await page.goto(pairing.TicketUrl, { waitUntil: "domcontentloaded" });
  assert.equal(await page.title(), "I-Total Transfer - send files");
  // The page reads the single-use secret from the fragment and removes it from the address bar.
  assert.ok(!page.url().includes("token="), "the pairing token must be removed from the address bar");
  await page.click("#pairButton");
  await page.waitForSelector("#sendSection:not([hidden])", { timeout: 15000 });
}

async function waitForResultText(page, text, timeout = 60000) {
  await page.waitForFunction((needle) => {
    const items = Array.from(document.querySelectorAll("#results li"));
    return items.some((item) => (item.textContent || "").includes(needle));
  }, text, { timeout });
}

let browser;
try {
  browser = await chromium.launch({ executablePath: browserPath, headless: true });

  // ---------------------------------------------------------------- happy path + collision
  {
    const { child, destination, pairing, output } = await startReceiver("happy", ["--auto-approve"], 90);
    const page = await browser.newPage();
    page.on("console", (message) => console.log("page:", message.type(), message.text()));
    try {
      console.log("Receiver:", pairing.Url, "auto-approve:", pairing.AutoApprove);
      await pairPage(page, pairing);
      console.log("Paired:", (await page.textContent("#pairStatus")).trim());

      const { filePath, bytes } = makeFile("IMG_E2E_0001.HEIC", 64 * 1024 + 17);
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
      assert.ok(receipt.includes("VERIFIED"), "the receipt state must be VERIFIED");
      assert.ok(receipt.includes("SHA-256 and byte count matched"), "the receipt must state what was verified");
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
      console.log("PASS browser sender: paired, uploaded, VERIFIED receipt, collision renamed, bytes match");
    } finally {
      if (!child.killed) child.kill();
      await delay(300);
      if (output()) console.log(output().trim().split(/\r?\n/).slice(-8).join("\n"));
      await page.close();
    }
  }

  // ---------------------------------------------------------------- cancellation with manual approval
  {
    // No --auto-approve: the first upload waits for the operator, which gives the test a stable
    // window in which to press Cancel.
    const { child, destination, pairing, output } = await startReceiver("cancel", [], 30);
    const page = await browser.newPage();
    try {
      console.log("Cancel receiver:", pairing.Url, "auto-approve:", pairing.AutoApprove);
      await pairPage(page, pairing);

      const first = makeFile("IMG_CANCEL_0001.MOV", 8 * 1024 * 1024);
      const second = makeFile("IMG_CANCEL_0002.HEIC", 32 * 1024);
      await page.setInputFiles("#files", [first.filePath, second.filePath]);
      await page.waitForFunction(() => {
        const button = document.querySelector("#sendButton");
        return button && !button.disabled;
      }, null, { timeout: 10000 });

      await page.click("#sendButton");
      // Wait until the first file has been hashed and is waiting for approval on the PC.
      await waitForResultText(page, "waiting for approval", 30000);
      assert.equal(await page.$eval("#hashProgress", (el) => el.value), 100,
        "hashing progress must reach 100% before the upload starts");

      // The active batch owns its snapshot: selection, folder, pairing and rotation are disabled,
      // Cancel stays available, and a forced change/rotate must not alter the batch.
      assert.equal(await page.isDisabled("#files"), true, "the file picker must be disabled during a batch");
      assert.equal(await page.isDisabled("#folder"), true, "the destination folder must be disabled during a batch");
      assert.equal(await page.isDisabled("#pairButton"), true, "pairing must be disabled during a batch");
      assert.equal(await page.isDisabled("#rotateButton"), true, "token rotation must be disabled during a batch");
      assert.equal(await page.isDisabled("#cancelButton"), false, "Cancel must stay available during a batch");

      const sessionInfoBefore = await page.textContent("#sessionInfo");
      await page.evaluate(() => {
        document.querySelector("#files").dispatchEvent(new Event("change"));
        document.querySelector("#rotateButton").click();
      });
      await delay(500);
      assert.equal(await page.locator("#fileList li").count(), 2,
        "a forced change event must not replace the active batch's file list");
      assert.equal((await page.textContent("#sessionInfo")).trim(), sessionInfoBefore.trim(),
        "a forced rotate click must not change the session during a batch");
      assert.ok(!(await page.textContent("#summary")).includes("rotated"),
        "a forced rotate click must not report a rotation during a batch");

      await page.click("#cancelButton");
      await waitForResultText(page, "CANCELLED", 30000);
      await waitForResultText(page, "NOT ATTEMPTED", 30000);

      assert.equal(await page.isDisabled("#files"), false, "the file picker must be restored after a batch");
      assert.equal(await page.isDisabled("#folder"), false, "the destination folder must be restored after a batch");
      assert.equal(await page.isDisabled("#pairButton"), false, "pairing must be restored after a batch");
      assert.equal(await page.isDisabled("#rotateButton"), false, "token rotation must be restored after a batch");
      assert.equal(await page.isDisabled("#cancelButton"), true, "Cancel must be disabled when no batch runs");

      const summary = (await page.textContent("#summary")).trim();
      console.log("Cancellation summary:", summary);
      assert.ok(summary.includes("CANCELLED"), "the summary must say the batch was cancelled");
      assert.ok(summary.includes("not attempted"), "the summary must count unattempted files");
      assert.ok(!/complete/i.test(summary), "a cancelled batch must never read as complete");

      assert.ok(!fs.existsSync(path.join(destination, "DCIM", "IMG_CANCEL_0001.MOV")),
        "a cancelled upload must not leave a destination file");
      assert.ok(!fs.existsSync(path.join(destination, "DCIM", "IMG_CANCEL_0002.HEIC")),
        "an unattempted file must not be written");
      const staging = path.join(destination, ".i-total", "wireless");
      if (fs.existsSync(staging)) {
        assert.equal(fs.readdirSync(staging).filter((name) => name.endsWith(".partial")).length, 0,
          "a cancelled upload must not leave a partial staging file");
      }
      console.log("PASS browser sender cancellation: CANCELLED + NOT ATTEMPTED, no destination file");
    } finally {
      if (!child.killed) child.kill();
      await delay(300);
      if (output()) console.log(output().trim().split(/\r?\n/).slice(-8).join("\n"));
      await page.close();
    }
  }
} finally {
  if (browser) await browser.close().catch(() => {});
  fs.rmSync(work, { recursive: true, force: true });
}
