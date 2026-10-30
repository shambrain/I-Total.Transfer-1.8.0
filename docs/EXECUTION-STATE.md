# Execution state — I-Total Transfer master directive

Updated: 2026-10-30. Read this file first when resuming. This checkpoint records the verified source checkpoint, subsequent documentation reconciliation, evidence status, and exact next actions. Always fetch the current branch tip before acting; this file cannot contain its own eventual commit SHA.

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
| Last product-source/build/artifact checkpoint reported | `9ae20fdd1f9522d9349b5662cd27aa08d45eda87` — `Cover the scope field in the session report and refresh the archives` |
| Documentation follow-up after that checkpoint | `032c993177f44f9f779ece5922c26f0f85faf762` updates the README test count; `383b0dc0043c05c7f5838d44a79709f517ef9c64` reconciles KI-26's final suite count; `5119935fac2dbb4e376855aecfec7b3431221444` and `ce4e428e74eb609e6a565153b00473e7e8e2a5c6` distinguish the historical AN06 PTP pass from current-candidate revalidation in both device matrices; `80ed219bbdce650545bf3325e169e1dd773ef070` corrects B15's scope-test count; `2f4c38a77bab5eb760cb12bf014afdaaef6c0188` appends the latest recorded build/test/artifact checkpoint to `docs/BUILD-VALIDATION.md`. The execution-state itself is updated through documentation-only commits. |
| Local workspace recorded in the prior checkpoint | `C:\Users\Administrator\Documents\GitHub\I-Total.Transfer-v1.5.0-source` — confirm it still points to this repository before running commands. |
| Local working-tree status | The user/OpenCode report says the tree was clean at `9ae20fd` before the documentation follow-ups. This assistant session accessed the GitHub repository through file/commit APIs, not the local Windows filesystem; therefore it did not independently run `git status`, build, or tests. Fetch the updated branch in the Windows workspace and verify status before editing further. |

## Latest product validation recorded at the code checkpoint

- Build: `dotnet build I-Total.Transfer.sln -c Release --no-incremental` — recorded as 0 warnings, 0 errors.
- Tests: `dotnet test tests\\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s` — final reported run: **511 passed, 0 failed, 2 skipped, 513 total** (about 32 seconds in the latest user report; the committed run record reports 31 seconds).
- Licensing flake (KI-26): two full-suite occurrences are recorded, on 2026-10-29 and 2026-10-30. After the later occurrence, the user reports 20 isolated passes and six consecutive full-suite passes. The repository KI-26 and README were updated to reflect the final 511-pass / 513-total run count. If it recurs, capture the complete assertion output and sequence file; do not conceal it.
- The test/build numbers above are checkpoint evidence, not a claim that tests were rerun during the current GitHub-only documentation update. Re-run locally after syncing, and report new results separately.

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

These are the hashes recorded in `dist/SHA256SUMS.txt` at the `9ae20fdd` code/artifact checkpoint. The follow-up commits described above are documentation-only. Verify files locally before distributing; any rebuild requires fresh hashes.

| Artifact | SHA-256 | Recorded size |
| --- | --- | --- |
| `publish/I-Total.Transfer.exe` | `E63C6632BB79759E9AEC444B38D416014AC2D86DFA3A17114997471C40A02602` | 112,236,358 B |
| `dist/I-Total.Transfer-1.8.0-win-x64.zip` | `66E3EADF300A5FB1AEA8F3C8A432076EECB119818615C227C88E4D4EE330D81A` | 54.46 MB |
| `dist/installer/I-Total.Transfer-1.8.0-portable-win-x64.zip` | `D6003378B17811E9C4B89E617C9927766D5AAC4D2FD87E166E8540AC1EE1C7AC` | 44.72 MB |
| `dist/installer/I-Total.Transfer-1.8.0-setup.exe` | `BF2207ABA0D4B9AFB8A1C9C2DB393692C99C6B57847994613036ECA715418507` | 35.30 MB |
| `dist/I-Total.Transfer-1.8.0-source.zip` | `B738EC4373E3893AE9700A5A1606AFE81ACA0FCE35A67B66AF23059A55D96DDE` | 15.05 MB |

