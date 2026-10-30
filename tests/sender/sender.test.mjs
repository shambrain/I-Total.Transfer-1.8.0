import fs from "node:fs";
import vm from "node:vm";
import crypto from "node:crypto";
import assert from "node:assert/strict";

const scriptPath = process.argv[2];
if (!scriptPath) {
  console.error("usage: node sender.test.mjs <path-to-sender.js>");
  process.exit(2);
}

const source = fs.readFileSync(scriptPath, "utf8");
const context = {
  console, Uint8Array, Uint32Array, ArrayBuffer, TextEncoder, Math, Number, String, Array, Error,
  Object, Promise, JSON, Boolean, AbortController, DOMException, URLSearchParams
};
context.globalThis = context;
vm.createContext(context);
vm.runInContext(source, context, { filename: scriptPath });

const api = context.IITotalSender;
assert.ok(api && typeof api.sha256Hex === "function", "IITotalSender.sha256Hex is missing");
assert.ok(api.Sha256 && typeof api.normalizePath === "function", "Sha256/normalizePath are missing");
assert.ok(typeof api.hashFile === "function", "hashFile is missing");
assert.ok(typeof api.verifyReceipt === "function", "verifyReceipt is missing");
assert.ok(typeof api.sendBatch === "function", "sendBatch is missing");

const enc = new TextEncoder();

