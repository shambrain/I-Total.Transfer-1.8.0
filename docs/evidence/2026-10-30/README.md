# Wireless sender evidence - 2026-10-30

These are raw outputs from the local sender verification. They are local browser/integration evidence,
not a physical iPhone or Android run.

| File | Command | Result |
| --- | --- | --- |
| `node-sender-test.txt` | `node tests/sender/sender.test.mjs src/I-Total.Transfer/Wireless/sender.js` | PASS: SHA-256 against OpenSSL for 13 sizes, path rules, hash cancellation before/between 4 MiB slices, 24 fail-closed receipt cases, five batch-cancellation sequences, summary honesty, fragment/query pairing-secret reading |
| `browser-sender-e2e.txt` | `node tests/sender/browser-e2e.mjs tools/DeviceDrill/bin/.../DeviceDrill.exe` | PASS phase 1: Chrome paired, selected a 64 KiB HEIC, uploaded, `VERIFIED` receipt, destination bytes equal, second send renamed ` (1).HEIC`. PASS phase 2: manual approval, Cancel while the first file waited, page reported `CANCELLED` + `NOT ATTEMPTED`, no destination file and no `.partial` |

The happy path used the real receiver with auto-approval on; the cancellation phase used manual approval.
The approval gate itself is also covered by `WirelessReceiverTests`. The physical sender row remains open
(KI-12). The ticket URL in the raw log is `http://127.0.0.1:<port>/pair#token=<secret>`; the secret is a
throwaway loopback ticket, not a credential for any real deployment.
