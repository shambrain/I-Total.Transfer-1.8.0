# Master implementation plan

Authoritative requirement inventory for I-Total Transfer. It records what exists, what is proven, and what
is still open. Detailed evidence lives in `TEST-PLAN.md`, `BUILD-VALIDATION.md`, `RELEASE-ACCEPTANCE.md`,
`PERFORMANCE-AUDIT.md`, `ARCHITECTURE-REVIEW.md`, `WIRELESS.md` and `RECOVERY.md`; this file is the index
and the state of record.

Last reconciled: 2026-10-24, against v1.8.0 source and the 462-test Release suite (460 passed, 2 skipped).
The UI design/token map is in `UI-DESIGN-SYSTEM.md`; the requirement-to-evidence map is in
`UI-REQUIREMENTS-MATRIX.md`.

## How to read the states

| State | Meaning |
| --- | --- |
| **TESTED AND PASSED** | Implemented, with automated tests that ran green in this workspace (or a live hardware drill explicitly dated). |
| **IMPLEMENTED, NOT FULLY VERIFIED** | Real code and automated tests exist, but a physical device, a second Windows version, or an external service was unavailable. Never reported as hardware-proven. |
| **BLOCKED BY EXTERNAL REQUIREMENT** | Code or scripts are ready, but completion needs something outside this workspace (certificate, Steamworks account, installer toolchain, payment backend). |
| **NOT STARTED** | No meaningful implementation yet. |
| **FAILED** | Was tested and did not meet the requirement. Must name the failure. |

A mock or simulated test is never a substitute for a hardware test. Hardware-dependent rows name the exact
drill that closes them.

---

## A. Product, platform and compatibility

| # | Requirement | State | Evidence / remaining work |
| --- | --- | --- | --- |
| A1 | .NET 10 WinForms desktop app, Windows 10 1809+ and Windows 11 x64 | TESTED AND PASSED (Windows 10 build 19045) | Builds and runs on this machine; manifest minimum OS declared. Windows 11 rendering remains unobserved (see D-test matrix). |
| A2 | Banner-derived palette; Neon default, Slate, Light, System; legacy `Dark` alias | TESTED AND PASSED | `ThemeTests` pins the banner-sampled Neon values, the charcoal Slate stack with its restrained steel-blue accent, the Light palette, the full semantic token set and measured WCAG contrast. `AppearanceSettingsTests` drives the three real preview controls. The editable FigCraft source and renders are recorded in `UI-DESIGN-SYSTEM.md`; live captures are in `UI-REQUIREMENTS-MATRIX.md`. |
| A3 | Borderless Fluent chrome, resizable, DPI-aware at 100/150/200 % | TESTED AND PASSED (100 %, 150 %, 200 % render) | 150 % drills recorded; the real window is 2152x1380 physical at 150 %. A 96-DPI harness render on 2026-10-24 covers the 100 % layout (all seven pages, three themes), and a real 192-DPI session covers 200 % at the minimum (2320x1480) and design (2840x1800) sizes. The Overview action row was pulled above the fold on 2026-10-22. Caption maximize/restore was re-checked on 2026-10-25 at 150 %: 2152x1380 restored exactly across two cycles. |
| A4 | Glossy squircle application icon | TESTED AND PASSED (asset present) | `app.ico`/`app.png` shipped; visible in window and taskbar. |
| A5 | Single-file self-contained x64 publish | TESTED AND PASSED | Publish command and artifact hashes in `PROJECT-STATUS.md`. |

## B. Device access