// ---------------------------------------------------------------- SHA-256
assert.equal(api.sha256Hex(enc.encode("")), "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
assert.equal(api.sha256Hex(enc.encode("abc")), "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
assert.equal(
  api.sha256Hex(enc.encode("The quick brown fox jumps over the lazy dog")),
  "d7a8fbb307d7809469ca9abcb0082e4f8d5651e46d3cdb762d02d0bf37c9e592"
);

const sizes = [0, 1, 7, 55, 56, 63, 64, 65, 127, 128, 1024, 65537, 1024 * 1024];
for (const size of sizes) {
  const bytes = crypto.randomBytes(size);
  const expected = crypto.createHash("sha256").update(bytes).digest("hex");
  assert.equal(api.sha256Hex(new Uint8Array(bytes)), expected, `one-shot mismatch at ${size} bytes`);

  const sha = new api.Sha256();
  let offset = 0;
  while (offset < bytes.length) {
    const step = Math.min(bytes.length - offset, 1 + ((offset * 7) % 4096));
    sha.update(new Uint8Array(bytes.subarray(offset, offset + step)));
    offset += step;
  }
  assert.equal(sha.hex(), expected, `chunked mismatch at ${size} bytes`);
}

const finalized = new api.Sha256();
finalized.update(enc.encode("x"));
finalized.hex();
assert.throws(() => finalized.update(enc.encode("y")), /finalized/);

// ---------------------------------------------------------------- paths
assert.equal(api.normalizePath("DCIM", "IMG_0001.HEIC"), "DCIM/IMG_0001.HEIC");
assert.equal(api.normalizePath("", "a.jpg"), "DCIM/a.jpg");
assert.equal(api.normalizePath("/DCIM/", "a.jpg"), "DCIM/a.jpg");
assert.equal(api.normalizePath("DCIM/Sub", "a.jpg"), "DCIM/Sub/a.jpg");
for (const bad of [
  ["..", "a.jpg"], ["DCIM", "../a.jpg"], ["DCIM", "a/b.jpg"], ["DCIM", "a\\b.jpg"],
  ["DCIM", "a:b.jpg"], ["DCIM", "CON"], ["DCIM", "a.jpg."], ["DCIM", ""], ["DCIM", "a?.jpg"]
]) {
  assert.throws(() => api.normalizePath(bad[0], bad[1]), undefined, `expected rejection for ${bad.join(" / ")}`);
}

assert.equal(api.formatBytes(0), "0 B");
assert.equal(api.formatBytes(1024), "1.0 KB");
assert.equal(api.formatBytes(1024 * 1024), "1.0 MB");

// ---------------------------------------------------------------- hashFile cancellation
const MIB = 1024 * 1024;
function fakeFile(name, size, onSlice) {
  return {
    name,
    size,
    slice(start, end) {
      if (onSlice) onSlice(start, end);
      const bytes = new Uint8Array(end - start);
      return { arrayBuffer: async () => bytes.buffer };
    }
  };
}

// (a) Cancelled before hashing starts: no slice is read at all.
{
  const controller = new AbortController();
  controller.abort();
  let slices = 0;
  await assert.rejects(
    () => api.hashFile(fakeFile("a.bin", 3 * MIB, () => { slices++; }), controller.signal),
    (error) => error.name === "AbortError"
  );
  assert.equal(slices, 0, "cancellation before hashing must not read a slice");
}

// (b) Cancelled between hash chunks: the next slice is never read.
{
  const controller = new AbortController();
  let slices = 0;
  const progress = [];
  await assert.rejects(
    () => api.hashFile(fakeFile("b.bin", 12 * MIB, () => { slices++; }), controller.signal, (fraction) => {
      progress.push(fraction);
      if (progress.length === 1) controller.abort();
    }),
    (error) => error.name === "AbortError"
  );
  assert.equal(slices, 1, "cancellation between chunks must stop after the current slice");
  assert.deepEqual(progress, [1 / 3], "hashing progress must be reported per slice");
}

// (c) Hashing progress is separate and complete for a normal file.
{
  const progress = [];
  const digest = await api.hashFile(fakeFile("c.bin", 10 * MIB), null, (fraction) => progress.push(fraction));
  assert.equal(digest.length, 64);
  assert.deepEqual(progress, [0.4, 0.8, 1]);
}

// ---------------------------------------------------------------- receipt verification
const SIZE = 65553;
const DIGEST = "9558c1cc258dc3da51aaec6883b8ba3c098aace341d9f1b4e84b188aa46f0092";
const good = { path: "DCIM\\IMG_E2E_0001.HEIC", bytes: SIZE, sha256: DIGEST.toUpperCase() };

{
  const verdict = api.verifyReceipt(good, DIGEST, SIZE);
  assert.equal(verdict.state, "VERIFIED");
  assert.equal(verdict.reason, "");
}
assert.equal(api.verifyReceipt({ ...good, path: "DCIM\\IMG_E2E_0001 (1).HEIC" }, DIGEST, SIZE).state, "VERIFIED",
  "a collision-renamed destination is a valid receipt path");
assert.equal(api.verifyReceipt({ ...good, path: "DCIM/Sub/IMG.JPG" }, DIGEST, SIZE).state, "VERIFIED",
  "forward slashes are accepted");
assert.equal(api.verifyReceipt(good, DIGEST.toUpperCase(), SIZE).state, "VERIFIED",
  "digest comparison is case-insensitive hex");

const invalidReceipts = [
  ["missing digest", { path: good.path, bytes: SIZE }],
  ["empty digest", { ...good, sha256: "" }],
  ["blank digest", { ...good, sha256: "   " }],
  ["short digest", { ...good, sha256: DIGEST.slice(0, 63) }],
  ["non-hex digest", { ...good, sha256: "z".repeat(64) }],
  ["incorrect digest", { ...good, sha256: "0".repeat(64) }],
  ["missing byte count", { path: good.path, sha256: DIGEST }],
  ["string byte count", { ...good, bytes: String(SIZE) }],
  ["incorrect byte count", { ...good, bytes: SIZE + 1 }],
  ["fractional byte count", { ...good, bytes: SIZE + 0.5 }],
  ["missing path", { bytes: SIZE, sha256: DIGEST }],
  ["empty path", { ...good, path: "" }],
  ["absolute path", { ...good, path: "\\DCIM\\IMG.HEIC" }],
  ["drive path", { ...good, path: "C:\\DCIM\\IMG.HEIC" }],
  ["traversal path", { ...good, path: "DCIM\\..\\IMG.HEIC" }],
  ["reserved name", { ...good, path: "DCIM\\CON" }],
  ["invalid character", { ...good, path: "DCIM\\IMG?.HEIC" }],
  ["trailing dot", { ...good, path: "DCIM\\IMG.HEIC." }]
];
for (const [label, body] of invalidReceipts) {
  const verdict = api.verifyReceipt(body, DIGEST, SIZE);
  assert.equal(verdict.state, "RECEIPT INVALID", `receipt must be invalid: ${label}`);
  assert.ok(verdict.reason.length > 0, `an invalid receipt needs a reason: ${label}`);
}
for (const body of [null, undefined, [], "ok", 200]) {
  assert.equal(api.verifyReceipt(body, DIGEST, SIZE).state, "RECEIPT INVALID",
    `a non-object receipt must be invalid: ${JSON.stringify(body)}`);
}

// ---------------------------------------------------------------- batch accounting
const FILES = [fakeFile("A.HEIC", 100), fakeFile("B.MOV", 200), fakeFile("C.JPG", 300)];
const A_DIGEST = "a".repeat(64);

function fakeHash(digest = A_DIGEST, onHash) {
  return async (file, signal) => {
    if (onHash) await onHash(file, signal);
    return digest;
  };
}
function okUpload(onUpload) {
  return async (request) => {
    if (onUpload) await onUpload(request);
    return {
      status: 200,
      body: { path: "DCIM\\" + request.file.name, bytes: request.file.size, sha256: request.digest }
    };
  };
}
function abortingUpload(controller) {
  return async () => {
    controller.abort();
    const error = new Error("The operation was aborted.");
    error.name = "AbortError";
    throw error;
  };
}

const terminalStates = (result) =>
  Array.from(result.items, (item) => item.state).filter((state) => state !== "HASHING" && state !== "UPLOADING");
// (a) Cancelled before hashing starts.
{
  const controller = new AbortController();
  controller.abort();
  const result = await api.sendBatch({
    files: FILES, folder: "DCIM", session: "s", signal: controller.signal,
    hashFile: fakeHash(), upload: okUpload()
  });
  assert.equal(result.sent, 0);
  assert.equal(result.cancelled, 0);
  assert.equal(result.unattempted, 3);
  assert.equal(result.cancelledBatch, true);
  assert.deepEqual(terminalStates(result), ["UNATTEMPTED", "UNATTEMPTED", "UNATTEMPTED"]);
}

// (b) Cancelled between hash chunks inside a multi-file batch (real hashFile).
{
  const controller = new AbortController();
  const files = [fakeFile("A.HEIC", 12 * MIB), fakeFile("B.MOV", 100), fakeFile("C.JPG", 100)];
  const result = await api.sendBatch({
    files, folder: "DCIM", session: "s", signal: controller.signal,
    upload: okUpload(),
    onProgress: (event) => {
      if (event.phase === "hash" && event.index === 0) controller.abort();
    }
  });
  assert.equal(result.sent, 0);
  assert.equal(result.cancelled, 1);
  assert.equal(result.unattempted, 2);
  assert.equal(result.cancelledBatch, true);
  assert.deepEqual(terminalStates(result), ["CANCELLED", "UNATTEMPTED", "UNATTEMPTED"]);
  assert.match(Array.from(result.items).find((item) => item.state === "CANCELLED").message, /while hashing/i);
}

// (c) Cancelled during an active upload.
{
  const controller = new AbortController();
  const result = await api.sendBatch({
    files: [FILES[0]], folder: "DCIM", session: "s", signal: controller.signal,
    hashFile: fakeHash(), upload: abortingUpload(controller)
  });
  assert.equal(result.sent, 0);
  assert.equal(result.cancelled, 1);
  assert.equal(result.unattempted, 0);
  assert.equal(result.cancelledBatch, true);
  assert.deepEqual(terminalStates(result), ["CANCELLED"]);
  assert.match(Array.from(result.items).find((item) => item.state === "CANCELLED").message, /unconfirmed/i);
}

// (d) Cancelled during a multi-file batch after one success.
{
  const controller = new AbortController();
  const result = await api.sendBatch({
    files: FILES, folder: "DCIM", session: "s", signal: controller.signal,
    hashFile: fakeHash(),
    upload: async (request) => {
      if (request.file.name === "B.MOV") {
        controller.abort();
        const error = new Error("The operation was aborted.");
        error.name = "AbortError";
        throw error;
      }
      return { status: 200, body: { path: "DCIM\\" + request.file.name, bytes: request.file.size, sha256: request.digest } };
    }
  });
  assert.equal(result.sent, 1);
  assert.equal(result.failed, 0);
  assert.equal(result.cancelled, 1);
  assert.equal(result.unattempted, 1);
  assert.equal(result.cancelledBatch, true);
  assert.deepEqual(terminalStates(result), ["VERIFIED", "CANCELLED", "UNATTEMPTED"]);
}

// (e) Cancelled after one file succeeds but before the next starts.
{
  const controller = new AbortController();
  const result = await api.sendBatch({
    files: FILES, folder: "DCIM", session: "s", signal: controller.signal,
    hashFile: fakeHash(), upload: okUpload(),
    onEvent: (entry) => { if (entry.state === "VERIFIED") controller.abort(); }
  });
  assert.equal(result.sent, 1);
  assert.equal(result.cancelled, 0);
  assert.equal(result.unattempted, 2);
  assert.equal(result.cancelledBatch, true);
  assert.deepEqual(terminalStates(result), ["VERIFIED", "UNATTEMPTED", "UNATTEMPTED"]);
}

// (f) A cancelled batch never summarises as a completed success.
for (const result of [
  { total: 3, sent: 1, failed: 0, cancelled: 1, unattempted: 1, bytes: 100, cancelledBatch: true, sessionExpired: false },
  { total: 3, sent: 0, failed: 0, cancelled: 0, unattempted: 3, bytes: 0, cancelledBatch: true, sessionExpired: false }
]) {
  const summary = api.summarizeBatch(result);
  assert.match(summary.text, /CANCELLED/);
  assert.match(summary.text, /not attempted/);
  assert.doesNotMatch(summary.text, /complete/i);
  assert.equal(summary.kind, "warn");
}
{
  const summary = api.summarizeBatch({
    total: 1, sent: 1, failed: 0, cancelled: 0, unattempted: 0, bytes: SIZE,
    cancelledBatch: false, sessionExpired: false
  });
  assert.match(summary.text, /Sent 1 of 1/);
  assert.match(summary.text, /not a whole-library transfer/);
  assert.equal(summary.kind, "ok");
}

// (g) A bad receipt is a failure, not a success, and says the outcome is unconfirmed.
{
  const result = await api.sendBatch({
    files: [FILES[0]], folder: "DCIM", session: "s", signal: null,
    hashFile: fakeHash(),
    upload: async () => ({ status: 200, body: { path: "DCIM\\A.HEIC", bytes: 100, sha256: "0".repeat(64) } })
  });
  assert.equal(result.sent, 0);
  assert.equal(result.failed, 1);
  assert.deepEqual(terminalStates(result), ["RECEIPT INVALID"]);
  assert.match(Array.from(result.items).find((item) => item.state === "RECEIPT INVALID").message, /may or may not have committed/i);
}

// (h) A refused upload is a failure, and the batch continues.
{
  const result = await api.sendBatch({
    files: [FILES[0], FILES[1]], folder: "DCIM", session: "s", signal: null,
    hashFile: fakeHash(),
    upload: async (request) => request.file.name === "A.HEIC"
      ? { status: 403, body: { error: "refused" } }
      : { status: 200, body: { path: "DCIM\\" + request.file.name, bytes: request.file.size, sha256: request.digest } }
  });
  assert.equal(result.sent, 1);
  assert.equal(result.failed, 1);
  assert.deepEqual(terminalStates(result), ["FAILED", "VERIFIED"]);

}

// (i) An expired session stops the batch and leaves the rest unattempted.
{
  const result = await api.sendBatch({
    files: FILES, folder: "DCIM", session: "s", signal: null,
    hashFile: fakeHash(),
    upload: async () => ({ status: 401, body: { error: "unauthorized" } })
  });
  assert.equal(result.sent, 0);
  assert.equal(result.failed, 1);
  assert.equal(result.sessionExpired, true);
  assert.equal(result.unattempted, 2);
}

// ---------------------------------------------------------------- pairing secret location
assert.equal(api.readPairingSecret({ hash: "#token=abc123", search: "" }), "abc123");
assert.equal(api.readPairingSecret({ hash: "#token=abc123", search: "?token=old" }), "abc123",
  "the fragment wins over a legacy query token");
assert.equal(api.readPairingSecret({ hash: "", search: "?token=legacy" }), "legacy",
  "an older query ticket still works");
assert.equal(api.readPairingSecret({ hash: "#other=1", search: "" }), "");

console.log(`sender.test.mjs: PASS (${sizes.length} SHA-256 sizes, path rules, ` +
  "hash cancellation x3, receipt fail-closed x24, batch cancellation x5, summary honesty, pairing location)");
