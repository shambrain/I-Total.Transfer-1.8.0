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

### KI-05 - No trusted code-signing certificate

- **Impact:** `signtool.exe` (Windows Kits 10.0.26100.0) and `Sign-Artifacts.ps1` are present, and the
  signing path has been drilled with a temporary self-signed certificate, but no trusted code-signing
  certificate exists, so nothing shipped is Authenticode-signed.
- **Mitigation:** the per-user installer script, portable ZIP and setup are complete and tested;
  `Build-Installer.ps1` skips the setup cleanly when `ISCC.exe` is absent; `Sign-Artifacts.ps1` fails closed
  (exit 2 without tools/credential, 3 for a missing PFX, 4 on signing failure, 5 on verification) and
  removes a PFX it imported even when signing fails.
- **To close:** obtain a trusted code-signing certificate, run `installer\Sign-Artifacts.ps1`, verify with
  `signtool verify /pa`, then publish the hashes.

### KI-06 - Steam entitlement is read from the install directory

- **Impact:** a Steam build only sees `steam_entitlement.lic` if it is placed beside the executable (the
  installer and portable zip copy it when present in `publish`).
- **Mitigation:** the missing-file message names the exact path. No silent fallback to "licensed".

## Device access (hardware unavailable on 2026-10-20)

### KI-07 - Resolved: Android path is live, and the locked-phone abort is not applicable to this MTP stack

- **Impact:** the HUAWEI was walked on 2026-10-22 (4,103 files / 14.54 GiB; 412 verified + 108 unverifiable in
  the earlier filtered copy; 0 failed). A 612,420,912-byte MP4 was cancelled mid-copy and resumed to
  `HASH-VERIFIED COMPLETE`, source/destination SHA-256 identical. A PTP-only handset was checked on
  2026-10-24: the Overview showed the camera-only warning and the log named PTP. On the same day the screen
  was locked during a 584.1 MB copy; MTP stayed active and the copy completed. A screen lock is not a
  disconnect on this Android stack, so no attached-but-not-answering abort is expected.
- **Mitigation:** the Android walk, volume labels, filtered copy, cancel/resume, PTP warning and source/destination
  verification are live-verified. Backend-fault behaviour is covered by `RecoveryFlowTests`.
- **To close:** no open Android action for this handset. A device that gates MTP on lock could exercise the
  abort path, but it is not a release blocker. A single-file buffer sweep and a 30-file mixed-size batch are published; no statistical benchmark is claimed.

### KI-08 - iPhone AFC backend is inert

- **Impact:** AFC cannot be used; the app falls back to WPD (verified) or Shell (read-only fallback).
- **Reason:** `native/dependencies.json` ships empty hashes, so `DependencyVerifier` refuses to load.
- **To close:** ship audited native binaries with pinned hashes and a licence review.

### KI-09 - Fixed mid-scan disconnect behaviour has not been re-run on hardware

- **Impact:** the earlier build wrote a truncated cache entry marked `Complete` after the phone vanished; the
  entry was deleted, the code fixed, and simulated tests pass. Only a real pull proves the fix.
- **To close:** TEST-PLAN check 26.

### KI-10 - iPhone lock / revoked USB permission not drilled

- **Impact:** the app reports "attached but not answering" and aborts safely in simulation. The HUAWEI
  keeps MTP active while locked, so AN08 is not applicable there; an iPhone lock and a revoked USB
  permission still have no physical observation.
- **To close:** matrix row IP08 (iPhone lock) or a device that revokes USB permission mid-copy.

### KI-11 - Windows 11 rendering unobserved

- **Impact:** rendering, caption contrast and DPI layout are proven on Windows 10 22H2 at 100 %, 150 %
  and 200 %. The 200 % pass is a real 192-DPI render of the minimum and design sizes, not a desktop
  screenshot. Windows 11 has not been run.
- **To close:** run the standard visual drill on a Windows 11 machine.

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
  pass, the 52,574-file / 23.2-minute pass and the 57,371-file warm pass are historical. A single-file buffer sweep and a 30-file mixed-size batch exist now (BT07/BT09/BT10); no statistical or device-write benchmark exists yet.
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

