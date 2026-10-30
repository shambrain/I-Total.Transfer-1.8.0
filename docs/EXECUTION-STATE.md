# Execution state — I-Total Transfer master directive

Updated: 2026-10-31. Read this file first when resuming. This checkpoint records the verified product-code checkpoint, later documentation/archive/checksum commits, evidence status, and exact next actions. Always fetch the current branch tip before acting; this file cannot contain its own eventual commit SHA.

## Specification and operating rules

- Primary specification: `I-Total-Transfer-OpenCode-Master-Directive.md` (reported 799 lines; read in full before implementation).
- Execution rules: `I-Total-Transfer-OpenCode-Execution-Instruction.md` (reported 132 lines; read in full).
- Those two directive files are local, untracked specification inputs according to the earlier workspace record. Confirm their exact contents in the active Windows workspace; do not silently substitute another copy.
- Use the architecture diagram to define component responsibilities and the transfer pipeline to govern runtime order. They are complementary.
- Preserve user changes and known-good artifacts. Do not run destructive Git commands. Before a hard session/tool limit, update this file with the exact resume point, evidence, blockers, and next action.
- Never treat a repository note or a test's name as proof that a command or physical drill ran. Distinguish recorded claims, independently re-verified repository evidence, automated tests, and physical-device observations.

## Repository checkpoint

| Item | Value |
| --- | --- |
| Repository | `shambrain/I-Total.Transfer-1.8.0` (GitHub repository) |
| Working branch | `work/master-directive` |
| Local workspace | `C:\Users\Administrator\Documents\GitHub\I-Total.Transfer-v1.5.0-source` — confirm it still points to this repository before running commands. |
| Current branch tip at the start of this correction | `596d10ed36102130110ad95f94d019fab0ab132a` — `Correct the physical-sender wording and refresh the archives` |
| Product-code checkpoint | `733b7b1658fd35f8c707857c2bac1916b8c10d51` — `Validate TLS identities exactly, cancel pending approvals on disconnect, lock batch controls` |
| Later documentation/archive/checksum commits | `bdf4341` (state/artifact refresh), `da56721` (branch-tip/source refresh), `596d10e` (physical-sender wording/archive refresh); these do not change product code |
| Local working-tree status | Clean at `596d10e` before this correction. No reset, clean or force-push was attempted. |

## Latest product validation recorded at the code checkpoint

- Build: `dotnet build I-Total.Transfer.sln -c Release --no-incremental` — **0 warnings, 0 errors**.
- Tests: `dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s` — **547 passed, 0 failed, 2 skipped, 549 total** (about 51 seconds), including 14 `ServerCertificateValidatorTests`, 13 `ApprovalInboxTests`, the real-socket pending-approval disconnect test and six `SingleInstanceGuardTests`.
- Node: `node tests\sender\sender.test.mjs src\I-Total.Transfer\Wireless\sender.js` — **PASS** (13 SHA-256 sizes, path rules, hash cancellation before/between slices, 24 receipt cases, five batch-cancellation sequences, summary honesty, fragment/query secret reading).
- Browser: `node tests\sender\browser-e2e.mjs <DeviceDrill.exe>` — **PASS** (auto-approve happy path with `VERIFIED` receipt and collision rename; manual-approval cancellation with `CANCELLED` + `NOT ATTEMPTED`, no destination or `.partial` file, disabled/restored batch controls and forced change/rotate no-ops).
- TLS identity: `ServerCertificateValidatorTests` (14) cover exact IP/DNS SANs, similar names, wildcards, EKU, key usage, validity, private key and no SAN. The chain-trust test uses a normal `X509ChainPolicy` with `TrustMode = CustomRootTrust` and a generated root; it does not use a permissive callback and does not claim Safari/Chrome trust.
- Physical phone tests: **not performed**. iPhone Safari, Android Chrome, phone TLS trust-store, AN06 current-candidate revalidation, IP07/AN07 cable pull and the KI-27 two-launch drill all remain pending; no physical result is claimed.
- Pending approval: `AClientThatDisconnectsWhileWaitingForApprovalCancelsTheOffer` closes a real socket while the offer waits; pending/waiting counts drop, the offer is `Cancelled`, a late approval is refused, no file/`.partial`/received item remains, and the path is retried successfully.
- Licensing flake (KI-26): the 2026-10-31 run captured `Expected:<SignatureInvalid>. Actual:<Malformed>`. The test's base64-character tamper occasionally corrupted the envelope before signature verification; it now flips a decoded signature byte. 20 isolated runs and three consecutive full suites passed. Product code was not changed.
- The test/build numbers above are checkpoint evidence for this workspace, not a claim that a later GitHub-only documentation update reran them. Re-run locally after syncing, and report new results separately.

