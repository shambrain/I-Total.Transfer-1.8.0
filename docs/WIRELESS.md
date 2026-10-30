# Wireless transport

The receiver is a real HTTP(S) endpoint on one private interface. It is **not** a way to enumerate an
iPhone: iOS exposes no LAN API for the Photos library, so an explicit sender - a Shortcut or a
companion app - must select the assets and send them. What follows is the complete contract that
sender has to implement, the security model it operates under, and the exact limits of what this
transport proves.

## What the receiver guarantees

| Guarantee | How it is enforced |
| --- | --- |
| It listens only on a private IPv4 interface | `PrivateNetwork` validates the bind address; `0.0.0.0`, `*`, public and carrier-grade addresses are refused before a socket is opened. |
| It runs as the normal per-user installation, without elevation | The receiver owns a `TcpListener` and speaks HTTP/1.1 itself. It does not use HTTP.sys, so no HTTP URL ACL or administrator prompt is required for a LAN address. |
| It never starts unless the operator enabled it | `AppSettings.WirelessEnabled` is checked in `Start()`. |
| Nothing is written without the operator's decision | Every upload is an *offer* held in `ApprovalInbox`. The response is only sent after the operator approves or refuses, unless automatic approval was explicitly switched on. The request body is not read while an offer is pending, so a refused file never reaches the disk - only an approved one is streamed to staging, hashed and committed. |
| A displayed secret cannot be replayed | The pairing secret is single-use. Redeeming it mints a separate session token; the secret is refused as an upload credential. |
| Credentials expire and rotate | Session tokens have a bounded lifetime (default one hour), can be rotated by the sender, are revoked on Stop, and are capped at eight live sessions. |
| Paths cannot escape the destination | `TransferRules.ValidateRelativePath` plus a reserved-path check (`.i-total`, the log file). |
| Bytes are what the sender said they were | SHA-256 is computed while streaming; a declared `X-Content-Sha256` that disagrees discards the payload. Size is checked against `Content-Length`. |
| An existing file is never overwritten | Collision policy is rename-incoming; the receipt records the name actually used. |
| No firewall rule is created | Nothing in the application calls `netsh advfirewall` or the firewall COM API. |

## Endpoints

| Method | Path | Credential | Purpose |
| --- | --- | --- | --- |
| `GET` | `/health` | none | Liveness and whether TLS is active. Never returns a secret. |
| `POST` | `/pair` | `X-Pairing-Secret: <secret>` | Redeems the single-use secret. Returns a session token **once**. |
| `POST` | `/rotate` | `X-Session-Token` | Retires the presented token and returns a new one. |
| `POST` | `/upload?path=DCIM/IMG_0001.HEIC` | `X-Session-Token` | Offers one file. Waits for the operator's decision. |

`X-Pairing-Token` is accepted as an alias for `X-Session-Token` so an early sender build keeps
working, but its value must be a **session token** - the pairing secret is rejected.

The upload body must carry an exact `Content-Length`. A chunked body is refused with `411` because the
free-space check and the size check need the declared byte count before any payload is accepted.

### Pair

```
POST /pair
X-Pairing-Secret: 8f3c...   (32 hex characters, single use)

200 {"status":"paired","session":"...","expiresUtc":"...","tls":false}
401 {"error":"pairing failed","detail":"That pairing secret has already been used. ..."}
```

The secret is spent the moment it is **successfully** redeemed. A wrong secret changes nothing, so a
typo does not burn the ticket; presenting the right secret a second time is refused as already used.
Ask for a new ticket in the application - and when a redemption succeeds, the ticket withdraws its
own URL and QR code, so a spent credential is never left looking scannable.

### Upload

```
POST /upload?path=DCIM/IMG_0001.HEIC
X-Session-Token: ...
X-Content-Sha256: <optional, lowercase hex>   (recommended: the receiver verifies it)
Content-Length: <exact byte count>
Content-Type: application/octet-stream
```

