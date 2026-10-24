# Release acceptance checklist

Audited **2026-10-19** against version **1.8.0** (unreleased) on the build machine: Windows 10 build 19045,
.NET 10, display scaling 150% (DPI 144). Every line below is either ticked **with the evidence that proves it
named**, or left unticked and labelled with exactly what it is waiting for. Nothing is ticked on the strength
of "it should work" — where the only evidence is a mock, a mock, or a code inspection, that is said so.

**Legend** — ✅ proven (evidence named) · 🟡 proven in part, or proven only against a mock / by inspection ·
⛔ blocked on the handset (nothing attached at audit time) · ❌ not implemented.

## Correctness

- [x] **Empty scan cannot start a backup.** ✅
  `TransferEngineTests.EmptyScanIsRejectedRatherThanReportedComplete`,
  `TransferRulesTests.EmptyPlanCannotBeComplete`, `CompletionRequiresNonEmptyPlanAndAllItemsProcessed`.
- [x] **Any enumeration failure blocks a "complete scan" state.** ✅
  `FilterAndConflictTests.EnumerationClassificationDistinguishesEmptyIncompleteAndComplete`; all three backends
  feed their folder failures and duplicate count through `TransferRules.ClassifyEnumeration`
  (`ShellIPhoneDevice.cs:180`, `WpdDevice.cs:151`, `AfcDevice.cs:132`), and `ScanResult.IsComplete` is derived
  from it, so a partial walk can never be presented as complete.
- [x] **Nested folders are recursively enumerated and mapped to safe destination paths.** ✅
  `TransferRulesTests.NestedMediaFolderIsPreservedSafely`, `NestedTraversalIsRejected`, `TraversalFilenameIsRejected`,
  `AbsoluteSourceFolderIsRejected`; `LargeLibraryStressTests.ScanningASingleFolderOfTwentySixThousandEntriesCompletesAndReportsProgress`;
  WPD walks the object tree in `EnumerateRecursive`.
- [x] **No source object/path is silently deduplicated; duplicate destination paths are reported.** ✅
  Every backend names the duplicate paths in a warning and classifies the scan **Incomplete**
  (`ShellIPhoneDevice.cs:170`, `WpdDevice.cs:145`, `AfcDevice.cs:129`); `ManifestStore.LoadOrCreate` refuses a
  manifest that already contains a duplicate and preserves it as evidence. *Residual, stated plainly:* if the
  operator chooses to copy a **warned** partial listing, the manifest keeps one entry per path — the duplicate
  was named before that decision was taken, so it is not silent, but the engine does not re-ask.
- [x] **Existing destination files are preserved by default; same-size files are labelled size-matched only.** ✅
  `FilterAndConflictTests.PreservePolicyNeverRenames`, `ExistingDestinationClassificationIsConservative`;
  `ReconciliationTests.NoSavedHash_WithMatchingSize_RecordsBaseline`, `SizeMatches_WithoutAnyHash_IsOnlySizeOnly`;
  `TransferEngineTests.ExistingCollisionIsPreservedAndReportedFailed`.
- [x] **Partial files are never finalized before stream closure and verification.** ✅
  The copy writes `<name>.partial`, checks the streamed length against the scan, flushes, then moves it into
  place (`WpdDevice.CopyToStage`); `RecoveryFlowTests.StoppingATransferRecordsWhatFinishedAndWhatDidNot` shows
  the destination holds only what was verified.
- [x] **Cancellation, crash and USB disconnect persist an incomplete state and permit safe resume.** ✅ *(disconnect proven in part)*
  `RecoveryFlowTests` (6) and `SessionRecoveryTests` (14): the session is stamped `RUNNING` before the first
  byte, `CANCELLED` on Stop and `ABORTED` on a fault, and a stopped session still reaches a verified-complete
  result. A backend fault stands in for a cable pull 🟡; the real pull is ⛔ (needs the handset).
- [x] **No session is complete if any planned file is failed, inaccessible or unverifiable.** ✅
  `TransferRulesTests.CompletionFailsIfAnyItemFailed`, `CompletionFailsIfAnyItemWasInaccessible`;
  `RecoveryFlowTests.ARecordedEntryThatCanNeverBeCopiedKeepsTheSessionFromClaimingCompletion`. A continued run
  takes its verdict from the **whole** manifest, not just its own work.