## KI-29 — inventory scope is separate from enumeration completeness

- Earlier iPhone listing: **57,731 files / 194,908,848,302 B**, `Complete`.
- Reconnect listing: **26,336 files / 114,344,214,625 B**, `Complete`, 563 seconds, no reported failed folders.
- The reconnect totals equal `202608_a` (**26,320 files / 114,331,766,452 B**) plus 16 of the 32 files in small folders. The byte gap from the earlier listing (**80,564,633,677 B**) equals `202610_a` + `202609_a` + those 16 files exactly.
- Thus two large albums were absent from the later exposed listing. Evidence does not establish that they were deleted or explain why they were absent. Do not invent a cause.
- `Core/ScanScope.cs` adds `InventoryScopeStatus` (`Unknown` / `Stable` / `Changed`), `InventoryScope`, `FolderInventory`, `ScanScope.Compare`, and cache-baseline policy. `ScanResult.Scope` is carried into scan and session reports; `CopyCoreAsync` logs a changed-scope warning; `MainForm` names missing folders and advises a rescan after the phone settles.
- A materially changed scope does not silently replace the larger cached baseline. A changed scope is a warning and does not, by itself, block copying. `Complete` still means the provider-exposed folders were enumerated, not that every album or cloud-only original was exposed.
- Ten tests are recorded for scope comparison, naming, tolerances, case-insensitivity, cache policy, and report wiring. Re-run the suite to verify the current branch.
- Evidence references: `docs/KNOWN_ISSUES.md` KI-29, `docs/PROGRESS.md`, `src/I-Total.Transfer/Core/ScanScope.cs`, `tests/I-Total.Transfer.Tests/ScanScopeTests.cs`, `docs/evidence/2026-10-29/reconnect-scan-observation.txt`, `docs/evidence/2026-10-29/pull2-iphone.json`.

## Mid-stream fault injection — automated only

- `MockBackend.MidStreamThrowPaths` writes half a `.partial`, throws, and cleans the partial.
- Two automated tests assert that the run aborts with manifest `ABORTED`, the entry remains `Copying` with no destination digest, no final destination file or `.partial` remains, and no completion report is written; once the fault is removed, resuming the same session rereads the source and finishes verified.
- This is deterministic automated fault-injection evidence. It does **not** prove the physical cable-pull case. Keep IP07 and AN07's physical mid-file-disconnect status pending.

## Physical hardware evidence and exact remaining drills

### Verified: unplug during scan

- Evidence: `docs/evidence/2026-10-29/pull-iphone.json` (the event occurred on 2026-10-30; preserve the existing path). The scan returned **2,557 files**, `Incomplete`; the subsequent copy failed with `0x802A0002` because the device had disappeared. The recorded result had no destination file or `.partial`, and the manifest said `INCOMPLETE ENUMERATION`.
- The separate matrix also retains an earlier mid-scan disconnect drill. Keep each run's date/build/evidence distinct; do not merge them into a single undocumented event.

### Pending: genuine physical mid-file disconnect (IP07 / AN07)

