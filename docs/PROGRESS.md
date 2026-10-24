# Progress log

One entry per working session, newest first. Each entry records what changed, the exact commands and results,
what remains blocked, and the next action. This is the continuation record: a new session should be able to
start from here without re-reading the whole conversation.

---

## 2026-10-22 - non-elevated wireless LAN, UI-18 page audit, honest status text

### Starting point

The working tree was the v1.8.0 source after the theme/no-device pass. The published build still used
`HttpListener` for wireless; the previous UI audit had captured `Wireless start failed: Access is denied.` at
150 % scaling. The current machine is Windows 10 Pro 19045, non-elevated user `DRRAMPAGE-PC\shambrain`,
with a single 3440x1440 display at 150 % (`AppliedDPI=144`). Both handsets were physically present during
this session (iPhone 13 Pro, iOS 27.0.1, WPD; HUAWEI Y7 Prime 2018, WPD). A later physical phase did run a
full live scan and a filtered HEIC/MOV copy; both are recorded below.

### Wireless transport defect and fix

`HttpListener` delegates to HTTP.sys, which requires an HTTP URL ACL for a non-loopback prefix. A normal
per-user install cannot add that ACL without elevation, so the real page failed before it opened a socket.
`WirelessReceiver` now owns a `TcpListener` and speaks HTTP/1.1 itself: request-line/header parsing,
`Content-Length` enforcement, `Expect: 100-continue`, response writing, a bounded drain before an early
refusal, graceful stop, and a matching `CurrentUser\My` certificate wrapped in `SslStream` when one exists.
Without a matching certificate the receiver still falls back to token-authenticated HTTP on the private
interface and says so in the status line, log and `/health`. `APrivateLanAddressBindsWithoutAUrlAcl` binds
`192.168.8.91` without elevation and answers `/health`. The full receiver suite is 21 tests, 0 failed.

### UI-18 audit result

All seven pages were walked at the host's 150 % scaling with `PrintWindow` and UIA dumps: Overview,
Appearance, Backup & queue, Media library, Wireless import, History and Diagnostics. The live build opened
the real no-scan dialogs (`No scan` / `Run a successful scan first.` and `Scan the device first.`), produced a
3,586-byte diagnostics ZIP, bound the wireless listener and served `/health` as the non-elevated user.

The audit found and fixed real defects:

- The Wireless listening status was clipped before the TLS reason; it now fits:
  `Listening on http://192.168.8.91:47821/ (HTTP; no certificate).`
- Double-encoded punctuation in runtime strings (`mojibake`) was corrected in `WirelessReceiver` and the
  Wireless page; the trailing ellipsis is now a real ellipsis.
- `Clear` under the transfer log was ambiguous and cleared only the console; it is now `Clear view` with an
  accessible description that the log file is kept.
- Diagnostics claimed `required dependencies verified` while all four native binaries were missing; the
  empty required set is now shown as `no native runtime verified - native backends stay disabled`.
- The Media-library empty state told a ready phone to connect and unlock; it now tells the operator to
  choose Scan device and only says to connect if no phone is attached. The empty-state subtitle wraps.
- The Start-backup confirmation used the unfiltered scan total, so a filtered job could promise 26,335
  files while copying eight. `TransferService.PlanSummary` now applies the same filter as the engine and
  the confirmation names the real planned count and bytes; `PlanSummaryUsesTheActiveFilterAndNeverTheWholeScan` pins it.

`PageControlTests` adds five real-control tests covering the Transfer, Library, History, Diagnostics and
Wireless toolbar events and the Clear-view boundary.

### Commands and exact results

```powershell
dotnet build I-Total.Transfer.sln -c Release --no-incremental
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test tests\I-Total.Transfer.Tests -c Release --blame-hang --blame-hang-timeout 240s
# Passed! Failed: 0, Passed: 446, Skipped: 2, Total: 448, Duration: 28 s
# The two skips are the pre-existing opt-in Shell apartment probes.

dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --filter "FullyQualifiedName~PageControlTests|FullyQualifiedName~WirelessReceiverTests|FullyQualifiedName~WirelessPageTests|FullyQualifiedName~DependencyVerifierTests" --blame-hang --blame-hang-timeout 180s
# Passed! Failed: 0, Passed: 50, Total: 50

dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
# publish\I-Total.Transfer.exe: 112,150,342 B
# SHA-256 C953BC086CCAD9B1948DCA20634BC6F55631571A6FD033810D9AF99CFFFF26D5
```

### Live published-build evidence

The published exe was launched with a scratch destination and driven through its own UI. The Wireless page
read `Listening on http://192.168.8.91:47821/ (HTTP; no certificate).`; a real HTTP request returned
`200 {"status":"ok","tls":false}`. Start, New ticket, Stop, Verify, Export scan, Clear view, Refresh history,
Run diagnostics and Export diagnostics all produced their real outcomes. Settings were restored from the
audit backup; the saved theme remains `Light`.

Evidence: `%TEMP%\opencode\ui-audit2\*.png` and matching `.txt` dumps, including
`wireless-listening-150.png`, `backup-queue-150.png`, `media-library-150.png`, `history-150.png`,
`diagnostics-run-150.png`, `dialog-verify-with-no-scan.png` and `dialog-export-scan-with-no-scan.png`.

### Physical scan and filtered copy (2026-10-22)

- A live UI `Scan device` on the iPhone completed: **55,185 files / 162,499,726,120 bytes (151.34 GiB)**,
  `status=Complete`, **23.3 min**, 11 folders, 0 failed folders, 0 warnings, 0 unknown sizes, 1,167
  live-photo pairs. The report is `%TEMP%\opencode\physical-heic-mov\destination\I-total-enumeration-report.json`.
  `ReuseLastKnownLibrary=false`, so this drill deliberately did not write a cache.
- The immediate `Start transfer` click raced the scan finalisation gate and no confirmation opened; no UI copy
  is claimed from that run.
- A filtered fetch through the real native WPD backend and the shared engine then copied **8 files** (3 HEIC +
  5 MOV, **2,868,790 bytes**) selected from the last complete listing. All eight entries are
  `IntegrityVerified` with `SourceAndDestinationHash`; every source and destination SHA-256 pair is identical,
  with 0 failed and 0 unverified. Report: `%TEMP%\opencode\physical-copy\destination\I-total-session-report.json`.
  This is physical fetch/verification evidence, not a claim about the UI copy path.
