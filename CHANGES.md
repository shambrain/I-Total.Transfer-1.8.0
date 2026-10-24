# Change Report & Checklist — I-Total Transfer by Deveg · v1.6.0 (2026-10-17)

This report describes the audit and the engineering changes applied **directly to the existing
codebase**. It separates what was verified in this environment from what remains hardware-dependent.

Legend: ✅ verified here · ⚙ implemented, not hardware-tested · ⛔ intentionally disabled (fail-closed)

---

## 0. Environment & baseline

- .NET SDK **8.0.423**, `net8.0-windows`. `global.json` pins the SDK feature band for deterministic builds.
- Baseline build was broken (test project had a missing MSTest `using`); documented test counts were stale.
  After fixing: baseline **42 tests**. Final: **81 tests**, Release build **0 warnings / 0 errors**.

---

## 1. Data integrity & recoverability (priority #1)

| # | Change | Where |
|---|--------|-------|
| 1 | Graded verification model `VerificationLevel { None, SizeOnly, DestinationHash, SourceAndDestinationHash }`; the strongest *achieved* level is recorded per file. | `Core/Models.cs`, `Core/TransferRules.cs` |
| 2 | Source SHA-256 is only claimed when a backend can read raw source bytes; Shell `CopyHere` yields `null` and the level stops at `DestinationHash`. | `Core/TransferRules.ClassifyVerification` |
| 3 | Copy pipeline: stream → staging (hash while streaming) → atomic commit → destination hash → compare; mismatch fails the item. | `Services/TransferService.cs` |
| 4 | Resume classification that never overwrites user data; same-size-without-digest is *unverifiable*, not verified. | `TransferRules.ClassifyExistingDestination` |
| 5 | Crash-safe, throttled, atomic manifest persistence (coalesced writes; force-flush at consequential transitions). | `Services/ManifestStore.cs` |
| 6 | Manifest schema migration + recovery: corrupt/foreign manifests are preserved as `session.json.bak`, duplicates rejected. | `ManifestStore.LoadOrCreate` |
| 7 | SQLite session database with a persistent WAL connection and batched transactions; history + problem-file queries. | `Services/SessionDatabase.cs` |
| 8 | Verification re-hashes the **live source** stream for a real source-to-destination comparison when the backend supports it. | `TransferService.VerifyAsync` |
| 9 | Repair path re-copies missing files from the device and only marks them verified when the hashes agree. | `TransferService.VerifyAsync(repair: true)` |
| 10 | Fixed a real bug: the wireless receiver moved the staging file while its write stream was still open (Windows lock) — commit now happens after the stream closes. | `Services/WirelessReceiver.cs` |

## 2. Complete enumeration (priority #2)

| # | Change | Where |
|---|--------|-------|
| 11 | `EnumerationReport` with `Complete/Incomplete/Empty/Failed`, per-folder outcomes, zero-byte/unknown-size counts, duplicate count, Live-Photo pairs. | `Core/Models.cs`, `Core/TransferRules.ClassifyEnumeration` |
| 12 | A folder that cannot be read makes the scan `Incomplete` with the failing folders listed — never silently omitted. | `Devices/*`, `TransferService` |
| 13 | Filters (extension allow-list, folder/name exclusions, sidecars, min/max size); excluded files are recorded as skipped. | `Core/TransferRules`, `Services/TransferService` |
| 14 | Conflict policies: preserve-and-report (default), skip, rename-incoming, overwrite. | `TransferRules.ResolveTargetPath` |
| 15 | Path hardening retained/extended: reserved names, trailing dots/spaces, control chars, traversal, absolute paths. | `TransferRules.ValidateRelativePath` |

## 3. Transfer performance (priority #3)

| # | Change | Where |
|---|--------|-------|
| 16 | Streaming copy (no whole-file buffering), configurable buffer 64–1024 KB. | `TransferService`, `AppSettings` |
| 17 | Per-file timeouts, bounded retries, cooperative pause/resume gate (non-blocking). | `Core/PauseController`, `TransferService` |
| 18 | Manifest writes coalesced (time/change-count) so a 26k-file library no longer rewrites JSON per file. | `ManifestStore` |
| 19 | Batched SQLite upserts instead of per-file connection open/close. | `SessionDatabase` |

## 4. UI (priority #4) — Designer-authored

| # | Change | Where |
|---|--------|-------|
| 20 | Replaced the runtime-built form with a **Designer-owned shell** (header, nav rail, content host, status bar). | `UI/MainForm.Designer.cs` |
| 21 | Added seven Designer-authored pages, each a `UserControl` with its own `*.Designer.cs`. | `UI/Pages/*` |
| 22 | Central **four-theme** system — `Neon` (default), `Slate`, `Light`, `Follow Windows` — with live OS light/dark detection; every advertised control is wired to a service. | `UI/Theme.cs`, `UI/MainForm.cs`, `Core/AppSettings.cs` |
| 23 | Metric tile control with its own Designer file. | `UI/Controls/MetricCard.*` |
| 24 | Rounded, hairline-bordered surfaces with DPI-scaled radii: `CardPanel`, `PillLabel`, `RoundedButton`, plus a shared `Surface` paint helper. Accent ramps/gradients for primary actions, the active nav item and the header rule. | `UI/Controls/*` |
| 25 | Brand icon: a generated seven-size `app.ico` used as the executable icon, the window icon and the header badge (embedded so it survives single-file publish). | `src/I-Total.Transfer/app.ico`, `app.png`, `UI/AppIcons.cs`, `I-Total.Transfer.csproj` |

