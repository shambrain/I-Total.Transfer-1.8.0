## 1.8.0 — 2026-10-20 — borderless chrome, honest device detection, unfinished-session recovery, Android storage

- **The wireless receiver now starts on a real LAN address without elevation.** The UI-18 live drill
  exposed the old `HttpListener` path failing with `Access is denied.` for the non-elevated per-user
  account, because HTTP.sys requires a URL ACL for every non-loopback prefix. The receiver now owns a
  `TcpListener` and speaks HTTP/1.1 itself: request-line/header parsing, `Content-Length` enforcement,
  `Expect: 100-continue`, response writing, body draining before an early refusal, and graceful stop.
  A matching `CurrentUser\My` certificate still wraps accepted sockets in `SslStream`; without one the
  receiver honestly falls back to token-authenticated HTTP on the private interface. TLS captions now
  say `no certificate` rather than implying a certificate is bound. The regression
  `WirelessReceiverTests.APrivateLanAddressBindsWithoutAUrlAcl` binds a private address on this machine,
  and the published build served `/health` at `192.168.8.91:47821` as the non-elevated user after the
  Wireless page showed `Listening on http://.../ (HTTP; no certificate).` (21 receiver tests).
- **UI-18 control audit fixes.** The page-wide audit found and fixed six real defects: double-encoded
  punctuation in the Wireless page (mojibake instead of an em dash) and a trailing ellipsis; a
  listening-status line that was clipped before the TLS reason; a diagnostics title that called an
  empty required set "required dependencies verified" while all four native binaries were missing (now
  `no native runtime verified - native backends stay disabled`); a Media-library empty state that told
  a ready phone to connect and unlock; the log button labelled `Clear`, which cleared only the
  console (now `Clear view`, with an accessible description naming that boundary); and the Start-backup
  confirmation using the unfiltered scan total, so a filtered job could promise 26,335 files while
  copying eight (`TransferService.PlanSummary` now applies the same filter and names the real count and
  bytes; the live Android confirmation read `Copy 1 files (584.1 MB)`, not the full 4,103-file scan). The empty-state subtitle now wraps instead of clipping at narrow widths. Page-level tests pin the
  toolbar events and the Clear-view boundary (`PageControlTests`, 5 real-control tests).
- **Physical iPhone scan and filtered copy (2026-10-22).** A live native-WPD scan completed at **55,185 files / 162,499,726,120 bytes (151.34 GiB)**, `Complete`, 23.3 min, 11 folders, 0 failed, 0 warnings, 1,167 live-photo pairs. A filtered fetch of 8 files (3 HEIC + 5 MOV, 2,868,790 bytes) through the real WPD backend and shared engine ended `HASH-VERIFIED COMPLETE`, with all eight source/destination SHA-256 pairs identical, 0 failed and 0 unverified. The 26,335-file / 10.3 min and 57,371-file / 10.99 s listings are historical.
- **Android cancel and resume passed on hardware (2026-10-22).** A live Android scan found **4,103 files / 15,612,056,299 bytes (14.54 GiB)**, `Complete`. A 612,420,912-byte MP4 was cancelled after more than 5 MB: the manifest read `CANCELLED`, one outstanding entry and 612,420,912 outstanding bytes. The resume plan contained that file; the resumed copy ended `HASH-VERIFIED COMPLETE (source-to-destination)`, 1/1 copied, 0 failed, 0 unverified, with identical source/destination SHA-256. The published UI was also driven through the same plan: Start named `Copy 1 files (584.1 MB)`, Stop produced the recovery bar (`1 planned - 0 copied - 1 outstanding - 4,102 skipped by filter`), and Resume opened the correct continuation dialog; that resume was cancelled, so no trial job was consumed. The 2026-10-20 4,106-file / 15.01 GiB listing is historical.
- **Portable installer self-location fixed.** The packaged `Install-I-Total.Transfer.ps1` defaulted its source to `..\publish`, so extracting the portable ZIP and running it failed unless `-SourceDir` was passed. It now uses its own folder when `I-Total.Transfer.exe` sits beside it, and still falls back to `..\publish` in the source tree. `PortableLayoutInstallsWithoutAnExplicitSourceDir` executes the real script in a portable-layout sandbox; `InstallerScriptTests` is now 5. The final portable ZIP was extracted and its own installer run with no `-SourceDir`: install, HKCU registration, uninstall and user-data preservation all returned OK.
- **Mid-scan unplug completeness defect fixed (2026-10-24).** A repeat unplug drill on the current build exposed that a disconnected iPhone could still return 28,655 names with 25,843 unusable sizes and be accepted as `Complete`. `TransferRules.ClassifyEnumeration` now treats a high unknown-size share (10 percent or more) as `Incomplete`, `MainForm` re-probes the phone before accepting a scan, and `Start transfer` refuses an incomplete listing. The repeat drill then ended `2,326 files / 8.05 GB, Incomplete` with 1,739 unknown sizes; the post-scan probe rejected it with “the iPhone stopped answering during the scan. The listing is incomplete and was not accepted.” No report or cache was written, and the attached HUAWEI was not read.
- **Source-archive reproducibility restored after git init.** The new `.git` repository caused the SDK to embed a source revision in the published assembly, while the source ZIP has no `.git`; a rebuild therefore produced a different executable. `<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>` makes the publish and the source-only rebuild byte-identical again. This was caught by extracting the source ZIP, rebuilding it, and comparing hashes.
- **PTP-only warning made visible (2026-10-24).** The HUAWEI was switched to Photo transfer (PTP; Windows PID `1083`). The scan returned 57 files / 7.5 MB and the activity log already raised the camera-only warning, but the Overview still said every group was reachable. The media card now shows `Camera-only connection (PTP): only DCIM and Pictures are visible. If its Files app shows more, switch the USB notification to "File transfer" and scan again.` and the status line is amber. `PageControlTests` pins the new hint.
- **Filtered post-copy verification scope fixed (2026-10-24).** After a one-file filtered copy, the app auto-ran destination verification over the full 4,103-file scan and reported 4,102 excluded files as `MISSING`. It now skips that whole-library pass when the filter planned fewer files and logs the reason; every copied file is already source/destination verified. A no-op filtered rerun confirmed the skip and did not charge a trial job.
- **Locked-phone drill observed on the HUAWEI (2026-10-24).** Locking the screen does not disable file transfer on this Android stack: the 584.1 MB copy completed and hash-verified. A lock is not a disconnect, so the locked-phone abort is not applicable; backend faults remain covered by simulation.
- **Android buffer sweep measured on hardware (2026-10-24).** One 612,420,912-byte MP4 through native WPD, source-to-destination hash verified: 64 KiB 18.246 s / 33.56 MB/s; 256 KiB 16.911 s / 36.21 MB/s; 1 MiB 16.369 s / 37.41 MB/s; 4 MiB 16.187 s / 37.83 MB/s. CPU 2.94-6.50 s, peak working set 70.1-85.5 MB. One run per buffer and one file only; this is a device observation, not a multi-file benchmark. 1 MiB remains the default.
- **Mixed-size Android batch measured (2026-10-24).** 30 files / 111,135,111 bytes (10 tiny, 10 about 1 MB, 10 about 10 MB) through native WPD: 4.681 s, 6.41 files/s, 23.74 MB/s, CPU 1.55 s, peak working set 69.9 MB; 30/30 copied, 0 failed, `SourceAndDestinationHash`. One run only, not a statistical benchmark.
- **Installed uninstaller finds a custom install folder (2026-10-24).** A real portable run exposed that `Uninstall-I-Total.Transfer.ps1` copied into an installation defaulted to `%LOCALAPPDATA%\Programs\I-Total Transfer` even when it sat in a folder chosen with `-InstallDir`, so the command printed in `README-INSTALL.txt` refused with exit 3. When no `-InstallDir` is passed, it now removes the folder that holds the `install-manifest.json` it wrote; a portable package (no manifest) is never treated as an installation. `TheInstalledUninstallerFindsACustomInstallFolderOnItsOwn` fails without the rule.
- **Minimum-window layout fixed (2026-10-24).** At the shell's declared minimum (1160x740) four real clips appeared: the Wireless `Require TLS` caption, `New ticket` button and sender notice ran past their card; the Library toolbar put `Repair missing` on top of the Files chip and pushed Apply off the card; the Transfer log toolbar hid `Clear view`; and the History database path vanished behind the sessions card. The Wireless card now reflows (wrapping captions, proportional buttons, vertical scrolling), the Library toolbar uses two rows below 1010 px, the log toolbar wraps, the History toolbar grows, and the queue columns keep every header visible. `PageControlTests` pins each fix at the minimum width.
- **200 % DPI rendered (2026-10-24).** A real 192-DPI session rendered all seven pages at the minimum logical window (2320x1480 device) and the design size (2840x1800): text, cards and the reflowed toolbars scale uniformly with no clipping. `Received (UTC)` became `Received` with the UTC wording in the column tooltip, so its header no longer wraps at 200 %. This is a DPI render, not a 200 % desktop screenshot; Windows 11 remains unobserved.
- **Inno Setup executable produced and tested (2026-10-24).** Inno Setup 6.7.3 was installed, so `Build-Installer.ps1` now emits `I-Total.Transfer-1.8.0-setup.exe` beside the portable ZIP. A silent per-user install into a temporary folder verified the installed exe hash, the HKCU uninstall entry, the Start Menu group, no firewall rule and untouched `%APPDATA%`; the installed app launched, and the silent uninstaller removed the folder, entry and shortcuts while keeping settings and the trial ledger. The setup is unsigned.
- **Clean-clone builds reproduce the published binary (2026-10-24).** A fresh `git clone` initially produced a different executable because the working tree held LF C# files while `core.autocrlf` checked the clone out as CRLF, which changes the deterministic MVID even though the IL is the same. `.gitattributes` now fixes `* text=auto eol=lf` (CRLF kept for `.sln`/`.bat`/`.cmd`, archives and images marked binary), `.editorconfig` agrees, and a re-cloned tree builds, passes the full suite and publishes to the byte-identical `411F4CC5...` executable.
- **Repository hygiene (2026-10-24).** Four tracked testhost hang dumps (about 1.1 GB) and a stale root-level 1.8.0 ZIP were removed from the tree, and `**/TestResults/` plus `*.dmp` are now ignored. The 1.7.0 archive is kept as the last pre-1.8 artifact. The old dumps remain in history; KI-22 names the rewrite that would reclaim them.
- **Diagnostics stopped calling an absent AFC runtime "verified" (2026-10-24).** With both handsets attached, *Run diagnostics* showed `Native AFC runtime: VERIFIED` beside the sentence that no pinned binary was verified, because `RequiredDependenciesSatisfied` is trivially true when the required set is empty. The row now reads `NOT PRESENT` unless at least one binary actually verifies; `AMissingAfcRuntimeIsNotReportedAsVerified` fails on the old expression.
- **Attached-branch diagnostics observed on hardware (2026-10-24).** With both phones attached, the AppleFirst page produced 10 checks (Apple `ATTACHED`, family `APPLE`, software `DETECTED`, drivers `FOUND`, trust `ACCESSIBLE`) and named both handsets; with only the HUAWEI attached it produced its 8 Android rows and showed no Apple rows. This closes the attached branch of the USB diagnosis; locked and unplugged variants remain.
- **96-DPI shell render and a clipped caption fixed (2026-10-24).** A DPI-unaware harness rendered the real `MainForm` at `DeviceDpi=96` (client root 1416x919) for all seven pages in Neon, Slate and Light. The brand band keeps its full 78 px height, title and subtitle, and no page clips or overlaps a control at 100 % logical size. The render found one real defect: the Wireless `Require TLS` caption was wider than its card and lost the closing bracket. It now reads `Require TLS (private-network HTTP if no matching certificate)`, and `WirelessTlsCaptionFitsInsideItsCardAtTheDesignedWidth` fails on the old wording (440 px needed, 414 px available). The captures are a 96-DPI render of the real controls, not a native 100 % desktop screenshot; this host reports 150 % and cannot produce one.
- **Regression suite at the 2026-10-24 96-DPI cut: 455 tests, 453 passed, 2 skipped, 0 failed (~27 s).** The added tests are the caption-fit, installed-uninstaller self-location, AFC-status and four minimum-window layout regressions; the two skips remain the pre-existing Shell-backend apartment probes. Release build 0 warnings / 0 errors.
- **Regression suite at the 2026-10-22 UI/wireless cut: 448 tests, 446 passed, 2 skipped, 0 failed
  (~28 s).** The added tests are the non-elevated LAN bind and the five page-control tests; the two
  skips remain the pre-existing Shell-backend apartment probes. Release build 0 warnings / 0 errors.