- The 7.3 GB MOV finished copying at **00:24:54**, and its report was written at **00:25:01**. The cable was pulled after completion. That run is not mid-file-disconnect evidence.
- Procedure: follow `docs/TEST-PLAN.md`; capture the start marker, pull the cable only while the large file is actively transferring, and preserve timestamps, app logs, manifest/journal, destination and `.partial` state, then reconnect and test the documented retry/resume path.
- Acceptance: the interrupted item is never reported hash-verified complete; a truncated file is not committed as success; journal/reconciliation counts are truthful; retry rereads/resumes only as actually supported and produces a matching source/destination SHA-256; unrelated destination files are not overwritten. Record automated and physical evidence separately.

### HUAWEI PTP — distinguish historical pass from current-candidate revalidation

- The current user/OpenCode report says to run AN06 again on the current candidate. Treat **current-candidate revalidation as pending** until fresh evidence is saved.
- Important repository reconciliation: `docs/DEVICE_TEST_MATRIX.md` already records a **historical LIVE PASS on 2026-10-24** on a prior published build: the HUAWEI was switched to Photo transfer (PTP), Windows identified PID `1083`, a 57-file / approximately 7.5 MB listing appeared, and the camera-only warning was shown. Do not erase that valid historical result or claim AN06 has never been performed; keep historical pass and current-candidate revalidation as separate fields.
- On the HUAWEI, choose **Photo transfer (PTP)** in the USB notification, wait for Windows to re-enumerate, then run `DeviceDrill probe --family android --require-family` and `DeviceDrill scan --family android --report <actual-report-path>` using the real CLI syntax and an evidence path that exists. Save the output and record the actual USB mode. Verify the camera-only scope warning. Switch back to **File transfer (MTP)** and scan again for comparison.
- Do not assume the PTP listing is supposed to include the whole phone. A smaller camera-only listing is expected; ensure the warning is accurate and a scan is not presented as a full-storage inventory.

## Candidate and artifact hashes at the last package checkpoint

These are the hashes recorded in `dist/SHA256SUMS.txt` at the 2026-10-31 exact-TLS/cancellation artifact checkpoint. Verify files locally before distributing; any rebuild requires fresh hashes.

| Artifact | SHA-256 | Recorded size |
| --- | --- | --- |
| `publish/I-Total.Transfer.exe` | `F4ADC1B13C4125113EF6761D02344B22D9E393FCAC1E63C66C650AA90CB05BD6` | 112,260,934 B |
| `dist/I-Total.Transfer-1.8.0-win-x64.zip` | see `dist/SHA256SUMS.txt` (repacked after this file) | 54.49 MB |
| `dist/installer/I-Total.Transfer-1.8.0-portable-win-x64.zip` | `0A50685A4AE10F5DE0C1B35C691FACF103CFDA7665E7773059112E5053EEB1E6` | 44.73 MB |
| `dist/installer/I-Total.Transfer-1.8.0-setup.exe` | `5C63BE6E51B4299F46CCB4D439206E127082ED1BE78AC3543FE512A512D71ECC` | 35.32 MB |
| `dist/I-Total.Transfer-1.8.0-source.zip` | see `dist/SHA256SUMS.txt` (repacked after this file) | 15.09 MB |

Recorded at this checkpoint: installer silent install/uninstall was rechecked (executable and README hashes match the tree; uninstall removed the install directory and HKCU key while keeping `%APPDATA%`); the executable was reproduced byte-identically from the source ZIP. The two ZIPs that contain this file are repacked after it is written, so their own hashes cannot be stored inside themselves; `dist/SHA256SUMS.txt` is authoritative for them.

## Other outstanding requirements — do not silently declare the whole directive complete

