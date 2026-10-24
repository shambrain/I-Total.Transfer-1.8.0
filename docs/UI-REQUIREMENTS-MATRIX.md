# UI requirements matrix - I-Total Transfer 1.8.0

This is the requirement-to-evidence map for the operator interface. It separates what the design document
shows, what the automated tests prove, what a real capture shows, and what has never been observed.

Legend:

- **PASS (automated)**: ran green against this source tree in this workspace.
- **PASS (live capture)**: the real executable was launched and captured on this machine.
- **DESIGN ONLY**: present in the editable FigCraft document, not proof of runtime behaviour.
- **OPEN**: not observed or not implemented; the blocker is named.

## 1. Requirements

| ID | Requirement | State | Evidence |
| --- | --- | --- | --- |
| UI-01 | Exactly three selectable appearances: Neon (default), Light, Slate. `System` follows Windows; `Dark` is a legacy alias for Neon. Amber is semantic, not a theme. | PASS (automated) | `ThemeTests.DefaultThemeIsTheBannerTheme`, `LegacyDarkSettingStillLoadsAndMapsToTheBannerTheme`, `ForResolvesEachPalette`, `SystemResolvesOnlyToALightOrDarkPalette`; `AppearanceSettingsTests`. |
| UI-02 | A fresh profile opens in Neon; a saved choice is restored and never silently overwritten. | PASS (automated) | `AppearanceSettingsTests.AFreshProfileStillDefaultsToNeon`, `ASavedThemeIsRestoredAndThePreviewFollowsIt`; `ThemeTests`. The saved profile on this machine is `Light` and was restored after every capture. |
| UI-03 | The Appearance section shows the palettes, not just three words. Neighbouring swatches must not share a theme. | PASS (automated + live capture) | `AppearanceSettingsTests.AllThreeEstablishedThemesHaveAVisiblePreview`, `ChoosingASwatchSelectsTheThemeAndRaisesTheLivePreviewEvent`; `%TEMP%\opencode\ui-capture\settings-appearance-final.png`. |
| UI-04 | Swatches use the same theme path as the combo box; clicking one raises one preview event. | PASS (automated) | `ChoosingASwatchSelectsTheThemeAndRaisesTheLivePreviewEvent` asserts exactly one event and the selected badge. |
| UI-05 | Every theme carries the full semantic token set, including focus, selection, disabled and scrim. | PASS (automated) | `ThemeTests.EveryPaletteCarriesTheFullFluentTokenSet`; `Theme.cs`. |
| UI-06 | Text, status and focus keep measurable contrast. | PASS (automated) | `ThemeTests.EveryPaletteKeepsTextStatusAndFocusReadable` (7.0/4.5/3.0 thresholds); `CaptionButtonContrastTests`. |
| UI-07 | Keyboard users can see focus and activate the theme previews. | PASS (automated) | `ThemeSwatch` handles Space/Enter and paints a 2 px `Focus` ring; `RoundedButton` paints the same ring; `AppearanceSettingsTests` calls the same `Activate` path. |
| UI-08 | The Overview primary action row stays above the fold at 1440x900 and 150 % scaling. | PASS (live capture) | 2026-10-22: media/metric rows trimmed, `AutoScrollMinSize` 742 -> 690; real capture `overview-neon-final.png` at 2152x1380 physical. UI Automation measured Start transfer at `x=1951..2251, y=1212..1272`, above the status bar. |
| UI-09 | A short window scrolls instead of slicing the action card. | PASS (automated design rule + live capture) | `DashboardPage.AutoScrollMinSize = 690`; capture at 150 %. The rule is deliberate: scroll is the fallback, not the normal layout. |
| UI-10 | Settings can be scrolled to the Appearance section without a clipped row. | PASS (live capture) | `settings-appearance-final.png`; the page scroll range was driven with `WM_VSCROLL` to the bottom. |
| UI-11 | No fake device, no fake metric, no fake success, and neutral wording when nothing is attached. | PASS (automated + live capture) | With no handset attached, the final Overview reads `No device connected`, `NO DEVICE`, `0` files, `Connect your phone to check` and `No accessible phone was found through Windows Shell.` The Shell probe reports `Apple` in its `DeviceInfo`, so `DashboardPage.SetDevice` deliberately switches to the neutral family when the status is `NotFound`; `DeviceCopyTests.ANotFoundProbeUsesNeutralWording` fails if `iPhone` reappears in the no-device state. The earlier iPhone-ready capture is dated and labelled; it is not used to claim a transfer. |
| UI-12 | Disabled actions state why they are disabled. | PASS (live capture + code) | `DashboardPage.SetState` enables Scan only with a ready device and Start only with a ready device plus a scan; the device card and operation band carry the reason (`No device connected`, `Nothing is running. Scan the device, then start a transfer.`). |
| UI-13 | Window caption buttons are real commands and keep contrast. | PASS (automated + live capture) | `CaptionButtonContrastTests`; header captures show min/max/close at the right edge. |
| UI-14 | The header identifies the handset and link, not a truncated readiness sentence. | PASS (live capture + code) | `MainForm.ShowDevice` now composes `Name + OperatingSystem + ConnectionType`; final capture reads `Apple iPhone - 27.0.1 - USB (WPD)` when a device is present and the neutral prompt when none is. |
| UI-15 | The design artefacts are editable, versioned sources, not flat mockups. | PASS (file present) | `i-total-transfer-design-system.figcraft` (69,239 bytes) with four pages; four exported renders. See `UI-DESIGN-SYSTEM.md`. |
| UI-16 | Windows 11 rendering and 200 % scaling are observed. | 200 % PASS; Windows 11 OPEN | A real 192-DPI (200 %) session was rendered on 2026-10-24: all seven pages at the minimum logical window (2320x1480 device) and at the design size (2840x1800), using the real `MainForm`; text, chips, cards and the reflowed toolbars scale uniformly and nothing clips. The window is larger than the logical desktop, so the harness evidence is a **200 % DPI render**; a DPI-aware `PrintWindow` capture of the published exe (2866x1871 device) confirms the same rendering on the real desktop. Windows 11 remains unobserved (KI-11/EN2). |
| UI-17 | Audible cues are heard on real hardware. | OPEN | `UiSound` synthesises tones; the harness cannot hear them. E6 remains IMPLEMENTED, NOT FULLY VERIFIED. |
| UI-18 | Every control on every page is audited as action / disabled-with-reason / documented future work. | PASS at 150 % (live) + 96-DPI render + automated | All seven pages were walked: Overview and Appearance previously, and Backup & queue, Media library, Wireless import, History and Diagnostics on 2026-10-22 with a real `PrintWindow` capture and UIA dump (`%TEMP%\opencode\ui-audit2`). Toolbar actions were clicked; disabled controls were recorded with the reason. The live filtered Start confirmation read **`Copy 1 files (584.1 MB)`**, not the full 4,103-file scan (`%TEMP%\opencode\ui-android-confirm\02-confirm.png`). A real Stop produced the recovery bar `1 planned - 0 copied - 1 outstanding - 4,102 skipped by filter`; Resume opened the correct 584.1 MB continuation dialog (`%TEMP%\opencode\ui-android-cancel\01-cancelled-recovery.png`). `PageControlTests` (11 real-control tests) pins every page's toolbar events and the Clear-view boundary. A 96-DPI harness render of the real shell on 2026-10-24 covered all seven pages in all three themes and exposed one clipped Wireless caption, now fixed; this host has no 100 % display, so a native 100 % desktop capture is still not available. See UI-16/KI-21. |

