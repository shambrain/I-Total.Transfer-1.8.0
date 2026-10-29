# Progress log

One entry per working session, newest first. Each entry records what changed, the exact commands and results,
what remains blocked, and the next action. This is the continuation record: a new session should be able to
start from here without re-reading the whole conversation.

---

> **Publication note (2026-10-29).** Before the repository was published, its history was rewritten with
> `git filter-repo` to remove four testhost crash dumps (about 1.1 GB) that predated the `**/TestResults/`
> ignore rule. GitHub rejects blobs over 100 MB, and those dumps were never part of the product. Commit
> hashes quoted in this log before this note identify the pre-publication history; the file contents, tests
> and evidence are unchanged. The release executable is unaffected.

## 2026-10-29 - master directive: Gate A audit and size-truth fixes

### Starting point

`main` at `17c0b1a`, clean except the two directive files and two diagram JPEGs (untracked specification
inputs). Baseline build: 0 warnings, 0 errors. Baseline suite: **460 passed, 2 skipped, 462 total, 32 s**.
No phone was attached; the host is Windows 10 22H2 with .NET SDK 10.0.302. A feature branch,
`work/master-directive`, was created from that baseline so `main` stays recoverable.

### Audit

Read the complete master directive (799 lines) and execution instruction (132 lines). Wrote
`docs/AUDIT-1.8.0.md`, `docs/IMPLEMENTATION-GAP-MATRIX.md`, `docs/DEVICE-COMPATIBILITY-MATRIX.md`,
`docs/TRANSFER-ENGINE-DESIGN.md` and `docs/EXECUTION-STATE.md` from inspection and executed commands.

### Defects found and fixed

1. **Zero-byte files and unreadable sizes were the same value.** Every backend computed
   `unknownSize = files.Count(f => f.Size <= 0)` and `ZeroByteCount = files.Count(f => f.Size == 0)`, so a
   genuine zero-byte file was counted as both and the engine marked it `CopiedUnverified`. `MediaFile` now
   carries `SizeKnown`, `TransferEntry` persists it, and `ScanAccounting` is the one place that decides
   zero-byte versus unknown. A known zero-byte file is hash-verified like any other; an unknown size stays
   unknown. Nine accounting tests plus three engine tests pin it.
2. **Shell and AFC classified completeness differently from WPD.** Only WPD passed the unknown-size count
   into `ClassifyEnumeration`. All three now build their report through `ScanAccounting.BuildReport`, so a
   listing of unmeasurable files cannot be `Complete` on one backend and `Incomplete` on another.
3. **Resuming a verified unknown-size file called it a collision.** `ClassifyExistingDestination` only
   accepted a match when the source size was positive, so the next run marked the file `Failed`. A recorded
   destination digest that still matches is now accepted as `SkipVerified` when the size is unknown, and the
   engine hashes the destination in that case.
4. **Byte totals understated unknown-size copies.** `TransferEntry.BytesWritten` now records the committed
   length, and `TransferSummary.BytesCopied` counts it instead of the 0 placeholder.

### Commands and exact results

```text
dotnet build I-Total.Transfer.sln -c Release --no-incremental
  -> 0 Warning(s), 0 Error(s)

dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s
  -> Passed! Failed: 0, Passed: 472, Skipped: 2, Total: 474, Duration: 30 s
```

The first run of the new tests failed 4 of 474; each failure was inspected: two were wrong expectations in
the new tests (a zero-byte count and the old "0 means unknown" convention), and two exposed real gaps (the
resume rule and the backend completeness classification). All four were corrected in product or test, and
the rerun is the 472/2/474 above.

### Continued: SQLite hashes, reconciliation, retry classification, stability timestamps

5. **SQLite did not persist what was verified (D4).** `Files` held state and error text only; the hashes
   and the verification grade lived in the JSON manifest. `SessionDatabase` now adds `SizeKnown`,
   `BytesWritten`, `SourceSha256`, `StagedPayloadSha256`, `DestinationSha256`, `Verification`,
   `SourceModifiedUtc`, `SourceChanged` and `Outcome` with an idempotent `ALTER TABLE` migration, writes
   them in `UpsertFiles`, and exposes `QueryFileStates`. `SessionDatabaseTests` covers the round-trip, an
   unknown size staying unknown, the migration of a database created by the old schema (row preserved,
   defaults applied), and the session row.
6. **The session report had no reconciliation (D8).** `ReconciliationReport` counts every scanned object
   once under the outcome that happened (copied, already present, filter-excluded, conflict-skipped,
   failed, inaccessible, outstanding), with bytes and verification grades; `TransferSummary.Reconciliation`
   exposes it, the JSON report carries it, and `I-total-reconciliation.csv` is written beside the report.
   `OutcomeReason` is recorded per entry and persisted in both stores.
7. **Permanent failures were retried (D5).** `CopyFailures` classifies exceptions (denied, missing,
   invalid, unsupported, device-not-connected) and AFC statuses; `CopyResult.FailureKind` carries the
   verdict; the engine stops the retry loop on a permanent failure and logs why. A transient failure is
   still retried. Test: `APermanentFailureIsNotRetriedButATransientOneIs` (fetch counts 1 vs 2).
8. **The stability pass compared only path and size (D6).** `ScanStability` now compares size, whether the
   size was readable, and the modification time when both passes report one. Three tests cover a same-size
   edit, a size that became unreadable, and the documented limitation that a timestamp present on only one
   side is not evidence of a change.

