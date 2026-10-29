# Build validation status — v1.8.0

## Executed in this workspace (2026-10-18, extended 2026-10-19/20/21/22 and 2026-10-24)

- .NET SDK **10.0.302** (pinned via `global.json` to `10.0.100` with `rollForward: latestFeature`),
  `net10.0-windows`.
- `dotnet build I-Total.Transfer.sln -c Release` → **0 warnings, 0 errors**.
- `dotnet test tests\I-Total.Transfer.Tests -c Release --blame-hang --blame-hang-timeout 240s` → **449
  passed, 0 failed, 2 skipped (opt-in hardware tests), 462 total**, in about thirty seconds.
  The 2026-10-21 addition is the two-phone isolation guard (`DeviceSelectionGuardTests` 6,
  `ScanIdentityTests` 8); the 2026-10-22 additions are the Appearance preview suite (4), the expanded
  token/contrast coverage and the no-device neutral-wording regression, the non-elevated private-LAN bind, the five page-control tests and the filtered-plan summary test, plus the 2026-10-24 96-DPI
  caption-fit regression, the installed-uninstaller self-location test and the AFC-status regression; the
  earlier count of 385/387 is superseded.
  Includes real **loopback and private-LAN** wireless tests (binding a `TcpListener` on a genuine socket: single-use pairing,
  session-token rotation and expiry, the approval gate in both directions, upload, SHA-256 on receipt, digest
  and size disagreement, traversal and reserved-path rejection, collision renaming, `/health`, refusal of
  public/wildcard binds, the staging sweep), the wireless page contract on real controls, the pairing QR
  encoder proven by an independent decoder, theme palette/migration tests,
  media-group mapping/gating tests, the camera-roll resolution rules, caption-button contrast on every
  theme, the scan-progress throttling rules, the Overview operation-band model and its live control
  state, the progress-pacing rules, the scan-cache honesty rules and persistence, the library page's
  cached-listing provenance, the transfer engine's staging/read-back behaviour, the
  context-capture regression tests, engine-throughput benchmarks that print MB/s and files/s,
  large-library stress tests (26,400 entries in one folder; 300,000 across 300 folders), the
  unfinished-session recovery rules and its real-engine continuation flow, the recovery surface's
  layout contract (the bar never squeezes the action panel), log redaction in both directions (a serial
  is masked, ordinary text is untouched), the second-pass scan comparison, the resumed-run path
  lookup (each device folder is read once per session, whatever the number of files in it), and the USB
  diagnosis ("remembered" never reads as "attached") with the diagnostics page's wrap/no-truncation
  contract and the diagnostics bundle's own export - which is driven while the log is held open, exactly
  as the running application holds it, and the Android device-family classifier with its generic-MTP
  storage policy (whole volumes, `Android\data`/`Android\obb` skips, the PTP-mode warning, the
  per-family group mapping, the two-phone order, the Apple-only attachment filter and the mid-scan
  disconnect guard) plus the family-aware diagnostics rows. **The Android device path was initially covered
  by unit tests only; that changed on 2026-10-21**, when the app walked both HUAWEI volumes (4,106 files /
  15.01 GiB, `Complete`) and copied a filtered 520-file selection (412 verified, 108 unverifiable, 0 failed).
  The live result is recorded in the 2026-10-21 bullet below; Android cancel/resume, lock, PTP and throughput
  remain open.
- **The commercial subsystem is covered by 37 new tests, and the installer is tested by running it.**
  `LicensingTests` (15) pins the trial rule (a job counts only when the transfer completed, was not
  cancelled, had no failures and wrote a non-zero file), the free cases (failed, cancelled, empty, partial,
  already-present), idempotent reporting through the engine's `JobId`, restart persistence, the loss of
  either of the two stores, a corrupt ledger, a read-only ledger, clock rollback, activation from a signed
  entitlement, refusal of tampered/expired/not-yet-valid/refunded/revoked/wrong-product/wrong-channel/
  wrong-app-id documents, the missing-key fail-closed default, and wireless metering. `EntitlementCodecTests`
  (5) proves the signature covers the readable half and turns ten malformed inputs into verdicts instead of
  exceptions. `TrialLedgerTests` (5) covers the file and HKCU stores and pessimistic merging.
  `SteamEntitlementTests` (4) covers app-id binding and refusal on the direct build.
  `LicenseSettingsTests` (3) drives the real Settings controls. `InstallerScriptTests` (6) executes the
  real PowerShell installer against temporary folders and a temporary HKCU key - install, wrong-hash refusal
  (exit 4), upgrade with settings/trial preserved, uninstall through the installed copy, and refusal to
  uninstall an unrelated folder (exit 3). The build stayed at **0 warnings, 0 errors**.