| UI-19 | Every page stays usable at the shell's minimum window (1160x740) and in a 200 % session. | PASS at 96-DPI minimum + real 200 % render | The 2026-10-24 minimum-size pass found four real defects: the Wireless TLS caption, `New ticket` button and sender notice clipped (the card now reflows, the captions wrap and the card scrolls); the Library toolbar overlapped the Files chip and hid Apply (two rows below 1010 px); the Transfer log toolbar hid `Clear view` (it wraps); the History database path disappeared behind the sessions card (the toolbar grows). Each is pinned by a `PageControlTests` case. The same build rendered at a real 192 DPI (200 %) shows no clipping. Captures: `%TEMP%\opencode\ui100-min\*.png` and `%TEMP%\opencode\ui200\*.png`. |

## 2. Overview control audit (2026-10-22)

| Control | Behaviour | Disabled / edge state |
| --- | --- | --- |
| `Check device` | Raises `CheckConnectionRequested`; re-probes the backend. | Disabled while a job is busy. |
| `Scan device` | Raises `ScanRequested`; starts a real enumeration. | Disabled when no backend reports a ready device or while busy; the device card says `No device connected`. |
| `Browse...` | Raises `BrowseDestinationRequested`; the shell opens the folder picker. | Disabled while busy. |
| Destination field | Editable path, read by the shell at job start. | Disabled while busy. |
| `Confirm each scan with a second pass` | Toggles the existing second-pass scan rule. | Always available; no hidden consequence. |
| `Start transfer` | Raises `StartBackupRequested`; the shell starts the verified copy. | Disabled until a ready device and a completed scan exist, or while busy. |
| `Pause` / `Resume` | Raises `PauseRequested`; text and glyph follow the paused state. | Disabled when idle. |
| `Stop` | Raises `StopRequested`; the engine stops and records what finished. | Disabled when idle. |
| Recovery bar `Resume` / `Not now` | Raises `RecoveryResumeRequested` / `RecoveryDismissRequested`. | Hidden when no unfinished session exists. |
| Navigation items | Switch pages through the shell. | Settings/History/Diagnostics are real pages; none is a placeholder. |
| Caption buttons | Minimize, maximize/restore, close. | Always available. |