- AFC backend remains fail-closed: `src/I-Total.Transfer/native/dependencies.json` has empty hashes; audited native binaries and license review are needed before activation.
- Windows 11 visual/device validation is not recorded; latest host evidence is Windows 10 22H2.
- Visual Studio Designer open/save/reopen round-trip is not recorded because Visual Studio was unavailable in the recorded host. Static Designer coverage is not a substitute for this check.
- Trusted Authenticode signing is blocked on a trusted signing certificate. Production licence key/issuing service and purchase URL are not configured. Live Steamworks ownership integration is blocked on eligibility, SDK and partner access; current signed-entitlement behavior is not a live Steam ownership call.
- Gate G3: the receiver serves a **real browser sender** (`/`, `/send`, `/pair#token=...`, `/sender.js`) with
  explicit file selection, pairing, cancellation-aware incremental SHA-256, approval, fail-closed
  receipts, cancellation accounting, active-batch controls, rotation and collision-safe storage. Verified by
  6 loopback sender tests, 14 exact-TLS tests, 13 approval-inbox tests, a real-socket pending-approval
  disconnect test, Node digest/path/receipt/cancellation tests and a Chrome end-to-end run on this PC (happy
  path, manual-approval cancellation and control assertions). A physical iPhone/Android browser run is still
  pending (KI-12); do not call the physical sender complete.
- KI-27 single-instance guard: `SingleInstanceGuard` (`Local\` mutex) is held by `Program.Main`; a second
  launch activates the existing window and exits. Six automated tests; physical two-launch drill pending.
- Gate G3 hardening decisions: `Require TLS` is a hard requirement (refusal, not a silent HTTP fallback) and
  certificate selection is exact SAN identity; the ticket secret is in the URL fragment and the page removes
  it from history; a receipt is `VERIFIED` only when digest, byte count, path and structure all match;
  cancellation never reads as success; a pending approval is cancelled promptly when the sender disconnects.
- Other recorded open items include cross-device disappearance hardware drill, iPhone lock/trust revocation, Narrator/NVDA audit, broader Android/iOS models, and the physical two-launch single-instance drill (the guard itself is implemented). Re-open `docs/IMPLEMENTATION-GAP-MATRIX.md`, `docs/KNOWN_ISSUES.md`, and `docs/DEVICE_TEST_MATRIX.md`; continue every safe, unblocked requirement rather than assuming the two manual drills are the only remaining requirements.

## Exact resume procedure

1. In the Windows workspace, fetch `origin`, confirm it maps to `https://github.com/shambrain/I-Total.Transfer-1.8.0.git`, and inspect `git status --short`, current branch, local HEAD, and origin branch tip. Preserve any local changes; do not reset or clean them. The product-code checkpoint `733b7b1` and the later archive/documentation commits through `596d10e` must be present before continuing.
2. Read this file, the full master directive, and the full execution instruction. Compare the state against the actual code, manifests, test output, and artifact files; update any stale claim with evidence rather than copying it forward.
3. Run the Release build and full deterministic suite locally on the synced source. Record the command, raw outcome, count, elapsed time, and test log. Resolve a new failure or preserve its full failure output; do not infer the result from an earlier run.
4. Reconcile README / `PROJECT-STATUS.md` / `docs/PROGRESS.md` / `docs/KNOWN_ISSUES.md` / `docs/DEVICE_TEST_MATRIX.md` / `docs/DEVICE-COMPATIBILITY-MATRIX.md` / `docs/IMPLEMENTATION-GAP-MATRIX.md` / `dist/SHA256SUMS.txt` so statuses and test counts agree. Do not edit historical records to make old runs appear to have current counts.
5. Continue unblocked engineering and regression work from the master directive. Then perform current-candidate AN06 and genuine IP07/AN07 only when the operator is physically available to change USB mode/pull the cable. Record both outcomes—even failures—with raw evidence.
6. After code or package changes, rebuild, run tests, verify hashes, inspect the diff, and checkpoint without destructive Git operations. Do not reuse artifact hashes if the artifact changed.
7. Before a session/tool limit, update this file with the exact completed step, last command and result, changed paths/commit, remaining blocker, and one next concrete action.

## Single next concrete action

On the physical iPhone, install/trust the exact-SAN certificate if `Require TLS` is on, open the current
candidate's ticket URL in Safari, select one HEIC photo and one original MOV, approve each on the PC, and
record the OS/browser version, the receipt, the destination SHA-256, and whether Safari converted the HEIC
(KI-12/KI-31). The Android Chrome run, AN06 revalidation, the mid-file cable pull (IP07/AN07) and the
two-launch single-instance drill (KI-27) remain separate pending operator actions; none is complete.