- [x] **The app distinguishes "all exposed items copied" from "entire Photos library backed up".** 🟡 *(wording; not machine-checked)*
  The enumeration report is a self-describing "exposed" count, the group taglines say "exposed through DCIM",
  the Overview reads "Total exposed media size", `ConnectionDiagnostics` labels iCloud originals **MANUAL CHECK**,
  and `README.md` states that a scan is not proof that cloud-only originals were downloaded. There is no test
  that can assert a sentence, so this stays half-ticked rather than claimed.

## Performance

- [x] **No per-file O(n) manifest search.** ✅
  The copy loop works from `ManifestStore.Index()` (a dictionary keyed by relative path); `ManifestStore.Find`
  is a linear scan but has no caller in the engine. Measured: `EngineThroughputBenchmarkTests` sustains
  **660 files/s** over 4,000 small files with source-and-destination hashing.
- [x] **No per-file full-folder enumeration on the normal source lookup path.** ✅ *(live path)* / ⛔ *(resume path, device effect)*
  A live copy opens the object id the scan recorded — one `GetStream`, no walk (`WpdDevice.OpenSourceStream`).
  The **resume** path cannot use those ids (they do not survive a reconnect), so it resolves each file by path,
  and that lookup used to re-walk the album and read a property per sibling **for every file** — quadratic in
  the album. `FolderNameIndex` now reads each folder once per session and answers from memory;
  `FolderNameIndexTests` pins the property directly (26,100 files across three albums cost **4** folder reads,
  not 26,100). The device-side gain still needs the drill ⛔.
- [x] **GUI update cadence is bounded; logs do not flush for every buffer read.** ✅
  `ThrottledProgressTests` (5); measured — the engine emits 4,000 updates for 4,000 files and the UI receives
  **21** (`EngineThroughputBenchmarkTests.ProgressReportingIsBoundedRatherThanOneUpdatePerFile`). The log keeps
  one writer open, appends a line per event and decides rollover from a byte count, so no buffer read touches it.
- [ ] **Benchmark 64 KiB, 256 KiB, 1 MiB and 4 MiB buffers on actual devices.** ⛔
  No handset was attached. What is proven locally: the setting reaches the copy path and is clamped
  (`SettingsTests.TransferOptionsBufferSizeIsClamped`; `WpdDevice` clamps to 64 KiB–4 MiB), and the engine
  sustains **587.8 MiB/s** over large files on this machine's own disk (engine-side only, never presented as
  a phone figure).
- [ ] **Record device/iOS/Windows/USB/destination context for benchmark results.** 🟡
  Engine benchmarks record file count, size, destination and outcome and print them; the device/iOS/USB half
  of the context block is written by the same drill that is blocked ⛔.
- [x] **Do not enable multi-file concurrent reads on a single device session until tests prove it safe.** ✅
  There is no concurrent copy path to enable: `TransferService` runs one sequential loop and uses `Task.Run`
  only to keep blocking device calls off the UI thread. The value is forced to 1 on save
  (`SettingsPage.cs:359`) and the control says so.

## Device and failure testing

- [x] **iPhone not connected / locked / not trusted / DCIM unavailable.** 🟡 *(two of four proven live)*
  *Not connected* ✅ live 2026-10-19: the tiles read "Connect your iPhone", the band stayed idle and the
  **Resume transfer** action was visible-but-disabled. *DCIM unavailable* ✅ live: this handset exposes no
  `DCIM` container at all, and `CameraRoll` accepts the storage container that holds the albums while keeping
  the canonical `DCIM` label — detection was fixed for exactly this. *Locked* and *not trusted* 🟡: the code
  paths return `LockedOrUntrusted` with unlock/Trust guidance, but were never provoked on hardware.