The Overview does not invent a status: the device card, media tiles, metrics and operation band are fed by
the backend and the engine. The final capture with no phone shows the honest empty state.

## 3. Appearance control audit

| Control | Behaviour | Disabled / edge state |
| --- | --- | --- |
| Theme combo | Selects Neon, Slate, Light or Follow Windows; raises the same preview event. | Always available in Settings. |
| Neon swatch | Selects Neon; check badge and border follow the selection. | Always available. |
| Slate swatch | Selects Slate; check badge and border follow the selection. | Always available. |
| Light swatch | Selects Light; check badge and border follow the selection. | Always available. |
| Description line | Names the selected theme and its intent. | Updates with the selection. |

The combo remains the keyboard/screen-reader path; the swatches are an additional visual path, not a second
settings store. `LoadSettings` writes the saved value into the combo, and `ApplyTo` reads the combo, so a
saved choice cannot be lost by clicking a swatch.

## 4. Backup & queue control audit (2026-10-22)

| Control | Behaviour | Disabled / edge state |
| --- | --- | --- |
| `Destination` | Calls the shell's `OpenDestination`; opens the real destination in Explorer. | Page button stays available; the shell reports a missing folder instead of pretending it opened. |
| `Log file` | Opens the real rolling log in Notepad. | The shell shows a message if no log exists yet. |
| `Export report` | Exports the enumeration report. | With no scan it opens the real `Scan the device first.` dialog; with a scan it writes the report. |
| `Clear view` | Clears the console text only; the accessible description says the log file on disk is kept. | Always available; it never deletes the log file. |
| Progress bar, speed, ETA, counts | Read-only live view from `TransferProgress`/`TransferSummary`; the final state comes from the run summary. | Shows `-` until measured; no synthetic throughput. |
| Persistent queue grid | Read-only rows from the engine, including state, verification and note. | Empty state says nothing is queued; no placeholder rows. |
| Activity console | Append-only real log lines with level colours. | `Clear view` resets the console, not the file. |

The current build (SHA-256 `C953BC08...`) was captured at 2152x1380 with `PrintWindow`
(`backup-queue-150.png`); `Clear view` raised the real action and was exercised. The earlier audit also
opened `Export report` with no scan and observed the real modal.

## 5. Media library control audit (2026-10-22)

| Control | Behaviour | Disabled / edge state |
| --- | --- | --- |
| Filter box | Free-text filter by name/folder; Enter applies, not just the button. | Always editable; empty text means no filter. |
| `All` / `Photos` / `Videos` / `Music` / `Files` chips | Select the group and immediately raise `ApplyFilterRequested`; the active chip is filled. | Always available. |
| `Apply` | Raises `ApplyFilterRequested` with the current text and group. | Always available. |
| `Repair missing` | Toggles the verification repair preference; read by `VerifyAsync`. | Always available. |
| `Verify destination` | Runs the real destination verification. | With no scan it opens the real `No scan` dialog: `Run a successful scan first.` |
| `Export scan` | Writes the enumeration report. | With no scan it opens the real `Scan the device first.` dialog. |
| Media grid | Read-only folder/name/size/modified/group rows from the scan. | Empty state is shown instead of stale rows; a cached listing carries a provenance line. |
| Summary / empty state | Counts, bytes and the honest next step. | The 2026-10-22 empty-state wording now says `Choose Scan device on the Overview page... If no phone is attached, connect and unlock it first.` and wraps instead of clipping. |

The live run opened both no-scan modals and dismissed them (`dialog-verify-with-no-scan.png`,
`dialog-export-scan-with-no-scan.png`). `LibraryChipsApplyFilterAndReportEveryAction` pins the chip,
filter, Apply, Repair, Verify, Export and empty-state wiring.

## 6. Wireless import control audit (2026-10-22)

