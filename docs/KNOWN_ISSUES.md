# Known issues and technical debt

Open items only. Each entry states the impact, the mitigation that is actually in place, and what would be
needed to close it. Resolved defects move to `CHANGELOG.md` with the build that fixed them.

## Commercial / distribution

### KI-01 - The shipped build has no licence public key

- **Impact:** `Enter licence…` returns `NotConfigured`. Purchased licences cannot be activated in this
  build; the free trial works normally.
- **Why:** the production RSA key pair and the issuing backend do not exist yet. Embedding a key generated
  in this workspace would publish a private key or create a licence that anyone could forge.
- **Mitigation:** fail-closed by design; the verdict and message name the reason. Activation and revocation
  are fully implemented and tested with generated keys.
- **To close:** generate the key pair offline, build the issuing service, drop the public half into
  `src/I-Total.Transfer/Licensing/public-key.pem`, rebuild, and re-run the activation drills with a real
  entitlement.

### KI-02 - No purchase page is configured

- **Impact:** the Buy button is disabled and explains why.
- **To close:** set `-p:PurchaseUrl=https://...` at build time (`LicenseOptions` reads the assembly
  metadata automatically).

### KI-03 - Steam ownership is proven by a signed entitlement, not by a live Steamworks call

- **Impact:** the Steam edition verifies a signed `steam_entitlement.lic` bound to the app id; it does not
  ask a running Steam client whether the account owns the app.
- **Why:** the Steamworks SDK and a partner account are not available here, and Steam has not confirmed that
  this non-game utility is eligible.
- **Mitigation:** channel binding, app-id binding, revocation and expiry all work and are tested; the
  limitation is stated in the README and in code comments.
- **To close:** confirm eligibility, integrate the SDK behind `SteamEntitlementProvider`, and add a live
  drill to `TEST-PLAN.md`.

### KI-04 - Trial reset resistance is not DRM

- **Impact:** with administrator rights, a determined user can delete both the file and the registry copy of
  the trial ledger and start over.
- **Mitigation:** two stores merged pessimistically, idempotent job fingerprints, clock-rollback detection,
  and the ledger is never silently reset by a corrupt file.
- **Deliberate:** no secret is shipped and nothing phones home, so a stronger scheme would be dishonest
  about what it protects. Documented rather than overclaimed.

### KI-05 - No installer toolchain or signing certificate on this machine

- **Impact:** the setup executable is not produced and nothing is Authenticode-signed.
- **Mitigation:** the per-user installer script is complete, tested end to end, and produces a working
  installation without admin rights; `Build-Installer.ps1` states `SETUP SKIPPED` and still produces the
  portable ZIP; `Sign-Artifacts.ps1` fails closed when the tool or credential is missing.
- **To close:** install Inno Setup 6, obtain a code-signing certificate, run `Sign-Artifacts.ps1`, verify,
  then publish the hashes.

### KI-06 - Steam entitlement is read from the install directory

- **Impact:** a Steam build only sees `steam_entitlement.lic` if it is placed beside the executable (the
  installer and portable zip copy it when present in `publish`).
- **Mitigation:** the missing-file message names the exact path. No silent fallback to "licensed".

## Device access (hardware unavailable on 2026-10-20)

### KI-07 - Android path is live for walk/copy, but cancel/resume, lock and PTP remain open

- **Impact:** the HUAWEI was walked and copied on 2026-10-20 (4,106 files; 412 verified + 108 unverifiable;
  0 failed), but a real cancel -> resume, a locked-phone abort and a PTP-only handset have not been run.
- **Mitigation:** the Android walk, volume labels, filtered copy and resume-preservation of unverified files
  are live-verified; the same engine and recovery code serve both families.
- **To close:** `DEVICE_TEST_MATRIX.md` rows AN06-AN08 and the cancel/resume drill. No Android throughput
  number is published.

### KI-08 - iPhone AFC backend is inert

- **Impact:** AFC cannot be used; the app falls back to WPD (verified) or Shell (read-only fallback).
- **Reason:** `native/dependencies.json` ships empty hashes, so `DependencyVerifier` refuses to load.
- **To close:** ship audited native binaries with pinned hashes and a licence review.

### KI-09 - Fixed mid-scan disconnect behaviour has not been re-run on hardware

- **Impact:** the earlier build wrote a truncated cache entry marked `Complete` after the phone vanished; the
  entry was deleted, the code fixed, and simulated tests pass. Only a real pull proves the fix.
- **To close:** TEST-PLAN check 26.

### KI-10 - Locked phone / revoked USB permission not drilled

- **Impact:** the app reports "attached but not answering" and aborts safely in simulation; no physical
  evidence for locked-phone or permission-revoked states.
- **To close:** matrix rows IP08, AN08.

### KI-11 - Windows 11 and 200 % scaling unobserved

- **Impact:** rendering, caption contrast and DPI layout are proven on Windows 10 22H2 at 100 % and 150 %
  only.
- **To close:** run the standard visual drill on a Windows 11 machine and at 200 %.

### KI-12 - No physical iOS wireless sender

- **Impact:** the receiver and its approval gate are live-verified from scripted clients; the browser sender
  leg on a real iPhone has never run, so release acceptance keeps that row open.
- **To close:** physical iPhone drill; do not claim seamless iOS background access at any point.

### KI-13 - Wireless trial metering is per sender session, in memory

- **Impact:** one pairing/redemption is one trial job no matter how many files it sends. If the receiver is
  restarted, every session token is invalidated anyway (in-memory pairing state), so nothing durable is
  needed.