### Commands and results after the continuation

```text
dotnet build I-Total.Transfer.sln -c Release --no-incremental
  -> 0 Warning(s), 0 Error(s)

dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s
  after D4/D8: Passed! Failed: 0, Passed: 481, Skipped: 2, Total: 483
  after D5:    Passed! Failed: 0, Passed: 482, Skipped: 2, Total: 484
  after D6:    Passed! Failed: 0, Passed: 485, Skipped: 2, Total: 487
```

### Gate E - WPD COM ownership

9. **Enumeration leaked COM objects.** `EnumerateChildren` created an `IEnumPortableDeviceObjectIDs` per
   folder and never released it; `GetProps` created a key collection and a values object per object;
   `TryGetValue` leaked its key collection and each per-key reader leaked its values object; and
   `ListDevices` never released the device manager. On a 26,000-object camera roll that is tens of
   thousands of provider objects left to finalization. All five paths now release deterministically in
   `finally` blocks (`ReleaseCom`). This is static evidence plus a clean build; no phone was attached to
   measure the effect, and none is claimed.

### Gate F - Designer and accessibility

10. **Designer coverage verified statically.** All seven pages have a `.Designer.cs` with a real
    `InitializeComponent` (37, 20, 20, 22, 41, 24 and 39 designer fields respectively) and `MainForm.Designer.cs`
    declares 36. No page builds its visual tree at runtime. Visual Studio is not installed on this host, so
    the open/save/reopen round-trip is not claimed.
11. **Interactive controls now have accessible names.** `UI/Accessibility.cs` fills `AccessibleName` from the
    caption, then the designer name, for buttons, text boxes, lists, combos and progress bars; `MainForm`
    applies it after building the pages. Two tests pin it: one asserts every page has no unnamed interactive
    control, one asserts the fallback order. A Narrator/NVDA audit has not been run (KI-24).

### Gate H - candidate build and packaging

```text
dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
  -> 0F15740792C5555FD2B5472FA5246E5538BA48B8AB3DA90BDF5D6A7D890854D3 (112,191,302 B)
```

The source ZIP was extracted into a clean folder and published there: the resulting executable has the
same SHA-256, so the source-only rebuild is byte-identical. `Build-Installer.ps1` produced portable
`00E46FB8...` and setup `9DE4D08F...`; the setup was installed silently into a temporary folder (exe and
README hashes matched), then uninstalled: folder, HKCU entry and shortcuts removed, `%APPDATA%` kept. `PROJECT-STATUS.md`, `BUILD-VALIDATION.md`, `RELEASE-ACCEPTANCE.md`,
`PERFORMANCE-AUDIT.md` and `KNOWN_ISSUES.md` were updated for the candidate; no hardware or performance
claim was added. The published executable was launched and stayed up for five seconds with the window title
`I-Total Transfer - verified phone media transfer` (working set 161.6 MB), then stopped; this is a launch
smoke test only.
12. **A no-op run no longer claims it produced a verified copy.** When every planned file was already
    present at an accepted grade, the summary said `HASH-VERIFIED COMPLETE`, which reads as if this run
    wrote something. It now says `ALREADY PRESENT — NO FILES NEEDED COPYING (verified against the session
    record)`, `TransferSummary.AlreadyPresent` carries the count, and a test pins the second run of an
    unchanged session.
13. **Verification results are persisted to SQLite as well as JSON.** `VerifyAsync` now writes the
    post-verification states (including `Failed` for a corrupted destination) and a
    `VERIFICATION COMPLETE/INCOMPLETE` session row; a test corrupts a destination, verifies, and reads the
    `Failed` row back from the database. The free-space preflight also no longer refuses a UNC destination
    whose free space cannot be read; it logs the limitation and lets the per-file copy report a real
    disk-full error.
### Blockers

No phone attached, so no hardware claim is made for the new size handling. AFC remains inert (empty
dependency hashes), Windows 11 and Visual Studio are unavailable, and signing/Steam remain external.

---
## 2026-10-29 - copy-path correctness audit (changed source, overwrite restore, staging)

### Starting point

HEAD was `33f1b99`; the published exe was `945E2D1F...` (112,154,438 B). Both handsets were detached, so
this session worked on the engine and its tests rather than on a device.

### Defects found and fixed

1. **A same-size source change was treated as unchanged.** `ManifestStore.MergeScan` overwrote the recorded
   source modification time before the copy loop, so `HandleExistingAsync` compared only the destination
   with its old hash and `SkipUnchanged` skipped a file that had changed on the phone. `TransferEntry` now
   carries `SourceChanged` (size or modification time), `PreserveAndReport` preserves the destination and
   reports the file, and `Overwrite` refetches it. Three tests fail on the old engine.
2. **A failed overwrite destroyed both versions.** `Overwrite` deleted the destination before the move; a
   SHA-256 mismatch then deleted the new copy, leaving nothing. The old file is now moved to
   `.i-total\backup` first, restored on any failure, and deleted only after the new bytes verify. Two tests
   pin restore and success.
3. **Failed/cancelled attempts left `.partial` staging files.** WPD and AFC now delete the partial in a
   finally block.
4. **Verification reported digest failures as size failures.** One `wrong` counter fed `WrongSize`, so a
   same-size corrupted destination was reported as a size mismatch. `WrongHash` is separate now, the
   report/UI/log name both, and a corrupted destination is recorded as `Failed` in the manifest.