- **Android cancel -> resume (2026-10-22, shared engine):** a live Android scan found **4,103 files /
  15,612,056,299 bytes (14.54 GiB)**, `Complete`. One 612,420,912-byte MP4 was cancelled after more than
  5 MB; the manifest read `CANCELLED`, one outstanding entry and 612,420,912 outstanding bytes. The resume
  plan contained that file; the resumed copy ended `HASH-VERIFIED COMPLETE (source-to-destination)`, 1/1
  copied, 0 failed, 0 unverified, source and destination SHA-256 identical. Evidence:
  `%TEMP%\opencode\physical-cancel-resume\destination`.
- **UI filtered confirmation (2026-10-24):** the current published build was launched with `AndroidFirst`
  and a `.mp4`/500 MB filter. The live scan finished, and the Start-backup dialog read
  **`Copy 1 files (584.1 MB)`**, not the full 4,103-file scan; it was cancelled without writing. Screenshot:
  `%TEMP%\opencode\ui-android-confirm\02-confirm.png`.
- **UI Stop and recovery surface (2026-10-24):** the same one-file plan was started and **Stop** was pressed.
  The recovery bar read `1 planned - 0 copied and verified - 1 outstanding - 4,102 skipped by filter`;
  **Resume transfer** opened the correct `Continue the unfinished session` dialog for the 584.1 MB file.
  The resume was cancelled, so no trial job was consumed. Screenshot:
  `%TEMP%\opencode\ui-android-cancel\01-cancelled-recovery.png`.
- **Mid-scan unplug (2026-10-24):** the first repeat exposed a *new* completeness defect: the pre-fix build
  accepted a disconnected scan as `Complete` with **28,655 files / 29.36 GB** and **25,843 unknown sizes**.
  `ClassifyEnumeration` now treats a high unknown-size share as `Incomplete`, `ScanAsync` re-probes the
  phone before accepting anything, and `Start transfer` refuses an incomplete listing. The second repeat
  ended **2,326 files / 8.05 GB, `Incomplete`** (1,739 unknown); the post-scan probe showed
  `the iPhone stopped answering during the scan. The listing is incomplete and was not accepted.` No
  enumeration report or cache was written, and the HUAWEI was never read. Evidence:
  `%TEMP%\opencode\disconnect-drill\01-after-disconnect.png`,
  `%TEMP%\opencode\disconnect-drill2\02-rejected-after-disconnect.png`.
- **PTP-only Android scan (2026-10-24):** the HUAWEI was switched to Photo transfer (PTP; Windows PID
  `1083`). The app scan returned 57 files / 7.5 MB and the Overview visibly showed
  `Camera-only connection (PTP): only DCIM and Pictures are visible. If its Files app shows more, switch the
  USB notification to "File transfer" and scan again.` The activity log carried the same warning. Screenshot:
  `%TEMP%\opencode\ptp-drill3\02-ptp-result.png`.
- **Locked-phone observation (2026-10-24):** the HUAWEI screen was locked during a real 584.1 MB copy.
  MTP remained available and the copy completed `SIZE-VERIFIED COMPLETE` with source/destination SHA-256
  equality; the trial counted one job. MTP remains active while the screen is locked, so the locked-phone
  abort is not applicable to this stack; AN08 is recorded that way.
- **Filtered post-copy verification defect and fix (2026-10-24):** after that filtered copy, the pre-fix app
  auto-verified the full 4,103-file scan and reported 4,102 excluded files as missing. `ReportTransferSummaryAsync`
  now skips whole-library verification when the filter planned fewer files and logs the reason. A no-op
  filtered rerun skipped the already-present file, showed no false verification report, and did not charge a
  trial job.
- **Android buffer sweep (2026-10-24):** one 612,420,912-byte MP4 through native WPD, source-to-destination
  hash verified, one run per buffer: 64 KiB 18.246 s / 33.56 MB/s; 256 KiB 16.911 s / 36.21 MB/s;
  1 MiB 16.369 s / 37.41 MB/s; 4 MiB 16.187 s / 37.83 MB/s. CPU 2.94-6.50 s; peak working set
  70.1-85.5 MB. This is a single-file device observation, not a multi-file benchmark; 1 MiB remains the
  default.
- **Android mixed-size batch (2026-10-24):** 30 files / 111,135,111 bytes (10 tiny 8-24 B, 10 about 1 MB,
  10 about 10 MB): 4.681 s, 6.41 files/s, 23.74 MB/s, CPU 1.55 s, peak working set 69.9 MB; 30/30
  copied and source/destination verified. One run, not a statistical benchmark.
- **Portable installer self-location (2026-10-24):** extracting the portable ZIP and running its own
  `Install-I-Total.Transfer.ps1` failed because the script defaulted `SourceDir` to `..\publish`. It now
  uses its own folder when the executable sits beside it, and still falls back to `..\publish` in the
  source tree. `PortableLayoutInstallsWithoutAnExplicitSourceDir` runs the real script in a portable
  sandbox; `InstallerScriptTests` is 5 and the full suite is 446 passed / 2 skipped / 448 total.
  The final portable ZIP was then extracted and its own installer run with no `-SourceDir`: install
  returned `{"ok":true,...}`, the executable and manifest were present, the HKCU key existed; the installed
  uninstaller removed the folder and key and kept `settings.json` (`userDataKept:true`).

### Source-archive reproducibility after git init (2026-10-24)

The new `.git` repository made the SDK embed a source revision in the published assembly. The source ZIP
excludes `.git`, so its clean rebuild produced a different executable (`C953BC08...` versus `D039F4BF...`).
Setting `<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>`
removed the git revision from the binary; publish and source-only rebuild now agree. This was found by actually
extracting the source ZIP and comparing the rebuilt hash, not by inspection.

### Files changed

