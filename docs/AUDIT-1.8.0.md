# Audit 1.8.0 — repository, baseline and defects

Date: 2026-10-29. Author: engineering session driven by
`I-Total-Transfer-OpenCode-Master-Directive.md` and `I-Total-Transfer-OpenCode-Execution-Instruction.md`.

This is the Gate A record. It states what was inspected, what was executed, what was found, and what
remains unverified. Every number below came from this workspace on the date shown; nothing is inherited
from an earlier session's claim without saying so.

## 1. Repository state at the start of the audit

| Item | Value |
| --- | --- |
| Repository root | `C:\Users\Administrator\Documents\GitHub\I-Total.Transfer-v1.5.0-source` |
| Branch | `main` (feature branch `work/master-directive` created after inspection) |
| HEAD | `17c0b1adff502ffa4b70fa83e19dff8f8e782676` |
| Tags / releases | none |
| Submodules | none |
| CI workflows | none (`.github/workflows` absent) |
| Working tree | clean except four untracked specification inputs: the two directive files and the two proposed-diagram JPEGs |
| Remote | `https://github.com/shambrain/I-Total.Transfer-1.8.0.git` (private) |
| Solution | `I-Total.Transfer.sln` — one app project, one test project |
| Source / test files | 89 `.cs` in `src`, 55 `.cs` in `tests` (excluding `bin`/`obj`) |

The directory name still says `v1.5.0-source`; the product and repository are 1.8.0. The folder was left
unchanged so existing paths and scripts keep working.

## 2. Environment

| Item | Value |
| --- | --- |
| OS | Microsoft Windows 10 Pro, build 19045 (22H2), 64-bit |
| .NET SDK | 10.0.302 (also installed: 8.0.423, 10.0.201, 11.0.100-rc.2) |
| Target framework | `net10.0-windows` |
| CPU / RAM | AMD Ryzen 5 7600X / 31.1 GB |
| Display | `\\.\DISPLAY27` 2293x960 at the host's 150 % scaling — no 100 % display is available |
| Phones | none attached during this audit |

## 3. Baseline build and test (before any change)

Commands and results:

```text
dotnet build I-Total.Transfer.sln -c Release --no-incremental
  -> Build succeeded, 0 Warning(s), 0 Error(s), 6.27 s

dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s
  -> Passed! Failed: 0, Passed: 460, Skipped: 2, Total: 462, Duration: 32 s
```

The two skips are the documented Shell apartment tests (`ShellBackendAnswersFromAnMtaThreadInsteadOfHanging`,
`ShellBackendProbesAndDisposesCleanly`), which cannot run in this harness — recorded as skips, not passes.

Artifacts present at baseline (hashes from `dist/SHA256SUMS.txt`, re-checked against the files):

| Artifact | SHA-256 |
| --- | --- |
| `publish/I-Total.Transfer.exe` (112,158,534 B) | `8FCB2B5095365995813AF028EFE4A27E6EC538A32ADEA12F153919AD0E61CABB` |
| `dist/I-Total.Transfer-1.8.0-win-x64.zip` | `0E41DEC310CEFAD3FD71C5181407B236E14F52F9E3684D4FE7F1C951F7513374` |
| `dist/installer/I-Total.Transfer-1.8.0-portable-win-x64.zip` | `21742374A48BC0FA6A7A5DDD3291CD87A8BC2ED472300F5DFE08628F53C21653` |
| `dist/installer/I-Total.Transfer-1.8.0-setup.exe` | `2E34EED05F69B04AD019C6E1641CB846BC7B08D6BE3C44BCE644314B49AEBC76` |
| `dist/I-Total.Transfer-1.8.0-source.zip` | `3412D03B111CCF6CD5740A1AB011F18E92198EB45AC58059A67F2C8D3F112BE7` |

These are the known-good 1.8.0 artifacts and stay recoverable. Any candidate built in this session is
separate until it passes the same build/test gates.

## 4. What the code actually does (traced, not assumed)