### KI-21 - No native 100 % desktop capture (layout now rendered at 96 DPI)

- **Impact:** all seven pages were audited row by row at the host's native 150 % scaling on 2026-10-22.
  The live audit clicked the page actions, recorded disabled controls with their reason, and found and fixed
  five real defects (clipped Wireless status, mojibake, ambiguous `Clear`, false dependency verdict,
  misleading empty step). The host's only display reports 150 % (`AppliedDPI=144`), so a native 100 %
  desktop capture cannot be produced here.
- **Mitigation:** on 2026-10-24 the real `MainForm` was rendered by a DPI-unaware harness at
  `DeviceDpi=96` (1416x919 client root) for all seven pages in Neon, Slate and Light. The brand band keeps
  its 78 px height with the full title and subtitle, and no page clips or overlaps a control at 100 %
  logical size. The render found one real defect: the Wireless `Require TLS` caption was wider than its
  card and lost the closing bracket. It is reworded to `Require TLS (private-network HTTP if no matching
  certificate)` and pinned by `WirelessTlsCaptionFitsInsideItsCardAtTheDesignedWidth`, which fails on the
  old wording (440 px needed, 414 px available). The captures are a 96-DPI render of the real controls,
  not a DWM-composited desktop screenshot; they are labelled that way in `UI-REQUIREMENTS-MATRIX.md`.
- **To close:** only a native 100 % desktop capture remains, and it needs a 100 % display; no layout or
  control-audit work is outstanding.

### KI-22 - Resolved: the test crash dumps are out of history

- **Resolution (2026-10-29):** the history was rewritten with `git filter-repo --path
  tests/I-Total.Transfer.Tests/TestResults --invert-paths` before publication. The largest reachable blob
  is now a 44.9 MB release ZIP; a fresh clone no longer downloads the four 230-313 MB dumps.
- **Note:** commit hashes quoted in `docs/PROGRESS.md` before the publication note identify the
  pre-publication history. The repository still grows with each committed archive revision; publishing
  archives as release assets instead of commits would keep it lean.

### KI-23 - The new size/reconciliation behaviour is not hardware-tested

- **Impact:** zero-byte files now verify at hash level, unknown sizes stay unknown, and the report
  reconciles every object. These are covered by automated tests against the mock backend and by static
  inspection of all three backends, but no phone was attached for this build.
- **Mitigation:** the previous hardware evidence (Android 4,103-file walk, iPhone 55,185-file walk, MP4
  resume) predates the change and is not claimed for it.
- **To close:** run `docs/DEVICE_TEST_MATRIX.md` IP02/IP03/AN02/AN05/AN11 on the candidate build and
  record the exact result, including any zero-byte or unknown-size items the handset exposes.

### KI-24 - Accessible names were added, but no screen-reader audit was run

- **Impact:** every interactive control on every page now gets an `AccessibleName` from its caption or
  designer name (`UI/Accessibility.cs`, called from `MainForm`), and a test fails if a page exposes an
  unnamed button, text box, list or progress bar. Focus order and announcement quality have not been
  checked with Narrator/NVDA.
- **To close:** run a Narrator pass over the seven pages and record what it announces.

### KI-25 - Old SQLite/manifest rows have no explicit outcome

- **Impact:** rows written before `OutcomeReason` existed carry `Outcome = None`. The reconciliation maps
  them from their state (verified -> already present, failed -> failed, queued -> outstanding), which is
  correct for the states the engine writes, but it is a derivation rather than a recorded fact.
- **Mitigation:** `ALegacyRowWithoutAnOutcomeIsMappedFromItsStateRatherThanCountedAsOutstanding` pins the
  mapping; new rows always record the outcome.
- **To close:** nothing to fix; a future schema version may backfill the column.

### KI-26 - One unreproduced failure of a licensing test

- **Observation:** during the final verification on 2026-10-29, one full run reported
  `ATamperedEntitlementIsRejectedAndLeavesTheTrialExactlyAsItWas` failed (490 passed, 1 failed, 2 skipped).
  The test passed in isolation immediately afterwards, and four consecutive full runs then passed
  (491 passed, 2 skipped, 493 total each).