- **Packaging ran against the publish output.** `installer\Build-Installer.ps1` produced the portable ZIP and
  `SHA256SUMS.txt`; with Inno Setup 6.7.3 present it also produced `I-Total.Transfer-1.8.0-setup.exe`. The
  setup was installed silently into a temporary folder (`/VERYSILENT /DIR=...`): the installed exe hashed to
  the published executable, the HKCU uninstall entry, uninstaller and Start Menu group existed, no firewall
  rule was created, the installed app launched and exited 0, and the silent uninstaller removed the folder,
  key and shortcuts while `%APPDATA%` survived. An upgrade over a running installation exited 0 after Inno closed the app. `Sign-Artifacts.ps1` still fails closed without a trusted
  certificate (signtool itself is present), so nothing is signed.
- **The support bundle now carries the trial counter, and the boundary is tested.** `DiagnosticsBundleTests`
  (5) builds a bundle from a 2-of-3 snapshot and asserts that the counter and standing are present while the
  purchase id is absent and `trial.json` is not copied. The published build was then driven through
  *Export diagnostics ZIP*; the resulting `licence.txt` read `Standing: Trial`, `Jobs used: 0`,
  `Job limit: 3`, `Jobs remaining: 3`, `New job allowed: True`, and `trial.json` was absent. The README
  inside the bundle states the same boundary.
- **The installer scripts have an encoding guard.** `InstallerScriptTests` (6) fails on any non-ASCII byte in
  `installer/*.ps1`, naming the file and line. This is the regression guard for the PowerShell 5.1
  ANSI/BOM defect that broke `Build-Installer.ps1` during development.- **The test suite itself was a defect source twice over, and both causes were fixed.** Creating a
  WinForms control installs a `WindowsFormsSynchronizationContext` on the calling thread, whose `Post`
  only runs inside a message pump. That produced two hangs. The caption-contrast test created controls on
  the runner's thread, so a later `async` test's continuation was posted to a queue nothing drained: 89
  tests passed and then the run stalled until the harness killed it, while the same test passed in
  isolation - control work now runs on a dedicated STA thread and `WinFormsTestBase` restores the
  context around control-creating classes. Separately, and more seriously, `TransferService`'s entry
  points captured the caller's context across their `await`s, so a blocking caller with a UI context
  deadlocked on the 26,400-entry stress test; they now start on the thread pool
  (`Services/ContextFree.cs`), with three tests that install a pump-required context and prove scan,
  copy and cancel all still return and leave nothing queued. Tests that need real hardware remain opt-in
  (`ITOTAL_HARDWARE_TESTS=1`) and time-bounded, so the default suite is deterministic and cannot wait on
  a busy phone.
- **Live iPhone diagnosis (the fault this release fixes).** A physical iPhone 13 Pro, unlocked and
  trusted, was enumerated through the *same* call the app makes (`Shell.Application.NameSpace(17)`):
  *This PC* lists it as a portable device (`vid_05ac&pid_12a8`), it has exactly one child
  (`Internal Storage`), and that container's children are **`202003_a`, `202606_a`, `202608_b`,
  `201901_a`, `202608_a`** — the DCIM albums themselves, with **no `DCIM` level anywhere** (`202608_a`
  alone holds 26,320 items, mixing `.HEIC`/`.JPG`/`.AAE`). The app's own log from the same session
  agrees: both backends reported the device and its storage as visible and failed only on the missing
  `DCIM` container. This is the evidence that identified the fault.
- **Live end-to-end on the fixed build (new).** The handset was reconnected and the fixed application
  driven against it:
  - detection: `Backend probe Windows Portable Devices (native): Ready — iPhone media is accessible
    through native WPD (hash-verified transfers available).` The header showed the green **iPhone ready**
    blip and `Model: Apple iPhone · iOS: 27.0.1 · Serial: FCC4L3VYCW · Link: USB (WPD)`;
  - scan: `Scan finished: 26,335 files, 106.49 GB, status=Complete, duration=0.1 min` (4.9 s on a warm
    driver cache), and the dashboard cross-checks: 13,021 photos (19.79 GB) + 666 videos (86.69 GB) +
    sidecars = 26,335, with 26,320 of them in the single `202608_a` album;
  - current scan (2026-10-22): `55,185 files / 162,499,726,120 bytes (151.34 GiB)`, `Complete`, 23.3 min,
    11 folders, 0 failed folders, 0 warnings, 1,167 live-photo pairs;
  - filtered copy (2026-10-22): eight HEIC/MOV files (2,868,790 bytes) through the real WPD backend and
    shared engine, all eight source/destination SHA-256 pairs identical, 0 failed, 0 unverified;
  - the connection honestly reports **2 of 4 groups** (Photos, Videos) and states that Music and Files
    are not exposed by this transport.
