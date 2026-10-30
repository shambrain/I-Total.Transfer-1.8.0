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
const context = { console, Uint8Array, Uint32Array, ArrayBuffer, TextEncoder, Math, Number, String, Array, Error };
context.globalThis = context;
vm.createContext(context);
vm.runInContext(source, context, { filename: scriptPath });

const api = context.IITotalSender;
assert.ok(api && typeof api.sha256Hex === "function", "IITotalSender.sha256Hex is missing");
assert.ok(api.Sha256 && typeof api.normalizePath === "function", "Sha256/normalizePath are missing");

const enc = new TextEncoder();
const hex = (buf) => Buffer.from(buf).toString("hex");

// Known SHA-256 vectors.
assert.equal(api.sha256Hex(enc.encode("")), "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
assert.equal(api.sha256Hex(enc.encode("abc")), "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
assert.equal(
  api.sha256Hex(enc.encode("The quick brown fox jumps over the lazy dog")),
  "d7a8fbb307d7809469ca9abcb0082e4f8d5651e46d3cdb762d02d0bf37c9e592"
);

// Random payloads, one-shot and chunked, against Node's OpenSSL implementation.
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

// The digest cannot be extended after it is finalized.
const finalized = new api.Sha256();
finalized.update(enc.encode("x"));
finalized.hex();
assert.throws(() => finalized.update(enc.encode("y")), /finalized/);

// Path rules: safe by default, explicit about what Windows cannot store.
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

console.log(`sender.test.mjs: PASS (${sizes.length} size vectors, path rules, formatBytes)`);