- **Impact:** the licensing rule itself is deterministic (RSA-PSS verification over a tampered signature)
  and the test uses an in-memory trial store, so no persisted state is involved. The failure could not be
  reproduced and its assertion message was not captured by the filtered output.
- **Mitigation:** the suite is run with `--blame-hang`; the result is recorded here rather than hidden.
- **To close:** if it recurs, capture the full MSTest failure (assert name, expected/actual) and the
  sequence file; do not mark the suite green until the cause is known.

### KI-27 - Two app instances can contend for the WPD provider

- **Observation (2026-10-29):** the iPhone cold scan and an Android copy were run in two separate
  processes at the same time. The WPD provider returned "Insufficient memory to continue the execution of
  the program" and "The requested resource is in use" (0x800700AA); the iPhone scan aborted safely with
  `DeviceStoppedAnsweringException` and wrote no report. Re-run alone, the same scan completed
  (57,731 files / 181.52 GiB).
- **Impact:** the application reads one device at a time by design, but nothing stops a user from
  launching a second copy of the app; the two then share the provider and one may abort. The abort is
  safe and visible (no false completion), but confusing.
- **Mitigation:** the failure is reported as "the phone stopped answering"; completed files are preserved.
- **To close:** decide whether to add a single-instance guard (a named mutex plus "bring the existing
  window forward") and test it with two launches.

### KI-28 - A locked iPhone disappears from WPD; the app may select the other attached phone

- **Observation (2026-10-29):** while the iPhone was locked and suspended, Windows no longer listed it
  as a WPD device (only the HUAWEI remained). A new session with `AppleFirst` selected the HUAWEI and the
  Overview named it. The in-flight copy that was already running completed.
- **Impact:** the "preferred handset" is a preference, not a requirement. A user who locks the iPhone and
  then scans will get the other phone if one is attached; the screen names the device, but the transition
  can surprise.
- **Mitigation:** the selected device is named in the Overview, the banner and the log; the drill harness
  now has `--require-family` and refuses a mismatch.
- **To close:** decide whether a locked/absent preferred family should block the action with an explicit
  message instead of falling back. That is a product decision, not a defect in the current contract.

### KI-29 - A reconnect can expose a smaller "Complete" listing

- **Observation (2026-10-30):** after the iPhone was unlocked and reconnected, the first cold scan exposed
  **26,336 files / 114,344,214,625 bytes, `Complete`**, in 563 s. Before the lock/unplug the same phone
  exposed **57,731 files / 194,908,848,302 bytes** with 13 folders. The later scan had no failed folder and
  no warnings, so the engine classified it Complete.
- **What the recorded numbers actually decompose to:** the 2026-10-29 folder totals are `202610_a`
  2,543 files / 32,405,919,122 B, `202608_a` 26,320 / 114,331,766,452 B, `202609_a` 28,836 /
  48,151,068,934 B, and 32 files / 20,093,794 B across the nine small folders. The later scan's 26,336
  files and 114,344,214,625 B equal **`202608_a` plus 16 of those small-folder files**; the byte
  difference from the earlier scan (80,564,633,677 B) equals **`202610_a` + `202609_a` + those 16 small
  files exactly**. So two large albums were absent from the later listing; nothing in the evidence says
  they were deleted, and no cause is asserted.
- **Impact:** "Complete" means "every folder the provider exposed was read", not "the phone has no more
  albums". A provider that mounts a subset after reconnect can produce a smaller Complete listing; the
  app cannot know about objects it cannot see.
- **Mitigation:** the new inventory-scope comparison (`ScanScope`, `ScanResult.Scope`) compares the fresh
  scan with the last complete cached listing at folder level and warns, naming the missing folders, when
  the scope changed. The report carries both facts separately. The optional second-pass stability check
  still detects a listing that moves during one scan.
- **To close:** no code can prove hidden albums. A rescan after the phone settles is the practical answer;
  the warning now tells the operator to do exactly that.