| Area | Entry point | Observed behaviour |
| --- | --- | --- |
| Scan | `TransferService.ScanCoreAsync` → `ITransferBackend.Scan` | Backend performs the walk on a worker thread; result carries `EnumerationReport` with per-folder outcomes. |
| Plan | `TransferService.CopyCoreAsync` | Refuses `Failed`/`Empty` scans, refuses duplicate destination paths, applies `FilterSpec`, checks free space, writes `RUNNING` into the manifest, opens SQLite, then copies. |
| Copy | `TransferService.CopyCoreAsync` loop → `ITransferBackend.CopyToStage` | Stages into `.i-total/staging`, validates size when known, commits with `File.Move`, records destination SHA-256, classifies verification, backs up an overwritten file until the new bytes verify. |
| Verify | `TransferService.VerifyCoreAsync` | Size check, saved-digest check, optional live source re-hash, repair path with staged hash check; writes `I-total-verification.json`. |
| Persistence | `ManifestStore` (JSON) + `SessionDatabase` (SQLite) | JSON manifest is the recovery plan; SQLite holds sessions/files/events. JSON survives SQLite failure by design. |
| Completion | `CopyCoreAsync` tail | Recomputed from manifest states, enumeration status and per-run counters; incomplete enumeration forces an incomplete verdict. |
| UI | `MainForm` + `UI/Pages/*` with `.Designer.cs` per page | Designer files exist for all seven pages; controls are wired in code-behind. |
| Licensing | `LicenseService` gated in `MainForm` before a new job | Resume is not gated; a licence check does not run mid-transfer. |

## 5. Defects found in this audit

Severity: **S1** data loss or false success, **S2** false reporting or broken recovery, **S3** usability or
maintainability. Each entry states evidence and what was done.

### D1 (S2) — zero-byte files and unreadable sizes were the same value

`MediaFile.Size` was a `long` with no way to say "not measured". All three backends did:

```csharp
int unknownSize = files.Count(f => f.Size <= 0);
ZeroByteCount: files.Count(f => f.Size == 0)
```

