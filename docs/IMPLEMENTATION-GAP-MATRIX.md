# Implementation gap matrix — 1.8.0

Single requirement ledger for the master directive. Every row names the requirement, where it lives in the
code, what evidence exists, and the honest status. Statuses are limited to: `NOT STARTED`, `IN PROGRESS`,
`IMPLEMENTED — NOT VERIFIED`, `AUTOMATED TEST VERIFIED`, `PHYSICAL DEVICE VERIFIED`, `BLOCKED`,
`NOT APPLICABLE`.

Evidence classes: **AT** current automated test, **SIM** simulated/mocked integration, **STATIC** code
inspection in this workspace, **LIVE** physical-device record already documented in
`docs/DEVICE_TEST_MATRIX.md` (not re-run in this session), **NONE** not tested.

Baseline: `main` at `17c0b1a`, build 0/0, 460 passed / 2 skipped / 462. After the first implementation
phase (D1–D3): 472 passed / 2 skipped / 474.

## Gate A — baseline and audit

| ID | Requirement | Where | Evidence | Status | Next action |
| --- | --- | --- | --- | --- | --- |
| A1 | Record branch, HEAD, tree, tags, submodules, CI | `docs/AUDIT-1.8.0.md` §1 | STATIC | AUTOMATED TEST VERIFIED (recorded) | keep current each session |
| A2 | Record environment and baseline build/tests | `docs/AUDIT-1.8.0.md` §2–3 | build 0/0; 460/2/462 | AUTOMATED TEST VERIFIED | re-run per phase |
| A3 | Reconcile contradictory totals across docs/CI | audit §3, `docs/BUILD-VALIDATION.md` | 462 total matches the recorded suite | AUTOMATED TEST VERIFIED | update on every suite change |
| A4 | Preserve the known-good release | `publish/`, `dist/`, `dist/SHA256SUMS.txt` | hashes in audit §3 | AUTOMATED TEST VERIFIED | never replace until gates pass |
| A5 | Feature branch from the verified baseline | `work/master-directive` | `git branch` | AUTOMATED TEST VERIFIED | checkpoint commits per phase |
| A6 | Audit outputs (`AUDIT`, gap matrix, device matrix, engine design, research, build/performance/release docs) | `docs/` | this file + siblings | IN PROGRESS | finish research/build/perf/release refresh at Gate H |

## Gate B — correctness and inventory

| ID | Requirement | Where | Evidence | Status | Next action |
| --- | --- | --- | --- | --- | --- |
| B1 | Distinguish complete/incomplete/empty/failed enumeration | `TransferRules.ClassifyEnumeration`, `ScanAccounting.BuildReport` | AT `EnumerationAccountingTests`, `FilterAndConflictTests` | AUTOMATED TEST VERIFIED | — |
| B2 | Never report a partial scan as complete | `WpdDevice.EnumerateRecursive` folder outcomes; `TransferService` completion | AT recovery tests; STATIC | AUTOMATED TEST VERIFIED | live unplug drill still pending (B9) |
| B3 | Account for every object: copied, skipped, failed, inaccessible, excluded | `TransferService.CopyCoreAsync` counters + manifest | AT `TransferEngineTests`, `RecoveryFlowTests` | AUTOMATED TEST VERIFIED | reconciliation report (D8) |
| B4 | Zero-byte file is a measurement; unreadable size is unknown | `MediaFile.SizeKnown`, `ScanAccounting`, backends | AT 9 accounting tests + 3 engine tests | AUTOMATED TEST VERIFIED | live confirmation on a phone |
| B5 | Recursive enumeration of every reachable container | `WpdDevice.EnumerateRecursive`, `ShellIPhoneDevice.ScanFolder`, `AfcDevice.EnumerateFolder` | STATIC; LIVE Android 4,103-file walk (2026-10-22) | PHYSICAL DEVICE VERIFIED (Android) | iPhone re-run after next publish |
| B6 | Both iPhone layouts (DCIM container and albums at storage root) | `CameraRoll.Decide`, `WpdDevice.ResolveMediaRootObjectId` | AT `CameraRollTests`; LIVE iPhone 13 Pro (2026-10-22) | PHYSICAL DEVICE VERIFIED | — |
| B7 | Duplicate destination paths are detected and refused | `EnsureUniqueSourcePaths`, `ClassifyEnumeration` duplicates | AT duplicate tests | AUTOMATED TEST VERIFIED | — |
| B8 | Stable session identity; a different phone is refused | `ManifestStore.SourceIdentityProblem`, `DeviceSelectionGuard`, `ScanResult.BelongsTo` | AT `DeviceSelectionTests`, recovery tests | AUTOMATED TEST VERIFIED | physical disappearance drill (IP09/BT08) |
| B9 | Unplug mid-scan produces Incomplete and no cache | `ScanCacheStore`, WPD folder outcomes | AT simulated; LIVE iPhone (2026-10-24) | PHYSICAL DEVICE VERIFIED (historical) | re-run after publish |
| B10 | iCloud/cloud-only scope warning, no false coverage claim | `WpdDevice` probe messages, README | STATIC; LIVE message observed | IMPLEMENTED — NOT VERIFIED | needs an Optimize-Storage device |
| B11 | Scan stability detects changes between two passes | `ScanStability.ChangedPaths` (path, size, size-known, timestamps) | AT `ScanStabilityTests` (10) | AUTOMATED TEST VERIFIED | — |
| B13 | A run with nothing to copy is reported as a no-op, not a fresh verified copy | `CopyCoreAsync` verdict, `TransferSummary.AlreadyPresent` | AT `ARerunWhereEverythingIsAlreadyPresentIsANoOpNotAFreshVerifiedCopy` | AUTOMATED TEST VERIFIED | — || B12 | Enumeration duration and items/second recorded | `ScanProgressReporter`, scan log line | STATIC; LIVE 55,185 files / 23.3 min | PHYSICAL DEVICE VERIFIED (historical) | — |