- **Theme system, Appearance previews and at-a-glance actions (2026-10-22).** The three established themes
  were tightened rather than replaced: Neon keeps the banner-sampled `#040A1E` backdrop, `#0B55F1` accent
  and six-stop gradient, while its working surfaces are deliberately desaturated navy; Slate is now a
  charcoal layer stack (`#101114` base, `#17191D` card) with a restrained steel-blue accent (`#4A6BC4`) so it
  is no longer a recoloured Neon; Light keeps `#F3F4F7` page, white cards and the brand blue accent with
  stronger borders and darker status text. Shared semantic tokens (`Focus`, `SelectionFill`,
  `SelectionBorder`, `DisabledFill`, `DisabledText`, `Scrim`) and measured `Theme.Contrast` /
  `Theme.StatusColor` helpers replace raw per-control colours, and the suite now asserts WCAG 7.0/4.5/3.0
  thresholds for body text, muted/status text and focus rings. Settings -> Appearance gained three clickable
  `ThemeSwatch` previews with a caption strip, hover/selected border and check badge; they drive the existing
  Theme combo/live-preview path rather than a second store, restore a saved Light or Slate choice, and leave a
  fresh profile on Neon. `System` still follows Windows. The Overview media/metric rows were trimmed and
  `AutoScrollMinSize` reduced from 742 to 690 so the primary action row is fully visible at 1440x900 and
  150 % scaling; the header caption now reads `Apple iPhone - 27.0.1 - USB (WPD)` instead of a truncated
  readiness sentence. The editable FigCraft source (four pages, 69,239 bytes) and four exported design
  renders live in `C:\Users\Administrator\I-Total-Transfer-FigCraft`; `docs/UI-DESIGN-SYSTEM.md` records the
  token/component map and `docs/UI-REQUIREMENTS-MATRIX.md` separates design renders, automated tests and real
  captures. The no-device state now uses neutral `phone` wording even though the Shell probe identifies
  itself as Apple; `DeviceCopyTests.ANotFoundProbeUsesNeutralWording` pins it. **The phone was disconnected on
  2026-10-22**, so the current captures show the honest `No device connected` state and no transfer is
  claimed from them.
- **Regression suite after the UI pass: 439 tests, 437 passed, 2 skipped, 0 failed (~26 s).** The four
  real-control `AppearanceSettingsTests`, the new token/contrast coverage in `ThemeTests` and the no-device
  neutral-wording regression in `DeviceCopyTests` were added; the two skips remain the pre-existing
  Shell-backend apartment probes.
- **The trial is measured in finished transfers, not launches - and a licence never interrupts work in
  flight.** The commercial model is now real code: a 3-job free trial that counts only a completed,
  uncancelled transfer which wrote at least one file with no failures. Failed, cancelled, empty and
  "everything was already present" runs cost nothing, and reporting the same finished run twice cannot
  consume a second job because the engine stamps every run with a `JobId` and the ledger stores a truncated
  SHA-256 fingerprint. The counter lives in both `%APPDATA%\I-Total Transfer\trial.json` and
  `HKCU\Software\I-Total Transfer\Trial`, merged pessimistically, so a restart, an update or deleting one
  copy cannot reset it; a corrupt copy is quarantined rather than read as a fresh trial, and a clock moved
  backwards is noted instead of believed. The licence gate is checked only when a **new** job starts -
  resuming an unfinished session is always allowed - and no store, verification or activation failure
  throws: a missing key, a bad signature, a refund or a read-only ledger becomes a verdict and leaves the
  transfer engine alone. Purchased entitlements are RSA-PSS/SHA-256 JSON envelopes signed by the issuing
  backend and verified against a public key embedded at build time; the app ships no private key, accepts
  only its own channel (`direct` or `steam`, the latter bound to `steam_appid.txt`), honours not-before,
  expiry and the issuer's `Refunded` flag, and applies a signed revocation list while ignoring an unsigned
  one. Settings now has a Licence section with the live counter, the rule that defines a job, an
  `Enter licence…` file picker, a Buy button that is disabled with a reason when the build has no store URL,
  and a Steam re-check on Steam builds. Wireless receiving is metered by the same trial: one sender session
  costs one job, charged after the first committed file, and a finished trial refuses new uploads with
  HTTP 402 while files already received are untouched. If the build carries no public key, activation says
  so (`NotConfigured`) and the trial continues - fail-closed, never a forged acceptance.
  `Licensing/LicenseService.cs`, `EntitlementCodec.cs`, `TrialLedger.cs`, `LicenseOptions.cs`;
  `LicensingTests` (15), `EntitlementCodecTests` (5), `TrialLedgerTests` (5), `SteamEntitlementTests` (4).
- **A safe per-user installer, an upgrade path and an uninstaller that cannot be aimed at the wrong folder.**
  `installer/Install-I-Total.Transfer.ps1` installs under `%LOCALAPPDATA%\Programs\I-Total Transfer` with no
  administrator prompt, no firewall rule, no service and no scheduled task. It can require the published
  executable's SHA-256 before writing anything, records every file it places in `install-manifest.json`,
  creates the Start Menu shortcut and the HKCU uninstall entry, and upgrades in place while leaving
  settings, trial ledger and licence under `%APPDATA%\I-Total Transfer` untouched. Uninstall removes only
  manifest-recorded names, keeps user data unless `-RemoveUserData` is passed, refuses a folder that is not
  an installation, and removes the installation folder only when empty. The Inno Setup script
  (`installer/I-Total.Transfer.iss`) mirrors the same rules for a packaged `setup.exe`, and
  `Build-Installer.ps1` always produces the portable ZIP; when Inno Setup 6 is absent it prints
  `SETUP SKIPPED` instead of pretending a setup exists. Evidence is executable, not documentary:
  `InstallerScriptTests` runs the real script and proves install, hash refusal (exit 4), upgrade with data
  preserved, uninstall through the installed copy, and refusal to uninstall an unrelated folder (exit 3).