```
src/I-Total.Transfer/Core/TransferRules.cs
src/I-Total.Transfer/Devices/Wpd/WpdDevice.cs
src/I-Total.Transfer/I-Total.Transfer.csproj
src/I-Total.Transfer/Services/WirelessReceiver.cs
src/I-Total.Transfer/Services/TransferService.cs
src/I-Total.Transfer/UI/MainForm.cs
src/I-Total.Transfer/UI/Controls/EmptyState.cs
src/I-Total.Transfer/UI/Pages/DiagnosticsPage.cs
src/I-Total.Transfer/UI/Pages/LibraryPage.cs
src/I-Total.Transfer/UI/Pages/TransferPage.Designer.cs
src/I-Total.Transfer/UI/Pages/WirelessPage.cs
src/I-Total.Transfer/UI/Pages/WirelessPage.Designer.cs
installer/Install-I-Total.Transfer.ps1
tests/I-Total.Transfer.Tests/FilterAndConflictTests.cs
tests/I-Total.Transfer.Tests/PageControlTests.cs
tests/I-Total.Transfer.Tests/WirelessReceiverTests.cs
tests/I-Total.Transfer.Tests/TransferEngineTests.cs
tests/I-Total.Transfer.Tests/InstallerScriptTests.cs
tests/I-Total.Transfer.Tests/QrCodeTests.cs
CHANGELOG.md, PROJECT-STATUS.md, docs/PROGRESS.md
docs/UI-REQUIREMENTS-MATRIX.md, docs/WIRELESS.md, docs/KNOWN_ISSUES.md
docs/MASTER_IMPLEMENTATION_PLAN.md, docs/DEVICE_TEST_MATRIX.md
```

### Packaging and archive verification (2026-10-22)

- `installer\Build-Installer.ps1 -Version 1.8.0 -ExpectedSha256 7EF01BBA...` produced the portable ZIP;
  its size and SHA-256 are in `dist\SHA256SUMS.txt`. `SETUP SKIPPED` because Inno Setup 6 is not installed.
  Nothing is Authenticode-signed.