## Gate C — planner, persistence and recovery

| ID | Requirement | Where | Evidence | Status | Next action |
| --- | --- | --- | --- | --- | --- |
| C1 | Persistent queue with explicit states | `SessionManifest`, `TransferEntry.State`, `SessionDatabase` | AT `RecoveryFlowTests`, `SessionRecoveryTests` | AUTOMATED TEST VERIFIED | — |
| C2 | SQLite persists hashes, verification grade, byte counts | `SessionDatabase` (columns + idempotent migration), `QueryFileStates` | AT `SessionDatabaseTests` (4) | AUTOMATED TEST VERIFIED | — |
| C3 | Free-space preflight with reserve | `CopyCoreAsync` (`AvailableFreeSpace`) | AT insufficient-space test | AUTOMATED TEST VERIFIED | — |
| C4 | Collision policy never overwrites unrelated content by default | `TransferRules.ClassifyExistingDestination`, `HandleExistingAsync` | AT conflict tests | AUTOMATED TEST VERIFIED | — |
| C5 | Overwrite is reversible until the new bytes verify | `RestoreReplacedTarget`, `.i-total/backup` | AT `AFailedOverwriteRestoresTheOriginalDestination` | AUTOMATED TEST VERIFIED | — |
| C6 | Bounded retries with backoff; permanent errors not retried | `CopyFailures`, `CopyResult.FailureKind`, `CopyCoreAsync` attempt loop | AT `APermanentFailureIsNotRetriedButATransientOneIs` | AUTOMATED TEST VERIFIED | — |
| C7 | Crash/kill recovery reconciles DB with files | `SessionRecovery`, `ManifestStore.LoadOrCreate` | AT recovery suite | AUTOMATED TEST VERIFIED | SQLite hashes (C2) |
| C8 | Resume only when source identity and destination state reconcile | `SourceIdentityProblem`, `HandleExistingAsync` | AT | AUTOMATED TEST VERIFIED | — |
| C9 | Unknown-size resume does not become a collision | `ClassifyExistingDestination`, digest-first rule | AT `EnumerationAccountingTests` | AUTOMATED TEST VERIFIED | — |
| C11 | A run continues on the JSON manifest when SQLite cannot be opened | `TryOpenDatabase`, `ManifestStore` | AT `ATransferStillCompletesWhenTheSqliteDatabaseCannotBeOpened` | AUTOMATED TEST VERIFIED | — |
| C10 | Source is never deleted | all backends | STATIC (no delete API on source paths) | AUTOMATED TEST VERIFIED | — |

## Gate D — integrity and audit