| Status | Meaning |
| --- | --- |
| `200` | Approved, received, digest verified, committed. Body: `{path, bytes, sha256}`. |
| `400` | Missing or unsafe path, or a reserved path. |
| `401` | No session, expired session, or the secret presented as a session. |
| `403` | The operator refused this file (or did not decide before the offer expired). |
| `409` | Another upload for the same path is already waiting for approval. With automatic approval on, both proceed and the second is renamed instead. |
| `411` | The request did not declare a `Content-Length`. |
| `413` | Larger than the 8 GiB per-file cap. |
| `422` | Declared digest or declared size disagreed with the bytes; nothing was kept. Checked **after** the operator's decision, because the body is not read while an offer is pending. |
| `507` | The volume cannot hold the declared size. |

The sender should treat anything but `200` as "not delivered" and retry later with a fresh pairing
ticket. A `403` is a decision, not a transient failure: the same path stays refused for the session.

### Rotate

```
POST /rotate
X-Session-Token: ...

200 {"session":"...","expiresUtc":"..."}
```

The old token stops working immediately. Rotate before a long batch rather than widening the lifetime.

## Sender recipe (iOS Shortcut)

Shortcuts can perform the whole flow without a companion app:

1. **Get Contents of URL** - `POST` to the pairing URL shown as a QR code, with header
   `X-Pairing-Secret`. Read `session` from the JSON response.
2. **Select Photos** (or *Get Latest Photos* with a count).
3. **Repeat with Each** - for every photo:
   - *Get Contents of URL*, method `POST`, URL `http://<address>:<port>/upload?path=DCIM/<filename>`,
     header `X-Session-Token: <session>`, request body *File*.
   - Check the status code. `200` means the operator approved it; anything else means it was not
     delivered.
4. Optionally **Get Contents of URL** to `/rotate` between batches.

`path` must be a relative path. The receiver accepts forward slashes and converts them for the
filesystem; a bare filename is placed under `DCIM`.

## Browser sender (implemented)

The receiver serves a real sender page at `/`, `/send`, and the ticket URL (`/pair?token=...`). Scanning
the QR opens the page in Safari or Chrome; the single-use code is read from the URL, removed from the
address bar with `history.replaceState`, and only redeemed when the operator presses **Pair with this PC**.
Merely opening the URL does not spend it (a loopback test pins that).

What the page does:

- **Explicit selection.** A multi-file picker (HEIC/HEIF, JPEG, PNG, MOV and other videos, AAE, DNG, TIFF)
  plus a destination folder field (default `DCIM`). Nothing is enumerated or sent implicitly.
- **Pairing and sessions.** The session token lives in memory only; it is never written to storage. Rotate
  replaces it, expiry is shown, and a `401` stops the batch and asks for a new pairing code.
- **Incremental SHA-256.** Web Crypto is only available in a secure context, and the receiver may run over
  token-authenticated HTTP on a private network, so the page carries its own incremental SHA-256. Files are
  hashed in 4 MiB slices (bounded memory), the digest is sent as `X-Content-Sha256`, and the receiver
  verifies it again while streaming.
- **Approval and receipts.** Each file waits for the operator's decision on the PC; a `200` shows the
  stored path, byte count and the receiver's SHA-256, and the page refuses to count a receipt whose digest
  disagrees with its own. A `403`, `413`, `422`, `507` or `401` is shown per file.
- **Cancellation.** Abort cancels the in-flight upload; the receiver discards the partial file. A failed
  POST is restarted from zero on retry - there is no partial-upload resume.
- **No whole-library claim.** The summary says "Only the files you selected were sent; this is not a
  whole-library transfer." The page cannot list the phone's library and cannot fetch cloud-only originals.

The page is embedded in the executable (`Wireless/sender.html`, `Wireless/sender.js`) and served with a
restrictive CSP (`default-src 'none'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'`), no
external resources and no referrer.

### Evidence for the browser sender

