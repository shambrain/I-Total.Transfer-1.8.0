"use strict";

/*
 * I-Total Transfer sender page logic.
 *
 * This file is served by the Windows receiver at /sender.js and is deliberately self-contained:
 * no CDN, no framework, no Web Crypto. Web Crypto's subtle.digest() is only available in a secure
 * context, and the receiver may legitimately run over token-authenticated HTTP on a private network,
 * so the page carries its own incremental SHA-256. The same implementation is exercised by Node tests
 * in tests/sender/sender.test.mjs.
 */

const K = new Uint32Array([
  0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
  0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
  0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
  0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
  0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
  0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
  0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
  0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
]);

function rotr(x, n) {
  return ((x >>> n) | (x << (32 - n))) >>> 0;
}

class Sha256 {
  constructor() {
    this.h = new Uint32Array([
      0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
      0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19
    ]);
    this.buffer = new Uint8Array(64);
    this.bufferLength = 0;
    this.bytesHashed = 0;
    this.finished = false;
    this._digest = null;
    this.w = new Uint32Array(64);
  }

  update(data) {
    if (this.finished) throw new Error("SHA-256 digest has already been finalized.");
    const bytes = data instanceof Uint8Array ? data : new Uint8Array(data);
    this.bytesHashed += bytes.length;
    let offset = 0;
    if (this.bufferLength > 0) {
      const take = Math.min(64 - this.bufferLength, bytes.length);
      this.buffer.set(bytes.subarray(0, take), this.bufferLength);
      this.bufferLength += take;
      offset += take;
      if (this.bufferLength === 64) {
        this._block(this.buffer, 0);
        this.bufferLength = 0;
      }
    }
    while (offset + 64 <= bytes.length) {
      this._block(bytes, offset);
      offset += 64;
    }
    if (offset < bytes.length) {
      this.buffer.set(bytes.subarray(offset), 0);
      this.bufferLength = bytes.length - offset;
    }
    return this;
  }

  digest() {
    if (this._digest) return this._digest;
    const bitLength = this.bytesHashed * 8;
    const padLength = (this.bufferLength < 56 ? 56 : 120) - this.bufferLength;
    const pad = new Uint8Array(padLength + 8);
    pad[0] = 0x80;
    const high = Math.floor(bitLength / 0x100000000);
    const low = bitLength >>> 0;
    pad[pad.length - 8] = (high >>> 24) & 0xff;
    pad[pad.length - 7] = (high >>> 16) & 0xff;
    pad[pad.length - 6] = (high >>> 8) & 0xff;
    pad[pad.length - 5] = high & 0xff;
    pad[pad.length - 4] = (low >>> 24) & 0xff;
    pad[pad.length - 3] = (low >>> 16) & 0xff;
    pad[pad.length - 2] = (low >>> 8) & 0xff;
    pad[pad.length - 1] = low & 0xff;
    this.update(pad);
    this.finished = true;

    const out = new Uint8Array(32);
    for (let i = 0; i < 8; i++) {
      out[i * 4] = (this.h[i] >>> 24) & 0xff;
      out[i * 4 + 1] = (this.h[i] >>> 16) & 0xff;
      out[i * 4 + 2] = (this.h[i] >>> 8) & 0xff;
      out[i * 4 + 3] = this.h[i] & 0xff;
    }
    this._digest = out;
    return out;
  }

  hex() {
    return Array.from(this.digest(), (b) => b.toString(16).padStart(2, "0")).join("");
  }

  _block(bytes, offset) {
    const w = this.w;
    for (let i = 0; i < 16; i++) {
      const o = offset + i * 4;
      w[i] = ((bytes[o] << 24) | (bytes[o + 1] << 16) | (bytes[o + 2] << 8) | bytes[o + 3]) >>> 0;
    }
    for (let i = 16; i < 64; i++) {
      const x = w[i - 15];
      const y = w[i - 2];
      const s0 = (rotr(x, 7) ^ rotr(x, 18) ^ (x >>> 3)) >>> 0;
      const s1 = (rotr(y, 17) ^ rotr(y, 19) ^ (y >>> 10)) >>> 0;
      w[i] = (w[i - 16] + s0 + w[i - 7] + s1) >>> 0;
    }

    let a = this.h[0], b = this.h[1], c = this.h[2], d = this.h[3];
    let e = this.h[4], f = this.h[5], g = this.h[6], h = this.h[7];
    for (let i = 0; i < 64; i++) {
      const s1 = (rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25)) >>> 0;
      const ch = ((e & f) ^ (~e & g)) >>> 0;
      const t1 = (h + s1 + ch + K[i] + w[i]) >>> 0;
      const s0 = (rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22)) >>> 0;
      const maj = ((a & b) ^ (a & c) ^ (b & c)) >>> 0;
      const t2 = (s0 + maj) >>> 0;
      h = g; g = f; f = e; e = (d + t1) >>> 0;
      d = c; c = b; b = a; a = (t1 + t2) >>> 0;
    }
    this.h[0] = (this.h[0] + a) >>> 0;
    this.h[1] = (this.h[1] + b) >>> 0;
    this.h[2] = (this.h[2] + c) >>> 0;
    this.h[3] = (this.h[3] + d) >>> 0;
    this.h[4] = (this.h[4] + e) >>> 0;
    this.h[5] = (this.h[5] + f) >>> 0;
    this.h[6] = (this.h[6] + g) >>> 0;
    this.h[7] = (this.h[7] + h) >>> 0;
  }
}