| # | Requirement | State | Evidence / remaining work |
| --- | --- | --- | --- |
| B1 | Apple access through supported services, WPD and AFC where available | IMPLEMENTED, NOT FULLY VERIFIED | `Devices/Afc/AfcDevice.cs`, WPD and Shell fallback exist. AFC is fail-closed because `native/dependencies.json` ships empty hashes. Live iPhone scan/copy has run through WPD, including a filtered HEIC/MOV fetch; AFC remains pending. |
| B2 | Android access through USB MTP/WPD | IMPLEMENTED, NOT FULLY VERIFIED (walk/copy/cancel-resume/PTP live; lock not applicable) | One native WPD backend plus per-family storage policy. HUAWEI Y7 Prime 2018 live: **4,103 files / 15,612,056,299 bytes (14.54 GiB) `Complete`**; filtered copy 412 verified + 108 unverifiable + 0 failed; a 612,420,912-byte MP4 was cancelled mid-copy and resumed to source/destination SHA-256 equality. PTP-only mode was checked on 2026-10-24 and shows the camera-only warning. A screen lock does not disable MTP, so the abort is not applicable to this stack. |
| B3 | Device discovery and honest capability reporting | TESTED AND PASSED | `ITransferBackend`, `BackendCapabilities`; unattached/unidentified wording is neutral and family-aware. |
| B4 | Two phones attached: family preference, not list order | TESTED AND PASSED (live 2026-10-21; disappearance drill pending) | With both attached, `AndroidFirst` selected the HUAWEI and `AppleFirst` selected the Apple iPhone. The same session found a cross-device scan defect; `DeviceSelectionGuard` + `ScanResult.BelongsTo` + UI/cache guards now refuse a switch, with 14 tests. A physical "selected phone disappears mid-scan" run remains pending (BT08). |
| B5 | Never assume both families expose the same media | TESTED AND PASSED | `PortableStorage` rules; PTP warning when only the camera volume is exposed. |
| B6 | Native dependency safety (fail closed) | TESTED AND PASSED | `DependencyVerifier`, shipped hashes empty for AFC. |

## C. Shared transfer engine

| # | Requirement | State | Evidence / remaining work |
| --- | --- | --- | --- |
| C1 | Enumerate media with metadata and media groups | TESTED AND PASSED | Engine tests plus live iPhone cold pass: **55,185 files / 162,499,726,120 bytes (151.34 GiB) `Complete` in 23.3 min** on 2026-10-22 (the 26,335-file / 10.3 min and 57,371 / 188,810,836,019 warm passes are historical), and Android **4,103 files / 15,612,056,299 bytes `Complete`** on 2026-10-22, including a cancelled-and-resumed 612,420,912-byte MP4 verified source-to-destination. |
| C2 | Responsive scan/enumeration/transfer/hash/UI at 55,000+ entries | TESTED AND PASSED (synthetic); live 55,185-file cold iPhone pass | Stress suite, throttled progress, pacing; the current cold iPhone scan completed in 23.3 min without a UI freeze. A single-file Android buffer sweep is live (BT07/BT09): 33.56-37.83 MB/s across 64 KiB-4 MiB, plus a 30-file mixed-size batch at 6.41 files/s and 23.74 MB/s. These are single observations, not a statistical benchmark. |
| C3 | Never report a complete scan when enumeration partially failed | TESTED AND PASSED | `TransferRules.ClassifyEnumeration`; `EnumerationStatus.Complete` is the only value written to the scan cache. |
| C4 | Persistent, transactional queue/manifest and schema migration | TESTED AND PASSED | `ManifestStore`, `SessionDatabase`, recovery tests. |
| C5 | Staged writes, collision handling, no overwrite of existing files | TESTED AND PASSED | `TransferRules.ResolveTargetPath`, staging + atomic move. |
| C6 | Size checks and SHA-256 verification where supported, honest weakest level | TESTED AND PASSED | `VerificationLevel`; per-entry staged/destination hashes. |
| C7 | Accurate copied / already-present / skipped / inaccessible / failed / unverifiable reporting | TESTED AND PASSED | Engine tests and completion report. |
| C8 | Retry, pause, safe resume; interrupted verification discarded | TESTED AND PASSED | `PauseController`, `SessionRecovery.BuildResumePlan`, resume-path fix. |
| C9 | Disconnect during enumeration or transfer (both families): abort safely, preserve completed files, never mark partial as copied | TESTED AND PASSED (simulated + live 2026-10-24) | `ConsecutiveObjectFailureLimit`, `DeviceStoppedAnsweringException`, unreadable-object accounting. The live mid-scan pull returned `Incomplete` and was rejected; no report or cache was written. |
| C10 | One phone lost while the other stays connected | TESTED AND PASSED (live 2026-10-24) | The iPhone was unplugged while the HUAWEI stayed attached; the app refused the switch and rejected the scan. |
| C11 | Locked phone / revoked USB permission | TESTED AND PASSED (simulated); lock not applicable on tested MTP | `RecoveryFlowTests` covers backend faults. The HUAWEI keeps MTP active while locked, so no lock-abort is expected on this stack. |
| C12 | Restart app or Windows with jobs incomplete | TESTED AND PASSED | Interrupted-session recovery; dismissal persisted by destination+mtime+counts. |
| C13 | Continue from session record without re-reading already verified files | TESTED AND PASSED | Resume rebuilds the plan from outstanding entries only. |
| C14 | Never delete or modify original phone media | TESTED AND PASSED (design + tests) | Read-only source streams; device write path is the only mutating one and is explicit. |