- The deployment ZIP (`dist\I-Total.Transfer-1.8.0-win-x64.zip`; size and SHA-256 in `dist\SHA256SUMS.txt`) contains the exe,
  `native\dependencies.json`, root documents, `INSTALL.txt`, the per-user installer and all docs. Its embedded
  exe was extracted and hash-compared with `publish\`: `7EF01BBA...` (match).
- The source ZIP (`dist\I-Total.Transfer-1.8.0-source.zip`; size and SHA-256 in `dist\SHA256SUMS.txt`) excludes
  `bin`/`obj`/`publish`/`dist`/`TestResults` and archive copies. It was extracted to a clean folder,
  rebuilt and published: **0 warnings, 0 errors**, producing the byte-identical
  `C953BC08...` executable (112,150,342 B), so the source archive is reproducible.
- `dist\SHA256SUMS.txt` is the authoritative archive hash record. The exe/portable/deployment/source
  hashes above match it.


### Blockers and limits

- **100 % pixel capture of UI-18 remains open.** This host has only a 150 % display; the page actions and
  150 % pixels were observed, and the wiring is tested, but no 100 % session exists here.
- **Physical evidence:** the iPhone scan/filtered copy, Android cancel/resume, PTP-only warning, mid-scan unplug
  and selected-phone-disappearance drills are live. The HUAWEI lock does not disable MTP, so no abort drill is
  applicable; single-file and mixed-size device observations are recorded above; no statistical multi-file benchmark is claimed.
- Production licence issuer/purchase URL, Steamworks, Inno Setup 6, Authenticode certificate and `signtool`
  remain external blockers. `Build-Installer.ps1` still prints `SETUP SKIPPED`; nothing is signed.
- Windows 11 rendering, 200 % scaling and audible cues remain unobserved.

### Next task

1. If a 100 % display is available, repeat the page capture set; otherwise keep the 100 % limitation recorded.
2. Run a device throughput benchmark when a stable USB setup is available; no lock/abort drill is required on this
   MTP stack.
3. Any further source edit invalidates the published hash: rebuild, retest, republish, repackage and refresh
   `dist\SHA256SUMS.txt`; docs-only edits still require new archives because they ship inside them.

---

## 2026-10-21 - two-phone isolation, Android live copy, honest iPhone timings

### Starting point

Both handsets were attached for this session: iPhone 13 Pro (`FCC4L3VYCW`, iOS 27.0.1, WPD) and
HUAWEI Y7 Prime 2018 (`26EBB18726155381`, volumes `SD card` and `Internal storage`). The previous iPhone
run had reported **57,371 files / 175.84 GB** (`TotalBytes` 188,810,836,019) as `Complete` in 11.0 s, and
that report is preserved in `%TEMP%\opencode\iphone-scan\I-total-enumeration-report.json`. A fresh capture
was being taken to put that listing beside a live scan when the session exposed a device-isolation defect.

### The defect found in this session

With `PreferredHandset = AppleFirst`, the header named **Apple iPhone** and the WPD probe reported it ready.
The live scan then finished with **4,106 files / 15.01 GB** - the HUAWEI's tree - and the cache writer
stored those rows under the Apple WPD cache key
`library-windows-portable-devices-69ffba35a0f78138.json`. The file content said `DeviceName=HUAWEI Y7 Prime
2018`, while its filename and key belonged to the iPhone.

Root cause: `WpdDevice.Probe` and `WpdDevice.Scan` each called `FindDevice` independently. The probe chose
the iPhone; if the iPhone was momentarily absent from the scan's fresh enumeration, `FindDevice` fell
through its preference list to the Android phone. `FinishScanCache` then used the **probe's** device info for
the cache key, so the other phone's listing was written under the iPhone's key.

This is a correctness and data-safety defect, not a display glitch: a scan or copy started for one phone
could read the other. The corrupted WPD cache was copied to
`%TEMP%\opencode\corrupt-iphone-cache-69ffba35.json` for the record and then deleted; the separate Shell
iPhone cache (`...9dabcc800e2280d9.json`, 57,371 / Complete) was untouched.

### Fix (code changed, simulated tests passed; physical re-run pending)

1. **Sticky backend binding.** `Core/DeviceSelectionGuard.cs` binds a backend to the first device it selects.
   A later, different device id is refused rather than adopted. `WpdDevice.Probe` reports
   `DeviceStatus.NotFound` with an honest "selected phone is no longer answering; the other attached phone
   was not used" message; `WpdDevice.Scan` throws the same message instead of reading the other phone. A new
   `Check device` creates a fresh backend, so changing phones remains possible; scanning and copying cannot
   silently switch.
2. **Scan identity on the result.** `ScanResult` now carries `Family` and `StableId`, plus
   `BelongsTo(DeviceInfo)`. WPD and Shell set them. This is the second layer: even a backend without the
   binding rule cannot have its listing accepted as another phone's.
3. **UI refuses a mismatched listing.** `MainForm.ScanAsync` calls `EnsureScanCameFromSelectedDevice` after
   the first and second scans. A mismatch throws before `_scan` is assigned, before the cache is written and
   before a transfer plan can be built. `FinishScanCache` independently refuses a listing that does not
   belong to the selected device.
4. **Cache key protection.** The cache is keyed by the selected device identity and now verifies the
   listing's identity first; the other phone's rows can neither be accepted nor overwrite the selected
   phone's cache.

New tests: `DeviceSelectionGuardTests` (6) and `ScanIdentityTests` (8), all passing. They cover a changed
stable id, a same-name/different-id case, different families without ids, same family/different name,
legacy no-identity backends, and the exact "selected phone gone, other phone was not used" message.

### Android hardware evidence already produced on the current source

The HUAWEI was read end to end through native WPD on 2026-10-20 and the artifacts are still in
`%TEMP%\opencode\android-copy-3`:

- full walk: **4,106 files / 16,114,057,157 bytes (15.01 GiB)**, `EnumerationStatus.Complete`, about 6 s;
- filtered copy: **520 planned**, **412 source-and-destination SHA-256 verified**, **108
  `COPIED-UNVERIFIABLE`**, **0 failed**, 3,586 skipped by policy, 1:15 elapsed;
- the unverifiable files are the phone's 0-byte `Internal storage\Sounds\.pictures\*.jpg` files; the run
  honestly ended `COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP`, not as a verified
  backup;
- resuming that destination preserved the 108 unverified entries (`PRESERVED UNVERIFIED`) with 0 failures
  instead of degrading them to failures;
- trial ledger stayed at **0 jobs used**: the run was not a meaningful completed job because it left
  unverifiable files, so it was correctly not charged.

### iPhone evidence and an honesty correction

The preserved report is real: `status=Complete`, 57,371 files, 188,810,836,019 bytes, 13 folders, 0 failed
folders, 0 unknown sizes, 1,198 live-photo pairs, duration **10.99 s**. It is **not** a cold-scan benchmark:
the same handset, after the app cache was deleted, walked `DCIM\202608_a` at about **32 files/s** (3,437
files in 108 s, every file showing `0 B` at that point). The 11 s pass was almost certainly served from
Windows/driver metadata cache after the earlier Shell enumeration. The 27 ms/object cold cost already
recorded in `PERFORMANCE-AUDIT.md` is the honest planning number; the 11 s figure must not be published as
a cold iPhone scan.

The full cold WPD scan then ran to completion on the fixed build, with the phone left alone and the cache
deleted first:

- **26,335 files / 114,344,029,232 bytes (106.49 GiB)**, `status=Complete`, **10.3 min**;
- the live progress line moved from 160 files/s to about 43 files/s as the walk went deeper; the folder
  walk ending was `DCIM\202608_a` at 25,849 files when the harness's completion test matched, and the log
  recorded `duration=10.3 min`;
- the cache writer created `library-windows-portable-devices-69ffba35a0f78138.json` with
  `DeviceName=Apple iPhone`, `Status=Complete`, `Files.Count=26,335`, `TotalBytes=114,344,029,232` and
  `captured=2026-10-21T04:05:46Z` - the first correct Apple WPD cache after the corrupted one was removed;
- the UI after the pass showed `Scan finished: 26,335 files · 106.49 GB` and the group totals
  Photos 19.79 GB / Videos 86.69 GB, not the HUAWEI's 4,106-file tree.

The handset now exposes **26,335** files, not the 57,371 counted on 2026-10-20. That is a real change in what
this phone exposes to WPD (the older report counted additional DCIM albums that are no longer present or
locally available); the 57,371 figure remains historical evidence, not the current library. Do not use it as
the present count.

**Release reproducibility correction.** The first source-ZIP rebuild produced a different exe hash even with
the same sources. The differences were traced to the regex source generator's non-deterministic type-name
hash and to the absolute PDB path recorded in Release's debug directory. `LogService` now uses static
`Regex` fields instead of `[GeneratedRegex]`, and Release builds set `DebugType=None` /
`DebugSymbols=false` (Debug/test keeps symbols and line numbers). A clean Release publish of the same
sources from two different directories now produces **the same hash**. The shipped exe has no PDB; stack
traces keep method names and messages but not source line numbers.

### Commands and results

```
dotnet build I-Total.Transfer.sln -c Release --no-incremental
  Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s
  Passed! Failed: 0, Passed: 405, Skipped: 2, Total: 407, Duration 24 s.
  (the two skips are the pre-existing Shell apartment probes)

dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
  I-Total.Transfer.exe   112,113,478 B
  SHA-256                4179C72EE4D2662A61594815744E110461B45D975DBCD7390A4437AAEF9FA3DD
