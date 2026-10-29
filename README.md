# I-Total Transfer

**by Deveg** — a Windows desktop app that copies media exposed to Windows over USB from a phone into a
user-chosen folder, and (when a verified backend permits) back again. It reads iPhones (camera roll) and
Android phones (whole storage volumes) through the same transport. The guiding rule is
simple: **never report a file as transferred unless it can be proven.** Every unverified or failed
item is named explicitly instead of being folded into a success count.

Version **1.8.0** · `net10.0-windows` · WinForms (Designer-authored) with a Fluent control library ·
.NET 10 LTS · runs on Windows 10 1809+ and Windows 11.

**Source and releases:** <https://github.com/shambrain/I-Total.Transfer-1.8.0>

**Screenshots:** 125 runtime captures - every page at 96, 150 and 200 % DPI including the minimum window,
all three themes, the live 150 % audits with their UI Automation dumps, the hardware drills and the
published exe - are in [`docs/screenshots/INDEX.md`](docs/screenshots/INDEX.md).

The window is borderless: the banner band doubles as the title bar (minimize / maximize / close painted
on the gradient, drag to move, double-click to zoom), while DWM still supplies the shadow, Aero Snap and
the Alt+Space system menu.

---

## What is verified in this workspace

| Check | Result |
| --- | --- |
| `dotnet build I-Total.Transfer.sln -c Release` | **0 warnings, 0 errors** |
| `dotnet test tests\I-Total.Transfer.Tests -c Release` | **460 passed, 0 failed, 2 skipped** (462 total; includes the trial/entitlement rules, signed-entitlement and revocation verification, the Steam app-id binding, the licence Settings row, the per-user installer run end to end, plus opt-in hardware tests; includes real loopback and private-LAN wireless - single-use pairing, rotation, the approval gate, digest/traversal rejection, and a non-elevated `TcpListener` bind - the pairing QR proven by an independent decoder, the wireless page contract on real controls, theme palette/migration, the Appearance preview controls (all three themes, selection, saved restore), media-group, camera-roll resolution, the Android device-family classifier and generic-MTP storage policy, the family-aware diagnostics rows, the two-phone preference order, the sticky device-selection guard and scan-identity refusal, the Apple-only attachment filter, the mid-scan disconnect guard, scan-cache provenance, progress-pacing, engine-throughput benchmarks, unfinished-session recovery through the real engine, the recovery surface's layout contract, log redaction in both directions, the second-pass scan comparison, the resumed-run path lookup, the "remembered vs attached" USB diagnosis built on this machine's real records, the diagnostics page's own contract, and the diagnostics bundle's export) |
| App startup smoke test (`I-Total.Transfer.exe`, 6s) | **launches and stays resident, no crash** |
| Release publish (`win-x64`, self-contained, single file) | **produces `publish\I-Total.Transfer.exe`** |
| iPhone diagnosis (live iPhone 13 Pro, via the app's own Shell call) | **device and storage container found; the container holds the camera-roll albums directly and has no `DCIM` level** — the cause of "connected but no DCIM container" |
| Window chrome (automated interaction test) | **maximize/restore returns the exact rectangle (Δw=0, Δh=0); band drag moves by exactly the pointer delta; minimize/restore exact; close exits** |
| Theme apply-on-save (automated) | **selecting Light repaints live and writes `Theme = Light` to `settings.json`** |
| Theme previews and design source (automated + live, 2026-10-22) | **Neon, Slate and Light swatches render, select the same theme as the combo, raise one live-preview event, and restore a saved Light choice; a fresh profile stays Neon.** The editable FigCraft source (four pages) and four design renders are recorded in `docs/UI-DESIGN-SYSTEM.md`; the runtime token/evidence map is `docs/UI-REQUIREMENTS-MATRIX.md`. The phone was disconnected, so the current Overview captures show the honest no-device state. |
| Page layout at 150% DPI | **all seven pages captured maximized, nothing clipped** |
| Scan-listing cache (live app, seeded listing) | **Settings shows "1 listing, 1,366 B"; the *Forget cached listings* confirmation removes it and the row updates to "No listing is on disk." with "Removed 1 cached library listing(s)." in the status bar** |
| USB evidence, live on this machine's real records | **Diagnostics reads `CACHED RECORD ONLY (2)` — *Apple iPhone, Apple iPad* — with 3 support records and nothing attached**, instead of reporting a remembered device as a connected one; the same verdict travels in the bundle. With nothing identified, the page also carries **`Device family: NOT IDENTIFIED`** and a **`CHECK PHONE`** trust row naming both prompts (Trust for an iPhone, File transfer for an Android phone) rather than an Apple-only one |
| Diagnostics bundle (published build, while running) | **a 7-file ZIP is written — environment, redacted settings, backends, dependencies, `logs-tail.txt`, `context.txt`, README** — where before this round *no* bundle was produced at all |
| Two phones attached at once (live, 2026-10-21) | **an iPhone 13 Pro and a HUAWEI Y7 Prime 2018 were both attached**; `AppleFirst` selected the iPhone and `AndroidFirst` selected the HUAWEI. The same session found a **cross-device scan defect** (a scan started for the iPhone could re-enumerate the HUAWEI after the iPhone briefly disappeared). `DeviceSelectionGuard`, `ScanResult.BelongsTo`, a UI refusal and a cache refusal now stop that switch; 14 tests pin the rule. The physical "selected phone disappears mid-scan" re-run remains pending |
| iPhone cold scan (live, final fixed build, 2026-10-22) | **55,185 files / 162,499,726,120 bytes (151.34 GiB), `Complete`, 23.3 min**, 11 folders, 0 failed folders, 0 warnings. The 2026-10-21 **26,335 files / 114,344,029,232 bytes (106.49 GiB) / 10.3 min** listing and the 2026-10-20 57,371-file / 188.81 GB warm pass are historical; the phone now exposes more albums |
| Filtered HEIC/MOV copy (live, 2026-10-22) | **8 files (3 HEIC + 5 MOV), 2,868,790 bytes** fetched through the real WPD backend and shared engine; all eight source/destination SHA-256 pairs identical, `HASH-VERIFIED COMPLETE`, 0 failed, 0 unverified. This was a filtered engine fetch, not the UI copy path |
| Android walk and copy (live, 2026-10-20, current source) | **4,103 files / 15,612,056,299 bytes (14.54 GiB), `Complete`**, from both volumes on 2026-10-22; the 2026-10-20 4,106-file / 15.01 GiB copy is historical. A 612,420,912-byte MP4 was cancelled mid-copy and resumed to `HASH-VERIFIED COMPLETE`, source and destination SHA-256 identical. A filtered copy planned 520 files: **412 source-and-destination SHA-256 verified, 108 `COPIED-UNVERIFIABLE`, 0 failed**, 3,586 skipped; ending honestly as **COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP**. Resume preserved the unverified entries. The trial was not charged because the run was not a meaningful completed job |
| Phone unplugged mid-scan (live, 2026-10-20) | **the run exposed a real defect**: the scan reported `status=Complete` while the disconnected phone made every later object unreadable. Objects that cannot be read now make the scan `Incomplete`, twenty consecutive failures abort the walk with an instruction to reconnect, and no incomplete listing is cached |
| Trial and licensing rules (automated, 2026-10-20) | **35 new tests**: the trial counts only completed, uncancelled transfers that wrote a file; failed/cancelled/empty/already-present runs are free; a repeated report is free; the counter survives a restart and the loss of one of its two stores; a clock moved back cannot reset it; a signed entitlement lifts the limit; tampered, expired, refunded, revoked, wrong-product, wrong-channel and wrong-app-id entitlements are refused with their own verdict; no public key means `NotConfigured`, never acceptance; one wireless sender session costs one job. No private key is shipped |
| Per-user installer (automated, 2026-10-20) | **install, upgrade, hash refusal and uninstall run against the real PowerShell script**: no admin rights, no firewall rule; upgrade keeps `settings.json`/`trial.json`; uninstall through the installed copy removes the folder and HKCU entry but keeps user data; a wrong hash exits 4 and installs nothing; uninstalling an unrelated folder exits 3 and deletes nothing; the portable ZIP installs from its own folder without an explicit `-SourceDir` |
| Release packaging (2026-10-24) | **portable ZIP and Inno Setup `setup.exe` produced from the publish output with `SHA256SUMS.txt`**; the setup installs per-user with no firewall rule and preserves `%APPDATA%`, and it is **unsigned** because no trusted code-signing certificate is available here |

Both handsets have now been read by the app: the iPhone through native WPD and the HUAWEI through the Android
volume walk and a filtered copy (see the table). Still **hardware-unverified** and labelled as such: a
statistical multi-file benchmark and an iPhone-side benchmark, a locked iPhone, a cloud-optimised library, a
physical cable pull mid-copy, and wireless from a real iOS sender. The mid-scan unplug, selected-phone
disappearance, attached USB diagnosis, Android cancel/resume, PTP warning, single-file buffer sweep and
30-file mixed batch are live. A screen lock does not disable MTP, so no Android lock-abort is expected. No
device result is fabricated.

---

## Backends

The engine talks to a device only through `ITransferBackend`. Concrete backends:

| Backend | Kind | Groups exposed | Read source | Hash source | Write to device | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| **Windows Portable Devices (native)** | WPD | iPhone: Photos, Videos. Android: all four. | yes | yes | no | One read-only COM backend with a per-family storage policy. An **iPhone** is read as a camera roll — a `DCIM` container, or the storage container itself when the driver publishes the albums directly. An **Android phone** is read as whole storage volumes (internal storage, SD card), each keeping its own name as its destination folder. Neither exposes a writable view through WPD that this app should use. |
| **AFC / libimobiledevice** | AFC | Photos, Videos, Music, Files | yes | yes | yes | Enumerates `/DCIM`, `/iTunes_Control/Music`, `/Recordings`, `/Books`, `/Downloads`, `/Purchases`, skipping and naming roots the device does not expose. **Inert until its pinned native runtime verifies.** |
| **Windows Shell (compatibility)** | Shell | Photos, Videos | read-back only | no | no | `CopyHere` fallback over the shell's camera-roll view (both layouts), run entirely on one STA thread. Can copy but **cannot** claim source-to-destination integrity. |

`BackendCapabilities.Categories` declares what each backend can reach, and the UI uses it: a group a
backend cannot expose is shown as unavailable rather than as an empty collection.

`BackendFactory` selects by preference (`Auto`/`WPD`/`AFC`/`Shell`) with an explicit, logged fallback
chain. All WPD COM calls run on a dedicated MTA thread so device access is serialized.

### Native dependency safety (fail-closed)

`native/dependencies.json` pins each native binary by SHA-256. On start-up and on the Diagnostics
page, `DependencyVerifier` hashes the on-disk binary and compares it to the pin:

- pinned + matching → **Verified**
- present but no pin → **NotPinned → refused**
- hash differs → **HashMismatch → refused**
- absent → **Missing**

The manifest currently ships with **empty hashes**, so every native binary is refused and the AFC
backend stays inert. That is intentional: dropping an unpinned DLL into `native\` can never enable it.
`docs/BUILD-VALIDATION.md` describes how to pin a reviewed build.

---

## Integrity model

Verification is graded, and the grade is persisted per file (`TransferEntry.Verification`):

| Level | Meaning | Requires |
| --- | --- | --- |
| `None` | nothing proven | — |
| `SizeOnly` | destination size matches the source-reported size | source size known |
| `DestinationHash` | staged payload SHA-256 == committed destination SHA-256 | local staging |
| `SourceAndDestinationHash` | **live source stream SHA-256 == destination SHA-256** | a backend that can read raw source bytes |

The staged read-back is only paid for when it is the *only* comparison available. A backend that hashes
raw source bytes while streaming already knows the source digest, so re-reading the staged file would cost
a second full pass over every byte — 106 GB on the test handset — and could only ever compare the copy
with itself. Comparing the streamed source digest with the committed destination is both cheaper and
stronger: it also catches a bad read, not just a bad write. Backends that cannot hash source bytes (the
Shell compatibility backend) still get the staged read-back, and which path ran is recorded per file
rather than assumed.

The UI and reports never upgrade a file beyond the level actually achieved. A same-size file with no
saved digest is reported **unverifiable**, not verified. Existing destination files are never
overwritten by default; different-size or hash-mismatched collisions are preserved and reported.

A zero-byte file is a measurement, not a missing one: it is hashed and verified like any other file. A
size the device does not report stays **unknown** — it is never turned into 0 bytes — and a copy with no
size and no source-byte hash is reported unverifiable. Every session writes `I-total-reconciliation.csv`
beside the JSON and per-file CSV, counting each exposed object once as copied, already present, excluded,
failed, inaccessible or outstanding, with the verification grade it reached.

Copy pipeline: stream → **staging** (hash while streaming) → **atomic commit** → destination hash →
compare against the strongest digest available (the streamed source digest, or the staged payload read
back when the backend cannot hash source bytes). A mismatch fails the item and removes the new commit.

Measured behaviour, the audit that produced the 1.8.0 performance work, and everything that is
deliberately left unclaimed are recorded in [`docs/PERFORMANCE-AUDIT.md`](docs/PERFORMANCE-AUDIT.md).

---

## Features

- **Complete enumeration with an explicit report.** A scan is `Complete`, `Incomplete`, `Empty`, or
  `Failed`. A folder that cannot be read produces an `Incomplete` report with the failing folders
  listed — it is never silently omitted. The report distinguishes exposed files from Photos-app
  counts and records Live-Photo pairs and zero-byte/unknown-size items. An object that cannot be read now
  makes its folder a failed read, and twenty failures in a row abort the walk with an instruction to
  reconnect — a phone unplugged mid-scan can no longer produce a "complete" listing of the part the app
  happened to see first.
- **Resume that cannot lie.** A crash-safe, throttled JSON manifest (`.i-total\session.json`) records
  per-file state, attempt counts and hashes; a SQLite session database (`.i-total\session.db`) keeps
  history and problem-file queries.
- **An unfinished transfer is visible, and it can be continued.** The destination folder is stamped
  `RUNNING` before the first byte moves, `CANCELLED` on a Stop and `ABORTED` on a fault, so a job that was
  killed with no chance to write anything is still identifiable by the stamp it left at the start. The
  Overview then shows an inline bar with the folder's own numbers and a **Resume transfer** action — with
  **Not now** to set it aside, remembered against that session alone so later progress always brings it
  back. Continuing rebuilds the plan from the record instead of re-enumerating the phone (a 26,335-entry
  record with 25,132 outstanding rebuilds as a 25,132-file / 172 GB plan), fetches **only** the outstanding
  files, and hashes every one of them again: nothing is trusted on the strength of the record. Files already
  verified are left alone rather than re-read, the session can only be declared complete when **nothing
  anywhere in it** is failed, unreadable, unverified or untried, and a copy whose verification was
  interrupted is discarded rather than left in the folder at a quietly weaker grade. See
  [`docs/RECOVERY.md`](docs/RECOVERY.md).
- **Last known library while a cold scan runs.** A cold camera roll is enumerated by the device at
  roughly 27 ms per *object* (measured: ~12 minutes for one 26,000-item album), during which the library
  page used to be simply empty. The last complete listing of each device is kept under
  `%APPDATA%\I-Total Transfer\scan-cache\` and shown immediately, marked *Last known library (captured …)*
  — never as the result of the run in progress. The live scan still runs, still replaces it, and reports
  what changed (added / removed / changed size). Only `Complete` enumerations are ever written, so a
  partial listing cannot overwrite a good one; a listing is refused if it is older than the configured
  age, from another device or backend, or from an older schema. The Settings page shows what is on disk
  and can delete it, and the feature can be switched off entirely.
- **Filters.** Extension allow-list, folder/name exclusions, sidecar exclusion, and min/max size.
  Excluded files are recorded as skipped, never dropped silently.
- **Conflict policies.** Preserve-and-report (default), skip, rename incoming, or overwrite.
- **Pause / resume / stop** that stay responsive (a cooperative pause gate, not a blocked thread).
- **Two phones, one choice.** An iPhone and an Android phone reach Windows on the same portable-device bus.
  Settings → *When both phones are attached, read* chooses which family is read first; the preference only
  reorders and still falls back when the preferred family is absent, and the phone that is not read is named
  in the connection message and on the Diagnostics page.
- **A three-job trial measured in finished work, then a one-time licence.** The free trial counts only a
  transfer that completed, was not cancelled, wrote at least one file and had no failures; a failed,
  cancelled, empty or "everything already present" run costs nothing, and re-reporting the same run is
  recognised by the run id. The counter is kept in `%APPDATA%\I-Total Transfer\trial.json` **and**
  `HKCU\Software\I-Total Transfer\Trial` and merged pessimistically, so a restart, an update or deleting one
  copy cannot reset it. A purchased licence is a signed file verified offline against a public key embedded
  in the build (no private key, no server call, no telemetry); signed revocation lists handle refunds and
  chargebacks, and the Steam edition has its own channel bound to `steam_appid.txt`. The gate is checked only
  before a **new** job starts - an unfinished session can always be resumed - and no licence error can stop
  a transfer already in flight. Settings → Licence shows the live counter, the rule that defines a job, an
  `Enter licence…` picker and a Buy button (disabled with its reason when the build has no store URL). A
  finished trial also refuses new wireless uploads (HTTP 402) while leaving received files untouched.
- **A safe per-user installer, with signing prepared.** `installer\Install-I-Total.Transfer.ps1` installs
  under `%LOCALAPPDATA%\Programs\I-Total Transfer` without administrator rights, without a firewall rule and
  without a service. It can require the published executable's SHA-256 first, records every installed file,
  creates the Start Menu shortcut and the HKCU uninstall entry, upgrades in place while keeping settings, the
  trial ledger and the licence, and its uninstaller removes only recorded files and refuses a folder it does
  not own. `installer\Build-Installer.ps1` builds the portable ZIP (and the Inno Setup `setup.exe` when
  ISCC is available); `installer\Sign-Artifacts.ps1` signs and verifies with `signtool`, failing closed when
  the tool or certificate is missing. See [`docs/INSTALLER.md`](docs/INSTALLER.md) and
  [`docs/RELEASE-SIGNING.md`](docs/RELEASE-SIGNING.md).
- **Wireless receiving** with a single-use pairing secret (shown as a URL and a scannable QR code),
  session tokens minted from it that expire, rotate and are revoked on Stop, strict relative-path
  validation, a per-file size cap, SHA-256 verified on receipt, and a private-interface bind. Incoming
  files wait in an approval list until the operator accepts them - and nothing is written to the folder
  until then - unless automatic approval is explicitly switched on. TLS is used when a certificate is
  bound; otherwise the token-authenticated session runs on the private network only and that fact is
  surfaced in the UI, the log and `/health`. The full sender contract is in `docs/WIRELESS.md`.
- **Windows → iPhone** send path (AFC only; refuses to pretend the Shell fallback can write).
- **Settings** persisted under `%APPDATA%\I-Total Transfer\settings.json`, validated/clamped on load,
  with a corrupt file quarantined rather than deleted.
- **Diagnostics bundle** — a redacted ZIP (environment, settings, backend status, dependency status,
  log tail) safe to share. Redaction is applied to the bundle's own contents *and* to every line the log
  receives: device serials, UDIDs and stable ids are masked to a short prefix, credentials are removed
  whole, and ordinary operational lines are left untouched (see `docs/RELEASE-ACCEPTANCE.md`, release
  engineering).

---

## The interface

The entire UI is declared in **WinForms Designer files** (`*.Designer.cs`) and is openable in the
Visual Studio designer. `MainForm` is a shell (header, navigation rail, content host, status bar)
hosting per-page `UserControl`s, each with its own Designer file:

| Page | What it does |
| --- | --- |
| **Dashboard** | Device state, headline metrics, destination picker, primary actions. |
| **Transfer & queue** | Live progress, throughput/ETA, the persistent per-file queue, log console. |
| **Library & verification** | Filtered browse of the enumerated media; run verification (with optional repair). |
| **Wireless** | Receiver bind settings, single-use pairing ticket with QR code, approval list for incoming files, received-file list with per-file SHA-256. |
| **History** | Recent sessions and problem files from the SQLite database. |
| **Settings** | Every option, persisted and clamped. |
| **Diagnostics** | USB evidence (which handsets are attached vs which Windows merely remembers), connection checks, native dependency provenance, diagnostics ZIP export. |

The shell is built from a small Fluent control library (`UI/Controls`): `BrandBand` paints the banner's
navy ramp behind the app header and the page header, `CardPanel`/`MetricCard`/`EmptyState` are rounded,
hairline-bordered surfaces with anti-aliased GDI+ corners at DPI-scaled radii, `RoundedButton` carries an
icon-font glyph and an accent gradient for primary actions, `PillLabel` renders the device-state badge,
and `FluentProgressBar` draws a gradient track with eased motion, a live percentage and an indeterminate
segment when no total is known. `FluentCheckBox`/`FluentRadioButton` replace the native check glyphs so
they follow the active palette exactly, while `ComboBox`, `NumericUpDown`, text boxes and scrollbars
follow the app theme through .NET 10's `Application.SetColorMode`.

The app icon is the supplied glossy squircle artwork, embedded as a nine-frame `app.ico` (16–256 px, so
the shell never has to resample it) and as a 1024 px badge for the header.

### The four media groups

The banner's promise — **Photos · Videos · Music · Files** — is the app's data model, not just a caption.
`Core/MediaCategories.cs` decides membership from the file extension, so a file is in exactly one group
everywhere it appears:

| Group | Tagline shown in the app | Extensions |
| --- | --- | --- |
| Photos | Keep your memories | `jpg jpeg heic heif png dng gif tiff tif bmp webp raw arw cr2 nef` |
| Videos | Big moments, no limits | `mov mp4 m4v avi hevc 3gp mts m2ts` |
| Music | Your sound, your way | `mp3 m4a aac wav flac aiff aif alac ogg opus` |
| Files | Everything in one place | everything else (documents, books, archives) |

The dashboard counts files and bytes per group, the library filters by group, and each backend declares
which groups it can actually expose (`BackendCapabilities.Categories`), so a group that a backend cannot
reach is shown as unavailable instead of silently empty.

### Themes

`UI/Theme.cs` holds the palettes and is selected in **Settings → Theme**:

| Theme | Look |
| --- | --- |
| **Neon** *(default)* | The banner itself: deep navy surfaces (`#040A1E` page, `#0C1A46` card) under a `#011157 → #002785 → #063FA7` band, with the sampled accent ramp `#14F0FC` cyan → `#05A1FC` sky → `#0B55F1` blue → `#222EEC` indigo → `#6F32FB` violet → `#B738FC` purple → `#F334F8` magenta → `#FB45BF` pink. |
| **Slate** | Neutral near-black greys with the brand blue accent — flat, no ramp. |
| **Light** | Light Fluent palette; the band inverts to a near-white ramp and the group accents stay. |
| **Follow Windows** | Resolves to Light or Neon from the OS `AppsUseLightTheme` flag and re-themes live when Windows flips. |

Every colour is sampled from the supplied banner rather than guessed. An existing `settings.json` that
still says `"Theme": "Dark"` loads unchanged and maps to **Neon**, so no preference is lost. Changing the
theme re-themes the whole window in place — no restart, no lost state.

Every control that the UI advertises is wired to a real service call. All Designer files use
`AutoScaleMode.Dpi` against a 96-DPI baseline, so the layout scales **uniformly** on high-DPI displays
(e.g. a 150% 4K/ultrawide screen) instead of stretching; the window is clamped to the monitor working
area and re-clamped when the DPI changes.

---

## Requirements

- Windows 10 1809+ / Windows 11 x64 (Windows 10 is past consumer end of support, but the app still
  targets and builds for it. Dark mode for the Win32 common controls is a Windows 11 feature, so on
  Windows 10 combo boxes use an explicitly themed flat style; spin-box arrows and scrollbars keep the
  OS look. Win11-only DWM features are used when present and skipped when absent.)
- .NET 10 SDK to build, or the .NET 10 Desktop Runtime to run a framework-dependent build
- iPhone unlocked and trusted by Windows, on a data-capable USB cable
- Apple Devices for Windows (or compatible Apple device support)

## Build & run

1. Install the .NET 10 SDK (10.0.100 or newer in the 10.0 feature band).
2. Run `Build-Release.bat` → publishes a self-contained `win-x64` single file under `publish\`.
3. Launch `publish\I-Total.Transfer.exe`.

For development, open `I-Total.Transfer.sln` in Visual Studio 2022 (or run `dotnet build`/`dotnet test`).
`global.json` pins the SDK feature band for deterministic builds.

## Install & uninstall

No administrator rights are needed and no firewall rule is created.

```powershell
# From the portable package (or the repository, after a publish):
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Install-I-Total.Transfer.ps1

# Upgrade: run the same command with the new package.
# Uninstall (keeps settings, trial and licence):
powershell -NoProfile -ExecutionPolicy Bypass -File .\Uninstall-I-Total.Transfer.ps1 -Uninstall
```

The installer places the application in `%LOCALAPPDATA%\Programs\I-Total Transfer`, keeps user data in
`%APPDATA%\I-Total Transfer`, and removes only the files it recorded when uninstalling. `-ExpectedSha256`
refuses to install an executable whose hash does not match. A packaged `setup.exe` is produced only when
Inno Setup 6 is installed; see [`docs/INSTALLER.md`](docs/INSTALLER.md) for every option, exit code and the
signing procedure in [`docs/RELEASE-SIGNING.md`](docs/RELEASE-SIGNING.md).

## Workflow

1. Connect the iPhone, unlock it, accept **Trust**, click **Check connection**.
2. Choose a destination with enough free space, click **Scan iPhone**. A failed/empty scan is refused.
3. Review the enumeration report and warning list. If the count looks low, fix iCloud-origins / cable /
   trust / driver issues and scan again — do not start the backup.
4. **Start full backup.** The engine streams, stages, commits atomically, and derives the strongest
   available verification level per file.
5. **Verify** (Library page) re-checks every path and size and compares stored digests; missing legacy
   digests are reported unverifiable. "Repair missing" re-copies files that are absent.
6. Review `.i-total\session.json`, `.i-total\session.db`, the enumeration report, the verification
   report, and `I-total-transfer.log` in the destination. Export a diagnostics ZIP from Diagnostics.

---

## Known limitations (hardware-unverified)

- **What is still hardware-unverified.** The fixed build *has* been run against a reconnected iPhone 13
  Pro: it detected the handset over native WPD (`Ready — iPhone media is accessible through native WPD
  (hash-verified transfers available)`) and completed a cold camera-roll scan of **55,185 files /
  162,499,726,120 bytes (151.34 GiB)**, `status=Complete`, in **23.3 min** on 2026-10-22: 11 folders,
  0 failed folders, 0 warnings, 0 unknown sizes, 1,167 live-photo pairs. The earlier 26,335-file /
  114,344,029,232-byte / 10.3 min listing and the 57,371-file / 188,810,836,019-byte / 10.99 s warm pass
  are historical; the phone now exposes more albums. A filtered HEIC/MOV copy of eight files (2,868,790
  bytes) also completed with source-and-destination SHA-256 equality.
  device-to-disk transfer benchmark (MB/s, files/s, CPU, peak memory), a transfer that is really cancelled
  mid-copy and then continued, the *attached* branch of the USB diagnosis (the cached-record branch is
  verified against this machine's own USB registry), a locked phone, a physical unplug while scanning, and
  wireless from a real iOS sender. Wireless security behaviour *is* covered by loopback tests and a live
  drill against the published build. No device result is claimed beyond the detection, the enumeration and
  the copy evidence above.
- **Android is read end to end, with two honest limits.** The HUAWEI Y7 Prime 2018 was walked through native
  WPD on 2026-10-22: **4,103 files / 15,612,056,299 bytes (14.54 GiB), `Complete`**, from both the *SD card*
  and *Internal storage* volumes, with the phone's own volume names as destination folders. A filtered copy
  planned 520 files and finished with **412 source-and-destination SHA-256 verified, 108
  `COPIED-UNVERIFIABLE`, 0 failed**, 3,586 skipped by policy; the unverifiable files are the phone's 0-byte
  `Internal storage\Sounds\.pictures\*.jpg` entries, so the run correctly ended **COPIED WITH
  UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP** rather than claiming a verified backup. Resuming
  that destination preserved the 108 unverified entries with 0 failures. The run was therefore **not** a
  meaningful completed job and did not consume a trial job. The Android cancel/resume, PTP, single-file buffer sweep and 30-file mixed batch are live; no statistical benchmark is claimed, and a screen lock is not a disconnect on this MTP stack. The classifier,
  the generic-MTP storage policy (whole volumes, the `Android\data`/`Android\obb` skips, the PTP warning),
  the two-phone preference order and the family-aware diagnostics rows are pinned by tests; TEST-PLAN
  hardware checks 24-27 and the matrix's remaining rows are the open items.
- **AFC ships disabled.** No native runtime is bundled; it activates only after a pinned runtime is
  installed and verified.
- **The shipped build cannot activate a purchased licence yet.** No production key pair or issuing backend
  exists, so the build carries no public key: `Enter licence…` reports `NotConfigured` and the three-job
  trial runs normally. Acceptance is fail-closed: a missing key never means a free licence. The verifier,
  revocation handling and Steam entitlement path are implemented and tested with generated keys; see
  `docs/KNOWN_ISSUES.md` KI-01/02 and `docs/MASTER_IMPLEMENTATION_PLAN.md` G9/G15.
- **Steam ownership is proven by a signed entitlement, not by a live Steamworks call.** The SDK and a
  partner account are not available here, and Steam has not confirmed that this utility is eligible for its
  store. The signed Steam entitlement is bound to the app id and is refused on the direct build.
- **No trusted code signature in 1.8.0.** The Inno Setup `setup.exe` is produced, but a trusted
  code-signing certificate is absent. `signtool.exe` and `Sign-Artifacts.ps1` are present and were drilled
  with a temporary self-signed certificate; the release remains unsigned. Unsigned builds may trigger
  SmartScreen.
- **Wireless needs an explicit iOS sender.** An iPhone cannot be enumerated over the network on its
  own; a Shortcut or companion app must select assets and POST them. The protocol, single-use pairing,
  approval gate, rotation, digest and traversal rules *are* covered by loopback tests **and** were driven
  live against the published executable with a real HTTP client (an upload with no credential or with the
  secret-as-session is refused and writes nothing; a file waits in the approval list with nothing on disk
  until accepted; a refusal sticks; traversal, digest mismatch and over-capacity are refused; a name clash
  is renamed rather than overwritten; and with TLS requested but no matching certificate the ticket says
  `http://` and `/health` says `tls:false`). What is still **not** done here is the other end of the wire:
  no physical iPhone Shortcut has delivered a file to this receiver. The contract is in `docs/WIRELESS.md`.
- **Enumeration scope.** Windows/AFC expose only media currently available to the host, and an Android
  phone exposes its own MTP view of its storage rather than a raw filesystem walk, so a complete scan means
  *everything the device offered*. A successful scan is not proof that iCloud-only originals were
  downloaded, nor that an Android app's private directory was readable.
- **A size match is not identity.** Only the strongest achieved level is ever claimed in the UI/logs.

## License

The source code in this repository is MIT (see `LICENSE`). The compiled application is distributed under a
one-time paid licence after a full-featured three-job trial; the trial and entitlement rules are described in
Features and in `docs/MASTER_IMPLEMENTATION_PLAN.md` section G. The trial is not DRM: it is enforced locally
with two redundant stores and no telemetry, and its limits are documented in `docs/KNOWN_ISSUES.md`.

See `CHANGES.md` for the change checklist, `PROJECT-STATUS.md` for the verification boundary, and
`docs/` for architecture, build-validation, test-plan, installer, signing and research notes.