## D. Simulation and test infrastructure

| # | Requirement | State | Evidence |
| --- | --- | --- | --- |
| D1 | Deterministic simulated device backend | TESTED AND PASSED | Fake backends in the test project; used by engine, retry, disconnect and stress suites. |
| D2 | Inject disconnects at enumeration/read/write/commit, timeouts, permission errors, no space, collisions | TESTED AND PASSED | Fault-injection tests. |
| D3 | Large/unusual filename fixtures | TESTED AND PASSED | Stress and filename tests. |
| D4 | Both device adapters against one engine contract | TESTED AND PASSED (contract level) | Interface conformance; physical adapters remain separately unverified (B1/B2). |
| D5 | No synthetic result presented as a physical benchmark | TESTED AND PASSED (policy) | `PERFORMANCE-AUDIT.md` labels synthetic and live numbers separately. |

## E. UI and operator honesty

| # | Requirement | State | Evidence |
| --- | --- | --- | --- |
| E1 | Clear operation indicator: indeterminate while totals unknown, determinate after measurement | TESTED AND PASSED | `OperationStatus`, Overview band; live captures. |
| E2 | Scan progress and transfer progress separate; never one misleading percentage | TESTED AND PASSED | Separate phases and labels. |
| E3 | Comfy, uncluttered layout that resists clipping at 150 % | TESTED AND PASSED | Anti-clipping work; Settings scroll drill. On 2026-10-22 the Overview media/metric rows were trimmed and `AutoScrollMinSize` reduced to 690 so the primary action row is visible at 1440x900 and 150 %; the real capture and UI Automation rectangles are recorded in `UI-REQUIREMENTS-MATRIX.md`. |
| E4 | Neutral wording when no phone is attached; no Apple-only claims on Android | TESTED AND PASSED | `Device family: NOT IDENTIFIED`, `CHECK PHONE`, family-aware media hints. |
| E5 | Diagnostics page and exportable, redacted log bundle | TESTED AND PASSED | `ConnectionDiagnostics`, `DiagnosticsBundle`, redaction policy. The bundle now also carries a licence/trial summary - standing, channel and the counter only, never the purchase id, ledger entries or fingerprints - which was exported from the published build and inspected on 2026-10-20. |
| E6 | Audible UI cues, optional | IMPLEMENTED, NOT FULLY VERIFIED (audibility) | `UiSound` synthesises real 16-bit mono PCM (success 0.200 s / 784-1175 Hz, click 0.028 s / 1620 Hz, error 0.240 s / 392-196 Hz); the default endpoint is active, but a loopback was masked by a continuous full-scale signal, so a listening test remains. |
| E7 | Theme choice is a real control path: previews select the same theme as the combo, persist, and never fake a palette | TESTED AND PASSED | `ThemeSwatch`, `SettingsPage` Appearance section, `AppearanceSettingsTests` (4 real-control tests), `ThemeTests`. Neon is the fresh default; Light and Slate are distinct; `System` resolves only to Light or Neon. |
| E8 | Every visible control performs a real action, is disabled with a stated reason, or is documented as future work | TESTED AND PASSED at 150 % + 96-DPI render + automated | All seven pages are audited row by row in `UI-REQUIREMENTS-MATRIX.md` (Overview, Appearance, Backup & queue, Media library, Wireless import, History, Diagnostics). The audit fixed five real defects and added `PageControlTests` (5). A 96-DPI harness render of the real shell (2026-10-24, seven pages x three themes) now covers the 100 % layout and exposed one clipped Wireless caption, fixed and pinned by test. A native 100 % desktop capture remains unavailable on this 150 % host; see UI-18/KI-21. |

## F. Wireless receiver