| Control | Behaviour | Disabled / edge state |
| --- | --- | --- |
| `Enable wireless receiving` | Enables Start and persists `WirelessEnabled` through the shell. | Start remains disabled until checked; the switch is disabled while listening. |
| Bind-address combo | Lists loopback and private IPv4 addresses; the selected address is the only bind target. | Disabled while listening; public/wildcard addresses are never offered. |
| Port | Numeric port used by the listener. | Disabled while listening. |
| Pairing ticket lifetime | Minutes before the single-use ticket expires. | Disabled while listening. |
| `Require TLS` | Tries a matching `CurrentUser\My` certificate; otherwise the receiver falls back honestly. | Disabled while listening; the caption says `without a matching certificate`. |
| Auto approve | Applies the preference to the running receiver immediately and logs the warning. | Disabled while listening. |
| `Start listening` | Binds the selected private address and creates the first ticket. | Disabled until the enable switch is on or while already running. |
| `Stop` | Stops the listener, revokes sessions and clears the ticket. | Disabled when stopped. |
| `New ticket` | Creates a fresh single-use ticket (URL + QR). | Disabled when stopped. |
| Pairing URL box | Read-only URL the operator can select/copy. | Empty when no ticket. |
| QR image | Scannable code for the current ticket. | Cleared as soon as the ticket is redeemed. |
| Pending grid | Read-only offers waiting for a decision. | No file is written while an offer is pending. |
| `Approve` / `Refuse` | Decide the selected offer. | Disabled when nothing is selected/pending. |
| `Approve all` / `Refuse all` | Decide every pending offer. | Disabled when the pending list is empty. |
| Received grid | Read-only path, bytes and SHA-256 head of committed files. | Empty state is honest; no synthetic receipt. |
| Activity log | Real events, including the exact TLS fallback reason. | Append-only; no fake success line. |

The live published build bound `192.168.8.91:47821` from the non-elevated user, showed
`Listening on http://192.168.8.91:47821/ (HTTP; no certificate).`, and answered `/health` with
`{"status":"ok","tls":false}`. A ticket was created, QR-rendered, then cleared by Stop. `PageControlTests`
and `WirelessPageTests` pin the buttons and state transitions.

## 7. History control audit (2026-10-22)

| Control | Behaviour | Disabled / edge state |
| --- | --- | --- |
| `Refresh history` | Reads the current destination's session database and repopulates both grids. | Always available; a missing database shows zero rows, not an error. |
| `Open destination` | Opens the destination in Explorer. | Always available; the shell handles a missing folder. |
| Session grid | Read-only session id/device/started/status rows. | Zero rows are displayed as `Recent sessions (0)`. |
| Problem grid | Read-only failed/inaccessible/unverified rows with attempts and note. | Zero rows are displayed as `Problem files (0)`; no invented failures. |
| Database path | Real path of the destination database. | Shows the path even when the file does not exist yet. |

The live run refreshed an empty scratch destination without error and showed the real database path.
`HistoryShowsRowsAndRaisesRefreshAndOpenDestination` pins both actions and both grids.

## 8. Diagnostics control audit (2026-10-22)

| Control | Behaviour | Disabled / edge state |
| --- | --- | --- |
| `Run diagnostics` | Probes the device, dependencies and attachment evidence, then fills both grids. | Guarded while another operation is running; the shell shows the wait cursor. |
| `Export diagnostics ZIP` | Writes a redacted bundle to the destination while the log is open. | Real confirmation dialog with the path; failure is reported, never a fake success. |
| `Apple connection guide` | Opens the official Apple support page. | Always available. |
| `Official Apple Devices page` | Opens the Microsoft Store page for Apple Devices. | Always available. |
| Diagnostics grid | Read-only check/status/detail rows; detail wraps and rows grow. | `CACHED RECORD ONLY`, `NONE` and dynamic statuses are coloured honestly. |
| Dependencies grid | Read-only binary/version/state/detail rows. | All four missing binaries read `Missing`, and the title now says `no native runtime verified - native backends stay disabled`, never `required dependencies verified`. |
| Summary | Names every attached handset and the selected backend. | With no phone it describes the PC, not a phone. |

The live run produced 10 checks and a 3,586-byte ZIP from a scratch destination. The dependency title was
corrected after the audit found it claiming a verified required set when every binary was missing.

## 9. Evidence commands


```powershell
dotnet build I-Total.Transfer.sln -c Release --no-incremental
dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s
dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --filter "FullyQualifiedName~ThemeTests|FullyQualifiedName~CaptionButtonContrastTests|FullyQualifiedName~AppearanceSettingsTests|FullyQualifiedName~SettingsTests|FullyQualifiedName~Dashboard"
```

Results recorded on 2026-10-24 (whole suite after the caption fix):

- Release build: 0 warnings, 0 errors.
- Full suite: 453 passed, 0 failed, 2 skipped, 455 total, 27 s.
- Targeted UI/theme suite: 42 passed, 0 failed, 42 total.