function sha256Hex(bytes) {
  const sha = new Sha256();
  sha.update(bytes);
  return sha.hex();
}

const RESERVED = /^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$/i;
const INVALID = /[<>:"|?*\u0000-\u001f]/;

function assertSegment(segment, what) {
  if (!segment || segment === "." || segment === "..") throw new Error(what + " contains an unsafe segment.");
  if (INVALID.test(segment) || /[. ]$/.test(segment)) throw new Error(what + " contains a name Windows cannot store.");
  if (RESERVED.test(segment)) throw new Error(what + " uses a name reserved by Windows.");
}

/**
 * Builds the receiver-relative path for one selected file. The server validates again; this only
 * catches the obvious mistakes before a byte is uploaded.
 */
function normalizePath(folder, name) {
  let base = (folder == null ? "" : String(folder)).trim().replace(/\\/g, "/").replace(/^\/+|\/+$/g, "");
  const fileName = (name == null ? "" : String(name)).trim();
  if (!fileName) throw new Error("A file name is required.");
  if (fileName.includes("/") || fileName.includes("\\") || fileName.includes(":")) {
    throw new Error("The file name contains a path separator.");
  }
  if (base === "") base = "DCIM";
  const segments = base.split("/");
  for (const segment of segments) assertSegment(segment, "The folder");
  assertSegment(fileName, "The file name");
  return base + "/" + fileName;
}

function formatBytes(value) {
  const n = Number(value) || 0;
  if (n >= 1024 * 1024 * 1024) return (n / (1024 * 1024 * 1024)).toFixed(2) + " GB";
  if (n >= 1024 * 1024) return (n / (1024 * 1024)).toFixed(1) + " MB";
  if (n >= 1024) return (n / 1024).toFixed(1) + " KB";
  return n + " B";
}

async function hashFile(file) {
  const sha = new Sha256();
  const chunk = 4 * 1024 * 1024;
  for (let offset = 0; offset < file.size; offset += chunk) {
    const slice = file.slice(offset, Math.min(offset + chunk, file.size));
    sha.update(new Uint8Array(await slice.arrayBuffer()));
  }
  return sha.hex();
}

function initSender() {
  const byId = (id) => document.getElementById(id);
  const secretInput = byId("secret");
  const pairButton = byId("pairButton");
  const pairStatus = byId("pairStatus");
  const sendSection = byId("sendSection");
  const sessionInfo = byId("sessionInfo");
  const folderInput = byId("folder");
  const fileInput = byId("files");
  const fileList = byId("fileList");
  const sendButton = byId("sendButton");
  const cancelButton = byId("cancelButton");
  const rotateButton = byId("rotateButton");
  const progress = byId("progress");
  const summary = byId("summary");
  const results = byId("results");

  const state = { session: null, expiresUtc: null, files: [], controller: null, sending: false };

  // The ticket URL carries the single-use secret. Keep it out of the address bar after reading it, so a
  // screenshot or a shared history entry does not hand the code to somebody else.
  const token = new URLSearchParams(location.search).get("token");
  if (token) {
    secretInput.value = token;
    history.replaceState(null, "", location.pathname);
  }

  function setPairStatus(text, kind) {
    pairStatus.textContent = text;
    pairStatus.className = kind || "";
  }

  function setSummary(text, kind) {
    summary.textContent = text;
    summary.className = kind || "";
  }

  function addResult(text) {
    const item = document.createElement("li");
    item.textContent = text;
    results.appendChild(item);
    return item;
  }

  async function pair() {
    const secret = secretInput.value.trim();
    if (!secret) { setPairStatus("Enter the pairing code shown on the PC.", "error"); return; }
    pairButton.disabled = true;
    setPairStatus("Pairing...");
    try {
      const response = await fetch("/pair", {
        method: "POST",
        headers: { "X-Pairing-Secret": secret },
        credentials: "omit"
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        setPairStatus(body.detail || body.error || "Pairing failed.", "error");
        return;
      }
      state.session = body.session;
      state.expiresUtc = body.expiresUtc;
      secretInput.value = "";
      sendSection.hidden = false;
      setPairStatus("Paired. The pairing code is spent and cannot be reused.", "ok");
      sessionInfo.textContent = "Session valid until " + new Date(body.expiresUtc).toLocaleTimeString() +
        (body.tls ? " over TLS." : " over the private network (not encrypted in transit).");
      sendButton.focus();
    } catch (error) {
      setPairStatus("Pairing failed: " + error.message, "error");
    } finally {
      pairButton.disabled = false;
    }
  }

  function renderFiles() {
    fileList.replaceChildren();
    state.files = Array.from(fileInput.files || []);
    let blocked = false;
    for (const file of state.files) {
      const item = document.createElement("li");
      let path;
      try {
        path = normalizePath(folderInput.value, file.name);
      } catch (error) {
        blocked = true;
        item.className = "error";
        item.textContent = file.name + " - " + error.message;
        fileList.appendChild(item);
        continue;
      }
      const title = document.createElement("div");
      title.textContent = file.name;
      const meta = document.createElement("div");
      meta.className = "meta";
      meta.textContent = formatBytes(file.size) + " - " + (file.type || "unknown type") + " - " + path;
      item.appendChild(title);
      item.appendChild(meta);
      fileList.appendChild(item);
    }
    sendButton.disabled = blocked || state.files.length === 0 || !state.session || state.sending;
    summary.textContent = "";
    results.replaceChildren();
    progress.value = 0;
  }

  async function rotate() {
    if (!state.session) return;
    rotateButton.disabled = true;
    try {
      const response = await fetch("/rotate", {
        method: "POST",
        headers: { "X-Session-Token": state.session },
        credentials: "omit"
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) { setSummary(body.detail || "Could not rotate the session.", "error"); return; }
      state.session = body.session;
      state.expiresUtc = body.expiresUtc;
      sessionInfo.textContent = "Session valid until " + new Date(body.expiresUtc).toLocaleTimeString() + ".";
      setSummary("Session token rotated; the old token no longer works.", "ok");
    } catch (error) {
      setSummary("Could not rotate the session: " + error.message, "error");
    } finally {
      rotateButton.disabled = false;
    }
  }

  async function send() {
    if (!state.session || state.sending || state.files.length === 0) return;
    state.sending = true;
    state.controller = new AbortController();
    sendButton.disabled = true;
    cancelButton.disabled = false;
    progress.value = 0;
    results.replaceChildren();

    const total = state.files.length;
    let done = 0, sent = 0, failed = 0, bytes = 0;
    setSummary("Sending " + total + " selected file(s)...");

    for (const file of state.files) {
      if (state.controller.signal.aborted) break;
      let item;
      try {
        const path = normalizePath(folderInput.value, file.name);
        item = addResult(file.name + " - hashing...");
        const digest = await hashFile(file);
        if (state.controller.signal.aborted) break;
        item.textContent = file.name + " - waiting for approval on the PC...";
        const response = await fetch("/upload?path=" + encodeURIComponent(path), {
          method: "POST",
          headers: {
            "X-Session-Token": state.session,
            "X-Content-Sha256": digest,
            "Content-Type": "application/octet-stream"
          },
          body: file,
          signal: state.controller.signal,
          credentials: "omit"
        });
        const body = await response.json().catch(() => ({}));
        if (response.status === 200) {
          if (body.sha256 && String(body.sha256).toLowerCase() !== digest) {
            item.className = "error";
            item.textContent = file.name + " - RECEIPT HASH MISMATCH (not counted as sent)";
            failed++;
          } else {
            item.className = "ok";
            item.textContent = file.name + " - sent as " + body.path + " (" + formatBytes(body.bytes) + ", SHA-256 verified on the PC)";
            sent++;
            bytes += Number(body.bytes) || 0;
          }
        } else if (response.status === 401) {
          item.className = "error";
          item.textContent = file.name + " - the session expired; pair again.";
          failed++;
          state.session = null;
          break;
        } else if (response.status === 403) {
          item.className = "error";
          item.textContent = file.name + " - refused on the PC.";
          failed++;
        } else {
          item.className = "error";
          item.textContent = file.name + " - not delivered (" + response.status + " " + (body.error || "") + ")";
          failed++;
        }
      } catch (error) {
        if (error.name === "AbortError") {
          if (item) item.textContent = file.name + " - cancelled.";
          failed++;
          break;
        }
        if (item) { item.className = "error"; item.textContent = file.name + " - failed: " + error.message; }
        failed++;
      }
      done++;
      progress.value = Math.round((100 * done) / total);
    }

    state.sending = false;
    state.controller = null;
    sendButton.disabled = !state.session;
    cancelButton.disabled = true;
    const tail = failed > 0 ? " " + failed + " not delivered." : "";
    setSummary("Sent " + sent + " of " + total + " selected file(s), " + formatBytes(bytes) + "." + tail +
      " Only the files you selected were sent; this is not a whole-library transfer.", failed > 0 ? "warn" : "ok");
  }

  pairButton.addEventListener("click", pair);
  secretInput.addEventListener("keydown", (event) => { if (event.key === "Enter") pair(); });
  fileInput.addEventListener("change", renderFiles);
  folderInput.addEventListener("input", renderFiles);
  sendButton.addEventListener("click", send);
  rotateButton.addEventListener("click", rotate);
  cancelButton.addEventListener("click", () => { if (state.controller) state.controller.abort(); });
}

const senderApi = { Sha256, sha256Hex, normalizePath, formatBytes, hashFile };
if (typeof globalThis !== "undefined") globalThis.IITotalSender = senderApi;
if (typeof document !== "undefined") {
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initSender);
  else initSender();
}