| # | Requirement | State | Evidence / remaining work |
| --- | --- | --- | --- |
| F1 | Short-lived, single-use pairing secret in a QR code | TESTED AND PASSED | `PairingState.Redeem`, hand-written `QrCode`, live drill. |
| F2 | Token rotation, explicit expiry, bounded sessions | TESTED AND PASSED | 32-hex tokens, rotate endpoint, 1 h lifetime, max 8. |
| F3 | Bind only to a private interface, without elevation or a URL ACL | TESTED AND PASSED | `PrivateNetwork` validation; `TcpListener` non-elevated LAN bind (`APrivateLanAddressBindsWithoutAUrlAcl`); live `/health` at `192.168.8.91`. |
| F4 | TLS with pinning, or an equivalent authenticated session | TESTED AND PASSED (with honest fallback) | `SslStream` + matching `CurrentUser\My` certificate when present; otherwise token-authenticated private HTTP with `UsingTls=false` in the status, log and `/health`. `WIRELESS.md`; live fallback recorded. |
| F5 | Show receiving device and files; require user approval; never an unauthenticated upload endpoint | TESTED AND PASSED | `ApprovalInbox`, 5 min approval timeout, pending offer UI. |
| F6 | Never open firewall ports automatically | TESTED AND PASSED | No firewall API or rule is created anywhere. |
| F7 | Staged, hash-verified, conflict-safe receive | TESTED AND PASSED | `.i-total\wireless\<guid>.partial`, `IncrementalHash`, `ConflictPolicy.RenameIncoming`, 422/507/409 rules. |
| F8 | Never claim seamless iOS background access | TESTED AND PASSED (wording) | Docs and UI say the sender must stay in the browser. |
| F9 | Wireless reception is metered by the same trial | TESTED AND PASSED (automated) | `LicenseService.TryStartWirelessUpload` / `RecordWirelessReceipt`; one sender session = one job. Live sender is pending (no iOS sender available). |

## G. Commercial model

| # | Requirement | State | Evidence / remaining work |
| --- | --- | --- | --- |
| G1 | Full-featured free trial | TESTED AND PASSED | Every feature works during the trial; nothing is disabled. |
| G2 | Configurable limit of successful, meaningful jobs (default 3) | TESTED AND PASSED | `TrialJobLimit` build property + `LicenseOptions.TrialJobs`; `LicensingTests`. |
| G3 | Failed, cancelled, empty, unsuccessful jobs cost nothing | TESTED AND PASSED | `JobFacts.IsMeaningful`; five negative cases tested. |
| G4 | Persistent trial state across restarts and updates | TESTED AND PASSED | File + HKCU stores merged pessimistically; restart and single-store-loss tests. |
| G5 | Repeated reporting of the same job cannot consume twice | TESTED AND PASSED | Engine `JobId` + truncated SHA-256 fingerprints; duplicate-report test. |
| G6 | Clear trial counter and purchase interface | TESTED AND PASSED (UI) | Settings Licence section: status, detail, Enter licence, Buy (disabled when unconfigured), Steam check on Steam builds. |
| G7 | One-time paid licence | TESTED AND PASSED (verification path) | Perpetual entitlement; optional expiry field for future use. |
| G8 | Shared entitlement interface with separate Steam and direct providers | TESTED AND PASSED (direct + signed Steam); Steamworks call BLOCKED BY EXTERNAL REQUIREMENT | `LicenseChannel`, `SteamEntitlementProvider`, app-id binding; live Steamworks ownership needs the SDK and partner account. |
| G9 | Signed direct-sale entitlements from a secure backend | TESTED AND PASSED (verifier); BLOCKED BY EXTERNAL REQUIREMENT (issuer) | RSA-PSS/SHA-256 envelope verified against an embedded public key; no private key ships. Production key pair and store backend do not exist yet. |
| G10 | Refunds and activation recovery | TESTED AND PASSED (offline revocation) | Signed revocation list revokes by id; unsigned lists ignored; refunded entitlements refused. Server-driven push is not implemented (no server). |
| G11 | Offline operation and licence-server failure tolerance | TESTED AND PASSED | Verification is entirely local; no network call exists. |
| G12 | No secrets or private keys in the desktop executable | TESTED AND PASSED (design + audit) | Only a public key can be embedded; PFX/password are environment inputs to the signing script. |
| G13 | Licence errors never corrupt or abandon an in-progress transfer | TESTED AND PASSED | Gate is only at new-job start; resume always allowed; store failures never throw; licence verified after transfer completion only to record a use. |
| G14 | Trial reset resistance that is honest about not being DRM | TESTED AND PASSED (documented limits) | Two-store merge; no secret, so a determined user with admin rights can still tamper. Documented in `KNOWN_ISSUES.md`. |
| G15 | Purchase page | BLOCKED BY EXTERNAL REQUIREMENT | `PurchaseUrl` build metadata is empty; UI disables Buy and explains. Set `-p:PurchaseUrl=...` when a store page exists. |

## H. Packaging and release

