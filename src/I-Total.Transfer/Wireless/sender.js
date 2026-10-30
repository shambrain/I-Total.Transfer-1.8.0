"use strict";

/*
 * I-Total Transfer sender page logic.
 *
 * This file is served by the Windows receiver at /sender.js and is deliberately self-contained:
 * no CDN, no framework, no Web Crypto. Web Crypto's subtle.digest() is only available in a secure
 * context, and the receiver may legitimately run over token-authenticated HTTP on a private network,
 * so the page carries its own incremental SHA-256. The same implementation is exercised by Node tests
 * in tests/sender/sender.test.mjs.
 *
 * The batch core (sendBatch/verifyReceipt/hashFile) is transport-injectable so the Node tests can
 * drive cancellation and malformed receipts deterministically without a browser or a network.
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

/**
 * Validates a destination path echoed by the receiver. The receiver reports Windows separators, so
 * both separators are accepted; absolute paths, drive letters, traversal, reserved names and names
 * Windows cannot store are not.
 */
function isSafeReceiptPath(path) {
  if (typeof path !== "string") return false;
  if (path.length === 0 || path.trim() !== path) return false;
  if (/^[\\/]/.test(path)) return false;
  if (/^[A-Za-z]:/.test(path)) return false;
  const segments = path.split(/[\\/]/);
  if (segments.length === 0) return false;
  for (const segment of segments) {
    if (!segment || segment === "." || segment === "..") return false;
    if (INVALID.test(segment) || /[. ]$/.test(segment)) return false;
    if (RESERVED.test(segment)) return false;
  }
  return true;
}

const SHA256_HEX = /^[0-9a-f]{64}$/;

/**
 * Fail-closed receipt check. A 200 alone never counts as success: the receipt must carry a
 * 64-character hexadecimal SHA-256 equal to the digest computed from the selected bytes, a numeric
 * byte count equal to the selected size, and a safe relative destination path.
 */
function verifyReceipt(body, expectedDigest, expectedBytes) {
  const invalid = (reason) => ({ state: "RECEIPT INVALID", reason });
  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return invalid("The PC response was not a receipt object.");
  }
  if (typeof body.sha256 !== "string") return invalid("The receipt has no SHA-256 digest.");
  const digest = body.sha256.trim().toLowerCase();
  if (digest.length === 0) return invalid("The receipt digest is empty.");
  if (!SHA256_HEX.test(digest)) {
    return invalid("The receipt digest is not a 64-character hexadecimal SHA-256.");
  }
  const expected = typeof expectedDigest === "string" ? expectedDigest.trim().toLowerCase() : "";
  if (!SHA256_HEX.test(expected)) return invalid("The locally calculated SHA-256 is not valid.");
  if (digest !== expected) return invalid("The receipt digest does not match the bytes that were sent.");
  if (typeof body.bytes !== "number" || !Number.isFinite(body.bytes) ||
      !Number.isInteger(body.bytes) || body.bytes < 0) {
    return invalid("The receipt has no numeric byte count.");
  }
  if (body.bytes !== expectedBytes) {
    return invalid("The receipt reports " + body.bytes + " bytes but the selected file is " +
      expectedBytes + " bytes.");
  }
  if (!isSafeReceiptPath(body.path)) {
    return invalid("The receipt destination path is missing or unsafe.");
  }
  return { state: "VERIFIED", reason: "" };
}

function formatBytes(value) {
  const n = Number(value) || 0;
  if (n >= 1024 * 1024 * 1024) return (n / (1024 * 1024 * 1024)).toFixed(2) + " GB";
  if (n >= 1024 * 1024) return (n / (1024 * 1024)).toFixed(1) + " MB";
  if (n >= 1024) return (n / 1024).toFixed(1) + " KB";
  return n + " B";
}

function abortError() {
  if (typeof DOMException === "function") {
    try { return new DOMException("The operation was aborted.", "AbortError"); } catch { /* fall through */ }
  }
  const error = new Error("The operation was aborted.");
  error.name = "AbortError";
  return error;
}

function isAbortError(error) {
  return !!error && (error.name === "AbortError" || error.code === 20);
}

function throwIfAborted(signal) {
  if (signal && signal.aborted) throw abortError();
}

