# Wireless transport

The receiver is a real HTTP(S) endpoint on one private interface. It is **not** a way to enumerate an
iPhone: iOS exposes no LAN API for the Photos library, so an explicit sender - a user-created Shortcut or a
companion app, neither shipped here - must select the assets and send them. What follows is the complete contract that
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
| `GET` | `/`, `/send`, `/pair` | none (HTML) | Serves the embedded browser sender. New tickets are `/pair#token=<secret>`; the fragment is never sent to the server. |
| `GET` | `/sender.js` | none | The sender script, served under the page's restrictive CSP. |
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

## Sender recipe (user-created iOS Shortcut)

This is a **manual workflow the user builds in Apple Shortcuts**, not a native Shortcut or companion app
shipped with I-Total Transfer. It is documented so the HTTP contract is usable without the browser sender;
it is not claimed as a product component, and it has not been run on a physical iPhone in this workspace.

Shortcuts can perform the whole flow without a companion app:

1. **Get Contents of URL** - `POST` to the pairing URL shown as a QR code, with header
   `X-Pairing-Secret`. Read `session` from the JSON response.
2. **Select Photos** (or *Get Latest Photos* with a count).
3. **Repeat with Each** - for every photo:
   - *Get Contents of URL*, method `POST`, URL `<ticket-scheme>://<address>:<port>/upload?path=DCIM/<filename>`,
     header `X-Session-Token: <session>`, request body *File*. Use the **same scheme and address as the
     ticket URL**; do not assume `http://`.
   - Check the status code. `200` means the operator approved it; anything else means it was not
     delivered.
4. Optionally **Get Contents of URL** to `/rotate` between batches.

`path` must be a relative path. The receiver accepts forward slashes and converts them for the
filesystem; a bare filename is placed under `DCIM`.

The scheme is not optional: with `Require TLS` on (the default), the ticket is `https://` and the receiver
refuses to start unless a certificate with an exact SAN match for the bind address is installed **and the
phone trusts that certificate or its issuing CA**. An `http://` upload works only when the operator has
explicitly cleared `Require TLS`; the receiver then says the traffic is not encrypted. A Shortcut cannot
make the phone trust a self-signed certificate by itself.

## Browser sender (implemented)

The receiver serves a real sender page at `/`, `/send`, and the ticket URL. New tickets are
`/pair#token=<secret>`: the secret is in the **fragment**, which a browser never includes in the request
target, so it is absent from the server's request line and from every later asset request. The page reads
it locally from `location.hash`, removes it from the address bar and history with `history.replaceState`,
and only redeems it when the operator presses **Pair with this PC**. Merely opening the URL does not spend
it (a loopback test pins that). An older `/pair?token=...` ticket is still read as a fallback, and the JSON
API still accepts `GET /pair?token=...`; new tickets never use the query string.

The fragment is **not encryption**. It keeps the secret out of request logs and history; TLS is a separate
setting (see below).

What the page does:

- **Explicit selection.** A multi-file picker (HEIC/HEIF, JPEG, PNG, MOV and other videos, AAE, DNG, TIFF)
  plus a destination folder field (default `DCIM`). Nothing is enumerated or sent implicitly.
- **Pairing and sessions.** The session token lives in memory only; it is never written to storage. Rotate
  replaces it, expiry is shown, and a `401` stops the batch and asks for a new pairing code.
- **Incremental SHA-256, cancellation-aware.** Web Crypto is only available in a secure context, and the
  receiver may run over token-authenticated HTTP on a private network, so the page carries its own
  incremental SHA-256. Files are hashed in 4 MiB slices; the abort signal is checked before every slice and
  again after each asynchronous read, so cancelling during hashing stops immediately. Hashing progress and
  upload progress are shown separately. The digest is sent as `X-Content-Sha256` and the receiver verifies
  it again while streaming.
- **Fail-closed receipts.** A `200` alone is never treated as success. `VERIFIED` requires a 64-character
  hexadecimal SHA-256 exactly equal to the digest computed from the selected bytes, a numeric receipt byte
  count exactly equal to the selected file size, a non-empty syntactically safe relative destination path,
  and a JSON object. Anything else is `RECEIPT INVALID`, which says the PC may or may not have committed
  the file and must be checked before retrying. Other outcomes are `FAILED` (`403`, `413`, `422`, `507`,
  network error) and `CANCELLED`.