- **Code signing is prepared and fails closed.** `installer/Sign-Artifacts.ps1` locates `signtool.exe`, takes
  either an installed certificate thumbprint or a PFX (imported into `CurrentUser\My` only for the run so its
  password never reaches a command line), signs with SHA-256 and an RFC 3161 timestamp, then runs
  `signtool verify /pa` on every artifact and aborts on the first failure. No certificate, key or password is
  part of the source tree or a package. This machine has neither the Windows SDK signing tools nor a
  certificate, so **nothing in 1.8.0 is Authenticode-signed**; the release notes and `INSTALL.txt` say so
  rather than implying otherwise. `docs/RELEASE-SIGNING.md` records the exact procedure and checklist.
- **The diagnostics bundle now includes a licence/trial summary.** `licence.txt` carries the standing
  (Trial / TrialExhausted / Licensed), the channel, jobs used/limit/remaining and whether a new job may start.
  The purchase identifier, the ledger entries and their fingerprints are deliberately excluded, and
  `trial.json` is not copied into the ZIP. Verified by a live export from the published build and by
  `DiagnosticsBundleTests`.
- **Installer scripts are guarded against encoding drift.** A non-ASCII byte in any `installer/*.ps1` now
  fails the suite with the file and line - Windows PowerShell 5.1 reads a BOM-less script as ANSI, so a
  UTF-8 dash can change how it parses.
- **Regression suite at the 2026-10-20 licensing cut: 407 tests, 405 passed, 2 skipped, 0 failed (~24 s).** Twenty tests were added after
  the licensing session, including the 14 two-phone isolation tests (`DeviceSelectionGuardTests` and
  `ScanIdentityTests`) and the filtered-plan/unverified-resume and compact-recovery-bar cases. The two skips
  remain the pre-existing Shell-backend apartment probes the harness cannot run.
- **Release builds are byte-reproducible.** The first source-ZIP rebuild produced a different executable from
  the same sources: the regex source generator contributed a non-deterministic type-name hash, and the
  Release PDB recorded an absolute path. `LogService` now uses static `Regex` fields instead of
  `[GeneratedRegex]`, and Release sets `DebugType=None` / `DebugSymbols=false` (Debug and test builds keep
  symbols and line numbers). A clean publish of the same tree from two different directories now produces
  the same SHA-256, and extracting the source ZIP and rebuilding produced the byte-identical
  `4179C72E...` executable (112,113,478 B). The shipped exe carries no PDB, so stack traces keep method names
  and messages but not source line numbers.
- **A phone unplugged mid-scan now stops the scan instead of "completing" it — found on hardware, fixed in
  three layers.** On 2026-10-20 an iPhone 13 Pro and a HUAWEI Y7 Prime 2018 were attached at the same time.
  Explorer enumerated both (the iPhone's twelve album folders, the Huawei's *SD card* and *Internal
  storage*), the app's presence probe saw the Android phone, and the iPhone walk ran for 23.2 minutes and
  counted **52,574 files / 166.72 GB**. The phone was then disconnected while that walk was finishing, and
  the scan still reported `status=Complete`: every object read after the cable moved failed, each failure
  was only a warning, and the album was still recorded as read — so a listing missing the tail of the tree
  called itself complete. An object that cannot be read now makes its folder a failed read (the scan is
  `Incomplete`, never `Complete`), twenty consecutive failures abort the walk with
  `DeviceStoppedAnsweringException` and an instruction to reconnect, and the scan-cache writer only ever
  sees a complete result. `TransferRules.DeviceStoppedAnswering` pins the boundary.
- **Two phones on one desk are a choice, not a race.** Both families reach Windows on the same
  portable-device bus, so whichever Windows listed first was read while the other was only named. Settings →
  Backend now offers *When both phones are attached, read: the iPhone / the Android phone*. The preference
  only reorders the two families and still falls back when the preferred one is absent — refusing to read
  the only phone plugged in would be worse than reading the one that was not named. The Apple USB diagnosis
  also filters by family now: with an Android phone answering on the same bus it had put *"HUAWEI Y7 Prime
  2018"* under a row titled *"Apple handsets seen over USB"*.
- **A scan cannot silently switch phones (defect found on hardware, 2026-10-21).** With both handsets
  attached and `AppleFirst`, the header selected the Apple iPhone, but the live scan returned the HUAWEI's
  4,106 files and the cache writer stored them under the iPhone's WPD key. `Probe` and `Scan` each
  re-enumerated the bus, so a moment in which the iPhone stopped answering let `FindDevice` fall through to
  the Android phone, while the cache key still came from the probe. `WpdDevice` is now bound to the first
  device it selects by `DeviceSelectionGuard`; a later, different device id is refused with an honest
  "the other attached phone was not used" message. `ScanResult` carries `Family` and `StableId`, and
  `BelongsTo` is checked after each scan and again before a cache write, so a mismatched listing cannot
  become `_scan`, a cache entry or a transfer plan. The corrupted cache was preserved at
  `%TEMP%\opencode\corrupt-iphone-cache-69ffba35.json` and deleted. `DeviceSelectionGuardTests` (6) and
  `ScanIdentityTests` (8) cover the rule; the physical disappearance drill is still pending.
- **Android phones are read, not mistaken for iPhones - and the whole phone is read, not its camera roll.**
  An Android phone reaches Windows through the same transport an iPhone does (MTP, surfaced as Windows
  Portable Devices), so no second device layer was written: what differs is the storage behind the transport,
  and that is now a policy chosen per attached device. The iPhone keeps its camera-roll rules; an Android
  phone is walked as **whole storage volumes** — "Internal shared storage" and any SD card, each keeping its
  own name as its destination folder so a card never merges into internal storage — which is why Music and
  Files become real groups for an Android device while an unidentified device keeps the conservative
  two-group answer. The family is decided from what the device calls itself (name, manufacturer, model) and
  never from `WPD_DEVICE_TYPE`: Microsoft's own reference says that field is for "the generic visual
  appearance of the device" and states applications "must use the capabilities of the portable device to
  determine functionally, not the WPD_DEVICE_TYPE value". The two mistakes are not equally bad, and the rules
  are ordered accordingly — reading an Android phone as an iPhone would back up its camera roll and call the
  scan complete, while reading an iPhone as generic storage only looks in more places.
  `Core/DeviceFamily.cs`, `Core/PortableStorage.cs`, `DeviceFamilyTests` (10), `PortableStorageTests` (13).
- **What Android refuses to expose is skipped, and its two failure modes are named instead of silently
  shrinking the backup.** `Android\data` and `Android\obb` are not readable by a file-transfer client, so
  descending would buy an error per app and not one file; thumbnail caches are copies of images already being
  copied. Both are skipped, while `Android\media` — where apps such as WhatsApp keep photos — is kept, and a
  user folder merely *called* `data` is not skipped at all. The two ways an Android backup quietly comes out
  short are an empty tree from "Charging only" mode (or a locked screen) and a reduced tree from "Transfer
  photos" (PTP) mode, which really does expose only `DCIM` and `Pictures`; the first is said in the device
  message, the second is carried as a scan warning naming the setting to change, and both are raised as
  possibilities rather than diagnosed, because the sources behind them are community reports rather than
  vendor documentation.
- **Nothing is called an iPhone that is not one.** The interface was iPhone-only in its wording — "IPHONE
  READY", "Unlock iPhone", "tap Trust", "iCloud originals", "Apple software" — and every one of those was
  wrong on an Android phone, some of them harmfully so (no Android phone will ever show a Trust prompt, and
  its firmware version is not iOS). The dashboard, the device header, the library's empty state, the idle
  operation text, the write refusal and the diagnostics page now follow the detected family, an unattached
  device reads *Not connected* rather than naming a handset, and a device that identifies as neither family
  is reported as **NOT IDENTIFIED** — with any unrecognised device Windows does list named in the message,
  because "no device found" next to a phone Explorer can see is the confusion this app exists to remove.
  `ConnectionDiagnosticsTests` (5), plus family-aware cases in `MediaCategoriesTests` and
  `FolderNameIndexTests` (a volume is not called `DCIM`, so the path lookup now skips the caller's own root
  label). The unattached case was re-driven on the published build: with no phone attached the window is
  neutral (*Phone to Windows*, *Plug in by USB and unlock the phone*), the diagnostics page carries
  **`Device family: NOT IDENTIFIED`**, and the trust row reads **`CHECK PHONE`** naming both prompts (tap
  Trust on an iPhone, choose *File transfer* on an Android phone) instead of an Apple-only instruction that
  sends an Android owner looking for a prompt their phone never shows. `iCloud originals` is shown for an
  Apple device only.
- **A device that is present but unidentified is no longer invisible to the connection check.** The
  handset-presence probe decided "is a handset attached?" with its own Apple-only test, so an attached
  Android phone read as *no device at all* — the same class of defect as the media probe confusing
  "remembered" with "attached". It now uses the same family classifier as the media backend, and the
  Explorer fallback ignores filesystem drives, so a volume whose label happens to contain a maker's name is
  not mistaken for a phone.