const HASH_CHUNK = 4 * 1024 * 1024;

/**
 * Incremental SHA-256 over the selected file in 4 MiB slices. The signal is checked before every
 * slice and again after each asynchronous read, so cancellation stops immediately instead of after
 * the remaining slices have been read. Hashing progress is reported separately from upload progress.
 */
async function hashFile(file, signal, onProgress) {
  throwIfAborted(signal);
  const sha = new Sha256();
  const size = Math.max(0, Number(file.size) || 0);
  if (size === 0) {
    if (onProgress) onProgress(1, 0, 0);
    return sha.hex();
  }
  let offset = 0;
  while (offset < size) {
    throwIfAborted(signal);
    const end = Math.min(offset + HASH_CHUNK, size);
    const buffer = await file.slice(offset, end).arrayBuffer();
    throwIfAborted(signal);
    sha.update(new Uint8Array(buffer));
    offset = end;
    if (onProgress) onProgress(offset / size, offset, size);
  }
  return sha.hex();
}

/**
 * Real upload with genuine upload-progress events. XMLHttpRequest is used instead of fetch because
 * fetch does not expose request-body progress and is not resumable anyway; the signal aborts it.
 */
function uploadWithXhr({ file, path, digest, session, signal, onProgress }) {
  return new Promise((resolve, reject) => {
    if (signal && signal.aborted) { reject(abortError()); return; }
    const xhr = new XMLHttpRequest();
    xhr.open("POST", "/upload?path=" + encodeURIComponent(path), true);
    xhr.setRequestHeader("X-Session-Token", session);
    xhr.setRequestHeader("X-Content-Sha256", digest);
    xhr.setRequestHeader("Content-Type", "application/octet-stream");
    xhr.responseType = "text";
    const onAbort = () => xhr.abort();
    if (signal) signal.addEventListener("abort", onAbort, { once: true });
    const cleanup = () => { if (signal) signal.removeEventListener("abort", onAbort); };
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable && event.total > 0 && onProgress) {
        onProgress(Math.min(1, event.loaded / event.total));
      }
    };
    xhr.onload = () => {
      cleanup();
      let body = null;
      try { body = JSON.parse(xhr.responseText); } catch { body = null; }
      resolve({ status: xhr.status, body });
    };
    xhr.onerror = () => { cleanup(); reject(new Error("network error")); };
    xhr.ontimeout = () => { cleanup(); reject(new Error("the request timed out")); };
    xhr.onabort = () => { cleanup(); reject(abortError()); };
    xhr.send(file);
  });
}

function defaultUpload(options) {
  return uploadWithXhr(options);
}

/**
 * Sends the selected files in order and accounts for every one of them.
 *
 * Cancellation is deterministic and never reported as success:
 * - cancelled before a file starts hashing: that file and all later ones are UNATTEMPTED;
 * - cancelled between hash slices: the current file is CANCELLED and the rest are UNATTEMPTED;
 * - cancelled during an upload: the current file is CANCELLED with an unconfirmed delivery and the
 *   rest are UNATTEMPTED.
 *
 * A failed file does not stop the batch; an expired session does (the rest are UNATTEMPTED).
 */
