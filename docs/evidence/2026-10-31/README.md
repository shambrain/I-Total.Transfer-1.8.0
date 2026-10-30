# Wireless/TLS evidence - 2026-10-31

These are raw local outputs from the exact-TLS, pending-approval cancellation, active-batch control and
single-instance verification. They are local automated/browser evidence, not a physical iPhone or Android
run.

| File | Command | Result |
| --- | --- | --- |
| `node-sender-test.txt` | `node tests/sender/sender.test.mjs src/I-Total.Transfer/Wireless/sender.js` | PASS: SHA-256, paths, hash cancellation, fail-closed receipts, batch cancellation, summary honesty, fragment pairing |
| `browser-sender-e2e.txt` | `node tests/sender/browser-e2e.mjs <DeviceDrill.exe>` | PASS: auto-approve happy path with `VERIFIED` receipt and collision rename; manual-approval cancellation with `CANCELLED`/`NOT ATTEMPTED`, disabled and restored batch controls, forced change/rotate no-ops |
| `full-suite.txt` | `dotnet test ... --blame-hang --blame-hang-timeout 240s` | 547 passed, 0 failed, 2 skipped, 549 total |
| `tls-identity-tests.txt` | `dotnet test ... --filter FullyQualifiedName~ServerCertificateValidatorTests` | 14 passed: exact IP/DNS SANs, similar names, wildcard rule, EKU/key usage, validity, private key, no SAN; the chain test rejects a leaf from an untrusted root and accepts it with a generated root in a normal custom trust store (no permissive callback) |
| `pending-approval-tests.txt` | `dotnet test ... --filter "Name~AClientThatDisconnectsWhileWaitingForApproval|FullyQualifiedName~ApprovalInboxTests"` | PASS: real-socket disconnect cancels the offer, late approval refused, path retried; inbox cancellation rules |
| `single-instance-tests.txt` | `dotnet test ... --filter FullyQualifiedName~SingleInstanceGuardTests` | 6 passed |
| `licensing-tamper-test.txt` | `dotnet test ... --filter Name~ATamperedEntitlementIsRejected` | PASS: tampered decoded signature byte is `SignatureInvalid`; the corrected test no longer occasionally returns `Malformed` (KI-26) |

The ticket URLs in the browser log are throwaway loopback tickets. Physical Safari/Chrome and phone
trust-store evidence remain pending (KI-12, KI-31).