- **Theme palettes** were sampled from the supplied brand artwork rather than guessed: icon/accent blue
  `#2F6FE0`; the ramped theme uses `#060B18` surfaces over a `#4FEBFC → #24AAF8 → #2F6FE0 → #642CE4 →
  #D944DA` accent ramp. A legacy `"Theme": "Dark"` value in an existing `settings.json` still loads and
  maps to `Neon`, so no user preference is lost.
- **Selected-page tracking fix:** the active navigation item was previously derived from `Control.Visible`,
  which reports `false` for every page until the form itself is shown — so nothing was highlighted at
  startup. The shell now tracks the selected page explicitly (`UI/MainForm.cs`).

- **High-DPI/scaling fix (found while verifying the GUI):** the shell and pages used `AutoScaleMode.Font`
  against a `(7,15)` font baseline, which on a 150% display produced a **non-uniform** stretch
  (~x1.43 horizontal vs ~x1.67 vertical) that distorted the whole interface. All nine Designer files now
  use `AutoScaleMode.Dpi` with a `(96,96)` baseline for a **uniform** scale factor, and `MainForm`
  clamps the window to the working area on show and on `DpiChanged`. Verified visually on a 3440x1380
  @150% display (Dashboard, Settings and each theme captured via a DPI-aware `PrintWindow`). Files: `UI/**/*.Designer.cs`, `UI/MainForm.cs`.

## 5. Backends

| # | Change | Where |
|---|--------|-------|
| 24 | `ITransferBackend` seam extended with `BackendCapabilities`, `OpenSourceRead`, `CopyToDevice`, `TryDeleteDeviceFile`; backward-compatible members preserved. | `Devices/ITransferBackend.cs` |
| 25 | ⚙ Native **WPD** read-only backend (`IPortableDeviceManager/Content/Resources`), all COM on a dedicated MTA thread; GUIDs/property keys taken from the Windows SDK headers. | `Devices/Wpd/*` |
| 26 | ⛔ **AFC** read/write backend that is inert unless its pinned runtime verifies. | `Devices/Afc/*` |
| 27 | `ShellIPhoneDevice` refactored (COM-leak-proof, fail-soft folders, full report) as the compatibility fallback. | `Devices/ShellIPhoneDevice.cs` |
| 28 | `BackendFactory` preference + explicit, logged fallback chain; `Describe` for the Settings page. | `Services/BackendFactory.cs` |

## 6. Native dependency safety (fail-closed)

| # | Change | Where |
|---|--------|-------|
| 29 | SHA-256 pin manifest; unpinned/mismatched binaries are **refused**. | `native/dependencies.json`, `Services/DependencyVerifier.cs` |
| 30 | ⛔ Empty hashes ship by design → no native DLLs bundled; AFC stays disabled until a reviewed runtime is pinned. | `native/dependencies.json` |
| 31 | Dependency status surfaced on the Diagnostics page and in the diagnostics bundle. | `UI/Pages/DiagnosticsPage.*`, `Services/DiagnosticsBundle.cs` |

## 7. Wireless, settings, diagnostics

| # | Change | Where |
|---|--------|-------|
| 32 | Token-authenticated LAN receiver: short-lived single-session token, private-interface bind, strict path validation, size cap, SHA-256 on receipt, TLS when bound. | `Services/WirelessReceiver.cs` |
| 33 | Validated/clamped settings with quarantine of a corrupt file and atomic save. | `Core/AppSettings.cs`, `Services/SettingsService.cs` |
| 34 | Redacted diagnostics ZIP (environment, settings, backends, dependencies, log tail). | `Services/DiagnosticsBundle.cs` |
| 35 | Rolling, redacting, level-aware logging. | `Services/LogService.cs` |
| 36 | Connection diagnostics now include the selected backend and native-runtime status. | `Services/ConnectionDiagnostics.cs` |

## 8. Tests (81, all passing)

| Area | Coverage |
| --- | --- |
| Existing engine (5) | source-hash integrity, Shell destination-hash-only, resume, collision, empty-scan refusal |
| Rules (existing) | completion, sizes, path validation |
| Reconciliation (existing) | manifest rebuild |
| **Settings (new)** | clamping, save/load round-trip, corrupt-file quarantine, reset, buffer bounds |
| **Dependency verifier (new)** | unpinned refused, hash mismatch, verified, missing, empty manifest, shipped manifest fail-closed |
| **Manifest store (new)** | fresh create, merge+reload, corrupt recovery (.bak), foreign-destination rejection |
| **Filters & conflicts (new)** | extension normalisation, include/exclude, sidecars, size, rename, preserve, verification classification, enumeration classification, Live-Photo pairs, reserved names |
| **Wireless (new)** | real loopback: unauthorized reject, authorized upload + SHA-256 recorded, traversal reject, health endpoint |
| **Theme (new)** | default is Neon; legacy `Dark` setting loads and maps to Neon; sampled Neon/Slate palette values; Light/Slate/System resolution; blend helper |

## 9. Packaging

- Version bumped to **1.6.0**; `global.json` pins the SDK feature band.
- Self-contained `win-x64` single-file publish; SHA-256 of the artifact is published alongside the ZIP.

---

## Verified vs. hardware-dependent

- ✅ Verified here: Release build (0/0), 81/81 tests, app startup smoke test, per-theme render capture, publish artifact + hash.
- ⚙ Implemented, not hardware-tested: WPD/AFC enumeration and byte transfer, real throughput, disconnect
  recovery, wireless with a real iOS sender, Shell `CopyHere` on live devices.
- ⛔ Intentionally disabled: AFC until a pinned runtime verifies; TLS-over-HTTPS until a certificate is
  bound (falls back to token-authenticated private-network HTTP and says so).

No physical-device results are claimed.