- **An iPhone and an Android phone were attached at the same time (new, 2026-10-20), and that session both
  re-proved the iPhone leg and exposed a defect.**
  - Both devices were listed under "This PC": the iPhone's `Internal Storage` held twelve album folders, and
    the HUAWEI Y7 Prime 2018 exposed **two volumes** — `SD card` (22 top-level entries, including a
    1,209-entry `music` folder) and `Internal storage` (19 entries, including `DCIM`, `Download`,
    `Pictures`) — both enumerated through the same Shell call Explorer uses. The app's presence probe saw the
    Android phone as well.
  - The iPhone scan ran for **23.2 min** and counted **52,574 files / 166.72 GB**. That figure is historical:
    after the isolation fix the phone was cold-scanned again on 2026-10-21 and exposed **26,335 files /
    114,344,029,232 bytes** in 10.3 min (see the 2026-10-21 bullet below), so the current library is the
    smaller, later listing, not the interrupted 2026-10-20 walk.
  - The phone was then physically disconnected while that walk was finishing. The log shows the scan
    reporting `status=Complete` at 07:54:10 and then **9,500+ `Could not read properties ... Shutdown was
    already called on this object`** warnings for one album at 07:54:56-07:55:10, followed by "Cached this
    complete listing". So a listing missing the tail of the tree called itself complete: each unreadable
    object was only a warning and the folder was still recorded as read. **Fixed in three layers** — an
    unreadable object marks its folder a failed read (the scan becomes `Incomplete`), twenty consecutive
    failures abort the walk with `DeviceStoppedAnsweringException` and an instruction to reconnect, and the
    cache writer only ever sees a complete result. `TransferRules.DeviceStoppedAnswering` and
    `FilterAndConflictTests` pin the boundary. **The fixed behaviour has not been re-observed live**, because
    the phones were disconnected before the rebuilt exe could be run.
  - The same session showed the Apple USB diagnosis reporting *"Attached and answering: HUAWEI Y7 Prime
    2018"* under a row titled *"Apple handsets seen over USB"*: the presence probe had (correctly) returned
    the Android phone, and the Apple-specific row took it. The evidence is now filtered by family
    (`AppleDeviceHistory.Evidence(IEnumerable<DevicePresence>)`), and the two-phone preference below was
    added so that the Android leg can be selected deliberately.
  - No Android walk or copy ran in that 2026-10-20 session. The `settings.json` write in the drill harness
    silently dropped the new preference property (a PowerShell 5.1 `PSCustomObject` assignment to a property
    that did not exist yet), so the app read the iPhone. **Superseded on 2026-10-21:** the Android walk and
    a filtered copy ran successfully (see the 2026-10-21 bullet above); cancel/resume, locked-screen and PTP
    drills remain open.
- **2026-10-21: Android walked/copied and a cross-device scan defect fixed.** Both handsets were attached.
  The HUAWEI was walked end to end through WPD on 2026-10-22: **4,103 files / 15,612,056,299 bytes (14.54 GiB)**,
  `Complete`, 307 folders. The 2026-10-20 **4,106 files / 16,114,057,157 bytes (15.01 GiB)** listing is historical.
  The 2026-10-20 filtered copy planned 520 files with **412 source-and-destination SHA-256
  verified, 108 `COPIED-UNVERIFIABLE` (0-byte `Sounds\.pictures`), 0 failed, 3,586 skipped** and ended
  honestly as `COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP`; resuming preserved the 108
  unverified files and the trial stayed at 0 jobs. During the same session a scan started for the Apple
  iPhone read the HUAWEI's 4,106 files and overwrote the iPhone's WPD cache. `DeviceSelectionGuard`,
  `ScanResult.BelongsTo`, the MainForm acceptance guard and the cache guard now refuse a phone switch;
  `DeviceSelectionGuardTests` (6) + `ScanIdentityTests` (8) pass. The corrupted cache was preserved at
  `%TEMP%\opencode\corrupt-iphone-cache-69ffba35.json` and deleted. The current cold iPhone pass completed on
  the fixed build on 2026-10-22: **55,185 files / 162,499,726,120 bytes (151.34 GiB), `Complete`, 23.3 min**,
  11 folders, 0 failed folders, 0 warnings. The 2026-10-21 **26,335 files / 114,344,029,232 bytes / 10.3 min**
  pass and the 2026-10-20 **57,371 files / 188,810,836,019 bytes / 10.99 s** warm pass are historical.