- **Trade-off:** there is no batch-complete signal in the protocol, so "job" is defined as the sender
  session. Documented in `LicenseService.TryStartWirelessUpload`.

### KI-14 - Large-library throughput numbers are not device benchmarks

- **Impact:** the cold live scan on 2026-10-22 counted **55,185 files / 151.34 GiB in 23.3 min** on this
  machine and phone; that is a single observation, not a benchmark. The earlier 26,335-file / 10.3-minute
  pass, the 52,574-file / 23.2-minute pass and the 57,371-file warm pass are historical. No buffer sweep or
  device write benchmark exists yet.
- **To close:** matrix rows BT07/BT08, published with the full context block.

## Robustness / technical debt

### KI-15 - Settings page has grown long

- **Impact:** the Licence section is at the bottom of a scrolling table; at 150 % it requires scrolling but
  is reachable and not clipped.
- **Mitigation:** the licence stop dialog opens Settings and the file picker directly, so an exhausted trial
  does not require finding the row.
- **Possible future:** a dedicated Licence page if more commercial state (invoices, seat management) arrives.

### KI-16 - PowerShell scripts must stay ASCII (guarded by a test)

- **Impact:** Windows PowerShell 5.1 reads BOM-less script files as ANSI; a UTF-8 em dash becomes a smart
  quote and breaks parsing. This actually happened in `Build-Installer.ps1` during development.
- **Mitigation:** installer scripts are ASCII-only; a rebuild/test run catches a regression immediately.
- **Closed 2026-10-20:** `InstallerScriptTests.EveryInstallerScriptStaysAsciiForWindowsPowerShell51` fails the suite on any non-ASCII byte, naming the file and line. Writing a BOM is no longer necessary while that guard runs in every test pass.

### KI-17 - Shell backend tests are skipped

- **Impact:** two apartment/probe tests cannot run in the harness.
- **Mitigation:** documented skips, not silent passes. Manual Shell fallback behaviour was verified earlier.

### KI-18 - Trial ledger is not included in the diagnostics bundle (resolved 2026-10-20)

- **Resolution:** the bundle now contains `licence.txt` with the standing, channel, jobs used/limit/remaining
  and whether a new job may start. The purchase id, the ledger entries and their fingerprints are excluded,
  and `trial.json` itself is not copied.
- **Evidence:** `DiagnosticsBundleTests.TheBundleCarriesTheTrialCounterButNotThePurchaseIdOrLedgerFingerprints`;
  a live export from the published build read `Trial`, `0` of `3`, and contained no `trial.json`.

### KI-19 - Cross-device scan isolation is fixed in code, but the physical disappearance drill is pending

- **Impact:** on 2026-10-21 a scan selected as Apple iPhone read the HUAWEI's tree after the iPhone briefly
  disappeared from re-enumeration, and overwrote the iPhone's WPD cache entry. The code now binds a backend
  to its selected phone and refuses a mismatch; the corrupted cache was preserved and deleted.
- **Mitigation:** `DeviceSelectionGuard`, `ScanResult.BelongsTo`, the MainForm acceptance guard and the cache
  guard; 14 simulated tests pass. A mismatched scan cannot reach `_scan`, the cache or a transfer plan.
- **To close:** on hardware, start a scan for one phone, make that phone stop answering (unplug/lock) while
  the other remains attached, and confirm the run aborts with "the other attached phone was not used" and
  writes no cache. `DEVICE_TEST_MATRIX.md` BT08.

### KI-20 - The iPhone's 10.99-second listing is warm-cache evidence, not a cold benchmark

- **Impact:** the 57,371-file / 188,810,836,019-byte report is real and `Complete`, but its 10.99 s duration
  followed an earlier Shell enumeration and is not reproducible cold. A cold build walk measured about
  32 files/s (3,437 files in 108 s), consistent with the ~27 ms/object transport cost. The current full
  cold pass on 2026-10-22 counted **55,185 files / 162,499,726,120 bytes (151.34 GiB) in 23.3 min**; the
  26,335-file / 10.3 min pass and the 57,371-file / 10.99 s warm pass are historical.
- **Mitigation:** `PROJECT-STATUS.md` and `DEVICE_TEST_MATRIX.md` label the 10.99 s as warm and publish the
  current 23.3-minute pass; planning uses the per-object cost, not the warm pass.
- **To close:** no further action needed for the timing honesty; a second cold pass on a different phone is
  optional evidence, not a release blocker.

### KI-21 - The 100 % pixel capture of the UI-18 audit is still missing

- **Impact:** all seven pages were audited row by row at the host's native 150 % scaling on 2026-10-22:
  Overview, Appearance, Backup & queue, Media library, Wireless import, History and Diagnostics. The live
  audit clicked the page actions, recorded disabled controls with their reason, and found and fixed five
  real defects (clipped Wireless status, mojibake, ambiguous `Clear`, false dependency verdict, misleading
  empty step). `PageControlTests` (5) and the existing page tests pin the wiring. The remaining gap is the
  **100 % pixel capture**, which this machine cannot produce because its only display reports 150 %
  (`AppliedDPI=144`).
- **Mitigation:** UI-18 is recorded as PASS at 150 % plus automated, with the 100 % capture named as open;
  no control is knowingly a placeholder. The 100 % layout remains a code/test claim, not a pixel observation.
- **To close:** run the page walk on a 96 DPI host or a 100 % session and attach the captures; no new control
  audit is required.