- **Cancellation accounting.** The page reports sent (`VERIFIED`), failed, cancelled and not-attempted
  files separately. Cancellation before hashing, between hash slices, during a multi-file batch, during an
  upload, or after one success but before the next file starts never produces a success summary: the
  summary explicitly says "The batch was CANCELLED" with the counts. An upload cancelled in flight is
  reported as delivery unconfirmed; the receiver discards the partial staging file. A failed POST is
  restarted from zero on retry - there is no partial-upload resume.
- **Pending-approval cancellation.** The receiver watches each connection without consuming body bytes.
  If the sender aborts while its offer is waiting for approval, the per-request token is cancelled, the
  offer becomes `Cancelled` (not pending, not a sticky refusal), a late operator approval is a no-op, and
  the same relative path can be offered again. No payload byte was read or written before approval.
- **Active-batch controls.** While a batch runs the file picker, destination folder, pairing input/button
  and token rotation are disabled; the batch uses a snapshot of the selected files. Cancel stays
  available. Every terminal state restores the controls, and a rotation response that arrives after a
  batch starts is ignored rather than overwriting the token the batch is using.
- **No whole-library claim.** The summary says "Only the files you selected were sent; this is not a
  whole-library transfer." The page cannot list the phone's library and cannot fetch cloud-only originals.

The page is embedded in the executable (`Wireless/sender.html`, `Wireless/sender.js`) and served with a
restrictive CSP (`default-src 'none'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'`), no
external resources and no referrer.

### Evidence for the browser sender

| Check | Evidence |
| --- | --- |
| Page and script served, no secret in the HTML, CSP present | `WirelessSenderTests` (6 loopback tests) |
| The ticket secret is in the fragment and absent from the request target and asset responses | `TheTicketSecretTravelsInTheFragmentAndNeverInARequestTarget`; opening the ticket URL does not spend it (`OpeningTheTicketUrlInABrowserDoesNotSpendTheSecret`) |
| JSON API clients still redeem `GET /pair?token=` | `AnApiClientStillRedeemsTheTicketWithAJsonGet` |
| SHA-256 matches OpenSSL across 13 sizes (0 B to 1 MiB), one-shot and chunked; path rules; hash cancellation before and between slices | `tests/sender/sender.test.mjs` |
| Receipt fails closed for missing/empty/malformed/wrong digest, missing/wrong byte count, missing/unsafe path, and accepts a collision-renamed path | `tests/sender/sender.test.mjs` (`verifyReceipt` cases) |
| Batch cancellation before hashing, between hash slices, during an upload, after one success, and during a multi-file batch; summary never reads as complete | `tests/sender/sender.test.mjs` (`sendBatch`/`summarizeBatch` cases) |
| Real browser end to end | `tests/sender/browser-e2e.mjs` with Chrome: pairs, selects a 64 KiB HEIC, uploads, `VERIFIED` receipt, destination bytes equal, second send renamed ` (1)` |
| Real browser cancellation with manual approval | `tests/sender/browser-e2e.mjs`: cancel while the first file waits for approval; page reports `CANCELLED` + `NOT ATTEMPTED`, no destination file, no `.partial`; the picker/folder/pairing/rotation controls are disabled during the batch, a forced change/rotate does not alter it, and every control is restored afterwards |
| Pending-approval disconnect | `AClientThatDisconnectsWhileWaitingForApprovalCancelsTheOffer`: a real socket closes while the offer is pending; `Inbox.Pending` and `WaitingUploads` drop to zero, the offer is `Cancelled`, a late approval is refused, no destination/`.partial`/received item remains, and the same path can be offered and approved again |
| Exact TLS certificate selection | `ServerCertificateValidatorTests` (14): exact IPv4/IPv6/DNS SAN, similar addresses/names, wildcard one-label rule, clientAuth-only EKU, missing EKU, key usage, expired/not-yet-valid, no private key, no SAN, and normal chain validation rejecting a leaf from an untrusted root then accepting it with the generated root in a custom trust store (no permissive callback) |
| Single-instance guard | `SingleInstanceGuardTests` (6): exclusion across threads, release on dispose, distinct names, session-local default name, blank-name fallback, double dispose |
| Sender disconnect mid-upload | `AClientThatDisconnectsMidUploadLeavesNoPartialOrDestination`: no destination file, no `.partial`, no received entry |