| # | Requirement | State | Evidence / remaining work |
| --- | --- | --- | --- |
| H1 | Safe per-user installer, upgrade, uninstall | TESTED AND PASSED | `installer/Install-I-Total.Transfer.ps1`: no admin, no firewall, hash-checked, manifest-scoped uninstall, keeps user data. Three automated end-to-end tests. |
| H2 | Packaged setup executable | TESTED AND PASSED (unsigned) | Inno Setup 6.7.3 (installed 2026-10-24) produced `I-Total.Transfer-1.8.0-setup.exe`. A silent install verified the installed exe hash, HKCU uninstall entry, Start Menu group, no firewall rule and untouched `%APPDATA%`; the app launched and the silent uninstaller removed the folder, entry and shortcuts. Signing is H4. |
| H3 | Portable ZIP | TESTED AND PASSED | `installer/Build-Installer.ps1` builds it with `INSTALL.txt` and `SHA256SUMS.txt`. |
| H4 | Code signing and release integrity | BLOCKED BY EXTERNAL REQUIREMENT | `signtool.exe` 10.0.26100.0 and `installer/Sign-Artifacts.ps1` are present; the script signs and verifies with a store thumbprint or a PFX (imported temporarily, password never on the command line) and fails closed. A temporary self-signed certificate drill proved SHA-256 signing and `verify /pa`; the release is unsigned because no trusted certificate exists. |
| H5 | Release artifact hashes and reproducible source archive | TESTED AND PASSED | Recorded in `PROJECT-STATUS.md`; re-verified after every publish. A clean `git clone` now rebuilds the byte-identical executable: `.gitattributes` fixes text at LF (`*.sln`/`*.bat`/`*.cmd` stay CRLF), and the 2026-10-24 clone passed 449 tests and published `411F4CC5...`. |
| H6 | Change log and version | TESTED AND PASSED | `CHANGELOG.md`, version 1.8.0. |

## I. Documentation and support

| # | Requirement | State |
| --- | --- | --- |
| I1 | README with verified/unverified boundary | TESTED AND PASSED |
| I2 | Test plan and release-acceptance matrix | TESTED AND PASSED |
| I3 | Recovery, wireless and architecture notes | TESTED AND PASSED |
| I4 | Installer and signing instructions | TESTED AND PASSED (docs written; execution blocked by H2/H4) |
| I5 | Device test matrix and known issues | TESTED AND PASSED (`DEVICE_TEST_MATRIX.md`, `KNOWN_ISSUES.md`) |
| I6 | Continuation record for the next session | TESTED AND PASSED (`PROGRESS.md`) |

---

## External blockers (nothing in this list can be solved by more code alone)

1. **Production licence key pair and issuing backend.** Generates signed entitlements and revocation lists.
   Until then `Licensing` verifies test keys only; the shipped build has no public key, so
   `Enter licence…` returns `NotConfigured` and the trial continues. This is fail-closed by design.
2. **Steamworks SDK and partner account.** Needed for a live ownership check and to confirm Steam accepts
   this non-game utility. The signed Steam entitlement path works without it.
3. **Code-signing certificate.** `signtool.exe` and the signing script are present; a trusted certificate is required to produce a trusted signature.
4. **Inno Setup 6.** Obtained on 2026-10-24 (6.7.3); the packaged setup now builds and was tested. A machine
   without it still skips the setup cleanly.
5. **Physical phones.** Both were attached together on 2026-10-21/24; the Android walk/copy, cancel/resume,
   PTP warning, single-file buffer sweep, cold iPhone scan, mid-scan unplug and selected-phone-disappearance
   drills ran. A lock does not disable MTP on the tested stack. A 30-file mixed-size batch is recorded; no statistical benchmark is claimed.
   `DEVICE_TEST_MATRIX.md` lists every drill.

## Suggested order for the next sessions

1. No further Android transfer drill is required; retain the single-file and mixed-batch observations as non-statistical evidence.
2. Decide the distribution channel and obtain the certificate; sign the exe and setup.
3. Obtain a code-signing certificate and sign the exe and setup; Inno Setup 6.7.3 is present and the setup builds.
4. Generate the production key pair on an offline machine, implement the store backend, set
   `Licensing\public-key.pem` and `-p:PurchaseUrl=...`, then re-run the activation and revocation drills
   with a real issued entitlement.
5. Steamworks eligibility decision, then either integrate the SDK or keep the signed-entitlement channel.
