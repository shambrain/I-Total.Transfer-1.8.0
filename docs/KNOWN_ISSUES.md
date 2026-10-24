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

### KI-05 - No code-signing certificate on this machine

- **Impact:** the Inno Setup `setup.exe` is built and tested, but nothing is Authenticode-signed.
- **Mitigation:** the per-user installer script, the portable ZIP and the setup are complete and exercised end
  to end without admin rights; `Build-Installer.ps1` skips the setup cleanly when `ISCC.exe` is absent;
  `Sign-Artifacts.ps1` fails closed when the tool or credential is missing.
- **To close:** obtain a code-signing certificate and the Windows SDK signing tools, run
  `installer\Sign-Artifacts.ps1`, verify with `signtool verify /pa`, then publish the hashes.

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

### KI-22 - Git history still carries the old test crash dumps

- **Impact:** four testhost hang dumps (about 1.1 GB) and their `.trx`/sequence files were committed on
  2026-10-18 and stayed tracked until 2026-10-24. They are now removed from the working tree and ignored
  (`**/TestResults/`, `*.dmp`), so a checkout is 146 MB instead of 1.37 GB, but the objects remain in the
  history pack: a clone still downloads them.
- **Mitigation:** `HEAD` no longer tracks them, and the source ZIP never included `TestResults`. The
  remaining cost is clone size/time, not release correctness.
- **To close:** rewrite history with `git filter-repo --path tests/I-Total.Transfer.Tests/TestResults
  --invert-paths` on a mirror, then force-push and re-clone. That is a deliberate repository operation,
  not something a release build should do unattended.