5. **Repair did not verify the repaired bytes.** The repair path moved the staged file and marked it
   verified without comparing hashes. It now checks the staged size and SHA-256, discards a corrupt
   repair, marks the entry `Failed` and leaves the file missing.

### Commands and exact results

```powershell
dotnet build I-Total.Transfer.sln -c Release --no-incremental
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test tests\I-Total.Transfer.Tests -c Release --blame-hang --blame-hang-timeout 240s
# Passed! - Failed: 0, Passed: 460, Skipped: 2, Total: 462, Duration: 28 s

dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
# publish\I-Total.Transfer.exe: 112,158,534 B
# SHA-256 8FCB2B5095365995813AF028EFE4A27E6EC538A32ADEA12F153919AD0E61CABB
```

The key regressions were also run against the pre-fix engine by stashing only the source changes:
`AChangedSourceIsCopiedAgainWhenOverwriteIsChosen` failed (`Expected:<2>. Actual:<1>`),
`AFailedOverwriteRestoresTheOriginalDestination` failed ("The previous destination must survive a failed
overwrite"), and `RepairingWithCorruptBytesIsReportedAndDiscarded` reported success on the old repair path.
All pass with the fixes.

### Files changed

```
src/I-Total.Transfer/Core/Models.cs
src/I-Total.Transfer/Services/ManifestStore.cs
src/I-Total.Transfer/Services/TransferService.cs
src/I-Total.Transfer/UI/MainForm.cs
src/I-Total.Transfer/UI/Pages/LibraryPage.cs
src/I-Total.Transfer/Devices/Wpd/WpdDevice.cs
src/I-Total.Transfer/Devices/Afc/AfcDevice.cs
tests/I-Total.Transfer.Tests/MockBackend.cs
tests/I-Total.Transfer.Tests/TransferEngineTests.cs
tests/I-Total.Transfer.Tests/VerificationFlowTests.cs
CHANGELOG.md, PROJECT-STATUS.md, README.md, docs/*
```

### Packaging

- The portable ZIP and Inno Setup executable were rebuilt from the new publish; the deployment and source
  archives were regenerated and `dist\SHA256SUMS.txt` / `dist\installer\SHA256SUMS.txt` are authoritative.
- The setup was installed and uninstalled silently: installed exe hash matched `publish\`, no firewall rule,
  user data kept.
- The source ZIP was rebuilt and published byte-identically; a clean clone of the release commit was checked.

### Gate E - WPD COM ownership

9. **Enumeration leaked COM objects.** `EnumerateChildren` created an `IEnumPortableDeviceObjectIDs` per
   folder and never released it; `GetProps` created a key collection and a values object per object;
   `TryGetValue` leaked its key collection and each per-key reader leaked its values object; and
   `ListDevices` never released the device manager. On a 26,000-object camera roll that is tens of
   thousands of provider objects left to finalization. All five paths now release deterministically in
   `finally` blocks (`ReleaseCom`). This is static evidence plus a clean build; no phone was attached to
   measure the effect, and none is claimed.

### Gate F - Designer and accessibility

10. **Designer coverage verified statically.** All seven pages have a `.Designer.cs` with a real
    `InitializeComponent` (37, 20, 20, 22, 41, 24 and 39 designer fields respectively) and `MainForm.Designer.cs`
    declares 36. No page builds its visual tree at runtime. Visual Studio is not installed on this host, so
    the open/save/reopen round-trip is not claimed.
11. **Interactive controls now have accessible names.** `UI/Accessibility.cs` fills `AccessibleName` from the
    caption, then the designer name, for buttons, text boxes, lists, combos and progress bars; `MainForm`
    applies it after building the pages. Two tests pin it: one asserts every page has no unnamed interactive
    control, one asserts the fallback order. A Narrator/NVDA audit has not been run (KI-24).

### Gate H - candidate build and packaging

```text
dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
  -> 0F15740792C5555FD2B5472FA5246E5538BA48B8AB3DA90BDF5D6A7D890854D3 (112,191,302 B)
```

The source ZIP was extracted into a clean folder and published there: the resulting executable has the
same SHA-256, so the source-only rebuild is byte-identical. `Build-Installer.ps1` produced portable
`00E46FB8...` and setup `9DE4D08F...`; the setup was installed silently into a temporary folder (exe and
README hashes matched), then uninstalled: folder, HKCU entry and shortcuts removed, `%APPDATA%` kept. `PROJECT-STATUS.md`, `BUILD-VALIDATION.md`, `RELEASE-ACCEPTANCE.md`,
`PERFORMANCE-AUDIT.md` and `KNOWN_ISSUES.md` were updated for the candidate; no hardware or performance
claim was added. The published executable was launched and stayed up for five seconds with the window title
`I-Total Transfer - verified phone media transfer` (working set 161.6 MB), then stopped; this is a launch
smoke test only.
12. **A no-op run no longer claims it produced a verified copy.** When every planned file was already
    present at an accepted grade, the summary said `HASH-VERIFIED COMPLETE`, which reads as if this run
    wrote something. It now says `ALREADY PRESENT — NO FILES NEEDED COPYING (verified against the session
    record)`, `TransferSummary.AlreadyPresent` carries the count, and a test pins the second run of an
    unchanged session.
13. **Verification results are persisted to SQLite as well as JSON.** `VerifyAsync` now writes the
    post-verification states (including `Failed` for a corrupted destination) and a
    `VERIFICATION COMPLETE/INCOMPLETE` session row; a test corrupts a destination, verifies, and reads the
    `Failed` row back from the database. The free-space preflight also no longer refuses a UNC destination
    whose free space cannot be read; it logs the limitation and lets the per-file copy report a real
    disk-full error.
### Blockers and limits

- A statistical multi-file benchmark, an iPhone-side benchmark, a locked iPhone, a cloud-optimised library,
  a physical cable pull mid-copy, locked/unplugged diagnostics and a real iOS wireless sender need hardware.
- Trusted code-signing certificate, RFC3161 success path, Windows 11, production issuer and Steamworks remain
  external.
- The source-change signal is the scan's size/modification time; a same-size change on a device that reports
  no modification time is not detectable without re-reading the source.

### Publication

- The GitHub repository is **shambrain/I-Total.Transfer-1.8.0** (private). It was created from this tree
  after `git filter-repo` removed four pre-`.gitignore` testhost dumps (about 1.1 GB) from history; the
  largest reachable blob is a 44.9 MB release ZIP.
- A gallery of **125 screenshots** was added under `docs/screenshots/`: all seven pages in Neon, Slate and
  Light at 96 DPI (21); the 96 DPI minimum window in four variants per page (28); the 150 % minimum window;
  the 200 % minimum and design sizes; the published exe at 200 % via UI Automation; the live 150 % audit
  with its UI Automation dumps; the earlier 2026-10-21 audit kept in full; the theme previews; the iPhone/
  Android/PTP/lock/cancel/unplug drills; the attached-diagnostics page; and three published-exe captures.
  `docs/screenshots/INDEX.md` labels each set as live evidence or harness render, names what it does not
  prove, and lists the workspace captures deliberately not shipped with the reason.
- The README links the gallery and the repository. The deployment and source archives were rebuilt with
  the screenshots and the new README; the portable/setup archives were rebuilt because the README is
  embedded in them.
- The executable is unchanged at `8FCB2B50...` (112,158,534 B); `dist\SHA256SUMS.txt` is the authoritative
  record for the rebuilt archives. The source ZIP was extracted and compared file by file with the tree:
  171 of 171 `docs/` files identical, including all 125 PNGs and `MANIFEST.txt`. The deployment ZIP was
  re-opened: 125 PNGs, `INDEX.md`, `MANIFEST.txt`, and the embedded executable and README hashes match the tree.

### Next task

1. Reattach the phones and run the 4 GB+, mid-copy-pull and locked/unplugged diagnostics drills.
2. Obtain a trusted certificate and complete the timestamped signing path.

---

## 2026-10-24 - 96-DPI shell render, three release defects fixed, release refreshed

### Starting point

The tree was clean at `60a97a2 Complete v1.8.0 hardware validation and release artifacts`; the published
exe was `C953BC08...` (112,150,342 B) and both handsets were attached. The one remaining internal UI gap was
the 100 % pixel capture (KI-21): the host's only display reports 150 % (`AppliedDPI=144`).

### 96-DPI render harness

`%TEMP%\opencode\ui100` is a DPI-unaware WinForms executable (`HighDpiMode.DpiUnaware`) that constructs the
real `MainForm` at a 1400x880 client and renders the shell root `_root` (1416x919) with `DrawToBitmap`;
`DeviceDpi` reads 96. All seven pages were captured in Neon, Slate and Light (21 PNGs).

A first attempt captured the whole form. Because the shell collapses its non-client area with
`WM_NCCALCSIZE`, that capture painted the OS caption over the client and looked like a clipped title.
Rendering `_header` on its own showed the full 78 px band with the complete title and subtitle, so the
harness now renders the shell root. This is 96-DPI pixel evidence of the layout, not a native desktop
capture, and it is labelled that way in `UI-REQUIREMENTS-MATRIX.md`, `KNOWN_ISSUES.md` and
`PROJECT-STATUS.md`.

### Real defect found and fixed

At 1416 px the Wireless `Require TLS` caption measured 422 px of text (440 px with the check glyph) inside a
450 px card with 36 px of inset, so the closing bracket was cut. The caption is now
`Require TLS (private-network HTTP if no matching certificate)` (330 px). The new
`WirelessTlsCaptionFitsInsideItsCardAtTheDesignedWidth` measures the painted text against the card and
fails on the old wording (`440 px needed, 414 px available`), which was confirmed by temporarily restoring
the old string and watching the test fail.

### Commands and exact results

```powershell
dotnet build I-Total.Transfer.sln -c Release --no-incremental
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test tests\I-Total.Transfer.Tests -c Release --blame-hang --blame-hang-timeout 240s
# Passed! - Failed: 0, Passed: 453, Skipped: 2, Total: 455, Duration: 27 s

dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
# publish\I-Total.Transfer.exe: 112,154,438 B
# SHA-256 945E2D1FF5E4169C2DE1412C7D70DBD90B9FE20A5E49249657D0632FFF85337A
```

### Second real defect found during release packaging

A real portable run (extract `dist\installer\...-portable-win-x64.zip`, install, uninstall) exposed that the
installed `Uninstall-I-Total.Transfer.ps1` ignored its own folder: with a custom `-InstallDir` it still
looked at `%LOCALAPPDATA%\Programs\I-Total Transfer` and refused with exit 3, so the command printed in
`README-INSTALL.txt` did not work. The uninstaller is the installer script copied into the installation, so
it now uses its own folder when no `-InstallDir` was passed and that folder holds the `install-manifest.json`
it wrote; a portable package (which has no manifest) is never treated as an installation.
`TheInstalledUninstallerFindsACustomInstallFolderOnItsOwn` runs the installed copy with only `-Uninstall`;
it failed with exit 3 before the fix and passes now.

### Third real defect found by the attached-diagnostics run

With both handsets attached, the real Diagnostics page showed `Native AFC runtime: VERIFIED` beside the
sentence "No pinned native binary verified". `DependencyReport.RequiredDependenciesSatisfied` is vacuously
true when no dependency is marked required, and every native binary is optional, so the old expression
chose VERIFIED for an empty runtime. The status now follows what was actually verified: `NOT PRESENT` when
nothing is, `VERIFIED` only when at least one binary verifies and the required set is satisfied, `PARTIAL`
otherwise. `AMissingAfcRuntimeIsNotReportedAsVerified` failed with `Expected:<NOT PRESENT>. Actual:<VERIFIED>`
before the change and passes after it.

The same live run produced the attached-branch evidence: 10 checks with both phones attached (Apple
`ATTACHED`, family `APPLE`, software `DETECTED`, drivers `FOUND`, trust `ACCESSIBLE`) and a summary naming
both handsets; with only the HUAWEI attached, 8 Android rows and no Apple rows. Rows are in
`%TEMP%\opencode\diag-live\diagnostics-attached.txt` and the page capture is
`%TEMP%\opencode\diag-live\diagnostics-attached-page.png`. The iPhone was briefly absent while the first
probe ran; the fallback to the HUAWEI under `AppleFirst` is what the preference rule intends and is recorded
in the same log and capture set.

### Clean-clone reproducibility and line endings

A fresh `git clone` of the release commit built cleanly but published `7C126C55...`, not `411F4CC5...`.
The cause was line endings: every `.cs` file was LF in the working tree, while `core.autocrlf=true` and
`* text=auto` checked the clone out as CRLF. The deterministic MVID follows the source bytes, so identical
IL still produced a different PE.

`.gitattributes` now states the policy (`* text=auto eol=lf`; `*.sln`, `*.bat` and `*.cmd` stay CRLF;
`*.zip`, `*.png`, `*.ico` and `*.dmp` are binary), `.editorconfig` matches, and the working tree was
re-checked-out from the index. A second clone then built at 0 warnings, passed the full suite
(449 passed, 2 skipped, 451 total, 29 s) and published the byte-identical `411F4CC5...` executable.
Repeated at the final build commit `7426568` after the 200 % work: the clone built at 0 warnings,
passed **453 tests** and published the byte-identical `945E2D1F...` executable (112,154,438 B).

The same pass removed four tracked testhost hang dumps (about 1.1 GB) and a stale root-level 1.8.0 ZIP
from `HEAD`, and added `**/TestResults/` and `*.dmp` to `.gitignore`. The checkout fell from 1.37 GB to
146 MB. The dumps remain in the history pack; KI-22 records the `git filter-repo` step that would
reclaim that space. The 1.7.0 archive is kept as the last pre-1.8 artifact.

```powershell
git clone <repo> $env:TEMP\opencode\clone-verify3
dotnet build I-Total.Transfer.sln -c Release --no-incremental
# Build succeeded. 0 Warning(s), 0 Error(s)
dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
# 411F4CC5CD8303D10E6C542AE8CD28AD9EAAEE82C60C720912FD371FA8988CFF  (byte-identical to publish\)
dotnet test tests\I-Total.Transfer.Tests -c Release --blame-hang --blame-hang-timeout 240s
# Passed! - Failed: 0, Passed: 453, Skipped: 2, Total: 455, Duration: 27 s
```

### Minimum-window layout audit

The shell's declared minimum (1160x740) was rendered with the 96-DPI harness for all seven pages. Four
real clips appeared and were fixed:

- Wireless: the TLS caption lost its bracket, `New ticket` was cut to `Ne`, and the sender notice was
  below the card. The card now reflows for its width (captions wrap, buttons keep their labels, a
  vertical scrollbar reaches the notice) in `WirelessPage.LayoutSettingsCard`, and `FluentCheckBox`/
  `FluentRadioButton` wrap a caption that does not fit instead of only ellipsizing it.
- Library: `Repair missing` sat on top of the Files chip and Apply ran off the card. The toolbar now uses
  two rows below 1010 px (`LibraryPage.LayoutToolbar`).
- Transfer: the log toolbar hid `Clear view`; it now wraps and grows. The queue columns were rebalanced so
  `Size`, `Verification` and `Note` all render at this width.
- History: the database path wrapped behind the sessions card; the toolbar now grows.

`PageControlTests` gained four cases at 884x532 (the shell's minimum content area); each one fails on the
old layout. Suite: **453 passed, 2 skipped, 455 total, 27 s**.

### 200 % DPI render

During the 200 % pass the active display was 3840x2160 at **192 DPI (200 %)** (the session later returned
to 3440x1440 at 150 %). A PerMonitorV2 harness rendered the
real `MainForm` at the minimum logical window (2320x1480 device) and at the design size (2840x1800) for
all seven pages: text, cards, chips and the reflowed toolbars scale uniformly and nothing clips.

The window is larger than the logical desktop (1920x1080), so these are **200 % DPI renders**, not
200 % desktop screenshots. Captures: `%TEMP%\opencode\ui200\*-200-min.png` and `*-200-design.png`. The
200 % check in `DEVICE_TEST_MATRIX.md` (EN3) and UI-16 now records the pass; Windows 11 remains open.


The caption **maximize/restore** was also re-checked on the 150 % configuration with the app's own button:
2152x1380 at 644,0 was restored exactly after two maximize cycles. (A direct `ShowWindow(SW_RESTORE)`
bypasses the app's `SC_RESTORE` handler and grows the frame; that is not the product path.)
At 200 % the `Received (UTC)` header wrapped past its row, so the column is now `Received` with the UTC
wording in its tooltip.

A real window capture of the published exe was taken with a DPI-aware `PrintWindow`: **2866x1871 device**
(1433x936 logical at 200 %), centred on the 3840x2160 screen, with the whole layout visible
(`%TEMP%\opencode\real200\published-200-aware.png`). All seven pages were then driven through UI Automation
and captured from the same published exe at 200 % (`%TEMP%\opencode\real200\pages\*.png`); Wireless,
Settings and the rest show no clipping; the same seven pages were rendered at the 150 % minimum
(1740x1110 device, `%TEMP%\opencode\ui150-min2\*.png`) with no clipping. A first attempt from a DPI-unaware PowerShell process
measured 1433x936 *device* and looked clipped; that was the capture tool's virtualisation, not the app
(`GetDpiForWindow` reports 192).

### Audible cues

`UiSound` was inspected through its real private `Build` path: the three cues are genuine 16-bit mono
44.1 kHz PCM - success 0.200 s, peak 7100, gliding 784-1175 Hz; click 0.028 s, peak 4371, 1620 Hz; error
0.240 s, peak 8312, 392-196 Hz (`%TEMP%\opencode\audio-drill2\*.wav`, `synthesis.txt`).

A WASAPI loopback capture on the default endpoint (BEHRINGER UMC 202HD, active) recorded peak 0.986 / RMS
0.310 in every 250 ms window, before and after the cues: a continuous full-scale signal dominates that
endpoint and masks them. Audibility by a person therefore remains unconfirmed; the evidence is recorded as
PCM + endpoint, not as a listening test.

### Signing drill and PFX-import defect

The Windows SDK signing tools are present (`signtool.exe` 10.0.26100.0), contrary to the earlier note, so
the signing path was drilled with a temporary self-signed code-signing certificate:

- `signtool sign /fd sha256` signed a copy of the published exe; `signtool verify /pa` passed while the test
  certificate was trusted in `CurrentUser\Root`; `Get-AuthenticodeSignature` read `Valid`; the release exe
  and setup stayed `NotSigned`; the certificate, trust entry, PFX and signed copy were removed
- the documented `-PfxPath` path **failed** on PowerShell 5.1: the line break after `-bor` became an extra
  constructor argument and the script reported `op_BitwiseOr` on an `Object[]`. The flags are now built as a
  single `X509KeyStorageFlags` value and passed through `-ArgumentList`; a repeat drill imported the PFX,
  reached signtool, failed on a read-only copy with **exit 4**, and removed the imported certificate
- the RFC3161 timestamp step could not complete: `signtool` hung on the timestamp request against DigiCert
  (and other authorities were not reachable in time), so the script's full success path remains unverified
  and no artifact was signed

The release remains unsigned; `Sign-Artifacts.ps1` still requires a timestamp and a trusted certificate.

### Files changed

```
src/I-Total.Transfer/UI/Pages/WirelessPage.Designer.cs
src/I-Total.Transfer/UI/Pages/LibraryPage.cs
src/I-Total.Transfer/UI/Pages/TransferPage.cs
src/I-Total.Transfer/UI/Pages/HistoryPage.cs
src/I-Total.Transfer/UI/Controls/FluentSelection.cs
src/I-Total.Transfer/Services/ConnectionDiagnostics.cs
tests/I-Total.Transfer.Tests/PageControlTests.cs
tests/I-Total.Transfer.Tests/ConnectionDiagnosticsTests.cs
tests/I-Total.Transfer.Tests/InstallerScriptTests.cs
installer/Install-I-Total.Transfer.ps1
installer/Sign-Artifacts.ps1
.gitattributes, .editorconfig, .gitignore
CHANGELOG.md, PROJECT-STATUS.md, README.md
docs/PROGRESS.md, docs/UI-REQUIREMENTS-MATRIX.md, docs/KNOWN_ISSUES.md
docs/BUILD-VALIDATION.md, docs/RELEASE-ACCEPTANCE.md, docs/PERFORMANCE-AUDIT.md
docs/MASTER_IMPLEMENTATION_PLAN.md
```

### Packaged setup executable

`winget install --id JRSoftware.InnoSetup` installed Inno Setup 6.7.3. `Build-Installer.ps1
-ExpectedSha256 945E2D1F...` then produced `dist\installer\I-Total.Transfer-1.8.0-setup.exe` in addition to
the portable ZIP. The setup was exercised silently:

- `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=<temp>` exited 0; the installed exe hashed to
  `945E2D1F...`; `unins000.exe`, the HKCU uninstall entry (`I-Total Transfer` 1.8.0, Deveg) and two Start
  Menu entries existed; no firewall rule was created; `%APPDATA%` and the trial ledger were untouched
- the installed application launched and exited 0
- `unins000.exe /VERYSILENT` exited 0 and removed the folder, the HKCU entry and the Start Menu group while
  `%APPDATA%\I-Total Transfer` survived

The setup is **unsigned**: no trusted certificate is present (signtool 10.0.26100.0 is), so
`Sign-Artifacts.ps1` still fails closed.

### Packaging

- `installer\Build-Installer.ps1 -Version 1.8.0 -ExpectedSha256 945E2D1F...` produced the portable ZIP and the
  Inno Setup executable; the container hashes are in `dist\installer\SHA256SUMS.txt`. Nothing is
  Authenticode-signed.
- The deployment and source ZIPs were regenerated with the new executable and the updated documents.
  `dist\SHA256SUMS.txt` and `dist\installer\SHA256SUMS.txt` are the authoritative hash records.
- Verification: the deployment ZIP's embedded exe hashes to `945E2D1F...`; the extracted source ZIP
  rebuilds and publishes to the byte-identical `945E2D1F...` executable (112,154,438 B).
- The portable ZIP was extracted and its own installer run with no `-SourceDir`: install returned
  `{"ok":true,...}` with the executable, manifest and uninstaller present and the HKCU key created; the
  installed uninstaller then removed the folder and key and kept `settings.json`. The first attempt failed
  with exit 3, which is the defect fixed above.
- An upgrade over a running installation was tested with the same setup: install (exit 0), launch the app,
  run the setup again - it closed the app (Inno `CloseApplications`), exited 0, kept the HKCU entry and
  left the installed exe at `945E2D1F...`; the uninstaller then removed the folder and key and kept
  `%APPDATA%`.
- The final archives were rebuilt on 2026-10-25 from the `945E2D1F...` publish. The final setup was
  installed silently into a temporary folder (installed exe hash matched), launched (exit 0) and
  uninstalled (folder, HKCU entry and shortcuts removed; `%APPDATA%` kept); no firewall rule was created.

### Gate E - WPD COM ownership

9. **Enumeration leaked COM objects.** `EnumerateChildren` created an `IEnumPortableDeviceObjectIDs` per
   folder and never released it; `GetProps` created a key collection and a values object per object;
   `TryGetValue` leaked its key collection and each per-key reader leaked its values object; and
   `ListDevices` never released the device manager. On a 26,000-object camera roll that is tens of
   thousands of provider objects left to finalization. All five paths now release deterministically in
   `finally` blocks (`ReleaseCom`). This is static evidence plus a clean build; no phone was attached to
   measure the effect, and none is claimed.

### Gate F - Designer and accessibility

10. **Designer coverage verified statically.** All seven pages have a `.Designer.cs` with a real
    `InitializeComponent` (37, 20, 20, 22, 41, 24 and 39 designer fields respectively) and `MainForm.Designer.cs`
    declares 36. No page builds its visual tree at runtime. Visual Studio is not installed on this host, so
    the open/save/reopen round-trip is not claimed.
11. **Interactive controls now have accessible names.** `UI/Accessibility.cs` fills `AccessibleName` from the
    caption, then the designer name, for buttons, text boxes, lists, combos and progress bars; `MainForm`
    applies it after building the pages. Two tests pin it: one asserts every page has no unnamed interactive
    control, one asserts the fallback order. A Narrator/NVDA audit has not been run (KI-24).

### Gate H - candidate build and packaging

```text
dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
  -> 0F15740792C5555FD2B5472FA5246E5538BA48B8AB3DA90BDF5D6A7D890854D3 (112,191,302 B)
```

The source ZIP was extracted into a clean folder and published there: the resulting executable has the
same SHA-256, so the source-only rebuild is byte-identical. `Build-Installer.ps1` produced portable
`00E46FB8...` and setup `9DE4D08F...`; the setup was installed silently into a temporary folder (exe and
README hashes matched), then uninstalled: folder, HKCU entry and shortcuts removed, `%APPDATA%` kept. `PROJECT-STATUS.md`, `BUILD-VALIDATION.md`, `RELEASE-ACCEPTANCE.md`,
`PERFORMANCE-AUDIT.md` and `KNOWN_ISSUES.md` were updated for the candidate; no hardware or performance
claim was added. The published executable was launched and stayed up for five seconds with the window title
`I-Total Transfer - verified phone media transfer` (working set 161.6 MB), then stopped; this is a launch
smoke test only.
12. **A no-op run no longer claims it produced a verified copy.** When every planned file was already
    present at an accepted grade, the summary said `HASH-VERIFIED COMPLETE`, which reads as if this run
    wrote something. It now says `ALREADY PRESENT — NO FILES NEEDED COPYING (verified against the session
    record)`, `TransferSummary.AlreadyPresent` carries the count, and a test pins the second run of an
    unchanged session.
13. **Verification results are persisted to SQLite as well as JSON.** `VerifyAsync` now writes the
    post-verification states (including `Failed` for a corrupted destination) and a
    `VERIFICATION COMPLETE/INCOMPLETE` session row; a test corrupts a destination, verifies, and reads the
    `Failed` row back from the database. The free-space preflight also no longer refuses a UNC destination
    whose free space cannot be read; it logs the limitation and lets the per-file copy report a real
    disk-full error.
### Blockers and limits

- A *native* 100 % desktop capture is still impossible here: the display used for the 200 % pass was
  3840x2160 at 200 %, and the session later returned to 3440x1440 at 150 %. The 96-DPI and 200 % renders
  cover the layout question; neither is a DWM-composited screenshot.
- Both handsets were detached at the end of this session (Windows keeps only stale records), so the
  4 GB+ video, locked-phone and cloud-optimised drills could not run; they need the phones reattached.
- iPhone lock / revoked USB permission still has no physical observation; the HUAWEI keeps MTP active
  while locked, so no Android lock drill applies.
- Production licence issuer/purchase URL, Steamworks and an Authenticode certificate remain external
  blockers (`signtool` is present). Windows 11 and audible cues remain unobserved; the 200 % layout is now
  rendered.

### Next task

1. No internal UI, transfer or packaging task is outstanding; only the external items above remain.
2. Any further source edit invalidates the published hash: rebuild, retest, republish, repackage and
   refresh `dist\SHA256SUMS.txt`. Docs-only edits still require new archives because they ship inside them.

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
# publish\I-Total.Transfer.exe: 112,154,438 B
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
installer/Sign-Artifacts.ps1
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


### Gate E - WPD COM ownership

9. **Enumeration leaked COM objects.** `EnumerateChildren` created an `IEnumPortableDeviceObjectIDs` per
   folder and never released it; `GetProps` created a key collection and a values object per object;
   `TryGetValue` leaked its key collection and each per-key reader leaked its values object; and
   `ListDevices` never released the device manager. On a 26,000-object camera roll that is tens of
   thousands of provider objects left to finalization. All five paths now release deterministically in
   `finally` blocks (`ReleaseCom`). This is static evidence plus a clean build; no phone was attached to
   measure the effect, and none is claimed.

### Gate F - Designer and accessibility

10. **Designer coverage verified statically.** All seven pages have a `.Designer.cs` with a real
    `InitializeComponent` (37, 20, 20, 22, 41, 24 and 39 designer fields respectively) and `MainForm.Designer.cs`
    declares 36. No page builds its visual tree at runtime. Visual Studio is not installed on this host, so
    the open/save/reopen round-trip is not claimed.
11. **Interactive controls now have accessible names.** `UI/Accessibility.cs` fills `AccessibleName` from the
    caption, then the designer name, for buttons, text boxes, lists, combos and progress bars; `MainForm`
    applies it after building the pages. Two tests pin it: one asserts every page has no unnamed interactive
    control, one asserts the fallback order. A Narrator/NVDA audit has not been run (KI-24).

### Gate H - candidate build and packaging

```text
dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
  -> 0F15740792C5555FD2B5472FA5246E5538BA48B8AB3DA90BDF5D6A7D890854D3 (112,191,302 B)
```

The source ZIP was extracted into a clean folder and published there: the resulting executable has the
same SHA-256, so the source-only rebuild is byte-identical. `Build-Installer.ps1` produced portable
`00E46FB8...` and setup `9DE4D08F...`; the setup was installed silently into a temporary folder (exe and
README hashes matched), then uninstalled: folder, HKCU entry and shortcuts removed, `%APPDATA%` kept. `PROJECT-STATUS.md`, `BUILD-VALIDATION.md`, `RELEASE-ACCEPTANCE.md`,
`PERFORMANCE-AUDIT.md` and `KNOWN_ISSUES.md` were updated for the candidate; no hardware or performance
claim was added. The published executable was launched and stayed up for five seconds with the window title
`I-Total Transfer - verified phone media transfer` (working set 161.6 MB), then stopped; this is a launch
smoke test only.
12. **A no-op run no longer claims it produced a verified copy.** When every planned file was already
    present at an accepted grade, the summary said `HASH-VERIFIED COMPLETE`, which reads as if this run
    wrote something. It now says `ALREADY PRESENT — NO FILES NEEDED COPYING (verified against the session
    record)`, `TransferSummary.AlreadyPresent` carries the count, and a test pins the second run of an
    unchanged session.
13. **Verification results are persisted to SQLite as well as JSON.** `VerifyAsync` now writes the
    post-verification states (including `Failed` for a corrupted destination) and a
    `VERIFICATION COMPLETE/INCOMPLETE` session row; a test corrupts a destination, verifies, and reads the
    `Failed` row back from the database. The free-space preflight also no longer refuses a UNC destination
    whose free space cannot be read; it logs the limitation and lets the per-file copy report a real
    disk-full error.
### Blockers and limits

- **100 % pixel capture of UI-18 was open at this cut.** This host has only a 150 % display; the page actions and
  150 % pixels were observed, and the wiring is tested, but no 100 % session exists here.
- **Physical evidence:** the iPhone scan/filtered copy, Android cancel/resume, PTP-only warning, mid-scan unplug
  and selected-phone-disappearance drills are live. The HUAWEI lock does not disable MTP, so no abort drill is
  applicable; single-file and mixed-size device observations are recorded above; no statistical multi-file benchmark is claimed.
- Production licence issuer/purchase URL, Steamworks, Inno Setup 6, Authenticode certificate and `signtool`
  remain external blockers. `Build-Installer.ps1` still prints `SETUP SKIPPED`; nothing is signed.
- Windows 11 rendering, 200 % scaling and audible cues remain unobserved.

### Next task

1. See the 2026-10-24 entry: the 96-DPI harness render covers the layout question; a native 100 % capture still needs a 100 % display.
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
2. **Safe per-user installer** (`installer/Install-I-Total.Transfer.ps1
installer/Sign-Artifacts.ps1`)
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