so every genuine zero-byte file was counted as *both* zero-byte and unknown-size, and the engine treated it
as unverifiable (`CopiedUnverified`). A camera roll containing only empty placeholders could never report a
verified complete backup, and the session report could not distinguish "0 bytes" from "size not reported".
The directive forbids exactly this coercion (Part 3: "Unknown sizes remain unknown; never coerce unknown to
zero").

**Fixed** in this session: `MediaFile.SizeKnown`, `TransferEntry.SizeKnown`, `ScanAccounting`, backend
parsers that report whether the size property was actually read, engine and verification paths that treat a
known 0 as measurable and an unknown size as unmeasurable. Tests: `EnumerationAccountingTests` (9) and three
engine tests. Full suite after the fix: 472 passed, 2 skipped, 474 total.

### D2 (S2) — Shell and AFC classified completeness differently from WPD

WPD passed the unknown-size count into `TransferRules.ClassifyEnumeration`; Shell and AFC did not, so a
listing whose sizes were all unreadable could be `Complete` on two of the three backends. **Fixed** by
building every report through `ScanAccounting.BuildReport`, which applies one rule everywhere. Test:
`AListingOfEmptyPlaceholdersIsCompleteButAListingOfUnmeasurableFilesIsNot`.

### D3 (S1) — resuming an unknown-size file could mark a verified copy as a collision

`ClassifyExistingDestination` only accepted a match when `sourceSize > 0`, and the engine only hashed the
existing destination when the sizes matched. A file with a verified source-to-destination hash but an
unreadable source size was therefore reclassified `PreserveCollision` → `Failed` on the next run, even
though its bytes were unchanged. **Fixed**: a recorded digest that still matches the destination is now
accepted as `SkipVerified` when the source size is unknown, and the engine hashes the destination in that
case. Tests: `AMatchingRecordedDigestResumesAnUnknownSizeFileWithoutCallingItACollision`,
`AnUnmeasuredSourceSizeStaysVisibleAndResumesWithoutBeingCalledACollision`.

### D4 (S2) — SQLite does not persist hashes, verification level or byte counts

`SessionDatabase.Files` stores `Destination, RelativePath, ExpectedBytes, State, Attempts, Error,
UpdatedUtc` only. The hashes and verification grade live in the JSON manifest; a report or recovery path
that reads SQLite alone cannot state what was verified. The directive (Part 12) requires transfer items to
persist source/destination hashes and verification level. **Open** — scheduled as the next implementation
phase (schema migration + write path + round-trip and migration tests).

### D5 (S3) — permanent backend errors are retried like transient ones

`CopyCoreAsync` retries any failure up to `maxRetries` with linear backoff. Access-denied, unsupported
resource and missing-object failures are permanent; retrying them costs time and produces misleading
"attempt 2/3" logs. `CopyResult` has no error classification. **Fixed** — `CopyFailures` classifies exceptions and AFC statuses; `CopyResult.FailureKind` carries the verdict; the engine stops retrying a permanent failure. Test: `APermanentFailureIsNotRetriedButATransientOneIs`.

### D6 (S2) — the stability pass compares only path and size

`ScanStability.ChangedPaths` indexes `file.Size`. A same-size content change between the two passes is
invisible, and two passes that both failed to read a size "agree". Timestamps are available in
`MediaFile.ModifiedUtc` but not compared. **Fixed** — `ScanStability` compares size, size-known and modification time (when both passes report one); three tests added.

### D7 (S3) — stale documentation

`docs/KNOWN_ISSUES.md` KI-22 still says the 1.1 GB test dumps remain in history; the history was rewritten
on 2026-10-29 and the largest reachable blob is 44.9 MB. The directive names
`docs/DEVICE-TEST-MATRIX.md`, while the repository uses `docs/DEVICE_TEST_MATRIX.md`; the compatibility
matrix added by this session reconciles both names. **Fixed for KI-22 in this session**; the filename
difference is recorded, not renamed, to avoid breaking links.

### D8 (S2) — the session report has no reconciliation section

`ExportReports` writes the manifest entries and a CSV of them, but not the Part 3 reconciliation totals
(discovered, eligible, excluded, already present, copied, source-hash verified, destination-only, size-only,
unverifiable, inaccessible, failed, outstanding, unknown-size counts/bytes). **Fixed** — `ReconciliationReport` counts every scanned object once by outcome, `TransferSummary.Reconciliation` exposes it, and the session report writes it to JSON and `I-total-reconciliation.csv`.

## 6. Strengths confirmed by inspection

- The engine refuses to start on `Failed`/`Empty` scans, refuses duplicate destination paths before writing,
  and forces an incomplete verdict when the scan was incomplete.
- Overwrite is explicit and reversible: the previous destination moves to `.i-total/backup` and is restored
  if the new bytes fail verification.
- Verification distinguishes `WrongSize` from `WrongHash`; a corrupted destination is recorded `Failed`, and
  a repair whose bytes fail their hash is discarded.
- Source-change detection uses size and modification time; `SourceChanged` is persisted and blocks a silent
  "already present" decision under `PreserveAndReport`.
- The WPD backend recurses every reachable container, accepts both observed iPhone layouts
  (`CameraRoll.Decide`), reports per-folder failures as `Incomplete`, aborts after 20 consecutive unreadable
  objects (cable pulled), and releases COM interfaces through `ResetSession`/`ReleaseCom`.
- The UI has real Designer files for all seven pages, and the licence gate runs before a new job, never
  during a transfer.

## 7. Not verified in this audit (explicit)

| Claim | Status |
| --- | --- |
| Physical iPhone/Android behaviour of the new size handling | NOT TESTED — no phone attached |
| Windows 11 | NOT TESTED — host is Windows 10 22H2 |
| AFC backend | BLOCKED — `native/dependencies.json` ships empty hashes, backend stays inert by design |
| Trusted code signing | BLOCKED — no certificate |
| Steam ownership | BLOCKED — no Steamworks SDK/partner account |
| Wireless sender on a real iPhone | BLOCKED — no physical sender drill |
| Performance effect of this session's changes | NOT MEASURED — no benchmark run yet; the changes do not touch the copy loop's buffer path |