| ID | Requirement | Where | Evidence | Status | Next action |
| --- | --- | --- | --- | --- | --- |
| D1 | Source bytes hashed while streaming; never invented | `WpdDevice.CopyToStage`, `AfcDevice.CopyToStage` | AT `SourceHashingBackendSkipsTheRedundantStagedReadBack`; LIVE hash equality | PHYSICAL DEVICE VERIFIED (Android) | — |
| D2 | Independent staged/destination verification and commit gating | `CopyCoreAsync` commit path | AT corruption tests | AUTOMATED TEST VERIFIED | — |
| D3 | Honest verification labels (`SourceAndDestinationHash`, `DestinationHash`, `SizeOnly`, `None`) | `TransferRules.ClassifyVerification`, `VerificationLevel` | AT `ReconciliationTests` | AUTOMATED TEST VERIFIED | — |
| D4 | Verification separates wrong size from wrong hash; corruption marks Failed | `VerifyCoreAsync` | AT verification suite | AUTOMATED TEST VERIFIED | — |
| D5 | Repair verifies repaired bytes and discards a corrupt repair | `VerifyCoreAsync` repair branch | AT `RepairingWithCorruptBytesIsReportedAndDiscarded` | AUTOMATED TEST VERIFIED | — |
| D6 | Checked arithmetic for byte totals | `ScanAccounting.Of` (`checked`), `Enumerable.Sum` | STATIC + AT | AUTOMATED TEST VERIFIED | extend to report totals (D8) |
| D7 | JSON/CSV report with reconciliation counts and bytes | `ReconciliationReport`, `ExportReports` (JSON + `I-total-reconciliation.csv`) | AT `ReconciliationReportTests` (5) | AUTOMATED TEST VERIFIED | — |
| D8 | Unknown size stays unknown in reports and totals | `TransferEntry.SizeKnown/BytesWritten`, `ScanAccounting` | AT engine tests | AUTOMATED TEST VERIFIED | include in reconciliation report |
| D11 | Verification verdict is durable in SQLite, not only JSON | `PersistVerification` | AT `ACorruptedDestinationIsPersistedAsFailedInSqliteAsWellAsJson` | AUTOMATED TEST VERIFIED | — || D9 | Unicode, long paths, reserved names | `TransferRules.ValidateRelativePath/SanitizeSegment` | AT reserved-name and traversal tests | AUTOMATED TEST VERIFIED | long-path (>260) drill not run |
| D10 | Zero-byte files verify | engine + `EnumerationAccountingTests` | AT | AUTOMATED TEST VERIFIED | live confirmation |

## Gate E — device backends and preflight

| ID | Requirement | Where | Evidence | Status | Next action |
| --- | --- | --- | --- | --- | --- |
| E1 | WPD device manager + real interfaces, connected vs remembered | `WpdDevice.Probe`, `ListDevices` | STATIC; LIVE both phones | PHYSICAL DEVICE VERIFIED (historical) | — |
| E2 | Opaque, session-scoped object IDs; re-resolve after reconnect | `ResolveObjectId`, `_nameIndex.Clear()` | STATIC | IMPLEMENTED — NOT VERIFIED | reconnect drill |
| E3 | Deterministic COM release on success/cancel/exception | `ResetSession`, `ReleaseCom`; enumerator/keys/values/manager released in `finally` | STATIC — five leaks found and fixed | IMPLEMENTED — NOT VERIFIED | no phone to measure handle counts |
| E4 | Device-supported buffer size / bounded default | `OpenStream(out optimalBufferSize)`, `_copyBufferBytes` | STATIC; AT buffer clamp | AUTOMATED TEST VERIFIED | benchmark sweep at Gate H |
| E5 | Short reads, EOF, unknown size, source disappearance handled | `CopyToStage` read loop; engine size rules | AT partial-read/unknown-size tests | AUTOMATED TEST VERIFIED | live pull drill |
| E6 | Android volumes walked, private trees skipped, PTP warning | `WpdDevice.Scan` Android branch, `PortableStorage` | AT `PortableStorageTests`; LIVE | PHYSICAL DEVICE VERIFIED (Android) | — |
| E7 | AFC inert unless pinned, hashed, verified | `DependencyVerifier`, `AfcDevice.RuntimeAvailable` | AT dependency tests | AUTOMATED TEST VERIFIED | BLOCKED: no audited binaries |
| E8 | Preflight states (locked, untrusted, media unavailable, charging-only…) | `WpdDevice.Probe`, `ConnectionDiagnostics` | AT diagnostics tests; LIVE PTP/lock | PHYSICAL DEVICE VERIFIED (partial) | iPhone lock drill (IP08) |
| E9 | Driver detection/repair route without silent installs | `ConnectionDiagnostics`, Settings text | STATIC | IMPLEMENTED — NOT VERIFIED | clean-machine drill |
| E10 | Windows-to-device only where backend supports it | `CanWriteToDevice`, AFC path | AT capability tests | AUTOMATED TEST VERIFIED | live AFC write BLOCKED |

## Gate F — Designer UI