- **What "complete" means is stated, not implied.** Windows sees Android's own MTP view of its storage:
  AOSP's `MtpDatabase` combines `MtpStorageManager` filesystem operations with `MediaProvider` metadata, so
  a complete Android scan means *everything the phone exposed* — and the diagnostics page says precisely
  that instead of implying the filesystem was read. **Superseded on 2026-10-21:** the app walked the HUAWEI
  end to end (4,106 files / 15.01 GiB, `Complete`) and copied a filtered selection honestly (412 verified,
  108 unverifiable, 0 failed), so the device-facing path is now live evidence rather than unit tests alone.
  At that 2026-10-21 cut, Android cancel/resume, locked-screen, PTP-only and throughput drills were unverified;
  the 2026-10-24 drills recorded above supersede that for cancel/resume, PTP and a single-file buffer sweep.

- **An unfinished transfer is now visible, and it can be continued without scanning again.** A 106 GB
  camera roll does not usually end cleanly: the cable is nudged, the phone sleeps, or Stop is pressed. The
  destination folder has always held the evidence — `.i-total\session.json` records every file, its state
  and its digests — but the record said nothing about whether the run finished (`LastSessionStatus` was
  written on the completed path only and read **nowhere**), and nothing in the interface offered to carry
  on. The record is now stamped `RUNNING` before the first byte moves, `CANCELLED` on a Stop and `ABORTED`
  on a fault, so a run that was killed with no chance to write anything is still identifiable by the stamp
  it left at the start. The Overview shows an inline bar — *"A transfer to this folder was interrupted"* —
  carrying the folder's own numbers (*26,335 planned · 1,203 copied and verified · 25,132 outstanding
  (12 failed, 4 unreadable)*) and a **Resume transfer** action, with **Not now** to set it aside. It costs
  no space when there is nothing to continue, it stays visible-but-disabled with an explanation when no
  handset is attached (the folder can answer for itself, which is exactly when an operator wants to know),
  and a dismissal is remembered against that session's own fingerprint — never a device identity — so
  further progress or a different folder always brings it back. See `docs/RECOVERY.md`.
- **Continuing fetches only what is outstanding, and still trusts nothing.** The plan is rebuilt from the
  record rather than a fresh enumeration, because the point of the record is that twelve minutes of
  enumeration do not have to be paid again: at device scale a 26,335-entry record with 25,132 outstanding
  rebuilds as a 25,132-file / 172 GB plan. Files already recorded as verified are left out — re-proving
  them would mean re-reading every destination file, 106 GB of pointless hashing — and they are not thrown
  away either: the manifest is shared, so the finished report still counts them, and an operator who wants
  them proven again has an explicit **Verify** action for it. Every file the plan *does* carry is fetched
  from the device again and hashed again under the normal rules; object identifiers are deliberately not
  used (they do not survive a reconnect), so the backend resolves each file by path, and paths the engine
  would refuse are dropped while the plan is built and counted rather than passed on as work.
- **A resumed run no longer re-walks the phone's folders once per file.** Object identifiers are deliberately
  not persisted — they do not survive a reconnect — so a continuation finds each file by its relative path,
  and the first implementation did that by re-enumerating the album and reading every candidate's name *for
  every file*. That is quadratic in the size of the album: on a 10,000-photo album it is tens of millions of
  property round-trips over USB before the first byte moves, which is a hunt, not a transfer. The lookup now
  reads each device folder **once per session** and answers every later file from memory
  (`Core/FolderNameIndex.cs`), matches case-insensitively the way the filesystem does, and keeps nothing
  across a dropped session, because a reconnect can renumber every object id. The property that matters is
  pinned rather than reasoned about: **26,100 files across three albums cost four folder reads**, not 26,100.
  What this buys on a real handset is not measured yet — that needs the phone — but the cost of locating a
  file no longer depends on how many files share its album.
- **A continued run can no longer announce a false complete.** A resumed session plans only part of the
  session, so its own counters cannot prove the session is finished. The verdict is now taken from the
  whole manifest: nothing may be failed, unreadable, unverified or in a non-terminal state anywhere in the
  folder, not merely nothing this run failed on. Without that, copying the last outstanding file could have
  produced "HASH-VERIFIED COMPLETE" while an older entry in the same folder had never been copied.
- **A copy whose verification was interrupted is no longer left behind.** A cancellation can land between
  the staged file being moved into place and its hash being compared, which used to leave a complete file in
  the destination that no record vouched for — and the next attempt would then find it present, see a
  matching size, and record it as a legacy baseline at **size-only** strength, quietly weakening a claim the
  operator never agreed to. Such a file is now discarded and its entry stays failed with an error saying so,
  which keeps one checkable invariant: a file in the destination is either verified or absent. The cost is
  re-fetching that one file; the benefit is that a continued session can still reach full
  source-to-destination hash verification, which a new test pins end to end.
- **The unfinished-work row is measured for the display it is on.** A row height assigned at run time is
  taken as *device* pixels while every value the designer emits is scaled for the monitor, so on a 150%
  display the new row was 63 logical pixels tall while the bar's own designer-scaled content was 126 — and
  the second line of the message was painted past the bottom edge of its card, into the row below. The row
  and the page's scroll floor are now converted together, from the layout pass, so they stay correct at any
  scale and when the window is moved to another monitor. This was caught in a 150% capture and is pinned by
  the bar's "the message must fit inside the bar" relation, which no test at 100% could have caught.
- **Record writing is retried, settings and session alike.** Replacing a file is not atomic against
  everything on Windows — a virus scanner, the search indexer or a backup tool can hold the destination for
  a moment and `File.Replace` then fails with *"Unable to remove the file to be replaced"* (observed once as
  a sporadic failure while running the suite). Losing the operator's settings is not acceptable, and neither
  is silently aging `.i-total\session.json`, which *is* the recovery plan a continuation is rebuilt from, so
  both writers now retry the replacement briefly before they are allowed to fail. The manifest write was
  already non-fatal (the engine keeps working from the state it holds and logs a warning); the test suite
  now pins that a locked record cannot lose or half-write the record already on disk and that the write
  catches up once the lock is released.
- **The second-pass scan check is linear, and no longer throws on a partial scan.** "Confirm each scan with a
  second full enumeration" compared the two passes by asking, for each path, whether the whole second listing
  contained it — a linear scan per path. On a 26,000-item camera roll that is roughly 340 million string
  comparisons **on the UI thread**, immediately after a cold scan, which is the one moment the window has to
  feel alive. Measured here: **4,705 ms** for one camera roll before, **5.7 ms** after
  (`Core/ScanStability.cs`, `ScanStabilityTests`). The same code built a dictionary straight from the first
  pass, which throws on a duplicate path — and a partial scan is exactly the case that is *allowed* to carry
  one — so a warned listing could turn into "Scan failed". Duplicates are now kept as what they are:
  incomplete and warned about.
- **Logs and diagnostics no longer carry a device serial, and a secret is no longer half-redacted.** Two
  defects, both of the kind that look correct until read closely. A device serial was **not redacted at all**:
  the dashboard's own `Serial: FCC4L3VYCW` line went into `I-total-transfer.log`, a file that ships inside the
  backup folder and inside the diagnostics bundle — whose README states in writing that it carries no full
  device identifiers. And the credential rule replaced the label and left the value: `pairing_token: 8f14e45f…`
  came back as `pairing_token=…REDACTED 8f14e45f…`, which reads as redacted and is not. Labelled identifiers
  (serial, UDID, ECID, IMEI/MEID, stable/device id, in the `Serial: …`, `UDID=…` and `"DeviceStableId": "…"`
  forms this application itself writes) are now masked down to a short prefix, an assignment consumes its
  value too, and ordinary operational text is left byte-for-byte — a redactor that mangles the log makes it
  useless without making it safer. `LogRedactionTests` (9) check both directions, including the log file the
  operator actually ends up with.