Recorded at checkpoint: source ZIP documentation comparison had zero mismatches; installer install/uninstall was rechecked; the executable was reproducible from the source ZIP. These are repository-reported checks and were not re-executed by this documentation-only update.

## Other outstanding requirements — do not silently declare the whole directive complete

- AFC backend remains fail-closed: `src/I-Total.Transfer/native/dependencies.json` has empty hashes; audited native binaries and license review are needed before activation.
- Windows 11 visual/device validation is not recorded; latest host evidence is Windows 10 22H2.
- Visual Studio Designer open/save/reopen round-trip is not recorded because Visual Studio was unavailable in the recorded host. Static Designer coverage is not a substitute for this check.
- Trusted Authenticode signing is blocked on a trusted signing certificate. Production licence key/issuing service and purchase URL are not configured. Live Steamworks ownership integration is blocked on eligibility, SDK and partner access; current signed-entitlement behavior is not a live Steam ownership call.
- Gate G3: the receiver now serves a **real browser sender** (`/`, `/send`, ticket URL, `/sender.js`) with
  explicit file selection, pairing, per-file incremental SHA-256, approval, receipts, cancellation,
  rotation and collision-safe storage. Verified by 5 loopback C# tests, Node digest/path tests and a Chrome
  end-to-end run on this PC (pair, 64 KiB HEIC, receipt hash, collision rename, bytes equal). A physical
  iPhone/Android browser run is still pending (KI-12); do not call the physical sender complete.
- Other recorded open items include cross-device disappearance hardware drill, iPhone lock/trust revocation, Narrator/NVDA audit, broader Android/iOS models, and the WPD two-process contention/possible single-instance decision. Re-open `docs/IMPLEMENTATION-GAP-MATRIX.md`, `docs/KNOWN_ISSUES.md`, and `docs/DEVICE_TEST_MATRIX.md`; continue every safe, unblocked requirement rather than assuming the two manual drills are the only remaining requirements.

## Exact resume procedure

1. In the Windows workspace, fetch `origin`, confirm it maps to `https://github.com/shambrain/I-Total.Transfer-1.8.0.git`, and inspect `git status --short`, current branch, local HEAD, and origin branch tip. Preserve any local changes; do not reset or clean them. The GitHub documentation follow-ups after `9ae20fdd` must be present before continuing.
2. Read this file, the full master directive, and the full execution instruction. Compare the state against the actual code, manifests, test output, and artifact files; update any stale claim with evidence rather than copying it forward.
3. Run the Release build and full deterministic suite locally on the synced source. Record the command, raw outcome, count, elapsed time, and test log. Resolve a new failure or preserve its full failure output; do not infer the result from an earlier run.
4. Reconcile README / `PROJECT-STATUS.md` / `docs/PROGRESS.md` / `docs/KNOWN_ISSUES.md` / `docs/DEVICE_TEST_MATRIX.md` / `docs/DEVICE-COMPATIBILITY-MATRIX.md` / `docs/IMPLEMENTATION-GAP-MATRIX.md` / `dist/SHA256SUMS.txt` so statuses and test counts agree. Do not edit historical records to make old runs appear to have current counts.
5. Continue unblocked engineering and regression work from the master directive. Then perform current-candidate AN06 and genuine IP07/AN07 only when the operator is physically available to change USB mode/pull the cable. Record both outcomes—even failures—with raw evidence.
6. After code or package changes, rebuild, run tests, verify hashes, inspect the diff, and checkpoint without destructive Git operations. Do not reuse artifact hashes if the artifact changed.
7. Before a session/tool limit, update this file with the exact completed step, last command and result, changed paths/commit, remaining blocker, and one next concrete action.

## Single next concrete action

Sync the current `work/master-directive` branch in the recorded Windows workspace and verify that the README and KI-26 documentation follow-ups are present; then run `git status`, the Release build, and the full test suite and record the actual raw results. Only after that baseline is confirmed proceed with remaining safe unblocked gaps. Physical AN06 candidate revalidation and genuine mid-file IP07/AN07 remain pending operator actions.