## 10. Evidence files

Design source:

- `C:\Users\Administrator\I-Total-Transfer-FigCraft\designs\i-total-transfer-design-system.figcraft`
- `C:\Users\Administrator\I-Total-Transfer-FigCraft\renders\i-total-transfer-foundations.png`
- `C:\Users\Administrator\I-Total-Transfer-FigCraft\renders\i-total-transfer-overview-neon.png`
- `C:\Users\Administrator\I-Total-Transfer-FigCraft\renders\i-total-transfer-overview-light.png`
- `C:\Users\Administrator\I-Total-Transfer-FigCraft\renders\i-total-transfer-overview-slate.png`

Runtime captures (real executable, DPI-aware, 150 %):

- `C:\Users\Administrator\AppData\Local\Temp\opencode\ui-capture\overview-neon-final.png`
- `...\overview-light-final.png`
- `...\overview-slate-final.png`
- `...\settings-appearance-final.png`
- `...\light-dpi-aware.png` (earlier build with an iPhone attached; dated evidence of a live device state,
  not of the current polish pass)

UI-18 page captures (current published build, `PrintWindow`, 150 %):

- `%TEMP%\opencode\ui-audit2\backup-queue-150.png`
- `...\media-library-150.png`
- `...\wireless-idle-150.png`, `...\wireless-listening-150.png`, `...\wireless-new-ticket-150.png`, `...\wireless-stopped-150.png`
- `...\history-150.png`, `...\history-refreshed-150.png`
- `...\diagnostics-before-150.png`, `...\diagnostics-run-150.png`
- `...\dialog-verify-with-no-scan.png`, `...\dialog-export-scan-with-no-scan.png`
- Matching `.txt` UI Automation dumps are in the same folder.

The Overview captures with no handset attached remain dated evidence of the no-device wording fix; both
phones were attached again on 2026-10-22, so those captures are not used to claim a live transfer. No new
scan or transfer was run during this UI audit.

96-DPI harness render (2026-10-24, DPI-unaware, real `MainForm`, `DeviceDpi=96`, client root 1416x919).
All seven pages in Neon, Slate and Light; 21 PNGs in `%TEMP%\opencode\ui100`:

- `shell-dashboard-96dpi-{neon,slate,light}.png`, `shell-transfer-...`, `shell-library-...`,
  `shell-wireless-...`, `shell-history-...`, `shell-settings-...`, `shell-diagnostics-...`.

This is pixel evidence of the layout at 100 % logical size, not a native desktop capture. The first
attempt captured the whole form and painted the OS caption over the client (the shell collapses the
non-client area with `WM_NCCALCSIZE`), which produced a false "title clipped" reading; the shell root
is rendered instead and `header-only.png` shows the full 78 px brand band. The render exposed the
clipped Wireless TLS caption recorded in the UI-18 row.

2026-10-24 follow-up defects, all fixed and pinned by tests:

- The Wireless `Require TLS` caption ran past its card and lost the closing bracket; it is reworded, and
  `WirelessTlsCaptionFitsInsideItsCardAtTheDesignedWidth` fails on the old text.
- The installed uninstaller ignored a custom `-InstallDir` and targeted the default folder;
  `TheInstalledUninstallerFindsACustomInstallFolderOnItsOwn` fails without the self-location rule.
- The Diagnostics row `Native AFC runtime` read `VERIFIED` while no pinned binary existed (the required set
  is empty, so "satisfied" was vacuously true); it now says `NOT PRESENT`, pinned by
  `AMissingAfcRuntimeIsNotReportedAsVerified`.

A live attached-diagnostics run on the same day produced 10 Apple checks with both handsets attached and
8 Android rows with only the HUAWEI: rows in `%TEMP%\opencode\diag-live\diagnostics-attached.txt`, page in
`...\diagnostics-attached-page.png`.

## 11. Open gaps

1. 100 % layout is now rendered at 96 DPI by the harness (2026-10-24, 21 captures, all pages and themes); a *native* 100 % desktop capture remains impossible on this host because its only display reports 150 %. The action/wiring audit and 150 % captures are complete.
2. Windows 11 rendering (UI-16); the 200 % layout is now rendered and recorded.
3. Audible cues (UI-17).
4. A live iPhone-ready Overview and a mid-scan Overview in the current build; both phones are attached on 2026-10-22, but no new scan/transfer was run in this UI audit.
5. A real selected-phone-disappears-mid-scan capture and the Android cancel/resume drill; see
   `DEVICE_TEST_MATRIX.md`.