async function sendBatch(options) {
  const files = Array.from(options.files || []);
  const folder = options.folder;
  const session = options.session;
  const signal = options.signal;
  const hash = options.hashFile || hashFile;
  const upload = options.upload || defaultUpload;
  const onEvent = options.onEvent || (() => {});
  const onProgress = options.onProgress || (() => {});
  const total = files.length;

  const result = {
    total, sent: 0, failed: 0, cancelled: 0, unattempted: 0, bytes: 0,
    cancelledBatch: false, sessionExpired: false, items: []
  };

  const record = (index, file, state, detail) => {
    const entry = { index, name: file ? file.name : "", state, ...detail };
    result.items.push(entry);
    onEvent(entry);
  };

  let index = 0;
  for (; index < total; index++) {
    const file = files[index];
    if (signal && signal.aborted) { result.cancelledBatch = true; break; }

    let path;
    try {
      path = normalizePath(folder, file.name);
    } catch (error) {
      result.failed++;
      record(index, file, "FAILED", { message: error.message });
      continue;
    }

    record(index, file, "HASHING", { path });
    let digest;
    try {
      digest = await hash(file, signal, (fraction) => {
        onProgress({ phase: "hash", index, file, fraction });
      });
    } catch (error) {
      if (isAbortError(error) || (signal && signal.aborted)) {
        result.cancelled++;
        result.cancelledBatch = true;
        record(index, file, "CANCELLED", {
          path, message: "Cancelled while hashing. Nothing was sent for this file."
        });
        index++;
        break;
      }
      result.failed++;
      record(index, file, "FAILED", {
        path, message: "The file could not be read for hashing: " + error.message
      });
      continue;
    }

    if (signal && signal.aborted) {
      result.cancelled++;
      result.cancelledBatch = true;
      record(index, file, "CANCELLED", {
        path, message: "Cancelled after hashing. Nothing was sent for this file."
      });
      index++;
      break;
    }

    record(index, file, "UPLOADING", { path, digest });
    let response;
    try {
      response = await upload({
        file, path, digest, session, signal,
        onProgress: (fraction) => onProgress({ phase: "upload", index, file, fraction })
      });
    } catch (error) {
      if (isAbortError(error) || (signal && signal.aborted)) {
        result.cancelled++;
        result.cancelledBatch = true;
        record(index, file, "CANCELLED", {
          path,
          message: "Cancelled during upload. Delivery is unconfirmed: check the PC destination before retrying."
        });
        index++;
        break;
      }
      result.failed++;
      record(index, file, "FAILED", {
        path,
        message: "The upload did not complete: " + error.message +
          " Delivery is unconfirmed: check the PC destination before retrying."
      });
      continue;
    }

    if (response.status === 200) {
      const verdict = verifyReceipt(response.body, digest, file.size);
      if (verdict.state === "VERIFIED") {
        result.sent++;
        result.bytes += Number(file.size) || 0;
        record(index, file, "VERIFIED", {
          path: response.body.path, bytes: response.body.bytes, digest
        });
      } else {
        result.failed++;
        record(index, file, "RECEIPT INVALID", {
          path: response.body && typeof response.body.path === "string" ? response.body.path : path,
          message: verdict.reason +
            " The PC may or may not have committed the file: check the destination before retrying."
        });
      }
    } else if (response.status === 401) {
      result.failed++;
      result.sessionExpired = true;
      record(index, file, "FAILED", {
        path, message: "The session expired. Pair with the PC again."
      });
      index++;
      break;
    } else if (response.status === 403) {
      result.failed++;
      record(index, file, "FAILED", {
        path, message: "Refused on the PC. The file was not accepted."
      });
    } else {
      result.failed++;
      const detail = response.body && (response.body.error || response.body.detail);
      record(index, file, "FAILED", {
        path,
        message: "Not delivered (" + response.status + (detail ? " " + detail : "") + ")."
      });
    }
  }

  if (result.cancelledBatch || result.sessionExpired) {
    for (let rest = index; rest < total; rest++) {
      result.unattempted++;
      record(rest, files[rest], "UNATTEMPTED", {
        message: result.cancelledBatch ? "Not attempted because the batch was cancelled." : "Not attempted because the session expired."
      });
    }
  }

  return result;
}

/**
 * The batch summary. A cancelled batch always says so and never reads as a completed transfer.
 */
function summarizeBatch(result) {
  const lines = [];
  lines.push("Sent " + result.sent + " of " + result.total + " selected file(s), " + formatBytes(result.bytes) + ".");
  let kind = result.failed > 0 || result.cancelledBatch || result.sessionExpired ? "warn" : "ok";
  if (result.cancelledBatch) {
    lines.push("The batch was CANCELLED: " + result.sent + " sent, " + result.failed + " failed, " +
      result.cancelled + " cancelled, " + result.unattempted + " not attempted.");
  } else if (result.sessionExpired) {
    lines.push("The session expired: " + result.sent + " sent, " + result.failed + " failed, " +
      result.unattempted + " not attempted.");
  } else if (result.failed > 0) {
    lines.push(result.failed + " file(s) were not delivered or could not be verified.");
  }
  lines.push("Only the files you selected were sent; this is not a whole-library transfer.");
  return { text: lines.join(" "), kind };
}