| Check | Evidence |
| --- | --- |
| Page and script served, no secret in the HTML, CSP present | `WirelessSenderTests` (5 loopback tests) |
| Opening the ticket URL does not spend the secret; JSON API clients still redeem it | `OpeningTheTicketUrlInABrowserDoesNotSpendTheSecret`, `AnApiClientStillRedeemsTheTicketWithAJsonGet` |
| SHA-256 matches OpenSSL across 13 sizes (0 B to 1 MiB), one-shot and chunked; path rules | `tests/sender/sender.test.mjs` (Node, 13 size vectors) |
| Real browser end to end | `tests/sender/browser-e2e.mjs` with Chrome: pairs, selects a 64 KiB file, uploads, receipt SHA-256 verified, destination bytes equal, summary counts 1 of 1, second send renamed ` (1)` |
| Sender disconnect mid-upload | `AClientThatDisconnectsMidUploadLeavesNoPartialOrDestination`: no destination file, no `.partial`, no received entry |

The browser test runs on the PC against the real receiver with auto-approval on; the approval gate itself
is covered by the receiver tests. A physical iPhone or Android browser run is **not** recorded yet
(KI-12); this is a local browser integration, not a phone test.

```powershell
npm --prefix tests\sender install
node tests\sender\sender.test.mjs src\I-Total.Transfer\Wireless\sender.js
node tests\sender\browser-e2e.mjs tools\DeviceDrill\bin\Release\net10.0-windows\DeviceDrill.exe
```
## TLS

`WirelessRequireTls` asks for `https://`. The receiver searches `CurrentUser\My` for a certificate
that has a private key, is currently valid, and whose subject or subject alternative names include the
address being served. If one is found, accepted sockets are wrapped in `SslStream` and the ticket,
status line and `/health` all say TLS is active.

If no matching certificate exists, the receiver **does not silently claim** TLS. It falls back to the
token-authenticated session over the private interface, sets `UsingTls = false`, shortens the status
line to `(HTTP; no certificate)`, writes the full reason to the activity log, and answers `/health`
with `{"status":"ok","tls":false}`. No `netsh` reservation, administrator prompt or self-signed
certificate is involved.

**What this does and does not protect.** On a private network the session is authenticated (an
unpaired client cannot upload, and an unapproved file is not written), but the payload is not
encrypted in transit when TLS is not active. Treat the Wi-Fi network as trusted, or install a
matching certificate.

## Limits

- **No enumeration.** The receiver cannot ask an iPhone for its library; the sender must choose files.
- **No background transfer.** A Shortcut runs while it runs. iOS may suspend it when the screen locks;
  the receiver will see the connection end and discard the partial file.
- **No resume of a partial upload.** A failed POST starts again from zero.
- **HEIC/ProRAW conversion is the sender's job.** Bytes are stored exactly as sent.
- **Local browser sender verified; physical sender not yet.** The protocol, pairing, approval, digest, traversal and
  rotation rules are covered by loopback tests (`WirelessReceiverTests` 21, `PairingStateTests` 13,
  `ApprovalInboxTests` 9, `PrivateNetworkTests` 21, `WirelessPageTests` 11) **and** were driven live
  against the published executable with a real HTTP client: `401` for no credential and for the secret
  used as a session, single-use redemption, a 1 MiB upload pending with nothing on disk until Approve,
  `403` on refusal with the refusal sticking, `400` on traversal, `422` and no staging file on a digest
  mismatch, and rename-under-collision. A real iPhone Shortcut has still not been run against it, so
  the sender leg is recorded as an open item in `docs/RELEASE-ACCEPTANCE.md`.

## Live non-elevated LAN check (2026-10-22)

The current published build (SHA-256 `C953BC08...`) was started through its own Wireless page by the
non-elevated user `DRRAMPAGE-PC\shambrain` with `Require TLS` on and no matching certificate. It bound
`192.168.8.91:47821` successfully, created a ticket with an `http://` URL, displayed
`Listening on http://192.168.8.91:47821/ (HTTP; no certificate).`, and `/health` answered
`{"status":"ok","tls":false}`. The previous build, which used `HttpListener`, failed the same action
with `Access is denied.` This is the regression covered by
`WirelessReceiverTests.APrivateLanAddressBindsWithoutAUrlAcl`.