| ID | Requirement | Where | Evidence | Status | Next action |
| --- | --- | --- | --- | --- | --- |
| F1 | Every page genuinely Designer-authored | `UI/Pages/*.Designer.cs` (7 pages), `MainForm.Designer.cs` | STATIC: all pages have Designer files | IMPLEMENTED — NOT VERIFIED | open/save/reopen in Visual Studio (not installed here) |
| F2 | No runtime `BuildLayout()` visual tree | page constructors | STATIC: layout in Designer files | AUTOMATED TEST VERIFIED (static) | — |
| F3 | DPI 100/125/150/200 and minimum/normal/wide sizes | theme/layout code, 125 screenshots | 96/150/200 renders; LIVE 150 % | PHYSICAL DEVICE VERIFIED (150 %) | 125 % render not captured |
| F4 | Green dot only when media is accessible; distinct states | `StatusBlip`, `DeviceStates` | AT `DeviceStateTests` | AUTOMATED TEST VERIFIED | — |
| F5 | Keyboard/focus/accessible names | `UI/Accessibility.cs`, `MainForm` | AT `EveryInteractiveControlOnEveryPageGetsAnAccessibleName`, `AnUnnamedTextBoxFallsBackToItsDesignerName` | AUTOMATED TEST VERIFIED (names) | focus order / Narrator audit (KI-24) |
| F7 | Real editable FigCraft artifacts mapped to app screens | `designs/i-total-transfer-design-system.figcraft`, `docs/FIGCRAFT-ARTIFACTS.md`, `docs/figcraft/` | Artifact opened, edited, saved and exported on 2026-10-29 | IMPLEMENTED — NOT VERIFIED (pixel parity) | runtime is the source of truth; no parity assertion | 
| F6 | Every control acts, is disabled with a reason, or is documented | UI audit 2026-10-22 | LIVE audit record | PHYSICAL DEVICE VERIFIED (historical) | re-audit after UI edits |

## Gate G — wireless

| ID | Requirement | Where | Evidence | Status | Next action |
| --- | --- | --- | --- | --- | --- |
| G1 | Authenticated receiver, single-use pairing, approvals, limits | `WirelessReceiver`, `PairingState` | AT wireless suite; LIVE loopback/LAN drill (2026-10-22) | AUTOMATED TEST VERIFIED | — |
| G2 | Path traversal, spoofed names, oversize requests rejected | `WirelessReceiver` | AT | AUTOMATED TEST VERIFIED | — |
| G3 | Real iOS/Android sender end to end | not in repository | none | NOT STARTED | needs a sender application; receiver-only is not wireless transfer |
| G4 | No firewall rule, private interface binding | `PrivateNetwork`, installer | AT private-network tests; STATIC | AUTOMATED TEST VERIFIED | — |

## Gate H — performance, compatibility, release

| ID | Requirement | Where | Evidence | Status | Next action |
| --- | --- | --- | --- | --- | --- |
| H1 | Measured buffer sweep / throughput with context | `docs/PERFORMANCE-AUDIT.md`, benchmark tests | LIVE single-file and 30-file observations; AT synthetic benchmark only | IMPLEMENTED — NOT VERIFIED | repeat measured sweep on hardware |
| H2 | Windows 10 supported; Windows 11 tested | README, installer | Windows 10 22H2 only | BLOCKED | needs a Windows 11 machine |
| H3 | Installer clean install/upgrade/uninstall, user data kept | `installer/`, `Build-Installer.ps1` | LIVE drills recorded | PHYSICAL DEVICE VERIFIED (host) | re-run after artifact change |
| H4 | Code signing | `installer/Sign-Artifacts.ps1` | fail-closed, self-signed drill only | BLOCKED | trusted certificate |
| H5 | Licensing: trial, entitlement, revocation, resume-after-exhaustion | `Licensing/*` | AT licensing suite | AUTOMATED TEST VERIFIED | production key pair BLOCKED |
| H6 | Steam ownership | `SteamEntitlementProvider` (signed file) | AT binding/expiry | BLOCKED | Steamworks SDK/partner account |
| H7 | Published claims map to evidence | README, `PROJECT-STATUS.md` | this matrix | IN PROGRESS | refresh at Gate H |

## Open defects carried in this ledger

| ID | Defect | Status | Next action |
| --- | --- | --- | --- |
| D4 | SQLite lacked hash/verification columns | AUTOMATED TEST VERIFIED | closed by `SessionDatabase` migration + `SessionDatabaseTests` |
| D5 | Permanent errors retried | AUTOMATED TEST VERIFIED | closed by `CopyFailures` + retry test |
| D6 | Stability pass ignored timestamps | AUTOMATED TEST VERIFIED | closed by `ScanStability` stamp comparison |
| D8 | Report lacked reconciliation totals | AUTOMATED TEST VERIFIED | closed by `ReconciliationReport` + JSON/CSV tests |