```

The 57,371-file cache that the pre-fix build corrupted was deleted and a correct 26,335-file Apple WPD cache
now exists. The separate stale Shell cache still holds the 2026-10-20 57,371 listing; it is not used while
the WPD backend is active and is not evidence of the current library.

### Files changed

```
src/I-Total.Transfer/Core/DeviceSelectionGuard.cs       (new)
src/I-Total.Transfer/Core/Models.cs                     (ScanResult.Family, StableId, BelongsTo)
src/I-Total.Transfer/Devices/Wpd/WpdDevice.cs           (binding in Probe/Scan, result identity)
src/I-Total.Transfer/Devices/ShellIPhoneDevice.cs       (Apple family on probe/scan results)
src/I-Total.Transfer/UI/MainForm.cs                     (scan identity guard, cache refusal)
src/I-Total.Transfer/Services/LogService.cs             (static Regex; deterministic build)
src/I-Total.Transfer/I-Total.Transfer.csproj            (Release DebugType=None; deterministic)
tests/I-Total.Transfer.Tests/DeviceSelectionGuardTests.cs (new, 6)
tests/I-Total.Transfer.Tests/ScanIdentityTests.cs       (new, 8)
docs/PROGRESS.md, docs/DEVICE_TEST_MATRIX.md, docs/KNOWN_ISSUES.md,
docs/TEST-PLAN.md, docs/MASTER_IMPLEMENTATION_PLAN.md, PROJECT-STATUS.md, CHANGELOG.md
```

### Blocked / pending

- A filtered iPhone copy and HEIC/MOV verification on the final build: not run in this session.
- Android real cancel -> resume, locked-phone drill, and a physical unplug while scanning: need the phones
  and a human; not claimed.
- Two-phone copy isolation after the binding fix: a unit-level guard is tested; a physical "other phone
  disappears mid-scan" drill is not. The normal Apple scan on the fixed build completed and cached the
  correct iPhone listing, which exercises the acceptance/cache path but not the disappearance branch.
- Production licence issuer/purchase URL, Steamworks ownership, Inno Setup, and Authenticode certificate
  remain external blockers (KI-01/KI-02/KI-03/KI-05).

### Next action

Run a filtered iPhone copy on the final build and record the verification levels; then run the Android real
cancel -> resume, and, with a human, start a scan for one phone and make it stop answering while the other
remains attached. Confirm that run aborts with "the other attached phone was not used" and writes no cache.
Do not call the cold scan fast: its own recorded duration is 10.3 min for 26,335 files. The final exe
(`4179C72E...`) differs from the cold-scan build (`5A0DC3CC...`) only in the Release determinism change and
the runtime-regex redaction; no device-path code changed, and the full suite passes on the final tree.

### Final packaging and reproducibility (same session)

The final Release tree was built, tested, published and packaged after the two-phone isolation fix:

```
dotnet build I-Total.Transfer.sln -c Release --no-incremental
  Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test tests\I-Total.Transfer.Tests -c Release --blame-hang --blame-hang-timeout 240s
  Passed! Failed: 0, Passed: 405, Skipped: 2, Total: 407.

dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
  I-Total.Transfer.exe   112,113,478 B
  SHA-256                4179C72EE4D2662A61594815744E110461B45D975DBCD7390A4437AAEF9FA3DD
```

The deployment ZIP and the portable ZIP were both opened and their embedded executable re-hashed:
**112,113,478 B / `4179C72E...`** in each, matching `publish\I-Total.Transfer.exe`. The source ZIP was
extracted to a clean directory, rebuilt and published there, and produced the **same** byte-identical
executable, so the source archive is reproducible rather than merely complete. `dist\SHA256SUMS.txt` is the
authoritative record:

```
4179C72EE4D2662A61594815744E110461B45D975DBCD7390A4437AAEF9FA3DD  publish/I-Total.Transfer.exe
1C9CC5698468FA249ADD8683B4CF5C842A7B685207FC74CACA0AE174FDC36044  I-Total.Transfer-1.8.0-win-x64.zip
DCC1CB13D3C3AA3B54024328917B254B6C085F9C4C39F41C30732F2287D2178D  installer/I-Total.Transfer-1.8.0-portable-win-x64.zip
```

The source archive's own hash is recorded only in `dist\SHA256SUMS.txt` (an archive cannot contain a
correct hash of itself). The deployment and portable archive hashes above are repeated there and were
recomputed after the documentation refresh, so `dist\SHA256SUMS.txt` remains authoritative.

At the end of the session both phones were disconnected, so no further physical drill could run. The
`README.md` verification table and known-limitations section, `docs/DEVICE_TEST_MATRIX.md`,
`PROJECT-STATUS.md`, `CHANGELOG.md` and the master plan were brought in line with the evidence above: the
current iPhone listing is **26,335 files**, the 57,371 figure is historical, and the Android walk/copy is
live evidence rather than a pending claim. No hardware row was marked passed from a simulated test.

---

## 2026-10-20 - commercial subsystem: licensing, trial, installer, signing preparation

### Starting point

v1.8.0 source was consistent and fully tested (348 passed of 350, 2 skipped). The outstanding commercial work was not started:
no trial, no entitlement verification, no purchase surface, no installer, no signing preparation. Both
phones were still physically disconnected. Inno Setup, `signtool.exe` and any code-signing certificate are
absent from this machine.

### Completed this session

1. **Licensing and trial (`ITotalTransfer.Licensing`)**
   - `LicenseModels.cs`: channels, standings, entitlement payload, verdicts, snapshots, job facts.
   - `EntitlementCodec.cs`: RSA-PSS/SHA-256 envelopes for entitlements and revocation lists. The payload is
     not parsed until the signature verifies. Every malformed input becomes a verdict, never an exception.
   - `TrialLedger.cs`: file + `HKCU` stores merged pessimistically; atomic writes; corrupt files
     quarantined; clock-rollback note; in-memory store for tests.
   - `LicenseOptions.cs`: build-time `TrialJobLimit` (default 3) and `PurchaseUrl`, optional embedded public
     key, Steam edition detection and signed Steam entitlement provider.
   - `LicenseService.cs`: new-job gate (resume always allowed), idempotent job recording, activation,
     revocation, wireless session metering, never throws on store/licence failure.
   - `SettingsPage`: new Licence section (status, explanation, Enter licence, Buy, Check Steam) with
     disabled-with-reason Buy when no store URL is configured.
   - `MainForm`: service creation at startup, gate before both new-transfer paths, recording after both
     summary paths, licence line in the completion dialog, licence actions wired, service disposed on close.
   - `WirelessReceiver`: optional licence service; refuses uploads with HTTP 402 when the trial is finished;
     charges one trial job per sender session on the first committed file.
   - `TransferSummary.JobId`: engine-generated run identity for idempotent recording.
2. **Safe per-user installer** (`installer/Install-I-Total.Transfer.ps1`)
   - No administrator rights, no firewall changes, no services, no scheduled tasks.
   - Installs to `%LOCALAPPDATA%\Programs\I-Total Transfer`, Start Menu shortcut, HKCU uninstall entry.
   - Optional `-ExpectedSha256`; refuses on mismatch and installs nothing.
   - Records every installed file in `install-manifest.json`; uninstall removes only those names, refuses a
     folder it does not own, and keeps `%APPDATA%\I-Total Transfer` unless `-RemoveUserData` is given.
   - Upgrade keeps settings, trial ledger and licence.
3. **Packaging and signing preparation**
   - `installer/I-Total.Transfer.iss` (Inno Setup 6, per-user, no firewall, user data preserved).
   - `installer/Build-Installer.ps1` (portable ZIP always; setup EXE only when ISCC exists, otherwise an
     explicit SKIPPED line; SHA-256 manifests).
   - `installer/Sign-Artifacts.ps1` (signtool discovery; store thumbprint or PFX imported temporarily so the
     password never reaches a command line; verifies every artifact; fails closed).
   - Non-ASCII characters were removed from the PowerShell scripts: Windows PowerShell 5.1 reads BOM-less
     files as ANSI and interprets a UTF-8 em dash as a smart quote, which broke parsing.
4. **Tests**: six new classes - `LicensingTests`, `EntitlementCodecTests`, `TrialLedgerTests`,
   `SteamEntitlementTests`, `LicenseSettingsTests`, `InstallerScriptTests`.
   The installer tests execute the real script (install, hash refusal, upgrade, uninstall, wrong-folder
   refusal) against temporary folders and a temporary registry key.
5. **Project documents created**: `docs/MASTER_IMPLEMENTATION_PLAN.md`, `docs/PROGRESS.md`,
   `docs/DEVICE_TEST_MATRIX.md`, `docs/KNOWN_ISSUES.md`, `docs/RELEASE-SIGNING.md`,
   `docs/INSTALLER.md`; `AGENTS.md` expanded into permanent engineering rules.

### Commands and results

```
dotnet build I-Total.Transfer.sln -c Release --no-incremental
  Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s
  Passed! Failed: 0, Passed: 383, Skipped: 2, Total: 385, Duration 27 s.