- **The unattached-device wording was re-driven on the published build with nothing attached (new).** After
  the family work, *Run diagnostics* led with `CACHED RECORD ONLY (2)` and carried **`Device family: NOT
  IDENTIFIED`** with the explanation that the checks describe this PC rather than a phone. Two Apple-specific
  rows were corrected in the same pass and re-verified: `Trust state` now reads **`CHECK PHONE`** and names
  both prompts (tap Trust on an iPhone, allow file access and choose *File transfer* on an Android phone)
  instead of sending an Android owner after a prompt their phone never shows, and `iCloud originals` is
  Apple-only, so an unidentified device sees 9 checks and no Apple-only advice. The header no longer says
  "iPhone to Windows" on an unattached device. The Settings page's new Backend row
  (*When both phones are attached, read* → *the iPhone*) was captured at 150% scaling with nothing clipped,
  and the bundle was exported again from the running build (7 files, 3,401 B). The listing the pre-fix build
  had cached from the cut-off scan was deleted from `%APPDATA%\I-Total Transfer\scan-cache\`, because a
  truncated listing that calls itself complete must not survive as a "last known library".
- **The Overview's new operation band was captured in the real window.** It renders between the headline
  metrics and the destination block with its sweep idle at rest, and the Light-theme capture of the same
  window shows the caption glyphs dark-on-light (the contrast defect) now legible. The band's
  *scanning* and *transferring* states are pinned by `OperationStatusTests` (6) and
  `DashboardOperationBandTests` (3, real controls), because the handset was detached before the band
  existed and a warm-cache scan finishes in 4.9 s — too fast to catch reliably by screenshot.
- **A clipping regression was caught and fixed in the same pass.** Adding the band took 52 px, which
  squeezed the action panel below its content height and sliced the integration line in half; the defect
  was confirmed in a `CopyFromScreen` capture, then fixed by declaring the page's true content height as
  `AutoScrollMinSize` (a short window scrolls; a tall one still stretches the action panel) and confirmed
  gone in a second capture.
- **Engine throughput was measured on local storage, and the measurement found a real defect (new).** A
  deterministic benchmark (`EngineThroughputBenchmarkTests`) drives the real engine - staging, streaming
  SHA-256, atomic commit, manifest and database writes - against a local disk: **4,000 small files went from
  45.76 s (≈ 87 files/s) to 5.69-6.60 s (≈ 670-703 files/s), roughly 8x**, with the verification level
  unchanged, while large-file throughput stayed at ~600 MiB/s. The causes were recovery bookkeeping, not the
  data path (a forced full-manifest write per file; two SQLite transactions per file plus one per planned
  entry at session open; a stat/open/append/close of the log file per logged line). These numbers describe
  the engine and the destination disk, **not** a phone: the device leg was not re-measured, because the
  handset was detached. See `docs/PERFORMANCE-AUDIT.md` §4.1.
- **The scan-listing cache was verified in the running application (new).** A listing was seeded into the
  real cache folder (`%APPDATA%\I-Total Transfer\scan-cache\`) and the packaged exe was driven with UI
  Automation: the Settings page read **"1 listing, 1,366 B"**, and pressing *Forget cached listings* raised
  the confirmation, whose OK removed the file — afterwards the folder was empty, the row read **"No listing
  is on disk."** and the status bar said **"Removed 1 cached library listing(s)."** The listing store, its
  inventory and its removal path are therefore verified against the real artifact, not just the unit tests.
  The *display of a cached listing beside a running scan* is unit-tested only: it needs a device, because
  the code path runs after the probe succeeds. See `docs/PERFORMANCE-AUDIT.md` §4.2.
- **Measured enumeration cost (new).** A timed Shell enumeration of that 26,320-item album took
  **723,017 ms** on a cold cache — **≈ 27 ms per object**, nearly all of it in the first
  `GetFolder.Items()`/`.Count`. Warm per-item property reads are 0.1–0.25 ms. The scan is therefore
  transport-bound, identically for Shell and WPD, which is why the fix was to make progress *visible*
  rather than to micro-optimise the property set. The full detail, including what is deliberately not
  claimed, is in [`PERFORMANCE-AUDIT.md`](PERFORMANCE-AUDIT.md).
- **An acceptance audit found and fixed three defects, two of them cost (new, 2026-10-19).** Working the
  release checklist against the code turned up places no hardware test had reached. (1) A **resumed** run
  located each file by re-reading its album and a property per sibling, once per file — quadratic in the
  album, so a 10,000-photo album meant tens of millions of USB round-trips before the first byte moved. Each
  device folder is now read **once per session** (`Core/FolderNameIndex.cs`); the property is measured
  without a device — **26,100 files across three albums cost four folder reads**, where the old lookup would
  have read an 8,700-file album 8,700 times. (2) The **second-pass scan comparison** was quadratic on the UI
  thread: **4,705 ms → 5.7 ms** for the same 26,000 paths, measured by re-running the removed code for the
  purpose (`Core/ScanStability.cs`). (3) A device **serial was not redacted at all** and a credential
  assignment left its value behind (`pairing_token: …` → `pairing_token=…REDACTED …`), in a log that ships
  inside the destination and inside a diagnostics bundle whose README claims no full device identifiers;
  `LogService.RedactSensitive` now masks labelled identifiers and consumes assignments, while leaving ordinary
  operational text byte-for-byte (`LogRedactionTests`, 9). The checklist, with the evidence for every line, is
  [`RELEASE-ACCEPTANCE.md`](RELEASE-ACCEPTANCE.md).
- **The connection diagnosis and the support bundle were driven live on the published exe (new, 2026-10-19).**
  With **no** handset attached, *Run diagnostics* led with `CACHED RECORD ONLY (2)` — *Apple iPhone, Apple
  iPad* — read from this machine's own USB registry, with the guidance quoting Device Manager's own
  *"Currently, this hardware device is not connected to the computer"*; the new wrap-and-grow rows were
  captured at the machine's real **150% scaling** with the whole paragraph readable, where the old fixed-height
  row would have cut it off. Pressing *Export diagnostics ZIP* on the **running** build then failed —
  `The process cannot access the file '…\I-total-transfer.log' because it is being used by another process` —
  and that is how a defect which had stopped **every** bundle from being written was found:
  `DiagnosticsBundle.Create` read the log with `File.ReadAllLines`, whose read-only sharing is incompatible
  with the writer the application holds open for its whole life. With the read made write-shared, the same
  press produced a **7-file** ZIP (environment, redacted settings, backends, dependencies, `logs-tail.txt`,
  `context.txt`, README) carrying that run's own log lines and the attachment verdict;
  `DiagnosticsBundleTests` now holds the log open exactly the way the product does.
- **The published artifact was verified through its own UI (new).** The packaged exe was launched and
  driven with UI Automation to the Settings page, where the new Performance rows were read back: the
  concurrency control reports `enabled=False` with the label *fixed at 1 — an iPhone session serialises
  copies*, and "Copy buffer size" is a live combo box. Separately, the exe inside the deployment ZIP was
  extracted and its SHA-256 compared against `publish\I-Total.Transfer.exe`; they match.
- **The unfinished-session bar was verified on the published build (new, 2026-10-19).** A 26,335-entry
  session record (11,143,635 B) was seeded into a scratch destination and the packaged exe was launched
  with that destination. The Overview rendered the interrupted-session bar quoting the record's own
  numbers — `Apple iPhone · 26,335 planned · 1,203 copied and verified · 25,132 outstanding (12 failed,
  4 unreadable)` — and the log recorded the unfinished session at `06:57:19.658`, *before* the `Welcome`
  line at `06:57:19.724`. UI Automation clicked **Not now**: the bar collapsed (the row returned to zero
  height and the page reflowed), the status bar read *"Unfinished session set aside. 25,132 file(s),
  172.12 GB, are still outstanding in the destination."*, and the session's own fingerprint was written to
  `AppSettings.DismissedRecovery` (`…|639279744630000000|26335|25132`). With the handset attached the same
  bar reported **Resume transfer `enabled=True`**; with it detached, `enabled=False` and the message said
  why. Replacing the record with 32 bytes of nonsense produced the other branch live: *"This folder's
  session record could not be read"* with **no action** offered (a dismissal only) and the damaged file
  still on disk, 32 bytes, afterwards. A capture of the fixed layout confirmed the two-line message renders
  in full and the action panel is no longer squeezed by the extra row (the defect fixed in the same pass,
  pinned by `DashboardRecoveryTests.TheOfferNeverSqueezesTheActionPanel`).
- **A second layout defect was found in that capture and fixed (new).** The window runs at **150% scaling**
  (measured: `GetDpiForWindow` = 144), where a row height assigned at run time is read as device pixels while
  the designer's values are scaled for the display. The new row therefore came out 94 device px (≈ 63
  logical) tall against a bar whose own designer-scaled content was 126, and the second line of the message
  was painted past the bottom of its card. Measured with UI Automation before and after the fix: the row's
  following content moved down by exactly 47 device px (94 → 141), the message's bottom (1,041) now sits
  inside the bar (which ends at ≈ 1,065), and the fixed capture shows both lines inside the amber card with
  the page scrolling rather than squeezing. The row and the page's floor are now converted for the display
  from the layout pass, so they also stay right if the window moves to another monitor. No test at 100%
  could have caught this; the unit test pins the relation (the message must fit inside the bar) and the
  150% case is recorded here as a capture. **What this does not cover:** the record was
  seeded, so the *engine* leg — a real transfer cancelled on a live device and then continued — is not in
  this record; the engine flow is pinned by `RecoveryFlowTests` (6, real engine, mock backend). See
  [`docs/RECOVERY.md`](RECOVERY.md).
- **Camera-roll resolution rules** are unit-tested (`CameraRollTests`, 8 tests) using those verbatim live
  folder names: a `DCIM` container wins wherever it appears, the storage container is accepted when its
  children are album folders or media, and an unrelated volume (`Music`, `Books`, `Downloads`, …) or a
  lone `.AAE` sidecar is rejected rather than mistaken for a camera roll.
- **Shell backend apartment** (`ShellBackendApartmentTests`, 2 tests): the Shell backend is driven from an
  MTA thread exactly as the dashboard drives it and must answer within the timeout instead of hanging,
  and must dispose cleanly (twice). Both pass with no device attached. This also caught a real defect
  during development: the message-pump `P/Invoke` originally failed to bind (`PeekMessage` is only
  exported as `…W`/`…A`) and killed the process from the worker thread — the pump is now charset-correct
  and cannot take the app down.
- Startup smoke test: the published `I-Total.Transfer.exe` launches and stays resident (no crash).
- `dotnet publish ... -r win-x64 --self-contained true -p:PublishSingleFile=true` → single-file exe;
  `native/dependencies.json` is copied next to it.
- **Window chrome interaction test** (scripted mouse/keyboard automation driving the real window):
  maximize → `-11,-11` 3462x1402 zoomed; restore → the exact prior rectangle (Δx=0, Δy=0, Δw=0, Δh=0);
  drag on the banner band → moved by exactly the pointer delta; minimize → iconic; restore-from-minimized
  → exact prior rectangle; close → the process exits.
- **Theme apply-on-save test** (scripted, driving the combo box): selecting *Light* repaints the shell in
  place and writes `Theme = Light` into `%APPDATA%\I-Total Transfer\settings.json`.
- GUI layout check: the window is captured at **150% DPI** (3440x1380 working area) through `PrintWindow`
  (`PW_RENDERFULLCONTENT`) and through `CopyFromScreen`; the two captures agree. The maximized client
  exactly fills the working area with the taskbar respected and the top of the banner band on-screen.
  All seven pages were captured maximized and inspected.
- Fluent surface check: both banner bands, the painted caption buttons on the band, rounded cards with
  hairline borders, the accent gradient on primary actions and the selected group chip, the nav selection
  tint, category accent ramps on the four media tiles and the four metric cards, the status blip, the
  empty state, the gradient progress bar with its percentage caption, and the custom check boxes all
  render; text and symbols (…, →, ·, ●, —) render without mojibake.
- **Rendering-defect verification.** An inverted-corner seam in the dashboard tiles was isolated by
  logging each control's paint size and clip rectangle: `MetricCard` was the only hand-painted control
  without `ControlStyles.ResizeRedraw`, so stretching it from its design width to its column width
  repainted only the newly exposed strip. The artefact was confirmed to be real (a `CopyFromScreen`
  capture) before fixing, and confirmed gone afterwards (one continuous accent stripe per tile).
- Icon check: `app.ico` is re-read from disk and every one of its nine frames renders the new artwork at
  the correct size.
- `"Follow Windows"` resolution was checked against the live registry flag (`AppsUseLightTheme = 0`).
  `ThemeTests` assert the sampled banner palette values, the default and the legacy-`Dark` migration.

## Master-directive candidate build (2026-10-29)

Executed in this workspace after the size-truth, SQLite, reconciliation, retry, stability, COM and
accessibility changes:

```text
dotnet build I-Total.Transfer.sln -c Release --no-incremental
  -> Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s
  -> Passed! Failed: 0, Passed: 510, Skipped: 2, Total: 512, Duration: 31 s

dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
  -> publish\I-Total.Transfer.exe, 112,207,686 bytes
     SHA-256 BAB606840BE38C11CAC8F7C2D6F63BE1389B73ACAF3BCFAFF56BBDCCDD9B6C1A
```

The source ZIP was extracted into a clean folder, published there, and produced the **same** executable
hash, so the source-only rebuild is byte-identical to this publish.

The two skipped tests are the documented Shell apartment tests. The installer was rebuilt from this
executable (`portable` and `setup`), silently installed into a temporary folder, checked (exe and README
hashes match), and uninstalled; the folder, HKCU entry and shortcuts were removed and `%APPDATA%` was kept.

The published executable was started on this machine and was still running after five seconds with the
window title `I-Total Transfer - verified phone media transfer` (working set 161.6 MB); it was then
stopped. This is a launch smoke test, not a UI or device test.

No physical device was attached for this build, so no hardware row is claimed for it. No performance
measurement was taken for these changes and none is claimed.
## Clean-clone reproducibility (2026-10-24)

A fresh `git clone` of the committed tree to a temporary folder was built, tested and published:

- build: **0 warnings, 0 errors**
- publish (`win-x64`, self-contained, single file): SHA-256 `411F4CC5...` - **byte-identical** to
  `publish\I-Total.Transfer.exe`
- full suite: **453 passed, 0 failed, 2 skipped (455 total), 27 s**

The first clone attempt produced `7C126C55...` instead. The cause was line endings: every C# file was LF
in the working tree, while `core.autocrlf=true` checked the clone out as CRLF, and the deterministic MVID
follows the source bytes. `.gitattributes` now sets `* text=auto eol=lf` (`*.sln`, `*.bat` and `*.cmd`
stay CRLF; `*.zip`, `*.png`, `*.ico` and `*.dmp` are binary), `.editorconfig` matches, and the working
tree was re-checked-out from the index. With that policy the clone, the source ZIP and the published
tree all compile to the same bytes.

## Not validated here

- **No production licence has ever been activated by this build.** No issuing backend or key pair exists, so
  the published executable carries no public key and reports `NotConfigured`. The verifier and its negative
  cases are tested with generated keys; a real purchase, refund and revocation push remain outstanding.
- **The Steam edition has never talked to Steamworks.** The signed-entitlement path is implemented and tested;
  a live ownership check needs the SDK and a partner account, and eligibility is unconfirmed.
- **Nothing shipped is Authenticode-signed.** signtool 10.0.26100.0 and `Sign-Artifacts.ps1` are present; a
  temporary self-signed certificate drill signed and verified a copy and was discarded. The release remains
  unsigned for want of a trusted certificate.

- **Detection and enumeration *were* re-run against a handset and passed** (see the live section above:
  a cold `55,185 files / 151.34 GiB, status=Complete` in 23.3 min on 2026-10-22; earlier sessions recorded
  26,335, 52,574 and 57,371 files). A filtered HEIC/MOV copy then fetched eight files through the real WPD
  backend and shared engine, all eight source/destination hashes equal, 0 failed. What remains owed is
  everything downstream of the listing: the handset dropped off USB while the before/after **transfer
  benchmark** was being set up, so the speed effect of removing the per-file existence probe and the
  redundant staged read-back is implemented, reasoned from measured call costs and unit-tested, but **not
  measured on a device** - and no number is claimed for it. See `docs/PERFORMANCE-AUDIT.md` A5.
- **The mid-scan disconnect fix has not been re-observed live.** The defect was found by a real disconnect
  (above); the rebuilt behaviour — `Incomplete` instead of `Complete`, and the walk aborting after twenty
  consecutive failures — is pinned by tests and by the rule's own boundary test, but no phone has been
  unplugged in front of the fixed exe. The two-phone preference *did* select the Android phone on
  2026-10-21, and that session exposed the cross-device scan defect now covered by the isolation guard;
  the physical selected-phone-disappears drill is still open.
- **The cold scan ran, but the cached listing beside a live scan was not captured.** The 2026-10-21 cold
  camera-roll pass completed in 10.3 min with the progress line on screen (26,335 files / 106.49 GiB). The
  combined *last-known library beside a running scan* capture is still missing: `ShowLastKnownLibrary` runs
  after the device probe succeeds, and only its store side (the Settings inventory and removal path) has been
  verified live. The library page's provenance line and the change report are pinned by tests.
- **Some device-facing legs still have not run.** A statistical multi-file benchmark, the attached
  branch of the USB diagnosis and a real iOS wireless sender remain **not** hardware-tested. The Android
  walk/copy, cancel/resume, PTP warning, single-file buffer sweep, mid-scan unplug and selected-phone
  disappearance are live; a lock is not a disconnect on this MTP stack. The app ships no driver of its own and needs none: Apple's Mobile Device
  Support *is* installed on this machine (Diagnostics reports the driver files at
  `C:\Program Files\Common Files\Apple\Mobile Device Support\Drivers\usbaapl64.inf`), and the app uses the
  operating system's own WPD/Shell stack. (The unplanned disconnect at the end of that session was survived
  without a crash: the next probe simply reported the device as not found.)
- **Android cancellation, PTP and a single-file buffer sweep are now live.** The 2026-10-21 walk/copy and
  2026-10-24 cancel/resume, PTP warning, buffer sweep and 30-file mixed batch are recorded above. No statistical benchmark remains
  open; a screen lock does not disable MTP on this handset.
- **The *attached* branch of the USB diagnosis was seen only in its defective form.** On 2026-10-20 it read
  `ATTACHED — Attached and answering: HUAWEI Y7 Prime 2018` under a row titled *"Apple handsets seen over
  USB"*; the family filter now keeps the Android name out of it, and that corrected wording is unit-tested
  only. The locked-handset variant (ATTACHED while the media rows explain that media is not readable) has
  never been observed.
- **The audible output of the UI sounds was not verified** — this machine's speakers cannot be listened
  to from the test harness. The tones are synthesised in memory and their code paths are exercised, but
  "you can hear a tick when you press a button" is a claim for a human to confirm.
- The AFC backend is intentionally inert (see pinning below), so no AFC transfer can occur yet. Its new
  multi-root enumeration is compiled and unit-adjacent code is exercised, but no device has listed
  `/iTunes_Control/Music`, `/Books`, `/Downloads` or `/Purchases` here.
- The `Follow Windows` live re-theme on an OS light/dark flip is coded (`SystemEvents.UserPreferenceChanged`)
  but was not exercised here, since toggling the OS theme was out of scope for this pass.
- **Captures and interaction tests were taken on Windows 10 Pro (build 19045).** Windows 11 rendering is
  therefore **not captured**: DWM rounded corners, the native dark scrollbars and titleless dark caption,
  and `Application.SetColorMode` remain code paths with documented fallbacks rather than verified pixels.
- Only the banner-band drag, the caption buttons, minimize/restore and close were automated. Dragging to a
  screen edge (Snap) and the eight-way edge/corner resize were reasoned about from the retained window
  styles but not scripted here.

## Pinning the native AFC runtime (fail-closed procedure)

`native/dependencies.json` lists each native binary with a `sha256` pin. An empty pin means the
binary is **refused**, and `DependencyVerifier` never loads it. To enable AFC, a maintainer must:

1. Obtain a reviewed, licensed build of each dependency (libimobiledevice, libplist, libusbmuxd,
   and the OpenSSL `libcrypto` it links).
2. Verify provenance (official release, matching architecture, recorded version).
3. Compute the SHA-256 of each binary.
4. Write the 64-hex-digit digest into the matching `sha256` field.
5. Re-run `dotnet test` — `DependencyVerifierTests` asserts no dependency may be enabled without a pin.
6. Record the pins in the release notes so a user can reproduce them.

Until steps 1–4 are completed for a reviewed build, dragging an arbitrary DLL into `native\` can
never enable the AFC backend.

## Before treating a build as release-ready

Run `Test-Project.bat` and `Build-Release.bat` on Windows 10 and Windows 11, then work through the
hardware checklist in `docs/TEST-PLAN.md` with a physical iPhone.