- **The last known library is shown while a cold scan runs.** A first enumeration of a large camera roll
  is paid at roughly **27 ms per object** — measured, about twelve minutes for one 26,000-item album —
  and until now the library page was simply empty for that whole time. The last complete listing of each
  device is now kept under `%APPDATA%\I-Total Transfer\scan-cache\` (outside the backup folder, because
  the listing describes the phone, not the destination) and shown immediately, headed
  *Last known library (captured …)* in a distinct colour. It is history and it says so: the live scan
  still runs, still replaces the view the moment it lands, and logs what changed — n added, n removed,
  n changed size, with a few example paths. The rules that keep it honest are enforced in the store, not
  in the UI: **only a `Complete` enumeration is ever written** (a partial or empty scan can never replace
  a good listing), a listing older than the configured age is refused rather than shown with a caveat, a
  listing belonging to another device or backend is refused (they address different files entirely, and
  the file also re-checks its own contents), an older schema is ignored rather than guessed at, and an
  unreadable file is moved aside as evidence instead of being silently deleted. Nothing is written at all
  when the feature is switched off, and the Settings page shows exactly what is on disk (**"1 listing,
  1,366 B"**) with a *Forget cached listings* action that deletes it after a confirmation which states
  plainly that only names and sizes are kept, never file contents. The cache is an optimisation and is
  treated as one: it can never be the reason a scan fails or a run stops. The Overview's tiles stay live
  and are never fed history.
- **Borderless window.** The OS caption is gone: `WM_NCCALCSIZE` collapses the non-client area and the
  banner band becomes the title bar, with minimize / maximize / close painted on it in the band's own
  gradient (each button repaints the exact slice of the ramp behind it, so the chrome reads as one
  uninterrupted surface). The window keeps the standard resizable styles, so DWM still supplies the drop
  shadow, Aero Snap, Snap Layouts, the Alt+Space system menu and correct taskbar/Alt+Tab behaviour. The
  band is draggable (a message filter turns a left-press over the band into a caption drag, excluding
  real controls), and a double-click on it maximizes or restores.
- **Maximize/restore is now exact.** Windows still reserves a frame for a window whose non-client area is
  collapsed, so it rounds the restored rectangle outwards — the window grew a little on every
  maximize/restore cycle. Maximizing is now pinned to the monitor working area with `WM_GETMINMAXINFO`,
  the normal rectangle is remembered at the moments that matter (`SHOWN`, `WM_EXITSIZEMOVE`, before a
  maximize), and a restore re-asserts it directly with `SetWindowPos`. Measured in an automated
  interaction test: maximise → restore returns to the *same* 2152x1380 rectangle, and drag-by-band moves
  the window by exactly the pointer delta.
- **The repeated "UNLOCK / TRUST REQUIRED" pill is gone.** The header now carries one quiet signal: a
  status blip (grey = nothing found, pulsing blue = probing, amber = present but not readable, green =
  ready) plus a short caption — *Not connected* / *Unlock iPhone* / *iPhone ready* / *Media available* /
  *iPhone detected* / *Check device* — and the device name underneath. The detailed wording appears once,
  in the dashboard hero pill.
- **Fixed: the iPhone was never detected, because the app required a folder this phone does not have.**
  Both backends insisted on a container literally named `DCIM` before they would report a single file.
  A live iPhone 13 Pro on current iOS exposes no such level: Windows presents its storage container with
  the camera-roll albums (`201901_a`, `202003_a`, `202606_a`, `202608_a`, `202608_b`) sitting *directly*
  at the storage root — over 20,000 files across them, 5,470 in one folder alone. The app found the
  handset, found its storage, and then declared "connected, but no DCIM container was exposed", without
  ever scanning anything. Media-root resolution now accepts **both** layouts — a `DCIM` container found a
  few levels down, or the storage container itself when its children are camera-roll albums or media
  (`CameraRoll`) — and keeps one canonical `DCIM` root label, so *preserve device folder structure
  (DCIM\…)* stays correct either way. This fault could not have been found by reading the code; it took
  the real device.
- **Fixed: every connection probe walked the entire camera roll.** The WPD search for `DCIM` descended
  *into* album folders and read the properties of every file inside them, so a "is the phone ready?"
  probe was in truth a full device scan — tens of thousands of property reads, on a timer. The search now
  never enters a camera-roll album (a container's child *names* are all the walk needs).
- **The Shell compatibility backend now runs all COM on one dedicated STA thread.** It created
  `Shell.Application` on whichever thread happened to call it — a thread-pool thread when the dashboard
  probed in the background — and reused that object from other threads afterwards. Probe, scan and
  staging copy now round-trip through a single STA worker that also pumps messages, which is what Shell's
  asynchronous `CopyHere` expects before it will complete. Stated plainly: this is hardening, not the
  proven cause of the miss — the app's own log shows this machine's Shell backend *did* see the handset,
  and the missing `DCIM` level was the whole failure.
- **The status band and the dashboard can no longer disagree about the device.** The band was filled from
  a probe of *every* backend while the hero was filled from the backend actually selected, so the two
  described different backends in the same breath (a WPD "no camera roll" caption above a Shell "not
  found" hero — the contradiction in the reported screenshots). Both are now fed by one probe of the
  backend in use, which also stops sending the phone two probes for one answer.
- **Honest device detection.** Probing distinguishes *absent*, *present*, *present but media unavailable*
  (locked, untrusted, iCloud-only, or MTP still warming up), *present with media*, and *error*, and the
  dashboard says which one applies. Additive `DeviceStates` helpers (`IsPresent`, `IsUsable`,
  `NeedsAttention`) keep the distinction.
- **Detection no longer fights Explorer, Apple's services or a running transfer.** A cheap presence check
  runs every 5 s (device list only, no content session); the full media probe runs on the rising edge and
  then every third tick while unreadable, and never while a device is already readable.
- **Audio feedback.** UI sounds are synthesised in memory (no assets): a tick on buttons, a lower toggle
  on checkboxes, and completion/attention tones on scan and transfer completion. Gated by
  *Play interface sounds* in Settings (default on).
- **Fixed: dashboard tiles left a seam.** Every hand-painted control in the shell opts into
  `ResizeRedraw` except `MetricCard`, so when the dashboard stretched a tile from its design width to
  fill its column only the newly exposed strip was repainted — the tile's old rounded right edge stayed
  in the middle of the card, the accent stripe visibly restarted against it, and the fill behind it
  looked like a second ghost tile. Verified by paint-logging the control's sizes and clips, and by
  comparing a window capture with a desktop capture (the artefact was real, not a capture artifact).
- **Fixed: the destination button was clipped at some scale factors.** The trailing "Browse…" button was
  scaled with integer division (`DeviceDpi / 96`), which is wrong at 125% and 150%; it now uses the exact
  ratio, so the caption is complete at every scale factor.
- **Fixed: the disabled-button wash.** Disabled actions painted a light grey slab because
  `Color.Transparent` carries RGB white; disabled fills are now composed from the theme's border and host
  colours and render as neutral, inert slabs whose text still meets contrast.
- **Fixed: the window could be re-centred on a DPI change.** The working-area clamp no longer repositions
  a window the operator has already placed, and never touches a maximized one.
- **Accessibility:** the caption buttons now expose names (*Minimize*, *Maximize or restore*, *Close*)
  instead of inheriting a sibling label's text.
- **Theme changes are live and persist.** Selecting a theme repaints the whole shell immediately, and
  saving writes the choice to `settings.json` — proven end to end by automating the combo box and reading
  the saved file back (Neon → Light).
- **Verified here:** the handset's namespace was enumerated through the same Shell call the app makes, and
  that is what exposed the missing `DCIM` level (the storage container's five children, one of them with
  thousands of `.HEIC`/`.JPG` items); the app's own log confirms both backends reached "storage is
  visible, but no DCIM" rather than failing earlier; and tests pin the resolution rules against those
  verbatim folder names plus a Shell-backend probe driven from an MTA thread. Release build 0 warnings /
  0 errors; at that point **405 tests passing, 0 failing** of 407, 2 opt-in hardware tests skipped (see below). Layout re-verified at
  150% DPI on Windows 10 Pro 19045. The unfinished-session recovery work added in this same release is
  described at the top of this section and in `docs/RECOVERY.md`; the three defects an acceptance audit found
  afterwards — a device serial that was not redacted, a quadratic second-pass comparison and a per-file folder
  walk on the resume path — are described in the bullets above and in `docs/RELEASE-ACCEPTANCE.md`.
  The Android storage work is also described at the top of this section. An Android phone was attached during
  the last hardware session — Windows listed it under "This PC", both of its storage volumes (*SD card* and
  *Internal storage*) were readable in Explorer, and the app's presence probe saw it — but the phones were
  disconnected before the Android walk and copy could run. The Android device path therefore remains
  unit-tested only, and no Android device result is claimed (see `docs/RELEASE-ACCEPTANCE.md`).
- **Verified live on the reconnected iPhone 13 Pro (iOS 27.0.1, serial FCC4L3VYCW).** The fixed build
  detected the handset through native WPD — *"Backend probe Windows Portable Devices (native): Ready —
  iPhone media is accessible through native WPD (hash-verified transfers available)."* — the header showed
  the green **iPhone ready** blip with `Model: Apple iPhone · iOS: 27.0.1 · Serial: FCC4L3VYCW · Link: USB
  (WPD)`, and a full scan of the camera roll completed and enumerated **26,335 files / 106.49 GB** with
  `status=Complete`. The dashboard numbers cross-check exactly — 13,021 photos (19.79 GB) + 666 videos
  (86.69 GB) + sidecars — and the largest album (`202608_a`) holds 26,320 of those files. The connection
  honestly reports 2 of 4 groups (Photos, Videos); Music and Files are shown as not exposed by this
  transport rather than as empty folders.
- **Measured: the hang complaint was real but the scan is transport-bound, not algorithm-bound.** A timed
  Shell enumeration of that 26,320-item album took **723,017 ms** on a cold cache — roughly **27 ms per
  object**, essentially all of it in the first `GetFolder.Items()`/`.Count`; once enumerated, per-item
  property reads fall to 0.1–0.25 ms because the driver and Shell cache them. WPD pays the same device
  cost, so trimming WPD's six-property set would not have helped. The same scan then completed in
  **4.9 s** on a warm cache. A first scan of this handset is therefore *legitimately* ten minutes of work,
  and the actual defect was that it was silent: progress went only to the Backup & queue page, so the
  Overview looked frozen while the device was dutifully enumerating. Re-created live before the fix —
  scan started 18:55:27, cancelled by hand at 19:05:50 after **10 m 23 s** with no completion and 0.8 %
  CPU (blocked on MTP, not deadlocked). Progress is now routed through a throttled `ScanProgressReporter`
  (at most one report per 250 ms, always on a folder change) to the status bar as well:
  `Scanning <folder> · N files · X GB · N files/s · mm:ss elapsed`. `files.Sum(...)` was also replaced by a
  running byte counter so the hot path cannot become quadratic in a 26,000-item folder.
- **Measured and fixed: the engine's own per-file overhead was costing far more than the copy.** A new
  deterministic benchmark (`EngineThroughputBenchmarkTests`) drives the real engine - staging, streaming
  SHA-256, atomic commit, manifest and database updates - against a local disk, and it immediately found a
  defect that no amount of device-side reasoning would have: copying **4,000 small files took 45.76 s, about
  87 files/s**, while 32 MiB files sailed along at 560 MiB/s. The cause was the recovery bookkeeping, not the
  data path. Every file forced the **entire** session manifest to be serialised to disk (`MarkDirty(force:
  true)`) - fine for a hundred files, ruinous for tens of thousands, because the manifest is the growing
  artefact, so the cost climbs as the transfer proceeds. On top of that, each file committed **two** SQLite
  transactions of its own, and opening a session committed one per planned entry (26,335 of them before the
  first byte moved on the real handset). And the log file was stat'd, opened, appended and closed on **every
  line**, which the copy path writes once per file. Each fix was measured on its own: the manifest write alone
  took 87 files/s to 533; batching the database writes took it to 606; keeping the log open took it to
  **670-703 files/s** - roughly **8x** - with the verification level unchanged (`SourceAndDestinationHash`)
  and large-file throughput untouched at ~600 MiB/s. Durability was not traded away: the plan, pause, cancel,
  abort and summary paths still force a flush and drain the pending queue, and recovery revalidates each file
  against the destination rather than trusting the record.
- **The live view is now paced instead of repainted per file.** With the engine reporting after every file,
  a 26,000-item library posted 26,000 updates to the message queue, each repainting the queue page and
  re-rendering the new Overview band. `Core/ThrottledProgress.cs` refreshes the UI at most every 250 ms
  (measured on that same benchmark: 4,000 engine updates became **21** UI updates), and nothing is lost,
  because the terminal state comes from the run's summary and the explicit completion, cancellation and
  failure paths. The pacing decision sits under a lock but hands the value on outside it, so the engine can
  never end up waiting on the UI thread.
- **Honest about what the speed-up means.** Both numbers are from local storage and are labelled as such;
  the *device* leg was not re-measured, because the handset was detached. Since a cold camera roll costs the
  transport ~27 ms per object and the engine now costs ~1.5 ms per file, the engine is no longer the lever -
  which is exactly why this entry claims an engine speed-up and not a transfer speed-up.
- **The Overview now shows the operation it is running, and can no longer clip to fit.** A dedicated
  *Current operation* band sits between the headline metrics and the destination block, and it separates
  the two phases that used to be invisible from this page: **Scanning** sweeps indeterminately for as
  long as the walk is open-ended (a camera roll whose size nobody knows yet), while **Transferring**
  becomes a determinate bar with counts, measured throughput, elapsed time and remaining time; **Paused**
  and **Stopped** are their own states again, so one percentage never stands in for both a scan and a
  copy. `Core/OperationStatus.cs` carries the phase, heading, detail line and a 0-1000 permille position,
  which is why a 106 GB total - many times `int.MaxValue` - still produces a correct position instead of
  overflowing. Adding the band cost 52 px, which exposed the sharper defect underneath: the page was a
  fixed vertical stack that simply squeezed its own action panel until the integration line was sliced in
  half (reproduced in a real capture, and now regression-tested). The page declares an
  `AutoScrollMinSize` for its true content height, so a short window scrolls instead of clipping while a
  tall window still stretches the action panel exactly as before.
- **Fixed: the caption buttons were nearly invisible on the Light theme.** `Theme.StyleButton`'s chrome
  branch hard-coded the glyph colour to `#E8F0FF` — near-white, which measures about **1.06:1** against
  the Light theme's white band. The glyphs now follow the theme and measure **16.3:1** on that band
  (sampled from a real window capture: glyph `RGB(21,23,28)` on band `RGB(243,244,247)`), and the
  hover/pressed fills darken against a light band instead of washing out. Four tests pin a minimum 3:1 on
  every theme, plus a premise guard that fails if the Light band ever stops being light.