- [ ] **Apple device software missing or repair needed.** ⛔ Needs a machine with no Apple portable-device stack.
- [x] **USB disconnect at scan, copy and verify phases.** 🟡 *(the scan phase happened for real and found a defect)*
  On 2026-10-20 a phone was physically disconnected while a 52,574-file scan was finishing. The scan had
  reported `status=Complete`; every object read after the cable moved then failed, each failure was recorded
  as one warning, and the album stayed marked as read — so a listing missing the tail of the tree called
  itself complete, and the cache stored it. Fixed: an unreadable object marks its folder a failed read (the
  scan becomes `Incomplete`), twenty consecutive failures abort the walk with
  `DeviceStoppedAnsweringException` and a reconnect instruction, and nothing incomplete reaches the cache.
  `TransferRules.DeviceStoppedAnswering` + `FilterAndConflictTests` pin the rule; the fixed run has not been
  re-observed live, and a cable pull mid-**copy**/**verify** remains ⛔ (those go through the backend-fault
  path pinned by `RecoveryFlowTests.ABackendFaultAbortsTheSessionAndLeavesItResumable`).
- [x] **Destination full, read-only, permission denied, long path and invalid name.** 🟡 *(paths and names proven; volume conditions not provoked)*
  Long/reserved/invalid names and traversal: `ReconciliationTests` (reserved-name set, trailing space/dot,
  control characters), `TransferRulesTests` (traversal, absolute folders). Full/read-only/permission-denied:
  a failing entry is recorded as failed and the session cannot claim completion
  (`CompletionFailsIfAnyItemFailed`) — but no run was performed against a full or write-protected volume.
- [x] **Same path + same size but different content is not described as hash verified.** ✅
  `FilterAndConflictTests.VerificationClassificationNeverUpgradesWithoutMatchingHashes`;
  `ReconciliationTests.SavedHashDiffersFromExistingHash_PreservesForReview`.
- [x] **Scan changed between passes; warning shown and plan can be refreshed.** ✅ *(logic proven; device trigger ⛔)*
  The second pass runs when asked (`MainForm.cs:629`), compares the two enumerations and refuses the plan with
  the changed paths named. The comparison is now linear — `ScanStabilityTests` (6), including the measured
  26,000-path case — and it no longer throws on a partial scan that carries a duplicate path.
- [x] **iCloud Photos with Optimize Storage; clear guidance without claiming automatic detection is definitive.** ✅ *(guidance)* / ⛔ *(a genuinely cloud-optimized library)*
  `ConnectionDiagnostics` lists iCloud originals as a **MANUAL CHECK** with Apple's "Download and Keep
  Originals" wording; the backends say the same in their empty/inaccessible messages; `README.md:265` states
  a scan is not proof that cloud-only originals came down. No automatic detection is claimed anywhere.
- [x] **HEIC/JPEG and MOV pairs remain individually preserved and are reported as possible Live Photo pairs.** ✅
  `FilterAndConflictTests.LivePhotoPairsAreCountedByStem`; `EnumerationReport.LivePhotoPairCount`;
  `CameraRollTests.CameraMediaIsPhotosAndVideosOnly` (pairs are never merged).
- [x] **Large video (4 GB+) and 25,000+ item library.** 🟡 *(25,000+ proven twice; 4 GB+ file not copied)*
  `LargeLibraryStressTests.ScanningASingleFolderOfTwentySixThousandEntriesCompletesAndReportsProgress` (26,000
  entries), `SummarisingAndFilteringThreeHundredThousandEntriesStaysLinear` (300,000), and live scans:
  a cold **55,185 files / 162,499,726,120 bytes (151.34 GiB) in 23.3 minutes** on 2026-10-22, plus historical
  **26,335 files / 106.49 GB in 10.3 minutes**, **52,574 files / 166.72 GB in 23.2 minutes** and a warm
  **57,371-file** pass. For 4 GB+ the 32-bit concern is covered
  (`OperationStatusTests.AByteTotalBeyondIntRangeStillProducesAPosition`),
  but the largest file any automated test copies is 32 MiB, so a single 4 GB+ video is ⛔.

## Connection diagnosis and support bundle

- [x] **A device Windows merely remembers is never reported as a device that is attached.** ✅
  The most confusing state in the field: Device Manager still lists an "Apple iPhone" — its own properties
  reading *"Currently, this hardware device is not connected to the computer"* — while the application says no
  device is present, and neither statement is false. `Core/AttachmentDiagnosis.cs` now keeps the two facts
  apart: the handsets answering a live probe, and the records Windows is merely holding. Handsets are decided
  from the **driver service** bound to each record (`WUDFWpdMtp` is the media interface; the composite parent,
  Apple's USB driver and usbmuxd are support interfaces that can never supply a file), so the reading does not
  depend on Apple's display names, and an attached handset is never downgraded by stale records beside it.
  Verified against this machine's **real** USB registry rather than a fixture: the Diagnostics page and the
  bundle both read `CACHED RECORD ONLY (2)` — *Apple iPhone, Apple iPad* — with 3 support records and nothing
  attached. `AttachmentDiagnosisTests` (10) uses that same record set, and the live read is asserted to return
  display names only, never the instance key Windows stores the serial number in.
- [ ] **The attached branch of that diagnosis on a live handset.** ⛔
  No handset was attached while it was written, so *attached* — and attached-but-locked, where the row must
  still read ATTACHED while the media rows explain the phone is not readable — is behaviour-pinned by tests but
  not observed on hardware. TEST-PLAN hardware check 24.
- [x] **The diagnostics page shows its findings whole.** ✅
  The detail column neither wrapped nor grew, so any finding longer than the column was silently truncated with
  nothing to say it had been — the worst failure mode for a page whose entire purpose is to explain a fault.
  Details now wrap and their rows grow, in both the check and the dependency grids, and the three USB readings
  are coloured apart (green attached, amber remembered-only, red never seen) instead of by a three-way exact
  match that could not match a dynamic status at all. `DiagnosticsPageTests` (6); captured on the published
  build at 150% scaling.
- [x] **The diagnostics bundle is produced at all.** ✅ **Fixed in this audit.**
  The export read the log tail with `File.ReadAllLines` while the application held the log open for writing.
  That grants only *read* sharing; Windows checks a new handle's sharing against the existing writer's access,
  so the read failed with a sharing violation, the exception escaped `DiagnosticsBundle.Create`, and **every**
  export produced no file whatsoever — on every machine, whenever the app had logged anything. The tail is now
  read with write sharing, and a log that still cannot be read costs its own file (*Log tail unavailable (…)*)
  rather than the whole bundle. Found by pressing the button on the running published build, then driven from
  it to a 7-file bundle; `DiagnosticsBundleTests` (4) holds the log open exactly the way the product does.

## Android backend

- [x] **The transport is shared, so no second COM backend was written.** ✅
  Android phones reach Windows through the same MTP/Windows Portable Devices surface as an iPhone —
  Microsoft lists "Media Transfer Protocol (MTP) music players, **phones**, and cameras" among WPD storage
  devices — so `Devices/Wpd/WpdDevice.cs` keeps one COM implementation and the attached device's family
  selects a storage *policy*. Two near-identical COM backends would have to be fixed twice, and the second
  copy would be the one nobody had a device to test.
  Evidence: [`RESEARCH-SOURCES.md`](RESEARCH-SOURCES.md) §Android.
- [x] **The family is decided from what the device says, never from the device-type hint.** ✅
  `Core/DeviceFamily.cs` classifies from friendly name, manufacturer and model. `WPD_DEVICE_TYPE` is
  deliberately unread: Microsoft says it is used for "the generic visual appearance of the device" and that
  applications "must use the capabilities of the portable device to determine functionally, not the
  **WPD_DEVICE_TYPE** value" — and classification here *is* a functional decision. `DeviceFamilyTests` (10)
  pins both directions, including that an Apple match is strict (`Apple iPhone` stays Apple even against a
  Samsung manufacturer string) and that manufacturer matching is by whole word, so `Volkswagen` is not `LG`
  and `Microtek` is not `MTK`. The asymmetry is deliberate and tested: reading an Android phone as an iPhone
  would back up its camera roll and report a complete scan, while reading an iPhone as generic storage only
  looks in more places.
- [x] **Android storage is walked as whole volumes.** ✅ *(logic)* / ⛔ *(device)*
  An Android phone publishes one storage container per volume ("Internal shared storage", an SD card), and
  `Core/PortableStorage.cs` walks each in full and keeps the volume's own name as its destination folder, so
  an internal volume and a card are never merged. Photos, Videos, Music and Files are all advertised for
  Android because they are genuinely there; an unidentified device keeps the conservative two-group iPhone
  answer. `PortableStorageTests` (13) covers the policy; the walk itself needs a phone.
- [x] **What Android refuses is skipped instead of reported as failure.** ✅ *(logic)*
  `Android\data` and `Android\obb` are not readable by a file-transfer client, so descending would spend a
  device round trip per app collecting errors; thumbnail caches are derived data. Both are skipped, while
  `Android\media` — where apps such as WhatsApp keep photos — is kept, and a user folder merely *called*
  `data` is not skipped at all. Asserted folder by folder.
- [x] **A locked phone, or the wrong USB mode, produces a warning rather than a quietly short backup.** ✅
  *(logic and wording)* / ⛔ *(trigger)*
  A phone in "Transfer photos" (PTP) mode really does expose only `DCIM` and `Pictures`, and a locked phone
  answers with an empty folder; both look like a healthy device with less data. The scan carries a warning
  naming the setting to change, the device message says unlock and choose *File transfer*, and the
  diagnostics page gives USB mode and lock state a row each. The wording says "may be in PTP mode" because
  the sources are community reports, not vendor documentation. `PortableStorageTests` +
  `ConnectionDiagnosticsTests` (5).
- [x] **No Apple wording is shown for an Android device.** ✅
  The dashboard, the device header, the library empty state, the operation idle text, the write refusal and
  the diagnostics rows were all iPhone-specific ("IPHONE READY", "tap Trust", "iCloud originals", "Apple
  software"). Each now follows the detected family, and a device that identifies as neither family is
  reported as **NOT IDENTIFIED** and named in the device message rather than being called a phone it is not.
  `ConnectionDiagnosticsTests`, `MediaCategoriesTests`, `DeviceFamilyTests`.
- [x] **Two phones attached are a choice, not a race.** ✅ *(logic)* / ⛔ *(device trigger)*
  An iPhone and an Android phone on the same desk reach Windows on the same portable-device bus, so "the
  first one WPD lists" was not a decision the operator made. `AppSettings.PreferredHandset` (Settings →
  *When both phones are attached, read*) reorders the two families; it never removes one, so the phone that
  is actually attached is still read when the preferred family is absent, and the phone that is not read is
  named in the connection message. `DeviceFamilyTests` pins the order and the `AppleFirst` default;
  `SettingsTests` pins the round-trip and that an older settings file without the property still means
  iPhone-first. **Live 2026-10-21:** with both attached, `AndroidFirst` selected the HUAWEI and `AppleFirst`
  selected the iPhone; Diagnostics named both. The same session exposed a cross-device scan defect (probe and
  scan could choose different phones; cache key used the probe), now refused by `DeviceSelectionGuard`,
  `ScanResult.BelongsTo`, the MainForm guard and the cache guard. The physical disappearance drill is still
  open (matrix BT08).
- [x] **The Android path has been run against a device for walk and copy (cancel/lock/PTP remain open).** ⛔
  The HUAWEI Y7 Prime 2018 was walked end to end on 2026-10-20: **4,106 files / 16,114,057,157 bytes (15.01
  GiB)**, `Complete`. A filtered copy planned 520 files: **412 source-and-destination SHA-256 verified, 108
  `COPIED-UNVERIFIABLE` (0-byte `Sounds\.pictures`), 0 failed, 3,586 skipped**, ending `COPIED WITH
  UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP`; resume preserved the 108 unverified entries and the
  trial stayed at 0 jobs. Cancel -> resume, locked-screen abort, PTP-only mode and an Android throughput
  benchmark remain open. TEST-PLAN check 25.
- [x] **"Complete" is described honestly for Android.** ✅ *(wording)*
  Windows sees Android's own MTP view of its storage — AOSP's `MtpDatabase` combines `MtpStorageManager`
  filesystem operations with `MediaProvider` metadata — so a complete Android scan means *everything the
  phone exposed*, and the diagnostics page says so instead of implying the filesystem was read.

## Wireless transport

- [x] **The pairing secret is short-lived, single-use and is not itself a write credential.** ✅
  `PairingStateTests` (13) and `WirelessReceiverTests` (21, real loopback and private-LAN `TcpListener`): the displayed secret
  is redeemed once for a separate 32-hex-character session token; a second `/pair` with the same secret is
  `401`; presenting the **secret** to `/upload` is `401` and writes nothing; an issued secret invalidates the
  previous one; sessions expire, rotate (the replaced token dies immediately) and are capped at eight.
- [x] **The receiver binds only to a private interface.** ✅
  `PrivateNetworkTests` (21) and `WirelessReceiverTests`: loopback and the three RFC1918 ranges bind, while
  `0.0.0.0`, `*`, `+`, public and carrier-grade (100.64/10) addresses are refused **before** a socket is opened,
  with the reason named. The old address list offered every interface the machine had; that is gone.
- [x] **Show the receiving device and the files, and require approval; never expose an unauthenticated upload.**
  ✅ `ApprovalInboxTests` (9), `WirelessReceiverTests`, `WirelessPageTests` (11). An upload becomes an *offer*
  carrying the sender's address and the path it wants; the response is withheld until the operator decides, and
  a refusal sticks for the session. Approving is a local action only - there is deliberately **no network route
  to approve**. Auto-approval exists as a visible switch, not a silent default. `/upload` without a session is
  `401` and writes nothing.
- [x] **No firewall port is opened automatically, and TLS is never claimed when it is not there.** ✅
  Nothing in the application touches `netsh advfirewall` or the firewall API. `WirelessReceiverTests` pins that a matching `CurrentUser\My` certificate is required before `SslStream` is used, and that without one the receiver falls back to HTTP while building the ticket URL
  from the scheme that actually bound. The
  status line, the activity log and `/health` all state which transport is in use; `docs/WIRELESS.md` writes the
  threat model plainly. Confirmed on the published build, not only in tests: with **Require TLS** checked and no
  matching certificate, the receiver fell back, the ticket URL was `http://`, the
  status line read `Listening on http://192.168.8.91:47821/ (HTTP; no certificate).`, and `/health` answered `{"status":"ok","tls":false}`.
- [x] **Unsafe paths, oversized bodies, digest and size disagreement, and collisions are refused.** ✅
  `WirelessReceiverTests`: traversal and `.i-total` are `400`; a declared digest that disagrees is `422` and
  leaves no staging file; a size disagreement is `422`; a volume that cannot hold the declared size is `507`;
  an existing file is renamed rather than overwritten and the receipt records the name actually used; two
  in-flight uploads of the same path are told so (`409`) instead of racing for the same free name.
- [ ] **Wireless from a real iOS sender (Shortcut/companion app).** ⛔
  Both handsets are attached, but no iOS Shortcut/companion sender exists in this environment. The protocol, pairing, approval, rotation,
  digest, traversal and page-contract rules are covered by the loopback tests above **and** were driven live
  against the published build with a real HTTP client (see the live row in `PROJECT-STATUS.md`), but the sender
  recipe in `docs/WIRELESS.md` has not been run from a real device. "A real iPhone delivered its photos over the
  LAN" is **not** claimed.
- [ ] **Certificate-pinned TLS.** **NO.**
  Not implemented and not claimed. The receiver uses a matching `CurrentUser\My` certificate when present but
  does not pin it; without one it runs the token-authenticated session on the private network and says so.
  An iOS Shortcut cannot pin a self-signed certificate, which is why the authenticated session exists
  instead of a self-signed TLS mode no sender could use.


## Release engineering

- [x] **Build succeeds from a clean checkout on current Visual Studio / .NET SDK.** ✅
  `docs/BUILD-VALIDATION.md`; the source zip is built from scratch at 0 warnings / 0 errors.
- [x] **Unit and integration tests pass on Windows.** ✅
  **444 passed, 0 failed, 2 skipped (446 total)** on 2026-10-22 (the two skips are hardware-marked Shell-backend
  apartment tests). The two-phone isolation guard added 14 tests on 2026-10-21; the Appearance preview
  controls added 4, the theme token/contrast coverage was expanded and the no-device neutral-wording
  regression was added on 2026-10-22. Earlier waves include 37 added with the commercial subsystem: 83 for the wireless credential model, the QR encoder,
  the approval gate and the receiver's real HTTP surface; 20 for the USB-evidence diagnosis, the diagnostics
  page contract and the bundle's own export; **30 for the Android work** (`DeviceFamilyTests` 10,
  `PortableStorageTests` 13, `ConnectionDiagnosticsTests` 5, plus one `FolderNameIndexTests` case for the path
  lookup's new root-label rule — a volume is not called `DCIM` — and one `MediaCategoriesTests` case for the
  family-aware group explanation); and **6 for the two-phone session** — three `AttachmentDiagnosisTests` for
  the family filter, one `DeviceFamilyTests` case for the selection order, one `SettingsTests` round-trip and
  one `FilterAndConflictTests` case for the disconnect boundary.
- [x] **Native dependency versions, hashes, licenses and provenance are recorded.** ✅
  `native/dependencies.json` plus `docs/RESEARCH-SOURCES.md`; `DependencyVerifierTests` (6), including
  `ShippedManifestHasNoPinnedBinaryThatCouldSilentlyEnableAfc` — the AFC backend stays inert until an operator
  deliberately pins a binary with a hash.
- [x] **Installer upgrade and uninstall behaviour are tested.** ✓
  `InstallerScriptTests` (3) executes the real per-user installer against temporary folders and a temporary
  HKCU key: install creates the executable, manifest, uninstaller and uninstall entry; upgrade replaces the
  executable while settings and the trial ledger survive; uninstall through the installed copy removes the
  folder and key but keeps user data; a wrong hash exits 4 without writing anything; uninstalling an unrelated
  folder exits 3 without deleting anything. No administrator rights and no firewall rule are involved.
- [ ] **Code signing is tested.** ✗ **Blocked by external requirement.** The Windows SDK signing tools and a
  code-signing certificate are absent from this machine. `installer/Sign-Artifacts.ps1` is implemented and
  fails closed (exit 2 without signtool, 3 without a credential, 5 on verification failure), but no signed
  artifact exists and none is claimed. See `docs/RELEASE-SIGNING.md`.
- [x] **No silent driver installation or security-setting changes.** ✅
  The installer adds files, a shortcut and an HKCU uninstall entry only; it creates no service, scheduled task,
  firewall rule or driver. The app uses the operating system's own WPD/Shell stack, and
  `DependencyVerifier` refuses an unpinned native binary even when it is present
  (`UnpinnedBinaryIsRefusedEvenWhenPresent`).
- [x] **Logs and exported reports avoid unnecessary private data.** ✅ **Fixed in this audit.**
  Two defects were found and corrected. (1) A **device serial was not redacted at all** — the dashboard line
  `Serial: FCC4L3VYCW` went into `I-total-transfer.log` verbatim, and that log is shipped inside the destination
  folder and inside the diagnostics bundle, whose own README claims it carries no full device identifiers. (2)
  A **secret assignment kept its value**: `pairing_token: …` was rewritten as `pairing_token=…REDACTED …`, which
  reads as redacted and is not. Both are masked now, the label is kept for support, and ordinary operational
  text is left byte-for-byte. `LogRedactionTests` (9), including a check on the file itself.

## Trial, licensing and the commercial gate

- [x] **The trial counts meaningful work, not launches.** A free install gets three jobs. A job is recorded
  only when the transfer completed, was not cancelled, had no failures and wrote at least one file with
  non-zero bytes. Failed, cancelled, empty, partial and "everything was already present" runs cost nothing.
  `LicensingTests`, `JobFacts.IsMeaningful`.
- [x] **The counter survives restarts, updates and the loss of one store.** It lives in
  `%APPDATA%\I-Total Transfer\trial.json` and `HKCU\Software\I-Total Transfer\Trial` and is merged
  pessimistically (highest count, union of job fingerprints, earliest first-seen, latest last-seen). A corrupt
  file is quarantined, not read as a fresh trial. A clock moved backwards cannot reset the count. Tests:
  restart, single-store loss, corruption, clock rollback, read-only ledger.
- [x] **A repeated report cannot consume a second job.** The engine stamps each run with `JobId` and the
  ledger stores a truncated SHA-256 fingerprint; reporting the same finished run again returns
  `AlreadyRecorded`. Tested.
- [x] **A purchased licence is verified offline against a public key only.** Entitlements and revocations are
  RSA-PSS/SHA-256 envelopes. The payload is not parsed until the signature verifies. Missing key means
  `NotConfigured` - never acceptance. No private key, certificate, password or payment secret is in the
  source, the executable or a package. Tampered, expired, not-yet-valid, refunded, revoked, wrong-product,
  wrong-channel and wrong-app-id cases each have a distinct verdict and a test.
- [x] **Licence errors never abort or corrupt an in-progress transfer.** The gate runs only when a new job
  starts; `TryStartJob(resuming: true)` is always allowed. Ledger and store failures are caught, the
  transfer finishes, and the UI reports that the count is memory-only. No timer or background check exists.
- [x] **The operator can see and act on the state.** Settings > Licence shows the live counter, the rule that
  defines a job, an `Enter licence...` picker, and a Buy button that is disabled with its reason when the
  build has no store URL; Steam builds also offer a re-check. `LicenseSettingsTests` drives the real controls.
- [x] **Support can see the counter without seeing the buyer.** The diagnostics bundle carries a
  `licence.txt` summary - standing, channel, jobs used/limit/remaining and the start decision - and never the
  purchase identifier, ledger entries or fingerprints; `trial.json` is not copied. `DiagnosticsBundleTests`
  and a live export from the published build.
- [x] **Wireless reception is metered by the same trial.** One sender session costs one job, charged after the
  first committed and verified file; an exhausted trial refuses new uploads with HTTP 402 and writes nothing.
  Tests: `OneWirelessSenderSessionCostsOneTrialJob`, `AFinishedTrialRefusesNewWirelessUploads`.
- [ ] **A production-issued entitlement has been activated.** BLOCKED BY EXTERNAL REQUIREMENT: no key pair,
  no issuing backend and no purchase URL exist yet, so the shipped build carries no public key and
  `Enter licence...` reports `NotConfigured` while the trial runs. This is fail-closed, and it is recorded
  as pending rather than passed. The same blocker covers a live refund/revocation push.

## Installer, packaging and signing

- [x] **Per-user installation only.** `installer/Install-I-Total.Transfer.ps1` targets
  `%LOCALAPPDATA%\Programs\I-Total Transfer`; no UAC prompt, no machine-wide state, no firewall rule, no
  service, no scheduled task.
- [x] **Hash-checked and manifest-scoped.** `-ExpectedSha256` refuses a mismatched executable before writing
  anything (exit 4). Every installed file is recorded in `install-manifest.json`; uninstall removes only
  those names and refuses a folder it does not own (exit 3).
- [x] **Upgrade and uninstall preserve the operator's data.** Settings, trial ledger, licence, cache and logs
  live under `%APPDATA%\I-Total Transfer` and survive both operations; `-RemoveUserData` deletes only a
  folder carrying this product's own data files.
- [x] **Portable package.** `installer/Build-Installer.ps1` produces the portable ZIP with `INSTALL.txt`
  and `SHA256SUMS.txt` and refuses to package an executable whose hash does not match.
- [ ] **Packaged setup executable.** BLOCKED BY EXTERNAL REQUIREMENT: Inno Setup 6 is not installed. The
  script (`installer/I-Total.Transfer.iss`) is written with the same per-user, no-firewall, data-preserving
  rules; `Build-Installer.ps1` prints `SETUP SKIPPED` rather than pretending a setup exists.
- [ ] **Authenticode signature.** BLOCKED BY EXTERNAL REQUIREMENT: no certificate and no signing tools here.
  `installer/Sign-Artifacts.ps1` signs, timestamps and verifies, and fails closed; `docs/RELEASE-SIGNING.md`
  records the procedure and the checklist. Nothing in 1.8.0 is claimed to be signed.
## What this means

**Release-ready today**, with the evidence above: the correctness contract, the engine's performance
behaviour, the destination-safety rules, the privacy of logs and reports, the wireless transport's security
contract (single-use pairing, session rotation, private-only binding, an approval gate that writes nothing
until you decide, and an honest TLS fallback - all of it driven live against the published build, not only
unit-tested), the connection diagnosis (live-verified against this machine's own USB records, so a device
Windows merely remembers can no longer be told apart from one that is there), the diagnostics bundle (driven
from the running build after fixing the defect that had stopped it being written at all), and the ability to
build and test without a device.

**Not yet release-ready**, and the four reasons:

1. **The handset legs.** Buffers on a real device, a 4 GB+ video, locked/not-trusted, a genuinely
   cloud-optimized library, a real cable pull mid-copy, the transferred bytes/second of the resumed path, and
   the *attached* branch of the new USB diagnosis in its corrected form (it was seen once, naming an Android
   phone under an Apple row) are all ⛔ — each needs the phone attached, and the resume drill in particular is
   what would confirm the `FolderNameIndex` change end to end. The scan phase *was* exercised by a real cable
   pull, which found the completeness defect now fixed; the fixed behaviour has not been re-observed.
2. **The wireless sender leg.** The receiver is proven; the *sender* is not. No iOS Shortcut or companion app
   has delivered a file from a real handset, so the pairing QR, the approval prompt and the digest check have
   never been exercised by a real client — only by a scripted one. The recipe is ready in `docs/WIRELESS.md`.
3. **Packaging and signing.** The per-user installer, upgrade and uninstall paths now exist, are executed by
   the automated tests, and the portable package is built. What remains is a packaged setup executable
   (Inno Setup 6 is not installed) and an Authenticode certificate (none exists here), so no signature is
   claimed. Those are external requirements, not a quiet omission.
4. **The Android leg.** The HUAWEI was walked and copied live on 2026-10-20 (4,106 files; 412 verified +
   108 unverifiable; 0 failed) and the resume preserved the unverified files. A real cancel -> resume, the
   PTP warning on a PTP-mode handset, a locked-screen abort and an Android throughput benchmark remain open.
   Until those run, Android is *live-verified for walk/copy, partly verified for recovery* and must not be
   described as fully verified in any release note.