```

The two skips are the pre-existing Shell-backend apartment probes that the harness cannot run here.

Installer smoke drill (manual, same script the tests drive):

```
install, upgrade, wrong-hash refusal (exit 4), uninstall:
  app folder removed, registry key removed, settings.json/trial.json kept.
wrong-folder uninstall: exit 3, unrelated file untouched.
```

Packaging drill against the previous publish:

```
installer\Build-Installer.ps1 produced I-Total.Transfer-1.8.0-portable-win-x64.zip (46,903,974 B) and
SHA256SUMS.txt; SETUP reported SKIPPED because Inno Setup 6 is not installed.
```

### Final publish, live UI drill and package

The earlier packaging drill ran against the pre-licensing publish and was superseded. After the commercial
work the source was rebuilt, retested and republished:

```
dotnet build ... -c Release --no-incremental        0 warnings, 0 errors
dotnet test ... --blame-hang --blame-hang-timeout 240s
  Passed! Failed: 0, Passed: 383, Skipped: 2, Total: 385, Duration 26 s
dotnet publish ... -o publish
  I-Total.Transfer.exe  112,113,478 B
  SHA-256 C57324F1533D0C78F95A101A6FBB37C28E522568D42F69E8D2256BC28339A256
installer\Build-Installer.ps1 -ExpectedSha256 C573... 
  dist\installer\I-Total.Transfer-1.8.0-portable-win-x64.zip produced
  (size and SHA-256 recorded in dist\installer\SHA256SUMS.txt)
  SETUP SKIPPED - Inno Setup 6 is not installed