- **Fixed: two ways a `SynchronizationContext` could hang the suite, and one of them was a real product
  bug.** Creating a WinForms control installs a `WindowsFormsSynchronizationContext` on the calling
  thread, whose `Post` only runs inside a message pump; that caused two distinct hangs. (1) The
  caption-contrast test created controls on the runner's own thread, so a later `async` test had its
  continuation posted to a queue nothing drained: 89 tests passed and then the run stalled until the
  harness killed it, while the same test passed in isolation. Control work now runs on a dedicated STA
  thread that dies with the test, and `WinFormsTestBase` snapshots and restores the context around every
  control-creating test so the runner cannot be poisoned a second time. (2) More seriously,
  `TransferService`'s entry points captured the caller's context across their `await`s, so any host that
  waited for the result instead of pumping messages deadlocked - the whole 26,400-entry scan test wedged
  behind a continuation queued on a UI context that no one was draining. Every public entry point now
  starts on the thread pool through `Services/ContextFree.cs`, which is the documented behaviour for
  library code, and three regression tests install a pump-required context and prove that a blocking
  caller still gets its answer, still copies, still cancels, and leaves nothing queued behind. The two
  tests that talk to real hardware remain opt-in (`ITOTAL_HARDWARE_TESTS=1`) and time-bounded, so the
  default suite is deterministic, needs no phone, and cannot wait on a busy one.
- **Transfer engine: two avoidable per-file costs removed.** (1) The copy path asked the device to confirm
  every source object id with an extra property read before streaming it — one avoidable round trip per
  file, i.e. tens of thousands queued ahead of a full camera roll. The id comes from the current session's
  own scan, so it is now used directly and only re-resolved by path if the device refuses it. (2) When the
  backend hashes raw source bytes while streaming (WPD does, and returns that digest on its `CopyResult`),
  the engine re-read the entire staged payload to hash it a second time — a full extra pass over the
  library (106 GB here) that could only ever compare the copy with itself. It now compares the streamed
  source digest against the committed destination, which is cheaper **and** stronger: it also catches a
  bad read, not just a bad write. Backends that cannot hash source bytes keep the staged read-back,
  because for them it is the only independent comparison available — both behaviours are pinned by tests.
- **Made two Settings controls honest.** "Copy buffer size" was offered but ignored: the WPD copy
  hard-coded a 1 MB buffer. The choice now reaches the backend through one shared clamp
  (`AppSettings.BufferSize`). "File concurrency" could not work at all — an iPhone session serialises
  copies and no backend read the value — so it is now pinned to 1, disabled, and labelled *fixed at 1 — an
  iPhone session serialises copies* instead of pretending to be adjustable.
- **The wireless receiver now has the security model it claimed.** It used to show one random token,
  accept it in a header for any number of uploads until it expired, and write every authenticated POST
  straight into the destination folder. The plan for the feature asks for a single-use pairing secret,
  rotation, expiry, an explicit operator decision about incoming files, and a receiver bound to one
  private interface; the implementation now does all five. The displayed secret is **single-use** and is
  redeemed once for a separate session token, so the value that lands in a URL, a QR image or a screenshot
  is not also a write credential - presenting it to `/upload` is refused, not honoured. Session tokens
  expire (one hour by default), can be rotated by the sender before a long batch, are capped at eight live
  sessions, and are revoked when the receiver stops. **Nothing is written until the operator approves it**:
  a POST becomes an offer in an approval list showing the path the sender wants, its size and the sender's
  address, and the request only completes after Approve or Refuse - a refusal also sticks, so a sender
  cannot out-wait it by retrying. Automatic approval exists, but it is a visible switch that says so, not a
  silent default. The bind address is validated as loopback or RFC1918 and a wildcard or public address is
  refused with the reason, which closes a real hole: the old address list offered every interface the
  machine had. The pairing ticket is now a **scannable QR code** as well as a URL, and the QR is proven by
  an independent decoder in the test suite rather than assumed from the picture.
- **Three defects in the old wireless path, found while rewriting it.** The ticket URL was built from the
  *requested* scheme, so when a TLS prefix failed to bind and the listener fell back to HTTP, the UI showed
  an `https://` address that could not work; the URL now describes the listener that actually started. A
  path was normalised with Windows separators for the approval identity, so an operator's refusal of
  `DCIM/x.HEIC` matched nothing and the sender waited out the full timeout. And two uploads of the same
  path could both resolve to the same collision-free name and overwrite each other; choosing the name and
  moving the file are now one critical section, and a second in-flight upload of the same path is told so
  instead of queueing behind an unresolvable name.
- **Driven live, not only unit-tested.** The receiver was run as the published executable against a real
  HTTP client over loopback, and the whole contract was observed rather than argued: an upload with no
  credential and an upload presenting the displayed secret both answered `401` and wrote nothing; redeeming
  the secret returned a session token and a second redemption of the same secret answered
  `401 ... already been used`; a 1 MiB upload sat **pending** with nothing on disk until Approve was clicked,
  then answered `200` and the file's SHA-256 matched the bytes sent; a refusal answered `403` and wrote
  nothing, and retrying that path was refused again; traversal answered `400` and created nothing outside the
  destination; an approved upload whose declared digest disagreed answered `422` and left **no staging file**;
  and two concurrent uploads of one name both succeeded with the second renamed to `IMG_0005 (1).HEIC` rather
  than overwriting the first. The TLS fallback was driven the same way, with **Require TLS** on and no
  certificate bound to the prefix: the receiver fell back, the status line named the missing certificate, and
  the ticket carried an `http://` URL - whereas the previous build advertised `https://` while actually
  listening on plain HTTP - and `/health` answered `tls:false`. Two interface defects surfaced only in the pixels: the explanatory paragraph on
  the Wireless page was cut off mid-sentence, and a QR code stayed on screen after its secret had been spent,
  offering a code that could not work. Both are fixed - the paragraph was shortened and given room, and a
  redeemed ticket now withdraws its URL and code and says so.