The browser test runs on the PC against the real receiver; the happy path uses auto-approval, and the
cancellation phase uses manual approval. The approval gate itself is also covered by the receiver tests. A
physical iPhone or Android browser run is **not** recorded yet (KI-12); this is a local browser
integration, not a phone test.

```powershell
npm --prefix tests\sender install
node tests\sender\sender.test.mjs src\I-Total.Transfer\Wireless\sender.js
node tests\sender\browser-e2e.mjs tools\DeviceDrill\bin\Release\net10.0-windows\DeviceDrill.exe
```
## TLS and the four separate protections

`Require TLS` is a **hard requirement**, not a preference. When it is on, the receiver searches
`CurrentUser\My` for a certificate that is currently valid, has a private key, is suitable for TLS server
authentication, and names the bind address in a SAN entry. If one is found, accepted sockets are wrapped
in `SslStream` and the ticket, status line and `/health` all say TLS is active.

### Exact certificate selection

Selection is a structured identity check (`ServerCertificateValidator`), not a substring search over the
subject or the formatted SAN text:

- An **IP bind address** requires an **exact IP SAN** (`iPAddress` GeneralName). `192.168.1.5` is not
  matched by a SAN for `192.168.1.50`, by `192.168.1.5` appearing in the CN, or by the same digits inside
  another name.
- A **DNS bind address** requires an exact DNS SAN, compared case-insensitively. A leftmost `*.` wildcard
  covers exactly one label (`*.example.com` matches `a.example.com`, not `example.com` or
  `a.b.example.com`). Embedded or partial wildcards are refused.
- The certificate must be within `NotBefore`/`NotAfter`, have a private key, and — when the extension is
  present — carry the server-authentication EKU (`1.3.6.1.5.5.7.3.1`) and a key usage that permits
  `digitalSignature` or `keyEncipherment`. A certificate with only clientAuth EKU is refused.
- A CN-only certificate is refused; a SAN is required. This is deliberate: modern clients ignore the CN
  when a SAN exists, and guessing from the subject is how substring bugs get shipped.

**Selection is only the server side.** The receiver cannot decide whether Safari or Chrome trusts the
chain. A leaf certificate that passes the selection checks is still rejected by the phone until its
issuing CA (or the certificate itself) is trusted on the device. A local TLS handshake test pins the
chain distinction: a default client rejects a leaf whose root it does not trust, and the same leaf passes
normal chain validation when the generated root is placed in a custom trust store. That is a .NET
`SslStream` client, not Safari or Android Chrome; it proves chain validation, not a phone's trust store.

### What must be installed on the phones

1. Create or obtain a certificate whose SAN contains the **exact address printed in the ticket URL**
   (`iPAddress=192.168.8.91` for an IP URL, or `dNSName=receiver.local` for a DNS URL). A certificate for
   `localhost` is not valid for the LAN address.
2. Install it in `CurrentUser\My` on the PC with its private key, within its validity period, with
   serverAuth EKU. The receiver selects it only for a matching bind address.
3. Install the issuing CA (or the self-signed certificate) on each phone as a trusted root:
   - **iPhone/iPad:** transfer the `.cer`/`.crt`, install the profile (Settings → General → VPN & Device
     Management), then enable full trust under Settings → General → About → Certificate Trust Settings.
   - **Android:** Settings → Security → Encryption & credentials → Install a certificate → CA
     certificate. Chrome uses the user trust store; Android apps that do not opt in to user CAs may not.
4. If the certificate is issued by a public CA for a public DNS name, the phone already trusts the chain,
   but the name must still resolve to the PC on the private network.

