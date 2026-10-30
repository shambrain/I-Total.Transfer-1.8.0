# Wireless sender evidence - 2026-10-30

These are raw outputs from the local sender verification. They are local browser/integration evidence,
not a physical iPhone or Android run.

| File | Command | Result |
| --- | --- | --- |
| `node-sender-test.txt` | `node tests/sender/sender.test.mjs src/I-Total.Transfer/Wireless/sender.js` | PASS: SHA-256 against OpenSSL for 13 sizes, one-shot and chunked, plus path rules |
| `browser-sender-e2e.txt` | `node tests/sender/browser-e2e.mjs tools/DeviceDrill/bin/.../DeviceDrill.exe` | PASS: Chrome paired, selected a 64 KiB HEIC, uploaded, receipt SHA-256 verified, destination bytes equal, second send renamed ` (1).HEIC` with both copies intact |

The browser test used the real receiver with auto-approval on; the approval gate itself is covered by
`WirelessReceiverTests`. The physical sender row remains open (KI-12).