- **The diagnosis now separates "Windows remembers this iPhone" from "this iPhone is here", because they are
  different facts.** A PC keeps a USB record for every Apple device it has ever seen, and Windows goes on
  showing it - Device Manager's own properties for such a record read *"Currently, this hardware device is not
  connected to the computer"* - while Apple's services go on listing it. So "my PC can see my iPhone" is
  routinely true of a phone that is not on the desk, and the app used to answer it with a bare *no device*,
  leaving the operator holding two contradictory readings and no way to tell which was lying. The Diagnostics
  page now leads with the USB evidence: the handsets answering a live probe, the records Windows is merely
  holding, and the support interfaces that accompany a handset (the composite parent, Apple's USB driver,
  usbmuxd) but cannot supply a single file. The distinction is decided from the **driver service** bound to
  each record - `WUDFWpdMtp` is the media interface, everything else is support - so it does not depend on
  Apple's display names, and it can never *downgrade* a live handset: an attached phone always reads as
  attached, whatever stale records sit beside it. A registry read that fails contributes no records instead
  of failing the check. On the machine this was written on it reports **CACHED RECORD ONLY (2): Apple iPhone,
  Apple iPad**, with 3 support records and nothing attached - the exact trap this exists to name - and the
  same evidence travels in the diagnostics bundle as display names and counts only, never the instance key
  Windows stores the serial number in.
- **Fixed: the diagnostics ZIP was never produced at all.** The bundle reads the log tail while the
  application is holding the log open for writing, and `File.ReadAllLines` grants only *read* sharing.
  Windows checks a new handle's sharing against the existing writer's access, so the read failed with a
  sharing violation, the exception escaped `DiagnosticsBundle.Create`, and pressing **Export diagnostics ZIP**
  produced **no file whatsoever** - not a truncated bundle, nothing. Every export failed, every time, because
  the application always has a log open by then; it was found by pressing the button on the running build, and
  no amount of reading that method would have shown it. The tail is now read with write sharing, and a log
  that still cannot be read costs its own file (*Log tail unavailable (…)*) rather than the whole bundle, so
  the operator always leaves with the evidence. Driven live against the published build: a 7-file bundle
  (environment, redacted settings, backends, dependencies, **log tail**, context, README) written while the app
  was running, with the log tail holding that run's own lines.
- **Fixed: the Diagnostics page cut its own findings off at the column edge.** The detail column neither
  wrapped nor grew, so any finding longer than the column was silently truncated with nothing to say it had
  been - and a diagnosis that hides half of its own sentence is worse than no page. Details now wrap and their
  rows grow, in both the check and the dependency grids; the new USB row, a full paragraph, is the case that
  proved it. The statuses were also coloured apart instead of by a three-way exact match: green for checked
  and fine, amber for *worth your attention* (including a cached record), red for *this is why it fails*
  (including nothing ever seen).