If no certificate matches, the receiver **refuses to start**. `Start()` throws before any socket is
opened, with a message naming the bind address and the two remedies: install a matching certificate, or
clear `Require TLS` to run the token-authenticated session over the private network without encryption.
The Settings caption says the same thing ("refuses to start without a matching certificate"), and the
Wireless page shows the failure and logs it. There is no silent HTTP fallback.

Clearing `Require TLS` is an explicit operator decision. The status line then says
`HTTP: traffic is not encrypted; private network only`, `TlsNote` says TLS is switched off, the activity
log records it, and `/health` answers `{"status":"ok","tls":false}`. No `netsh` reservation,
administrator prompt or self-signed certificate is involved.

**Four separate protections, never conflated:**

| Protection | What it does | What it does not do |
| --- | --- | --- |
| Private-interface restriction | The receiver binds only to a private IPv4 address; wildcard, public and carrier-grade addresses are refused. | It does not encrypt traffic, and it does not stop another host on the same LAN from reaching the port. |
| Pairing authentication | A short-lived, single-use secret mints one session token; the secret cannot upload. | It does not encrypt the payload, and it is not operator approval. |
| Operator approval | An upload becomes an offer; nothing is written until the operator accepts (unless automatic approval is explicitly on). | It does not encrypt traffic and it does not prove the sender is the person at the PC. |
| TLS encryption | When a matching certificate is installed and `Require TLS` is on, the whole HTTP exchange is encrypted. | It does not decide which files are sent, and it does not replace pairing or approval. |

On a private network without TLS the session is authenticated (an unpaired client cannot upload, and an
unapproved file is not written), but the payload is not encrypted in transit. Treat the Wi-Fi network as
trusted, or install a matching certificate.

## Limits

- **No enumeration.** The receiver cannot ask an iPhone for its library; the sender must choose files.
- **No background transfer.** A Shortcut runs while it runs. iOS may suspend it when the screen locks;
  the receiver will see the connection end and discard the partial file.
- **No resume of a partial upload.** A failed POST starts again from zero.
- **HEIC/ProRAW conversion is the sender's job.** Bytes are stored exactly as sent. Some browsers, notably Safari when picking from Photos, may convert HEIC to JPEG before the page receives it; the PC stores what the browser delivers, and the receipt names the stored file.
- **Local browser sender verified; physical sender not yet.** The protocol, pairing, approval, digest,
  traversal, cancellation and rotation rules are covered by loopback tests (`WirelessReceiverTests` 25,
  including TLS refusal and disconnect cleanup; `PairingStateTests` 13; `ApprovalInboxTests` 13;
  `PrivateNetworkTests` 21; `WirelessPageTests` 11; `WirelessSenderTests` 6; `ServerCertificateValidatorTests`
  14; `SingleInstanceGuardTests` 6), by the Node tests and by
  the Chrome end-to-end run. They were also driven live against the published executable with a real HTTP
  client: `401` for no credential and for the secret used as a session, single-use redemption, a 1 MiB
  upload pending with nothing on disk until Approve, `403` on refusal with the refusal sticking, `400` on
  traversal, `422` and no staging file on a digest mismatch, and rename-under-collision. A physical
  iPhone/Android browser run has still not been done, so the physical sender row stays open (KI-12).

## Historical live non-elevated LAN check (2026-10-22)

This records a build from before the TLS semantics were tightened. That build was started through its own
Wireless page by the non-elevated user `DRRAMPAGE-PC\shambrain` with `Require TLS` on and no matching
certificate. It bound `192.168.8.91:47821` and silently fell back to `http://`, displaying
`Listening on http://192.168.8.91:47821/ (HTTP; no certificate).`, with `/health` answering
`{"status":"ok","tls":false}`. The previous `HttpListener` build had failed the same action with
`Access is denied.`

**That fallback no longer exists.** On the current candidate, `Require TLS` with no matching certificate
refuses to start with an actionable message (`RequiringTlsWithoutAMatchingCertificateRefusesToStart`), and
clearing the setting is the only way to run private-network HTTP. The non-elevated bind itself is still
covered by `WirelessReceiverTests.APrivateLanAddressBindsWithoutAUrlAcl`.