function readPairingSecret(locationLike) {
  const hash = locationLike && typeof locationLike.hash === "string" ? locationLike.hash : "";
  const search = locationLike && typeof locationLike.search === "string" ? locationLike.search : "";
  let secret = "";
  if (hash.length > 1) {
    const params = new URLSearchParams(hash.slice(1));
    secret = params.get("token") || "";
  }
  // The query form is kept only so an older ticket still works; new tickets use the fragment.
  if (!secret && search.length > 1) {
    secret = new URLSearchParams(search).get("token") || "";
  }
  return secret;
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
  const hashProgress = byId("hashProgress");
  const uploadProgress = byId("uploadProgress");
  const hashLabel = byId("hashLabel");
  const uploadLabel = byId("uploadLabel");
  const summary = byId("summary");
  const results = byId("results");

  const state = { session: null, expiresUtc: null, files: [], controller: null, sending: false, rotating: false, blocked: false };

  // The ticket URL carries the single-use secret in the fragment, which a browser never sends to the
  // server. Read it locally and remove it from the address bar and history immediately. This is not
  // encryption: TLS remains a separate setting.
  const token = readPairingSecret(location);
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

  function addResult(text, kind) {
    const item = document.createElement("li");
    item.textContent = text;
    item.className = kind || "";
    results.appendChild(item);
    return item;
  }

  /**
   * A batch is a fixed snapshot: while it runs, the file picker and destination folder are disabled so
   * the selected array cannot be replaced, pairing and rotation are disabled so a token change cannot
   * invalidate the uploads, and Cancel stays available. Every terminal path calls this again.
   */
  function refreshControls() {
    const busy = state.sending;
    fileInput.disabled = busy;
    folderInput.disabled = busy;
    secretInput.disabled = busy;
    pairButton.disabled = busy || state.rotating;
    rotateButton.disabled = busy || state.rotating || !state.session;
    sendButton.disabled = busy || state.rotating || !state.session || state.files.length === 0 || state.blocked;
    cancelButton.disabled = !busy;
  }

  async function pair() {
    if (state.sending || state.rotating) return;
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
      refreshControls();
    }
  }

  function renderFiles() {
    // The active batch holds a snapshot; a stray change event must not replace it mid-flight.
    if (state.sending) return;
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
    state.blocked = blocked;
    refreshControls();
    summary.textContent = "";
    results.replaceChildren();
    hashProgress.value = 0;
    uploadProgress.value = 0;
    hashLabel.textContent = "Hashing: not started";
    uploadLabel.textContent = "Upload: not started";
  }

  async function rotate() {
    if (!state.session || state.sending || state.rotating) return;
    const before = state.session;
    state.rotating = true;
    refreshControls();
    try {
      const response = await fetch("/rotate", {
        method: "POST",
        headers: { "X-Session-Token": before },
        credentials: "omit"
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) { setSummary(body.detail || "Could not rotate the session.", "error"); return; }
      // A batch may have started while the request was in flight. Never overwrite the token that
      // batch is using; its rotation response is simply ignored.
      if (state.sending || state.session !== before) return;
      state.session = body.session;
      state.expiresUtc = body.expiresUtc;
      sessionInfo.textContent = "Session valid until " + new Date(body.expiresUtc).toLocaleTimeString() + ".";
      setSummary("Session token rotated; the old token no longer works.", "ok");
    } catch (error) {
      setSummary("Could not rotate the session: " + error.message, "error");
    } finally {
      state.rotating = false;
      refreshControls();
    }
  }

  function itemText(entry) {
    const name = entry.name || "file";
    switch (entry.state) {
      case "HASHING": return name + " - hashing on this device...";
      case "UPLOADING": return name + " - waiting for approval and upload on the PC...";
      case "VERIFIED":
        return name + " - VERIFIED as " + entry.path + " (" + formatBytes(entry.bytes) +
          ", SHA-256 and byte count matched)";
      case "RECEIPT INVALID":
        return name + " - RECEIPT INVALID: " + entry.message;
      case "FAILED": return name + " - FAILED: " + entry.message;
      case "CANCELLED": return name + " - CANCELLED: " + entry.message;
      case "UNATTEMPTED": return name + " - NOT ATTEMPTED: " + entry.message;
      default: return name + " - " + entry.state;
    }
  }

  function itemClass(entry) {
    if (entry.state === "VERIFIED") return "ok";
    if (entry.state === "CANCELLED" || entry.state === "UNATTEMPTED") return "warn";
    return "error";
  }

  async function send() {
    if (!state.session || state.sending || state.rotating || state.files.length === 0) return;
    // Snapshot the batch before any await. The picker and folder are disabled while it runs, and the
    // snapshot guarantees a late change event cannot alter what this batch is sending.
    const files = state.files.slice();
    const session = state.session;
    const folder = folderInput.value;
    const total = files.length;
    state.sending = true;
    state.controller = new AbortController();
    refreshControls();
    results.replaceChildren();
    hashProgress.value = 0;
    uploadProgress.value = 0;
    hashLabel.textContent = "Hashing: not started";
    uploadLabel.textContent = "Upload: not started";

    const rows = new Map();
    const setRow = (index, entry) => {
      let item = rows.get(index);
      if (!item) { item = addResult("", ""); rows.set(index, item); }
      item.textContent = itemText(entry);
      item.className = itemClass(entry);
    };

    setSummary("Sending " + total + " selected file(s)...");

    let result = null;
    try {
      result = await sendBatch({
        files,
        folder,
        session,
        signal: state.controller.signal,
        onEvent: (entry) => {
          setRow(entry.index, entry);
          if (entry.state === "HASHING") {
            hashLabel.textContent = "Hashing " + entry.name + " (file " + (entry.index + 1) + " of " + total + ")";
          }
          if (entry.state === "UPLOADING") {
            hashProgress.value = 100;
            hashLabel.textContent = "Hashed " + entry.name;
            uploadLabel.textContent = "Uploading " + entry.name + " (file " + (entry.index + 1) + " of " + total + ")";
          }
          if (entry.state === "VERIFIED" || entry.state === "RECEIPT INVALID" ||
              entry.state === "FAILED" || entry.state === "CANCELLED" || entry.state === "UNATTEMPTED") {
            uploadProgress.value = entry.state === "VERIFIED" ? 100 : 0;
          }
        },
        onProgress: (event) => {
          if (event.phase === "hash") {
            hashProgress.value = Math.round(100 * event.fraction);
            hashLabel.textContent = "Hashing " + event.file.name + ": " + Math.round(100 * event.fraction) + "%";
          } else {
            uploadProgress.value = Math.round(100 * event.fraction);
            uploadLabel.textContent = "Uploading " + event.file.name + ": " + Math.round(100 * event.fraction) + "%";
          }
        }
      });
    } catch (error) {
      setSummary("The batch stopped unexpectedly: " + error.message, "error");
      return;
    } finally {
      // Every terminal state restores the controls, including an unexpected exception.
      state.sending = false;
      state.controller = null;
      if (result && result.sessionExpired) state.session = null;
      refreshControls();
    }

    if (result.cancelledBatch) uploadLabel.textContent = "Upload: cancelled";
    const summaryText = summarizeBatch(result);
    setSummary(summaryText.text, summaryText.kind);
  }

  pairButton.addEventListener("click", pair);
  secretInput.addEventListener("keydown", (event) => { if (event.key === "Enter") pair(); });
  fileInput.addEventListener("change", renderFiles);
  folderInput.addEventListener("input", renderFiles);
  sendButton.addEventListener("click", send);
  rotateButton.addEventListener("click", rotate);
  cancelButton.addEventListener("click", () => {
    if (!state.controller) return;
    state.controller.abort();
    setSummary("Cancelling the batch...", "warn");
  });
}

const senderApi = {
  Sha256, sha256Hex, normalizePath, isSafeReceiptPath, verifyReceipt, formatBytes,
  hashFile, uploadWithXhr, sendBatch, summarizeBatch, readPairingSecret, isAbortError
};
if (typeof globalThis !== "undefined") globalThis.IITotalSender = senderApi;
if (typeof document !== "undefined") {
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initSender);
  else initSender();
}