- **Still not verified here (not claimed):** the *speed* effect of the two engine changes on hardware. The
  iPhone dropped off USB while the before/after benchmark was being set up, so the per-file round-trip and
  staged-read-back savings are implemented, reasoned from the measured call costs, and unit-tested, but
  **not measured end-to-end on a device** - a number is deliberately not claimed. Also unverified: the
  audible output of the UI sounds (the harness cannot listen to the machine's speakers) and Windows 11
  rendering (DWM rounded corners, native dark controls, `SetColorMode`) - this machine is Windows 10
  19045, so those remain code paths rather than captures. And on the wireless transport: the protocol,
  pairing, approval, rotation, digest and traversal rules are covered by loopback tests **and** were exercised
  live against the published build, but **no physical iPhone Shortcut has been run against the receiver** -
  the sender contract is documented in `docs/WIRELESS.md` and that run is recorded as an open acceptance item.
  On the diagnosis added in this release: the *cached-record* branch is verified against this machine's own
  USB registry, and the *attached* branch is behaviour-pinned by tests but has **not** been seen on a live
  handset - no phone was attached while it was written, so the wording an operator will read on a connected,
  unlocked, trusted iPhone is reasoned from the code path rather than observed.

## 1.7.0 — 2026-10-18 — Fluent redesign built on the brand banner, .NET 10

- **The banner is now the design system, not a decoration.** Every colour in the app is sampled from the supplied artwork: band `#011157 → #002785 → #063FA7`, glow `#2EADE8`, accent `#0B55F1`, and the full accent ramp `#14F0FC` cyan → `#05A1FC` sky → `#0B55F1` blue → `#222EEC` indigo → `#6F32FB` violet → `#B738FC` purple → `#F334F8` magenta → `#FB45BF` pink, plus the warm `#F9945C`/`#FCC146` pair for warnings.
- **New shell.** The window is a fixed navigation rail (grouped *Transfer / Activity / System*, glyph icons, accent-tinted selection) beside a content area under two painted banner bands: the app band (icon, wordmark, tagline, live device-state badge) and the per-page band (page glyph, title, one-line purpose). The page itself is a flat theme surface, so the band is the only place the gradient appears — the same restraint the banner uses.
- **Fluent control library** (`UI/Controls`): `BrandBand`, `CardPanel`, `MetricCard`, `PillLabel`, `RoundedButton`, `FluentProgressBar`, `FluentCheckBox`, `FluentRadioButton`, `EmptyState`, `InfoBar`, `Surface`. Anti-aliased GDI+ corners at DPI-scaled radii, cached fonts, `Segoe UI Variable`/`Segoe Fluent Icons` when present with `Segoe UI`/`Segoe MDL2 Assets` fallbacks on Windows 10.
- **.NET 10, plus native dark chrome where the OS provides it.** `net10.0-windows`, SDK pinned via `global.json`, `Microsoft.Data.Sqlite` 10.0.13. `Application.SetColorMode` (`Classic`/`Dark`/`System`) gives the Win32 common controls the app's colour mode — Microsoft documents that dark mode as **Windows 11 only**, so on Windows 10 combo boxes fall back to an explicitly themed flat style rather than keeping a light drop button, and the remaining native chrome stays as the OS draws it. Window chrome is themed through `DwmSetWindowAttribute`: immersive dark caption on Windows 10 17763+, rounded corners and caption tint on Windows 11 22621+ (each degrades silently when absent).
- **The banner's four groups are the data model.** `MediaCategory` + `MediaCategories.Of(extension)` decide Photos / Videos / Music / Files membership; `MediaSummary` aggregates counts and bytes; `MediaFile.Category` derives from the same rules; `BackendCapabilities.Categories` lets a backend declare which groups it can actually expose. The taglines are used verbatim in the UI: *Keep your memories*, *Big moments, no limits*, *Your sound, your way*, *Everything in one place*.
- **Dashboard rebuilt** around that model: a device hero card, "What your iPhone exposes" with four accent-tinted group tiles, four headline metrics (files found, total size, verified, needs attention), and the destination/actions card with honest policy lines.
- **Library rebuilt:** a filter toolbar (search box, five group chips, repair toggle) over the file grid with an empty state that distinguishes "nothing scanned yet" from "no matches", beside the verification report with one-click destination verification and scan export.
- **Transfer view:** gradient progress bar with eased motion, live percentage, indeterminate mode when no total is known, and throughput/ETA promoted to headline statistics.
- **New app icon:** the supplied glossy squircle embedded as a nine-frame `app.ico` (16, 20, 24, 32, 40, 48, 64, 128 as 32-bit DIBs; 256 as PNG) plus a 1024 px header badge.
- **AFC now enumerates the whole media area** — `/DCIM`, `/iTunes_Control/Music`, `/Recordings`, `/Books`, `/Downloads`, `/Purchases` — skipping roots the device does not expose and naming them in the warnings instead of silently reporting zero files. Write paths resolve relative to the correct root instead of forcing `DCIM/`.
- **Honest availability:** startup runs a silent connection probe so the dashboard is populated immediately; a backend that cannot expose a group shows it as unavailable rather than empty.
- **Tests: 90 passing.** New coverage for the banner palette, `System` resolution (Light or Neon), extension → group mapping, `MediaFile.Category`, group titles/taglines and capability gating.
- Release build 0 warnings / 0 errors; Neon, Slate and Light verified by DPI-aware window captures.
- **Still hardware-unverified (not claimed):** WPD/AFC enumeration and throughput on a physical iPhone, disconnect recovery, wireless from a real iOS sender, and the DWM caption tint on an active Windows 11 window.

## 1.6.0 — 2026-10-17 — verified transfer engine, native backends & Designer UI

- **Integrity first:** graded per-file verification (`SizeOnly` → `DestinationHash` → `SourceAndDestinationHash`); the strongest level actually achieved is persisted and reported. A size match is never reported as byte identity; unreadable items are reported **unverifiable**.
- **Streaming copy pipeline:** stage → hash-while-streaming → atomic commit → destination hash → compare; a mismatch fails the item and removes the new commit. Different-size/hash-mismatched collisions are preserved and reported; nothing is overwritten by default.
- **Complete enumeration:** `EnumerationReport` with `Complete/Incomplete/Empty/Failed`, per-folder outcomes, Live-Photo pairs, zero-byte/unknown-size counts. A folder read failure makes the scan `Incomplete` and is named — never silently omitted.
- **Filters & conflicts:** extension allow-list, folder/name exclusions, sidecar exclusion, min/max size; conflict policies preserve/skip/rename/overwrite.
- **Recoverability:** crash-safe throttled atomic JSON manifest with schema migration + `.bak` recovery; SQLite session database with WAL, batched upserts, history and problem-file queries; resume classification that never overwrites user data.
- **Native WPD backend** (`IPortableDeviceManager/Content/Resources`, read-only, dedicated MTA COM thread, GUIDs taken from Windows SDK headers) and an **AFC** read/write backend that is inert unless its runtime verifies.
- **Fail-closed native dependencies:** `native/dependencies.json` pins binaries by SHA-256; unpinned/mismatched binaries are refused. Ships with empty hashes → no DLLs bundled, AFC disabled.
- **Wireless receiver:** short-lived single-session pairing token, private-interface bind, strict path validation, size cap, SHA-256 on receipt, TLS when a certificate is bound (else token-authenticated private-network HTTP, stated plainly). Fixed a staging-lock bug found by a loopback test.
- **Designer UI:** the whole interface is Designer-authored — a shell (header, nav rail, content host, status bar) plus seven `UserControl` pages (Dashboard, Transfer & queue, Library & verification, Wireless, History, Settings, Diagnostics), each with its own `*.Designer.cs`.
- **Modern shell styling:** cards, the device-status badge and the navigation items are rounded, hairline-bordered surfaces (`CardPanel`, `PillLabel`, `RoundedButton`, `MetricCard`) painted with anti-aliased GDI+ and DPI-scaled radii; the header carries an accent hairline and primary actions/navigation use the accent ramp. The app icon is now the brand artwork — a generated seven-size `app.ico` embedded as both the executable icon and the header badge.
- **Four themes (Neon is the default):** `Neon` (deep navy surfaces with the sampled cyan→sky→blue→violet→magenta accent ramp `#4FEBFC → #24AAF8 → #2F6FE0 → #642CE4 → #D944DA`), `Slate` (neutral near-black greys with the brand blue `#2F6FE0` accent), `Light`, and `Follow Windows`, which reads the OS `AppsUseLightTheme` flag and re-themes live when Windows flips light/dark. A legacy `Dark` value in an existing `settings.json` still loads and maps to `Neon`.
- **High-DPI scaling fixed:** the form and every page now scale **uniformly** via `AutoScaleMode.Dpi` against a 96-DPI design baseline. The previous font-based autoscaling produced a non-uniform stretch (~x1.43 horizontal vs ~x1.67 vertical) that distorted the entire interface on a 150% display. The window is also clamped to the monitor working area and re-clamped on DPI change, so nothing (status bar, buttons) is pushed off-screen at any scale factor.
- **Settings & diagnostics:** validated/clamped settings with corrupt-file quarantine and atomic save; redacted diagnostics ZIP; rolling, redacting, level-aware logging.
- **Tests: 81 passing** (adds settings, dependency-verifier fail-closed, manifest-store recovery, filter/conflict, real loopback wireless, and theme palette/migration tests). Release build 0 warnings/0 errors; self-contained win-x64 publish verified.
- Version 1.6.0; `global.json` pins the SDK feature band.
- **Still hardware-unverified (not claimed):** WPD/AFC enumeration and throughput on a physical iPhone, disconnect recovery, and wireless from a real iOS sender.

## 1.5.0 — 2026-10-17 — verifiable integrity & architecture increment

- Established a working, reproducible build and test baseline (the prior "no .NET SDK" notes were stale; the build machine has .NET 8 SDK 8.0.423).
- Fixed the test project so it actually compiles (missing MSTest `using`); the advertised "12 passing tests" had never been built here.
- Introduced the `ITransferBackend` seam (`Shell` / `Wpd` / `Afc`) and refactored the Shell backend behind it, so the transfer engine, manifests, verification, and recovery are backend-agnostic.
- Added a first-class integrity model: `VerificationLevel` (`None`/`SizeOnly`/`DestinationHash`/`SourceAndDestinationHash`), `TransferEntry.SourceSha256`, and per-entry `Verification`. Session summaries are now labeled by the weakest verification actually achieved; a size match is never reported as more than size verification.
- Hardened `ValidateRelativePath` against reserved Windows device names (CON/PRN/NUL/COM1-9/LPT1-9), trailing dots/spaces, and control characters.
- Made resume/reconciliation and verification classification pure and unit-tested (`ClassifyExistingDestination`, `ClassifyVerification`).
- Fixed a SQLite connection-pooling resource leak (session.db stayed locked); disabled pooling for the per-operation tracker.
- Added a `MockBackend` and end-to-end engine tests (copy, source-vs-staged hashing, resume, collision, empty-scan rejection).
- Test suite: 42 passing. Release build: 0 warnings/0 errors. Self-contained win-x64 publish verified.
- Still outstanding (not claimed): native WPD COM stream backend, pinned/verified AFC runtime, iOS wireless sender, full Designer migration of tab content, and physical-device integration/throughput tests.

## 1.2.0 — 2026-10-16

- Added a WinForms Designer-owned partial class for the form shell and responsive root layout.
- Added a visible device-state dot: green only when the current Shell probe reports accessible iPhone media; black otherwise.
- Made the root layout owned by the Designer partial rather than adding a second root panel at runtime.
- Kept the limitation explicit: device detection still depends on Windows Shell and is not a native WPD enumeration/stream implementation.
- No Windows build or physical-device test was possible in the current environment.

# Changelog

## 1.0.0 — Project foundation

- New product identity: I-total transfer by Deveg.
- Dark Windows Forms dashboard with Transfer, Queue & Reports, Verification, Diagnostics and Settings pages.
- Connection preflight before scanning and again before transfer.
- Windows Shell iPhone media discovery and folder enumeration.
- Scan rejects empty listings, inaccessible folders, unnamed items and duplicate destination paths.
- Optional second full scan to compare paths and reported sizes for library-list consistency.
- Destination free-space check with reserve.
- Staged copying, retries, cancellation, stable-file polling, exact reported-size check, and no overwrite of different-size destination collisions.
- Persistent JSON session manifest plus SQLite session/file/event tracking; CSV/JSON reports.
- Size-based verification with explicit non-cryptographic status; unknown source sizes are marked unverifiable.
- Apple software/driver-file diagnostics, official Apple help links, configurable retry count and keep-awake option.
- Automated unit tests for completion gating, size comparison and path traversal protection.

## Known limitations

- Native WPD resource-stream backend is not yet implemented; the app still uses the Windows Shell compatibility backend.
- Windows Shell CopyHere remains asynchronous and can behave differently across Windows/iOS versions.
- No trusted source hashes are available through the current backend; size matching is not cryptographic integrity verification.
- Driver repair is guided through official Apple resources rather than automatically installing privileged packages.
- Full runtime testing on Windows and physical iPhones is still required.


## 1.1.0 — Architecture and enumeration audit (2026-10-16)

- Added a recursive scan of the exposed DCIM tree; inaccessible child folders abort the scan instead of being silently omitted.
- Added folder caching and `ParseName`-first source lookup to avoid repeatedly enumerating every file in a large folder when the Shell provider supports direct lookup.
- Replaced per-file linear manifest lookup with a case-insensitive dictionary lookup.
- Extended safe relative-path validation to nested folders and added traversal/absolute-path tests.
- Added architecture review, research source register and release acceptance checklist.
- Corrected the proposed backend strategy: AFC is optional/capability-probed, not guaranteed full Photos-library access; native WPD stream backend remains a required engineering milestone.
- No claim of a successful .NET build or physical iPhone integration test is made in this environment.

## 1.1.1 — UI responsiveness and device status
- Changed the Windows application title to `I-Total Transfer` (brand attribution remains inside the application header as “by Deveg”).
- Increased the initial/minimum window size and enabled DPI-aware WinForms scaling to reduce clipping on Windows 10/11 display scaling.
- Repositioned the device status area relative to the header width rather than a hard-coded X coordinate, preventing overlap at different window sizes.
- Added idle-time device polling every five seconds so a connected/unlocked iPhone can update the status without requiring a manual click. Polling is suspended during scans/transfers to avoid concurrent device operations.
- This remains the Windows Shell compatibility backend. These UI changes do not add a native WPD stream backend and do not establish that all iPhone media is exposed by iOS.


## 1.4.0 — staged-payload integrity

- SHA-256 the staged transfer payload before committing it.
- SHA-256 the destination after commit and require exact digest equality.
- Reject and attempt to remove the destination when the staged/destination digests differ.
- Persist both hashes in the manifest.
- Clarify that Shell CopyHere does not provide a raw iPhone source stream; source-to-target identity is not claimed.
- Remaining unimplemented: native WPD COM stream backend, pinned AFC runtime/dependencies, wireless iOS sender/pairing, fully Designer-authored screen layouts, and hardware validation.
