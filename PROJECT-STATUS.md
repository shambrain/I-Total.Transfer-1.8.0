# Build status & verification boundary — v1.8.0 (2026-10-24)

This is a complete **source** project, not a signed installer. It is built and tested here, but the
device-facing paths cannot be hardware-verified in this environment.

## Verified in this workspace

| Check | Command | Result |
| --- | --- | --- |
| Release build | `dotnet build I-Total.Transfer.sln -c Release` | ✅ 0 warnings, 0 errors |
| Tests | `dotnet test tests\I-Total.Transfer.Tests -c Release --blame-hang --blame-hang-timeout 240s` | ✅ 449 passed, 0 failed, 2 skipped (451 total; including the non-elevated LAN bind, the six page-control tests, the installer self-location test, the 96-DPI caption-fit and the AFC-status regressions; ~29 s, no phone required) |
| **Android storage policy** | `DeviceFamilyTests` (11) + `PortableStorageTests` (13) + `ConnectionDiagnosticsTests` (5) | ✅ the family is decided from what the device calls itself (never `WPD_DEVICE_TYPE`, which Microsoft documents as visual only and says must not decide function); an Android phone is walked as **whole volumes**, each keeping its own name as its destination folder; `Android\data`/`Android\obb` and thumbnail caches are skipped while `Android\media` is kept; a camera-only listing raises the PTP warning; the diagnostics rows are family-specific. ⛔ **Live Android result (2026-10-20)**: a HUAWEI Y7 Prime 2018 was walked end to end through native WPD: **4,106 files / 16,114,057,157 bytes (15.01 GiB)**, `Complete`, about 6 s. A filtered copy planned 520 files: **412 source-and-destination SHA-256 verified, 108 `COPIED-UNVERIFIABLE` (0-byte `Sounds\.pictures` files), 0 failed, 3,586 skipped**, ending `COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP`. Resume preserved the 108 unverified entries with 0 failures. The trial stayed at 0 jobs because that run was not meaningful. |
| **Two phones attached** | `DeviceFamilyTests` + `SettingsTests` + `AttachmentDiagnosisTests` (13) | ✅ Settings → *When both phones are attached, read* reorders the two families without making either unreachable (`AppleFirst` is the default and the enum's zero value, so a pre-Android machine behaves exactly as before); the preference round-trips through `settings.json`, and an older file without the property still means iPhone-first. The Apple USB diagnosis now filters attached devices by family - with an Android phone answering on the same bus it had shown `HUAWEI Y7 Prime 2018` under *"Apple handsets seen over USB"*. Live ordering was verified with both attached (`AndroidFirst` selected the HUAWEI; `AppleFirst` selected the Apple iPhone), but the same session found a **cross-device scan defect**: a scan started for the iPhone read the HUAWEI after the iPhone briefly disappeared from re-enumeration and overwrote the iPhone cache. `DeviceSelectionGuard`, `ScanResult.BelongsTo`, a UI guard and a cache guard now refuse that switch; 14 tests cover the rule. The physical selected-phone-disappears drill passed on 2026-10-24: the iPhone was unplugged while the HUAWEI stayed attached, and the app refused the switch and rejected the listing. |
| **Cross-device scan isolation** | `DeviceSelectionGuardTests` (6) + `ScanIdentityTests` (8) + live two-phone run | **LIVE PASS 2026-10-24.** The 2026-10-21 defect is fixed: a scan now binds to the selected phone and refuses a switch. On 2026-10-24 the iPhone was unplugged while the HUAWEI remained attached; the app showed that the selected iPhone was no longer answering, stated that the HUAWEI was not used, and rejected the scan as incomplete. No HUAWEI rows were accepted, cached or queued. |
| **Phone unplugged mid-scan** | live runs 2026-10-20 and 2026-10-24 + `FilterAndConflictTests` | **LIVE PASS, with a new defect found and fixed.** The 2026-10-20 pull had produced a false `Complete`. On 2026-10-24 the pre-fix build still accepted 28,655 names with 25,843 unknown sizes as `Complete`; the fix now classifies a high unknown-size share as `Incomplete`, re-probes the phone before acceptance, and refuses Start transfer on an incomplete listing. The final repeat ended **2,326 files / 8.05 GB, `Incomplete`**, with 1,739 unknown sizes, and the UI showed **“the iPhone stopped answering during the scan. The listing is incomplete and was not accepted.”** No report or cache was written. |
| **Attached diagnostics, live** | current build, both handsets attached, real WPD and PnP, 150 % host | 2026-10-24: AppleFirst *Run diagnostics* produced 10 checks - Apple handsets `ATTACHED` (2 support interfaces named as unable to supply media), Windows device access `PASS`, family `APPLE`, software `DETECTED`, drivers `FOUND`, trust `ACCESSIBLE` - and the summary read `Attached: HUAWEI Y7 Prime 2018 (Android), Apple iPhone (Apple).` With only the HUAWEI attached, the page produced its 8 Android rows, named the HUAWEI alone and showed no Apple rows. The run exposed `Native AFC runtime: VERIFIED` while no pinned binary was present; after the fix a repeat run showed `NOT PRESENT` (`AMissingAfcRuntimeIsNotReportedAsVerified`). Rows: `%TEMP%\opencode\diag-live\diagnostics-attached.txt`; page: `...\diagnostics-attached-page.png`. Locked and unplugged variants remain. |
| **No-phone diagnostics after the family work** | published 1.8.0 exe, no handset attached, 150% scaling, real capture | ✅ the page leads with `CACHED RECORD ONLY (2)` and carries **`Device family: NOT IDENTIFIED`** (amber) with the explanation that the page describes this PC, not a phone; **`Trust state` reads `CHECK PHONE`** and names both prompts (Trust for an iPhone, File transfer for an Android phone) instead of sending an Android owner after a prompt that never appears; `iCloud originals` is Apple-only, so the page shows 9 checks and no Apple-only advice for an unidentified device; the header is neutral (*Phone to Windows*, *Plug in by USB and unlock the phone*); 7-file bundle exported while running |
| **Trial and entitlement rules** | `LicensingTests` (15) + `EntitlementCodecTests` (5) + `TrialLedgerTests` (5) + `SteamEntitlementTests` (4) + `LicenseSettingsTests` (3) | **PASS** - the trial counts only a completed, uncancelled transfer that wrote a file with no failures; failed/cancelled/empty/already-present runs are free; a repeated report is free; the counter survives restart and the loss of one of its two stores; a clock moved back cannot reset it; a read-only ledger still lets the transfer finish and says so; signed entitlements lift the limit; tampered, expired, not-yet-valid, refunded, revoked, wrong-product, wrong-channel and wrong-app-id entitlements are refused with their own verdict; no public key means `NotConfigured`; one wireless sender session costs one job. No private key or certificate ships |
| **Per-user installer** | `InstallerScriptTests` (6), real PowerShell script, temporary folders + temporary HKCU key | **PASS** - install writes the executable, manifest and uninstaller and registers an HKCU uninstall entry naming the uninstaller; upgrade replaces the executable while `settings.json`/`trial.json` survive; uninstall through the installed copy removes the folder and key but keeps user data; a wrong hash exits 4 and installs nothing; uninstalling an unrelated folder exits 3 and deletes nothing. No admin rights, no firewall rule. The final portable ZIP was extracted and its own installer ran with no `-SourceDir`: install, manifest, HKCU key, uninstall and user-data preservation all returned OK. The installed uninstaller now finds its own folder, so an installation made with `-InstallDir` is removable by the command printed in `README-INSTALL.txt`; `TheInstalledUninstallerFindsACustomInstallFolderOnItsOwn` fails without that rule. |
| **Diagnostics bundle licence summary** | `DiagnosticsBundleTests` (5) + a live export from the published build | **PASS** - `licence.txt` carries standing, channel, jobs used/limit/remaining and the start decision; the purchase id, ledger entries and fingerprints are excluded and `trial.json` is not copied. The live bundle read `Trial`, `0` of `3`, `New job allowed: True` |
| **Installer script encoding guard** | `InstallerScriptTests` (6) | **PASS** - any non-ASCII byte in `installer/*.ps1` fails the suite with the file and line, because Windows PowerShell 5.1 reads a BOM-less script as ANSI |
| **Wireless security model** | `PairingStateTests` (13) + `ApprovalInboxTests` (9) + `PrivateNetworkTests` (21) | ✅ rules pinned: the displayed pairing secret is single-use and is **refused as an upload credential** (redeeming it mints a separate session token); sessions expire, rotate with the old token dying immediately, are capped at eight and are revoked on Stop; an issued secret invalidates the previous one; an offer waits for the operator, a refusal sticks against a retry, auto-approval is an explicit preference, offers expire; loopback and the three RFC1918 ranges bind, while `0.0.0.0`, `*`, `+`, public and carrier-grade addresses are refused with the reason named |
| **Wireless receiver, real HTTP** | `WirelessReceiverTests` (21, real loopback and private-LAN `TcpListener`) | ✅ genuine sockets, not mocks: unauthorized and secret-as-session uploads are `401` and write nothing; redeeming a secret twice is refused; an offer is visible in the approval list **before any byte is written** and the sender's request only completes on Approve (`200`) or Refuse (`403`); a declared digest that disagrees is `422` and leaves no staging file; traversal and `.i-total` are `400`; an existing file is renamed, not overwritten; rotation retires the old token; the health endpoint never echoes the secret; a public address, the wildcard and `WirelessEnabled = false` all refuse to start; a restart sweeps orphaned staging fragments. **Added 2026-10-22:** `APrivateLanAddressBindsWithoutAUrlAcl` binds `192.168.8.91` as the non-elevated user and answers `/health`, the regression for the old HTTP.sys `Access is denied.` failure. |
| **Wireless receiver, driven live** | the current published `.exe` + a real HTTP client over loopback and the private LAN | ✅ the whole contract observed, not argued: `401` for no credential and for the secret used as a session (nothing written); the secret redeemed once and a second redemption refused as already used; a 1 MiB upload **pending with nothing on disk** until Approve, then `200` with the stored SHA-256 matching the bytes sent; a refusal `403` with a retry of that path refused again; traversal `400`; an approved upload with a mismatched declared digest `422` and **no staging file left**; two concurrent uploads of one name both `200`, the second renamed `IMG_0005 (1).HEIC`. With **Require TLS** on and no matching certificate in `CurrentUser\My`, the receiver fell back and the ticket URL was `http://` - the old build printed `https://` while listening on plain HTTP - the status line read `Listening on http://192.168.8.91:47821/ (HTTP; no certificate).` and `/health` answered `{"status":"ok","tls":false}`. Two UI defects found only in the pixels were fixed: a clipped paragraph and a QR code left on screen after its secret was spent |
| **Wireless page contract** | `WirelessPageTests` (11, real controls) | ✅ Start is disabled until wireless is enabled; a ticket renders both the URL and a visible square QR image; the approval list shows exactly the waiting files with their sender; decided offers are not listed; Approve/Refuse/Approve-all are enabled only when they can act, and Approve raises the **selected** path; changing the auto-approve switch is reported; stopping clears the ticket, the QR and the list |
| **Pairing QR code** | `QrCodeTests` (9) + an independent decoder (`QrDecoder`) | ✅ every payload is read back by a decoder that shares no code with the encoder - format bits, mask, zig-zag walk, block de-interleaving - across every version the encoder claims (1-10, including the multi-block versions 4+), plus the three finder patterns, a 4-module quiet zone, and refusal rather than truncation above the ceiling |
| **Unfinished-session recovery** | `SessionRecoveryTests` (14) + `RecoveryFlowTests` (6) + `DashboardRecoveryTests` (9) | ✅ rules pinned: every entry state counted and outstanding bytes exact; finished/cancelled/aborted/running/never-recorded records read as meant (`INCOMPLETE — NOT A VERIFIED COMPLETE BACKUP` is not mistaken for success); another folder, a newer schema, a damaged record and an unreadable manifest each refused with a reason and the damaged file left in place; the plan carries only outstanding entries with their own sizes/extensions/timestamps and no object identifiers, dropping paths the engine would refuse. Through the real engine: stamped `RUNNING` **before the first byte is fetched**; Stop writes `CANCELLED` with what finished, failed and was never reached; a backend fault writes `ABORTED` and stays continuable; continuing fetches **exactly** the outstanding files (asserted on the backend's fetch log), leaves the verified file untouched, reaches full source-to-destination verification, and refuses to call the session complete while an entry it could never copy is outstanding; a copy whose verification was interrupted is not left in the destination; and a momentarily locked record cannot lose or half-write the record already on disk (`ManifestStoreTests`) |
| **Recovery surface, live** | published 1.8.0 exe + a **seeded** 26,335-entry record (11,143,635 B) | ✅ the Overview bar rendered the record's own numbers (`26,335 planned · 1,203 copied and verified · 25,132 outstanding (12 failed, 4 unreadable)`) and logged the unfinished session **before** "Welcome"; *Not now* collapsed the bar, wrote *"Unfinished session set aside. 25,132 file(s), 172.12 GB, are still outstanding in the destination."* to the status bar, persisted the fingerprint to `settings.json` and left every copied file untouched; with the handset attached the same bar reported **Resume transfer enabled**. It was captured at the machine's real **150% scaling**, where a first capture showed the row measured in the wrong units (the second message line painted outside its card); after the fix the row's following content moved down by exactly 47 device px, the message ends inside the bar, and the fixed capture shows both lines in the card. Construction (the record) was seeded; reading, display, dismissal, the device gating and both layout fixes were the real published build |
| **Engine throughput** | `EngineThroughputBenchmarkTests` (3, real engine, local disk) | ✅ small files **87 → ≈ 700 files/s** (≈ 8x) with verification unchanged; large files ~600 MiB/s (unchanged); measured per-file engine cost ≈ 1.5 ms vs ≈ 27 ms of transport cost per object |
| **Live view pacing** | `ThrottledProgressTests` (5) + the benchmark | ✅ 4,000 engine updates become 21 UI updates at a 250 ms ceiling; the terminal state still comes from the run's summary |
| **Log privacy** | `LogRedactionTests` (9) | ✅ **two defects found by the 2026-10-19 audit and fixed**: a device serial was not redacted at all (the dashboard's `Serial: …` line reached `I-total-transfer.log`, which ships inside the destination and inside a diagnostics bundle whose README claims no full device identifiers), and the credential rule consumed the label but left the value (`pairing_token: …` → `pairing_token=…REDACTED …`). Labelled identifiers (serial/UDID/ECID/IMEI/MEID/stable-or-device id) are now masked to a short prefix, assignments consume their value, and ordinary operational text is unchanged — asserted in both directions, including on the log file itself |
| **Unfinished-session resume cost** | `FolderNameIndexTests` (9) | ✅ a resumed run has no object ids to reuse (they do not survive a reconnect), so it locates each file by path — and it used to re-read the album and a property per sibling **for every file**, quadratic in the album. Each device folder is now read **once per session** and answered from memory, dropped on any session reset: **26,100 files across three albums cost 4 folder reads**. The device-side gain is not measured (needs the handset); the lookup no longer depends on album size |
| **Second-pass scan check** | `ScanStabilityTests` (6) | ✅ the two enumerations are compared by dictionary lookup, not by a scan of the listing per path. Measured: **4,705 ms → 5.7 ms** for 26,000 paths on the UI thread; it no longer throws when a partial (warned) scan carries a duplicate path |
| **Scan-listing cache** | `ScanCacheTests` (9) + `ScanCacheStoreTests` (11) + `LibraryPageProvenanceTests` (4) | ✅ rules pinned: only a `Complete` enumeration is written; a partial/empty scan cannot replace a good listing; another device/backend, an older schema and an over-age listing are refused with a stated reason; corruption is quarantined, never fatal; the device identity is not written into the file name; the backend object id is deliberately not persisted; the difference report counts additions/removals/size changes; the page's provenance line survives the empty state. Live (real app, seeded listing): Settings read **"1 listing, 1,366 B"**, *Forget cached listings* removed it after confirmation and the row read **"No listing is on disk."** |
| **Cache shown during a cold scan** | Needs the handset | ⛔ Unit-tested only. `ShowLastKnownLibrary` runs after the device probe succeeds, so with the iPhone detached it cannot be exercised end to end — the code path and its provenance text are pinned by tests, but no live capture of a cached library beside a running scan exists yet. |
| **Overview operation band** | `OperationStatusTests` (6) + `DashboardOperationBandTests` (3, real controls) | ✅ sweeps while a phase is unmeasured, becomes determinate only with a real total, names the phase, shows nothing when idle; a 106 GB total still yields a correct position (permille, no `int` overflow) |
| **No clipping on a short window** | real capture before/after + `AutoScrollMinSize` on the page | ✅ the integration line was sliced in half once the band was added; the page now scrolls instead of squeezing the action panel |
| **Theme preview and contrast (2026-10-22)** | `ThemeTests` + `AppearanceSettingsTests` (4 real-control tests) + calibrated captures | ✅ three selectable themes only (Neon default, Light, Slate; `System` still follows Windows); Neon keeps `#040A1E` / `#0B55F1`, Slate is a charcoal stack with `#4A6BC4`, Light keeps `#F3F4F7` / white / `#0B55F1`; shared `Focus`, `SelectionFill`, `SelectionBorder`, `DisabledFill`, `DisabledText` and `Scrim` tokens; measured 7.0/4.5/3.0 contrast thresholds; Appearance swatches drive the existing combo path and a saved Light theme is restored and highlighted. Design source: `docs/UI-DESIGN-SYSTEM.md`; evidence map: `docs/UI-REQUIREMENTS-MATRIX.md`. |
| **96-DPI shell render (2026-10-24)** | DPI-unaware harness, real `MainForm`, `DeviceDpi=96`, client root 1416x919, all seven pages in Neon/Slate/Light | 21 pixel captures (`%TEMP%\opencode\ui100\shell-*-96dpi-*.png`). The 78 px brand band shows the full `I-Total Transfer` title with the subtitle beneath it, and no page clips a caption or overlaps another control at 100 % logical size. A first attempt that captured the whole form painted the OS caption over the client (the shell collapses the non-client area with `WM_NCCALCSIZE`), producing a false "title clipped" reading; the shell root is rendered instead. The render exposed one real defect: the Wireless TLS caption ran past its card and lost the closing bracket. It is reworded and pinned by `WirelessTlsCaptionFitsInsideItsCardAtTheDesignedWidth`, which fails on the old wording (440 px needed, 414 px available). This is a 96-DPI render of the real controls, not a native 100 % desktop capture, which this 150 % host cannot produce. |
| **UI-18 page control audit (2026-10-22)** | current published build at 150 %, `PrintWindow` captures, UI Automation dumps, `PageControlTests` (5 real-control tests) | Every action on Backup & queue, Media library, Wireless import, History and Diagnostics was walked: the current build opened real `No scan` / `Scan the device first` modals from Verify and Export scan, cleared the console through `Clear view`, refreshed History, ran Diagnostics, and wrote a 3,586-byte bundle. Wireless Start created a real ticket and `/health` returned 200 as the non-elevated user; Stop cleared it. Approve/Refuse/Approve-all/Refuse-all were confirmed disabled with no pending offers, and Start was disabled until the enable switch was set. The audit found and fixed the clipped listening line, double-encoded punctuation, the `Clear` label ambiguity, the Media-library empty-state wording, and the false `required dependencies verified` title when all native binaries are missing. Captures: `%TEMP%\opencode\ui-audit2\*.png`; dumps: matching `.txt`. The 100 % layout is covered by the 96-DPI harness row above; a native 100 % desktop capture is not possible on this 150 % display. The page-level wiring is covered by tests. || **Overview above the fold** | real 150 % capture + UI Automation | ✅ media/metric rows trimmed and `AutoScrollMinSize` 742 -> 690; Start transfer measured at `y=1212..1272` with the status bar at `y=1332`, so the primary action is fully visible. Captures: `%TEMP%\opencode\ui-capture\overview-{neon,light,slate}-final.png`. |
| **Context capture cannot deadlock a host** | `ContextFreeExecutionTests` (3, pump-required context) | ✅ a blocking caller with a UI context scans, copies and cancels without hanging, and leaves nothing queued on that context |
| iPhone diagnosis | live iPhone 13 Pro, unlocked + trusted, via the app's own Shell call | ✅ storage container found; **no `DCIM` level** — its five camera-roll albums (26,320 files in one) sit directly at the storage root |
| **Live end-to-end detection** | fixed build, reconnected iPhone 13 Pro (iOS 27.0.1, serial `FCC4L3VYCW`) | ✅ native WPD reports Ready; header shows the green blip, `Model: Apple iPhone · iOS: 27.0.1 · Serial: FCC4L3VYCW · Link: USB (WPD)` |
| **USB evidence: remembered vs attached** | `AttachmentDiagnosisTests` (13) + **this machine's own** USB registry | ✅ only the media interface (`WUDFWpdMtp`) counts as a handset, so the support records a PC keeps for every Apple cable it has seen are reported but never counted as phones; a cached iPhone **and** a cached iPad read as `CACHED RECORD ONLY (2)` with nothing attached and never as connected; an attached handset is never downgraded by stale records beside it; a machine that has never seen an Apple device says so and names the driver/cable remedies; Windows' indirect `@…inf,%key%;Readable Name` values resolve to the readable half. Live: a read of this PC's real registry returned `cached handsets=2 (Apple iPhone, Apple iPad); support=3` - the fixture is the machine, not a mock |
| **Connection diagnosis, live** | published 1.8.0 exe, **no handset attached**, real desktop capture at 150% scaling | ✅ Diagnostics led with **CACHED RECORD ONLY (2)** - *Apple iPhone, Apple iPad* - plus the guidance naming Device Manager's own *"Currently, this hardware device is not connected to the computer"* wording, in place of the bare *no device* the app used to show; the summary line repeated the verdict; the detail column wrapped and its row grew, so the whole paragraph was readable (previously long findings were silently cut off at the column edge) |
| **Diagnostics bundle, live** | published 1.8.0 exe + *Export diagnostics ZIP* **while running** | ✅ **a defect found by pressing the button**: the export read the log tail with `File.ReadAllLines` while the application held the log open for writing, so the sharing violation escaped `DiagnosticsBundle.Create` and **every** export produced no file whatsoever. Fixed (write-shared read; an unreadable log now costs only its own file) and re-driven: a **7-file** ZIP - environment, redacted settings, backends, dependencies, `logs-tail.txt`, `context.txt`, README - written from the running build, with `logs-tail.txt` carrying that run's own lines and `context.txt` carrying the same attachment verdict and no hex identifiers |
| **Diagnostics page and bundle contract** | `DiagnosticsPageTests` (6, real controls) + `DiagnosticsBundleTests` (4) | ✅ the USB evidence is the **first** row and its status is shown whole; the three readings are coloured apart (attached green, remembered-only amber, never-seen red - the old three-way exact match could not match a dynamic status at all); findings wrap and their rows grow in both grids; the bundle is produced while the log is held open **exactly** as `LogService` holds it; a serial in the log is masked while the attachment evidence survives redaction verbatim |
| **Live full scan** | current published build, *Scan device*, 2026-10-22 | `Scan finished: 55,185 files, 151.34 GB, status=Complete, duration=23.3 min` on native WPD; 11 folders, 0 failed, 0 warnings, 0 unknown sizes, 1,167 live-photo pairs. The scan report is `%TEMP%\opencode\physical-heic-mov\destination\I-total-enumeration-report.json`. The cache was deliberately not written because `ReuseLastKnownLibrary=false` for this drill. |
| **Current iPhone WPD listing** | live cold scan, 2026-10-22 | **55,185 files / 162,499,726,120 bytes (151.34 GiB)**, `Complete`, 23.3 min. The 2026-10-21 26,335-file / 114,344,029,232-byte / 10.3 min listing and the 2026-10-20 57,371-file / 188,810,836,019-byte / 10.99 s warm pass are historical; the phone now exposes more albums. |
| **Android live walk and copy** | HUAWEI Y7 Prime 2018, current source, `%TEMP%\opencode\android-copy-3` | ✅ **4,103 files / 15,612,056,299 bytes (14.54 GiB)** `Complete` on 2026-10-22; the 2026-10-20 4,106-file / 15.01 GiB copy remains historical. A 612,420,912-byte MP4 was cancelled mid-copy (`CANCELLED`, one outstanding) and resumed to `HASH-VERIFIED COMPLETE`, source and destination SHA-256 identical, 0 failed. The separate 2026-10-20 filtered copy ended `COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP`. |
| **PTP-only Android scan, live** | HUAWEI in Photo transfer (PTP), 2026-10-24 | Windows enumerated PID `1083`; scan returned 57 files / 7.5 MB. The Overview visibly showed **“Camera-only connection (PTP): only DCIM and Pictures are visible…”** and the log carried the same warning. Screenshot: `%TEMP%\opencode\ptp-drill3\02-ptp-result.png`. |
| **Locked-phone observation** | HUAWEI locked during a 584.1 MB copy, 2026-10-24 | Locking the screen did **not** interrupt MTP; the copy completed `SIZE-VERIFIED COMPLETE` with source/destination SHA-256 equality and the trial counted one job. MTP stays active while the screen is locked, so a locked-phone abort is **not applicable** to this handset; backend faults remain covered by simulation. |
| **Filtered post-copy verification** | current published build, 2026-10-24 | The pre-fix app auto-verified all 4,103 scan files after a one-file copy and reported 4,102 excluded files as missing. It now skips the whole-library pass when the filter planned fewer files and logs the reason. A no-op rerun skipped the already-present file, showed no false verification report, and did not charge a trial job. |
| **Android buffer sweep, live** | one 612,420,912-byte MP4, native WPD, 2026-10-24 | 64 KiB 18.246 s / 33.56 MB/s; 256 KiB 16.911 s / 36.21 MB/s; 1 MiB 16.369 s / 37.41 MB/s; 4 MiB 16.187 s / 37.83 MB/s. CPU 2.94-6.50 s; peak working set 70.1-85.5 MB. All four `HASH-VERIFIED COMPLETE`; one run per buffer and one file, so this is not a multi-file benchmark. |
| **Android mixed-size batch, live** | 30 files / 111,135,111 bytes, native WPD, 2026-10-24 | 10 tiny (8-24 B), 10 about 1 MB, 10 about 10 MB: **4.681 s, 6.41 files/s, 23.74 MB/s**, CPU 1.55 s, peak working set 69.9 MB; 30/30 copied, 0 failed, `SourceAndDestinationHash`. One run only, not a statistical benchmark. |
| **Filtered HEIC/MOV physical copy** | shared engine + native WPD, 2026-10-22 | Eight files (3 HEIC + 5 MOV, 2,868,790 bytes) selected from the last complete listing and fetched through the real WPD backend: **8/8 `IntegrityVerified` with `SourceAndDestinationHash`**, every source/destination SHA-256 pair identical, 0 failed, 0 unverified. Report: `%TEMP%\opencode\physical-copy\destination\I-total-session-report.json`. This was a filtered engine fetch, not the UI copy path. |
| **Filtered Start confirmation, live** | current published build, AndroidFirst, 2026-10-24 | Live scan of 4,103 files; a `.mp4`/500 MB filter selected one 584.1 MB file. The Start-backup dialog read **`Copy 1 files (584.1 MB)`**, not the full scan, and was cancelled without writing. Screenshot: `%TEMP%\opencode\ui-android-confirm\02-confirm.png`. The same one-file plan was started, then **Stop** was pressed: the recovery bar read `1 planned - 0 copied and verified - 1 outstanding - 4,102 skipped by filter`, and **Resume transfer** opened the correct 584.1 MB continuation dialog; the resume was cancelled. Screenshot: `%TEMP%\opencode\ui-android-cancel\01-cancelled-recovery.png`. |
| Live scan totals cross-check | dashboard vs album contents | `202609_a` **28,836** + `202608_a` **26,320** + 29 files across nine smaller folders = **55,185**; every folder succeeded and the report carries no duplicate or unknown-size warning. |
| Cold-cache enumeration cost | timed Shell enumeration of `202608_a` | ✅ 723,017 ms for 26,320 items (**≈ 27 ms/object**); warm per-item reads 0.1–0.25 ms |
| Scan progress reporting | `ScanProgressReporterTests` (6 tests) + live log | ✅ throttled, always reports on folder change, formatted pull line routed to the status bar |
| Caption-button contrast | `CaptionButtonContrastTests` (4 tests) + real capture | ✅ ≥ 3:1 on every theme; measured **16.3:1** on Light (glyph `RGB(21,23,28)` on band `RGB(243,244,247)`) vs 1.06:1 before |
| Staged-read-back optimisation | `TransferEngineTests` (2 new tests) | ✅ a source-hashing backend records no staged digest and stays `SourceAndDestinationHash`; a Shell-like backend still reads the payload back |
| Test-suite determinism | full run, repeated, with `--blame-hang` | ✅ completes in ~25 s, 0 failures; hardware tests opt-in and time-bounded; a WinForms `SynchronizationContext` can no longer leak between test classes |
| Camera-roll resolution rules | `CameraRollTests` (8 tests) | ✅ both layouts accepted, verbatim live folder names used, unrelated volumes and sidecars rejected |
| Shell backend apartment | `ShellBackendApartmentTests` (2 tests, opt-in) | ✅ probe answers from an MTA caller, disposes cleanly, no hang |
| Startup smoke test | launch `I-Total.Transfer.exe`, wait 6s | ✅ resident, no crash |
| Publish | `dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` | ✅ produced single-file exe |
| GUI layout @ 150% DPI | launch + desktop capture (3440x1380 @150%) | ✅ uniform scale, no clipping/distortion — all seven pages captured maximized |
| Fluent surfaces | captures of every page | ✅ banner bands, rounded cards, group accents, chip rows, empty states, gradient progress bar |
| Window chrome | scripted interaction test on the real window | ✅ maximize/restore exact (Δw=0, Δh=0), band drag exact, minimize/restore exact, close exits, caption buttons named |
| Theme apply-on-save | scripted combo-box test + read back `settings.json` | ✅ repaints live, persists `Theme` (Neon → Light) |
| Tile rendering | paint-size/clip logging + window and desktop captures | ✅ inverted-corner seam found and fixed (`MetricCard` was missing `ResizeRedraw`) |
| Theme render | capture Neon (default), Slate, Light | ✅ rounded surfaces, borders, accent gradient, nav highlight, header rule, brand icon, no mojibake |
| Theme logic | `ThemeTests` | ✅ default = Neon, legacy `Dark` → Neon, sampled banner palette values, `System` → Light/Neon |
| Media groups | `MediaCategoriesTests` | ✅ extension mapping, `MediaFile.Category`, titles/taglines, capability gating |
| App icon | load `app.ico`, render every frame | ✅ 9 frames (16–256 px) all render the new artwork |

The wireless receiver is covered by **real loopback network tests** (bind, single-use pairing, session
token rotation and expiry, the approval gate in both directions, upload, SHA-256 on receipt, digest and
size disagreement, traversal and reserved-path rejection, collision renaming, health endpoint, refusal of
public/wildcard binds, staging sweep) - these are genuine network tests speaking HTTP over a real socket,
not mocks. The sender contract they implement is written down in `docs/WIRELESS.md`.

## Not verified here (hardware-dependent)

The build machine is Windows 10 Pro 19045. Apple's USB driver stack **is** present here — Diagnostics reports
Apple software *DETECTED* and the driver files *FOUND* at
`C:\Program Files\Common Files\Apple\Mobile Device Support\Drivers\usbaapl64.inf`. A physical iPhone 13 Pro
**was** attached, unlocked and trusted, long enough to confirm detection and a complete cold enumeration
(**55,185 files / 162,499,726,120 bytes / 151.34 GiB** on 2026-10-22, 23.3 minutes; 11 folders, 0 failed,
0 warnings). The 2026-10-21 26,335-file / 106.49 GiB / 10.3 min pass and the 2026-10-20
57,371-file / 188,810,836,019-byte warm pass are historical because the phone now exposes more albums.
walked and copied; the cross-device scan defect was found and fixed. The physical selected-phone disappearance and mid-scan unplug drills passed on 2026-10-24, as did Android cancel/resume. PTP-only is now live; the HUAWEI lock does not disable MTP, so the abort path is not applicable to this stack. Some statements below
predate this hardware work and are superseded by the rows above. The fixed completeness path was re-observed on the phone: the listing was rejected rather than accepted. Nothing device-facing beyond detection, enumeration, the Android filtered copy and the tested cancellation/recovery paths is claimed as working on hardware:

- **The *attached* branch of the USB diagnosis in its corrected form.** The cached-record branch is verified
  against this machine's own registry (see the table above). The attached branch was observed once, on
  2026-10-20, and it was wrong: with an Android phone answering on the same bus the Apple row read
  `Attached and answering: HUAWEI Y7 Prime 2018`. The family filter now prevents that and the corrected
  wording is unit-tested, but no live handset has produced it; the locked-handset variant (row ATTACHED while
  the media rows explain that media is not readable) has never been observed. TEST-PLAN hardware check 24.

- **A transfer that is really interrupted on a live device and then continued.** The recovery record, the
  plan rebuild, the completeness rule and the interface are covered by the tests and the seeded live check
  above, and the app currently reports a genuine iPhone `Ready` over native WPD — but the device-facing leg
  (a real cancel mid-copy over WPD, followed by a real continuation that fetches only the outstanding files)
  has not been run, because it writes real data to a real disk for minutes and was not run without the
  operator's go-ahead. Everything about the *record* is proven; the USB leg of the continuation is not. The
  path lookup that leg depends on was rewritten on 2026-10-19 (`FolderNameIndex` — each device folder is read
  once per session instead of once per file); its logic is pinned by tests, but the improvement it is meant to
  bring on a real album has not been measured against a handset.
- **On-device transfer throughput and the speed effect of the 1.8.0 engine changes.** The phone
  disconnected while the before/after benchmark was being prepared, so no end-to-end device number is
  claimed for the removal of the per-file existence probe or the redundant staged read-back. Both are
  reasoned from measured call costs and pinned by unit tests (see `docs/PERFORMANCE-AUDIT.md` §5).
- **A cached listing shown beside a live cold scan.** The cold scan itself completed on 2026-10-21 with the
  progress line on screen, but the combined capture (last-known library beside the running scan) was not
  taken. The code path and its provenance text are pinned by tests; re-observing it needs a device
  reconnect. TEST-PLAN row BT06.
- Native WPD streaming and the Shell `CopyHere` path on a live device.
- AFC read/write (also intentionally inert: no pinned runtime ships — see below).
- Large-video handling and Live Photo pairing on-device.
- **Disconnect recovery during a copy or verify.** A real cable pull *did* happen during a scan on
  2026-10-20: the app survived without crashing, but the scan still called itself complete — the defect now
  fixed. The copy and verify phases have not been interrupted on hardware; they go through the backend-fault
  path pinned by `RecoveryFlowTests.ABackendFaultAbortsTheSessionAndLeavesItResumable`.
- **Android cancel/resume and PTP mode.** The Android walk, cancel/resume and PTP-only scan are now live
  on 2026-10-24 (4,103 files; the 612,420,912-byte MP4 resumed to source/destination SHA-256 equality; PTP
  showed the camera-only warning). The HUAWEI lock does not disable MTP, so a locked-phone abort is not applicable to this stack;
  backend faults remain covered by simulation. A single-file buffer sweep is recorded above; no multi-file Android throughput number is claimed. TEST-PLAN hardware
  check 25 is closed for this handset.
- Wireless from a real iOS sender (Shortcut/companion app). The protocol, single-use pairing, session
  rotation, approval gate, digest and traversal rules are covered by real loopback HTTP tests, but a
  physical iPhone Shortcut has not been run against the receiver here, so "a real iPhone delivered its
  photos over the LAN" is not claimed. The recipe to run it is in `docs/WIRELESS.md`.
- **Windows 11 rendering:** the app targets `net10.0-windows` with a `Windows 10 1809` minimum. The
  captures and interaction tests here were taken on **Windows 10 Pro (build 19045)**, so DWM rounded
  corners, the native dark scrollbars/titleless dark caption, and `Application.SetColorMode` remain
  guarded code paths rather than verified pixels. Icon-font, `Segoe UI Variable` and rounded-corner
  fallbacks are likewise code paths, not captures.
- **Audible UI feedback:** sounds are synthesised in memory and wired to buttons, check boxes and
  completion/attention events, but this machine's speakers cannot be listened to from the test harness,
  so "a tick is audible when you press a button" is left for a human to confirm.
- **Snap and edge resize:** the band drag, the caption buttons, minimize/restore and close were
  automated; dragging to a screen edge (Aero Snap) and the eight-way edge/corner resize were reasoned
  about from the retained window styles but not scripted here.

## Commercial and packaging gaps (not verified, or blocked externally)

- **A purchased licence cannot be activated in this build.** No production key pair or issuing backend
  exists, so no public key is embedded: `Enter licence...` reports `NotConfigured` and the three-job trial
  runs. The verifier, revocation list and Steam channel are implemented and tested with generated keys.
- **Steam ownership has no live Steamworks check.** The signed Steam entitlement is bound to the app id and
  refused on a direct build, but the SDK and a partner account are unavailable and Steam has not confirmed
  eligibility for this utility.
- **No setup executable and no Authenticode signature.** Inno Setup 6, the Windows SDK signing tools and a
  code-signing certificate are absent from this machine. `Build-Installer.ps1` prints `SETUP SKIPPED`;
  `Sign-Artifacts.ps1` fails closed; the portable ZIP and the per-user installer script are complete and
  covered by the three automated tests above.
## Fail-closed native runtime

`native/dependencies.json` pins native binaries by SHA-256 and **currently ships with empty hashes**.
`DependencyVerifier` therefore refuses every native binary, so the AFC backend cannot load anything
that was not explicitly reviewed and pinned. See `docs/BUILD-VALIDATION.md`.

## To treat a build as release-ready

1. Run `Test-Project.bat` and `Build-Release.bat` on Windows 10 and Windows 11.
2. Complete the hardware checklist in `docs/TEST-PLAN.md` with a physical iPhone (ideally two iOS
   versions, Live Photos, a >1 GB video, and iCloud "Optimize Storage" on and off).
3. Only then claim device results. Until then, describe the device paths as implemented-but-unverified.

## Definition of "verified" in this app

The app claims, at most, the strongest verification level it actually achieved per file
(`SizeOnly` → `DestinationHash` → `SourceAndDestinationHash`). A size match is never reported as
byte identity, and an item that cannot be re-read is reported **unverifiable**, not successful.

## Release artifacts (built here, 2026-10-24)

| Artifact | Size | SHA-256 |
| --- | --- | --- |
| `publish\I-Total.Transfer.exe` (self-contained, single file, win-x64) | 112,150,342 B | `411F4CC5CD8303D10E6C542AE8CD28AD9EAAEE82C60C720912FD371FA8988CFF` |
| `dist\I-Total.Transfer-1.8.0-win-x64.zip` (deployment) | see `dist\SHA256SUMS.txt` | see `dist\SHA256SUMS.txt` |
| `dist\installer\I-Total.Transfer-1.8.0-portable-win-x64.zip` (portable package) | see `dist\installer\SHA256SUMS.txt` | see `dist\installer\SHA256SUMS.txt` |
| `dist\I-Total.Transfer-1.8.0-source.zip` (complete source) | see `dist\SHA256SUMS.txt` | see `dist\SHA256SUMS.txt` |

`dist\SHA256SUMS.txt` is the **authoritative** record for the archives, deliberately kept outside
them: this file ships inside both archives, so an archive cannot carry a correct hash of itself. The exe
hash above is stable and reproducible from `publish\` at any time.

These figures supersede the earlier 1.8.0 values (`C4B0DFE1…`, then `D094452C…`, then `A6D6E843…`, then
`362A5BDF…`, then `D9609BF4…`, then `AE679E79…` for the exe and `3BF0D1D4…`, then `AB08B0C8…`, then
`43602D4B…`, then `16B78EB7…`, then `187C5085…` for the deployment ZIP), each of which described a publish
taken before the next round of work: `C4B0DFE1` predates the detection fix, `D094452C` predates the
scan-progress, Overview-band, context-capture and caption-contrast work, `A6D6E843` predates the
engine-throughput and progress-pacing fixes, `362A5BDF` predates the scan-listing cache, `D9609BF4` predates
the unfinished-session recovery work, and `AE679E79` predates the 150% scaling fix for the recovery row and
the retry on the session record. `527BF4EC.` was the publish that followed those, carrying the 2026-10-19
audit fixes (the redaction of device identifiers, the linear second-pass comparison, the once-per-folder
path lookup a resumed run uses) and the wireless rewrite: the single-use pairing secret, session-token
rotation, the approval gate, the private-only bind policy, the hand-written QR encoder, and the two
interface defects the live drill surfaced (a clipped paragraph and a QR code left on screen after its
secret had been spent). `8A591FDC.` added the USB-evidence diagnosis that tells a device Windows merely
remembers from one that is attached, the diagnostics page's no-truncation fix, and the support-bundle export
fix (no bundle had ever been written while the app was running). `7612BBBE.` followed and added the Android
work (one family-aware WPD backend with a per-family storage policy, family-aware wording and diagnostics),
the two-phone preference, the mid-scan disconnect guard, and the neutral unattached-device wording
(`CHECK PHONE` with both prompts, no Apple-only advice for an unidentified device); an intermediate publish
(`83517885.`) carried the same functional work but the old unattached wording and was superseded within the
same round. `C57324F1.` was the commercial publish: it added the commercial
subsystem - the three-job trial and its dual-store ledger, RSA-PSS entitlement and revocation verification,
the Settings Licence section, wireless trial metering, the per-user installer and the packaging/signing
scripts - and it was smoke-tested through its own UI, including the Licence row. `21D0D686.` added the
licence/trial summary to the diagnostics bundle and the installer ASCII guard; `772DEC2C.` added
filtered-plan byte accounting and preserved `CopiedUnverified` on resume; `5A0DC3CC.` added the two-phone
isolation guard (`DeviceSelectionGuard`, `ScanResult.BelongsTo`, UI and cache refusals); `4179C72E.` was the
2026-10-21 deterministic Release publish, `24944655.` and `13251D4B.` are previous 2026-10-22 publishes, and `C953BC08.` was the 2026-10-24 publish (filtered-plan confirmation plus the TcpListener/UI-18 fixes); `42C6C975.` was the 96-DPI caption-fit publish, and `411F4CC5.` is the current publish (AFC-status fix) (static Regex
redaction and no Release PDB, so a rebuild from the source ZIP should match byte for byte). Every earlier hash listed
here was a real, separately published build rather than a rebuild of the same bytes. The deployment
ZIP's embedded copy of the exe was **extracted and hash-compared against `publish\`** during packaging — they
match, which is what "verified archive" means here rather than a merely unopened ZIP. The six earlier shipped
documents
(`docs\TEST-PLAN.md`, `docs\BUILD-VALIDATION.md`, `docs\PERFORMANCE-AUDIT.md`, `docs\RECOVERY.md`,
`docs\RELEASE-ACCEPTANCE.md`, `docs\WIRELESS.md`) plus the commercial documents
(`docs\INSTALLER.md`, `docs\RELEASE-SIGNING.md`, `docs\KNOWN_ISSUES.md`,
`docs\MASTER_IMPLEMENTATION_PLAN.md`, `docs\DEVICE_TEST_MATRIX.md`) were
hash-compared against the source tree during packaging and are byte-identical. The deployment ZIP also
carries this file, `CHANGELOG.md` and the test/performance documents, so a recipient can see the
verification boundary without the source tree. The source ZIP is extracted and **rebuilt clean
(0 warnings / 0 errors)** as part of packaging, and the rebuilt publish is hash-compared with
`publish\I-Total.Transfer.exe`. On 2026-10-21 the extracted tree produced the then-current
`4179C72E...` executable (112,113,478 B); on 2026-10-22 the current source ZIP was extracted, rebuilt
clean (0 warnings / 0 errors) and produced the byte-identical `C953BC08...` executable (112,150,342 B),
so the source archive is reproducible, not merely unopened.

The deployment ZIP contains the exe, `native\dependencies.json` (kept outside the single-file bundle so
pins can be edited without a rebuild), `README.md`, `CHANGELOG.md`, `LICENSE`, `PROJECT-STATUS.md`,
`INSTALL.txt`, the per-user installer script, and
`docs\{TEST-PLAN,BUILD-VALIDATION,PERFORMANCE-AUDIT,RECOVERY,RELEASE-ACCEPTANCE,WIRELESS,INSTALLER,RELEASE-SIGNING,KNOWN-ISSUES,MASTER-IMPLEMENTATION-PLAN,DEVICE-TEST-MATRIX}.md`. The source ZIP contains the complete
solution and the `installer` folder
(excluding `bin`/`obj`/`publish`/`dist` and intermediate archives), including `.Designer.cs` files, tests
and build scripts. The published exe reports `FileVersion 1.8.0.0` / `ProductVersion 1.8.0` and was
smoke-tested through its own UI on this exact build: launched and driven via UI Automation to Settings (where
the Backend section's new row read **"When both phones are attached, read — the iPhone"** at 150% scaling,
with nothing clipped) and to Diagnostics (which led with **`CACHED RECORD ONLY (2)`**, carried
**`Device family: NOT IDENTIFIED`**, read **`CHECK PHONE`** for the trust row, and exported a 7-file bundle
while the application held its log open). The same build's Settings page was then scrolled to its Licence section at 150% scaling: the row read
**"Trial — 3 of 3 transfer jobs left"**, the explanation began *"A job is counted only when a transfer
writes at least one file and finishes with no failures"*, **Enter licence.** was enabled, **Buy a licence**
was disabled (no purchase URL is configured in this build), and the Steam action was absent because this is
not the Steam edition. The section was reachable and unclipped, and the trial ledger on disk read
`JobsUsed: 0` with no consumed jobs. The earlier Performance-row read-back — the concurrency control
reporting `enabled=False` with its label, and "Copy buffer size" as a live combo box. On 2026-10-22 the current build (`C953BC08...`) was additionally driven through the five UI-18 pages with both handsets attached: the Wireless page bound `192.168.8.91:47821` without elevation and `/health` returned 200; Verify/Export scan opened their real no-scan dialogs; Clear view cleared only the console; Diagnostics ran and wrote a 3,586-byte bundle. The `CACHED RECORD ONLY (2)` / `Device family: NOT IDENTIFIED` description above belongs to the earlier disconnected session and is dated evidence, not a claim for this attached state.