```

Final archives are recorded in `dist\SHA256SUMS.txt` (and
`dist\installer\SHA256SUMS.txt` for the portable package). Those files are deliberately kept outside the
archives: this file ships inside the source and deployment packages, so an archive cannot carry a correct
hash of itself. The executable hash above is stable and reproducible from `publish\`.

**The shipped portable package was installed and removed once.** Extracting
`dist\installer\I-Total.Transfer-1.8.0-portable-win-x64.zip` and running its own
`Install-I-Total.Transfer.ps1` against temporary folders and a temporary HKCU key returned
`{"ok":true,...}`, copied the executable, PDB, uninstaller and README-INSTALL, and created the uninstall key
(exit 0). The installed uninstaller returned `{"ok":true,...,"userDataKept":true,"registryKeyRemoved":true}`,
removed the application folder and key, and left the temporary settings file in place (exit 0). The temporary
tree and key were then deleted. This exercises the package a user actually downloads, not only the script in
`installer\`.
The deployment ZIP's embedded executable was extracted and hash-compared with `publish\` (match). The source
ZIP was extracted and **rebuilt clean (0 warnings / 0 errors)**; it now contains `installer\` and all five new
project documents. The stale `dist\I-Total.Transfer-1.6.0-source.zip` was deleted.

**Live UI drill of the published build.** The new executable was launched and driven with UI Automation at
150 % scaling. Settings was scrolled to the bottom, where the Licence section read:

- **"Trial — 3 of 3 transfer jobs left"**, with the explanation "A job is counted only when a transfer writes
  at least one file and finishes with no failures. Cancelling, failing, or finding every file already present
  costs nothing."
- **Enter licence.** enabled; **Buy a licence** disabled (no purchase URL in this build); no Steam action
  (not the Steam edition).
- The section was fully inside the window and unclipped; the scrollbar reached its end.
- `%APPDATA%\I-Total Transfer\trial.json` read `"JobsUsed": 0`, `"ConsumedJobs": []`, proving the first-run
  counter is not consumed by launching or inspecting the app.

The window was returned to its previous size/theme and the settings file restored. Screenshot:
`%TEMP%\opencode\settings-licence.png`.

What this drill does **not** prove: a purchased licence cannot be activated (no issuing key pair/backend), and
no transfer was run in this session, so "a completed job decrements the counter" rests on the automated
engine tests rather than on this live pass.
### Follow-up: support visibility and installer encoding (same session)

Two items recorded as debt in `KNOWN_ISSUES.md` were closed without hardware:

1. **KI-18 - the diagnostics bundle now carries the trial state.** `DiagnosticsBundle` gained an optional
   `LicenseSnapshot`; it writes `licence.txt` with the standing, channel, jobs used/limit/remaining and the
   start decision, and deliberately omits the purchase id, ledger entries and fingerprints (the ledger file is
   not copied). `MainForm` passes `_license?.Snapshot()`. `DiagnosticsBundleTests` now has 5 tests and asserts
   both the presence of the counter and the absence of the purchase id/`trial.json`; a bundle built without a
   licence service says so instead of omitting the file.
2. **KI-16 - an automated ASCII guard.** `InstallerScriptTests` now has 4 tests and fails on any non-ASCII byte
   in `installer/*.ps1`, naming the file and line.

Commands and results:

```
dotnet build ... -c Release --no-incremental                 0 warnings, 0 errors
dotnet test ... --filter "DiagnosticsBundleTests|InstallerScriptTests"
  Passed! Failed: 0, Passed: 9, Skipped: 0, Total: 9
dotnet test ... --blame-hang --blame-hang-timeout 240s
  Passed! Failed: 0, Passed: 385, Skipped: 2, Total: 387, Duration 25 s
dotnet publish ... -o publish
  I-Total.Transfer.exe 112,113,478 B
  SHA-256 21D0D68690927BFC33E8D3D97D5E770FB806C50FF7B63B27B086F827DD31AEB7
```

**Live diagnostics export.** The published executable was driven with UI Automation: Diagnostics ->
*Export diagnostics ZIP*. The bundle written under `...\Pictures\I-total iPhone Backup\` contained
`licence.txt` reading `Standing: Trial`, `Channel: None`, `Jobs used: 0`, `Job limit: 3`,
`Jobs remaining: 3`, `New job allowed: True`, `Purchase id: deliberately not included`; `trial.json` was
absent, and the README described the same boundary. The earlier bundle had 7 entries; this one has 8, the new
one being `licence.txt`.

Archives are rebuilt after these edits; `dist\SHA256SUMS.txt` is authoritative for them.
### Tests affected by this session

- New: 35 automated tests - `LicensingTests` 15, `EntitlementCodecTests` 5, `TrialLedgerTests` 5,
  `SteamEntitlementTests` 4, `LicenseSettingsTests` 3, `InstallerScriptTests` 3.
- Suite total grew 350 -> 385 (passed 348 -> 383; the two Shell-backend probes remain skipped).
- Two earlier test expectations were corrected during the session because the tests, not the product, were
  wrong: a "tampered payload" test had signed an identical payload with a second key (so the bytes were the
  same and the signature legitimately verified), and a normalisation test expected the use count to stay
  below the number of recorded jobs.

### Files changed
src/I-Total.Transfer/Services/TransferService.cs  (JobId on both summaries)
src/I-Total.Transfer/Services/WirelessReceiver.cs (licence gate + metering)
src/I-Total.Transfer/UI/MainForm.cs               (service, gates, recording, licence actions, dispose)
src/I-Total.Transfer/UI/Pages/SettingsPage.cs     (licence section and actions)
CHANGELOG.md, README.md, PROJECT-STATUS.md, docs/TEST-PLAN.md, docs/RELEASE-ACCEPTANCE.md,
docs/BUILD-VALIDATION.md, docs/ARCHITECTURE-REVIEW.md, docs/NEXT-IMPLEMENTATION-PLAN.md,
docs/RESEARCH-SOURCES.md, docs/PERFORMANCE-AUDIT.md
```

### Blocked / pending

- Production licence key pair, issuing backend and purchase URL: **BLOCKED BY EXTERNAL REQUIREMENT**. The
  shipped build therefore has no public key; direct activation reports `NotConfigured` and the trial runs.
- Steamworks ownership call: **BLOCKED BY EXTERNAL REQUIREMENT** (SDK + partner account). Signed Steam
  entitlement path is implemented and tested.
- Inno Setup setup.exe: **BLOCKED BY EXTERNAL REQUIREMENT** (ISCC not installed). Package script reports it.
- Code signing: **BLOCKED BY EXTERNAL REQUIREMENT** (no certificate, no signtool).
- All physical-device drills: see `DEVICE_TEST_MATRIX.md`. Both phones disconnected on 2026-10-20.

### Next action

The publish, live UI drill and package are complete for this round. Next, when hardware is reconnected:
close TEST-PLAN checks 24-27 (attached diagnosis, Android walk/copy/PTP, mid-scan pull, two-phone
preference) and the cancel/resume, HEIC+MOV and benchmark rows in `DEVICE_TEST_MATRIX.md`. Separately, obtain
the code-signing certificate and Inno Setup, then run `installer\Sign-Artifacts.ps1` and
`installer\Build-Installer.ps1` against this publish before publishing the hashes. The production licence key
pair, issuing backend and purchase URL remain external blockers (KI-01/KI-02).

---

## Session 2026-10-22 - theme system, editable design source and above-the-fold actions

### Baseline actually run in this workspace

| Command | Result |
| --- | --- |
| `dotnet build I-Total.Transfer.sln -c Release --no-incremental` | 0 warnings, 0 errors |
| `dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s` | **437 passed, 0 failed, 2 skipped, 439 total, 26 s** |
| Targeted UI/theme suite (`ThemeTests`, `CaptionButtonContrastTests`, `AppearanceSettingsTests`, `SettingsTests`, `Dashboard*`) | **42 passed, 0 failed, 42 total** |

The immediately preceding run in this tree was 431/433. This pass adds five tests (the four real-control
`AppearanceSettingsTests` plus the new token/contrast coverage in `ThemeTests`) and then the no-device
neutral-wording regression in `DeviceCopyTests`, for 437/439. The older documents still quoted 405/407;
that figure was stale. The two skipped tests remain the opt-in Shell-backend probes.

### Theme system changes

- `Theme.cs`: Neon kept the banner-sampled backdrop (`#040A1E`), accent (`#0B55F1`) and six-stop gradient;
  working surfaces are deliberately desaturated navy. Slate was rebuilt as a charcoal layer stack
  (`#101114` base, `#17191D` card) with a restrained steel-blue accent (`#4A6BC4`) so it is not a recoloured
  Neon. Light keeps `#F3F4F7` page, white cards and the brand blue accent, with stronger borders and darker
  status colours.
- Added shared semantic tokens `Focus`, `SelectionFill`, `SelectionBorder`, `DisabledFill`, `DisabledText`
  and `Scrim`, plus `Theme.Luminance`, `Theme.Contrast` and `Theme.StatusColor`.
- `RoundedButton` now draws a 2 px keyboard focus ring from the `Focus` token, with the dotted focus fallback
  retained.
- `ThemeSwatch` is a new clickable miniature preview with a dedicated caption strip, hover/selected border
  and a selected check badge. It activates on click or Space/Enter and raises the existing theme-preview
  path; it is not a second settings store.
- `SettingsPage` Appearance section now shows Neon, Slate and Light swatches under the existing Theme combo,
  with a one-line description. A saved theme is restored to both the combo and the matching swatch; a fresh
  profile stays on Neon; `System` remains an OS-following combo option.

### Layout and header changes

- `DashboardPage.Designer.cs`: the metric row was reduced by 10 px and `AutoScrollMinSize` from 742 to 690.
  This keeps the primary action row above the fold at 1440x900 and 150 % scaling; before this, Start
  transfer sat under the status bar and the operator had to scroll to reach it. The action card still takes
  the remaining height on a taller window.
- `MainForm.cs`: the header caption now composes `Name + OperatingSystem + ConnectionType`
  (`Apple iPhone - 27.0.1 - USB (WPD)`) instead of a long readiness sentence that truncated to an ellipsis.
  The readiness sentence remains in the device card.
- No-device wording: `DashboardPage.SetDevice` now treats `DeviceStatus.NotFound` as an unidentified family
  even when the Shell probe's `DeviceInfo` reports Apple. The media title, state pill, OS label, tile captions
  and detail line fall back to neutral "phone" wording; `ShellIPhoneDevice`'s NotFound message is now
  `No accessible phone was found through Windows Shell.` rather than an Apple-only claim. This removes the
  `Connect your iPhone` / `What your iPhone exposes` text from the no-device screen and the Apple-only status
  bar line. `DeviceCopyTests.ANotFoundProbeUsesNeutralWording` fails if "iPhone" reappears in that state.

### Editable design source

- `C:\Users\Administrator\I-Total-Transfer-FigCraft\designs\i-total-transfer-design-system.figcraft`
  (69,239 bytes; four pages: `00 - Design system`, `01 - Neon`, `02 - Light`, `03 - Slate`).
- The foundations board carries semantic token swatches per theme, component/state sets, badges, and
  type/radius/spacing notes. Badge labels on bright amber/green/red fills were changed to dark labels in the
  two dark themes for contrast.
- Three 1440x900 Overview screens were rebuilt after the first render exposed an action-card overflow: the
  action card now ends above the 868 px status bar, metric glyphs are present, and the page glyph is a
  rounded plate rather than a font-dependent symbol.
- Exported renders:
  - `C:\Users\Administrator\I-Total-Transfer-FigCraft\renders\i-total-transfer-foundations.png`
  - `...\i-total-transfer-overview-neon.png`
  - `...\i-total-transfer-overview-light.png`
  - `...\i-total-transfer-overview-slate.png`

These are design renders. They do not prove runtime behaviour.

### Runtime evidence

DPI-aware `PrintWindow` captures of the current build at 150 % (2152x1380 physical):

- `C:\Users\Administrator\AppData\Local\Temp\opencode\ui-capture\overview-neon-final.png`
- `...\overview-light-final.png`
- `...\overview-slate-final.png`
- `...\settings-appearance-final.png`

The phone was disconnected during this session, so the final Overview captures honestly show
`No device connected` / `NO DEVICE` and `0` files. The no-device wording is now neutral
(`What your phone exposes`, `Connect your phone to check`, `No accessible phone was found through Windows
Shell.`); an earlier capture that said `Connect your iPhone` was superseded and retaken after the fix. The
earlier `light-dpi-aware.png` (same folder) shows a live iPhone-ready state from a previous build; it is
dated evidence of detection, not of the current polish pass or of a transfer. No device action is claimed
from the disconnected captures.

UI Automation measured the real Neon window: Start transfer at `x=1951..2251, y=1212..1272`, with the status
bar starting at `y=1332`, so the button is fully above the bar. The saved profile remains `"Theme": "Light"`;
every capture script backed it up and restored it.

### Files changed

New:

```
docs/UI-DESIGN-SYSTEM.md
docs/UI-REQUIREMENTS-MATRIX.md
src/I-Total.Transfer/UI/Controls/ThemeSwatch.cs
tests/I-Total.Transfer.Tests/AppearanceSettingsTests.cs
```

Modified:

```
src/I-Total.Transfer/UI/Theme.cs
src/I-Total.Transfer/UI/Controls/RoundedButton.cs
src/I-Total.Transfer/UI/Pages/SettingsPage.cs
src/I-Total.Transfer/UI/Pages/DashboardPage.Designer.cs
src/I-Total.Transfer/UI/Pages/DashboardPage.cs
src/I-Total.Transfer/UI/MainForm.cs
src/I-Total.Transfer/Devices/ShellIPhoneDevice.cs
tests/I-Total.Transfer.Tests/ThemeTests.cs
tests/I-Total.Transfer.Tests/DeviceCopyTests.cs
docs/MASTER_IMPLEMENTATION_PLAN.md
docs/PROGRESS.md
PROJECT-STATUS.md
docs/KNOWN_ISSUES.md
docs/DEVICE_TEST_MATRIX.md
CHANGELOG.md
```

### Release artifacts (2026-10-22)

- `dotnet publish ... -o publish` produced `publish\I-Total.Transfer.exe`, **112,133,958 B**,
  SHA-256 `249446554DEAB64121DE09563AD7B3747FF310D469F11081869BAEC46C1FBCB4`.
- `installer\Build-Installer.ps1 -Version 1.8.0 -ExpectedSha256 24944655...` produced the portable ZIP
  with ### Files changed-signing certificate and signtool**: still missing, so no `setup.exe` and no signed
  artifact.
- **Windows 11 and 200 % scaling**: still unobserved.
- **Audible cues**: cannot be heard from the harness.

### Next task

1. Rebuild, publish and package this tree; refresh `dist\SHA256SUMS.txt` and the exe hash in
   `PROJECT-STATUS.md`.
2. When a handset is reconnected: capture the current iPhone-ready Overview and a mid-scan Overview, then
   run the mid-scan disconnect, selected-phone-disappears, Android cancel/resume and HEIC/MOV drills.
3. Continue the row-by-row control audit for the five pages not covered by `UI-REQUIREMENTS-MATRIX.md`
   (UI-18), and mark any genuinely unfinished control as future work instead of leaving it ambiguous.

