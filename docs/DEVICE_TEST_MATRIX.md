# Device test matrix

Physical-device evidence for I-Total Transfer. A simulated test is listed as simulated and never counted as
hardware proof. Dates are the date the drill actually ran.

Status vocabulary:

- **LIVE PASS** - performed on the named hardware, on the date shown.
- **SIMULATED PASS** - driven by the deterministic test backends; not hardware proof.
- **PENDING HARDWARE** - implementation exists but the drill has not run.
- **NOT APPLICABLE** - no such mode exists for that family.

Phones available in this workspace: iPhone 13 Pro (`VID_05AC&PID_12A8`, serial `FCC4L3VYCW`, iOS 27.0.1)
and HUAWEI Y7 Prime 2018 (`VID_12D1&PID_1082`, serial `26EBB18726155381`, volumes "SD card" + "Internal
storage"). Both were attached together on 2026-10-21; the Android phone was walked and copied, and a
cross-device scan defect was found and fixed. The current iPhone listing is the 2026-10-22 cold scan
(**55,185 files / 162,499,726,120 bytes / 151.34 GiB**, `Complete`, 23.3 min). The 2026-10-21 26,335-file
listing and the 2026-10-20 57,371-file warm pass are historical. Both handsets were reattached on 2026-10-22;
a filtered HEIC/MOV copy also completed through the shared engine (8 files, hash-verified).



## iPhone 13 Pro (Apple / WPD)

| # | Drill | Status | Evidence / note |
| --- | --- | --- | --- |
| IP01 | Attach, unlock, trust; app reports "attached and answering" | LIVE PASS (2026-10-20, pre-fix build) | Detection root cause fixed earlier: this phone exposes no `DCIM` container; either container is accepted. |
| IP02 | Full camera-roll scan | LIVE PASS (cold, final fixed build, 2026-10-22) | **55,185 files / 162,499,726,120 bytes (151.34 GiB)**, `status=Complete`, **23.3 min**, 11 folders, 0 failed folders, 0 warnings, 0 unknown sizes, 1,167 live-photo pairs. The scan report is `%TEMP%\opencode\physical-heic-mov\destination\I-total-enumeration-report.json`. The 2026-10-21 26,335-file / 106.49 GiB / 10.3 min listing and the 2026-10-20 57,371-file / 188.81 GB warm pass are historical: the phone now exposes more albums. |
| IP03 | Copy a verified selection to disk | LIVE PASS (pre-1.8.0 build) | Per-file SHA-256; completion report captured. |
| IP04 | HEIC + MOV from this real roll, metadata preserved | LIVE PASS (2026-10-22, shared engine, filtered) | Eight files selected from the last complete listing: 3 `.HEIC` + 5 `.MOV`, 2,868,790 bytes, copied through the real native WPD backend and the shared engine. Every entry ended `IntegrityVerified` with `SourceAndDestinationHash`; all eight source/destination SHA-256 pairs are identical, 0 failed, 0 unverified. Report: `%TEMP%\opencode\physical-copy\destination\I-total-session-report.json`. This was a filtered engine fetch, not the UI copy path; the separate live UI scan is IP02. |
| IP05 | Cancel a transfer, then resume from the session record | PENDING HARDWARE | Recovery logic is simulated-pass; the real cancel/resume drill is open. |
| IP06 | Unplug mid-scan: scan must end `Incomplete`, no cache written, no false completion | SIMULATED PASS; PENDING HARDWARE | The 2026-10-20 pull exposed the old defect (reported `Complete` with 9.5k `0x802A0002` warnings); fixed. Re-run is TEST-PLAN check 26. |
| IP07 | Unplug mid-file: partial file not marked copied, resume re-reads it | SIMULATED PASS; PENDING HARDWARE | Fault injection covers read/commit failures. |
| IP08 | Lock the phone mid-transfer | PENDING HARDWARE | Expected: attached-but-not-answering, safe abort. |
| IP09 | Reconnect the same phone: resume targets the right source | PENDING HARDWARE | Stable identity checks are in the manifest. |
| IP10 | Connect a different phone after disconnect: resume must not bind to it | SIMULATED PASS; PENDING HARDWARE | Device identity comparison is tested. |
| IP11 | Diagnostics row `Attached and answering: Apple iPhone` | LIVE PASS (2026-10-21) | With both attached, the AppleFirst Diagnostics showed 10 checks, Apple `ATTACHED`, family `APPLE`, software `DETECTED`, drivers `FOUND`, trust `ACCESSIBLE`; the summary named both phones. The AndroidFirst bundle named both as well. The locked-phone variant remains pending. |
| IP12 | AFC backend availability | NOT VERIFIED (fail-closed) | `native/dependencies.json` ships empty hashes; AFC is inert by design. |
| IP13 | Apple driver/software missing or needing repair | PENDING HARDWARE | Diagnostics explain the required Apple software; repair drill not run. |

## Android - HUAWEI Y7 Prime 2018 (WPD/MTP)

| # | Drill | Status | Evidence / note |
| --- | --- | --- | --- |
| AN01 | Attach, unlock, choose "File transfer (MTP)" | LIVE PASS (2026-10-20, Explorer only) | Windows Explorer read "SD card" and "Internal storage"; the 1,209-entry `music` folder recursed for >15 min. |
| AN02 | App detects the phone as Android and enumerates every volume | LIVE PASS (2026-10-20, current source) | Native WPD reported the Android family; both volumes were walked. **4,106 files / 16,114,057,157 bytes (15.01 GiB)**, `Complete`; complete listing cached. |
| AN03 | Volume names come from the device ("SD card", "Internal storage") | LIVE PASS (2026-10-20) | The destination roots are `SD card` and `Internal storage`, taken from the phone's own volume labels. |
| AN04 | `Android\data` and `Android\obb` skipped, `Android\media` walked, `.thumbnails` skipped | SIMULATED PASS; PENDING HARDWARE | Unit-tested rules. |
| AN05 | Copy a small selection with verified bytes | LIVE PASS (2026-10-20; honest unverifiable result) | Filtered copy planned 520 files: **412 source-and-destination SHA-256 verified, 108 `COPIED-UNVERIFIABLE` (0-byte `Sounds\.pictures` files), 0 failed, 3,586 skipped**; ending `COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP`. Resume preserved the 108 unverified entries (`PRESERVED UNVERIFIED`), 0 failed. The trial was not charged because the run was not a meaningful completed job. |
| AN06 | PTP-only device raises the camera-only warning | SIMULATED PASS; PENDING HARDWARE | `PortableStorage` warning; no PTP-mode device tested. |
| AN07 | Unplug mid-scan / mid-file | SIMULATED PASS; PENDING HARDWARE | Same engine guards as iPhone; real pull pending. |
| AN08 | Lock the phone mid-transfer | PENDING HARDWARE | |
| AN09 | Two phones at once: `PreferredHandset = AndroidFirst` reorders selection | LIVE PASS (2026-10-21; with caveat) | With both attached, `AndroidFirst` selected the HUAWEI and `AppleFirst` selected the Apple iPhone. The same session exposed a cross-device scan defect: the probe could select one phone and the scan re-enumerate to the other. `DeviceSelectionGuard` now binds the backend and refuses that switch; 14 tests cover the rule. The physical "selected phone disappears mid-scan" drill is still pending. |
| AN10 | Windows→Android send (device write) | PENDING HARDWARE | Code path exists and is unit-tested; no live write was attempted. |

## Both phones / cross-cutting

| # | Drill | Status | Evidence / note |
| --- | --- | --- | --- |
| BT01 | Both phones attached; iPhone preferred by default | LIVE PASS (2026-10-21, current build) | Both attached. `AppleFirst` selected the Apple iPhone; `AndroidFirst` selected the HUAWEI. The cross-device scan defect above was found here and fixed. |
| BT02 | One phone unplugged while the other stays: only the lost family shows unavailable | SIMULATED PASS; PENDING HARDWARE | Family-filtered presence evidence. |
| BT03 | Both unplugged while jobs are queued | SIMULATED PASS | Queue state and messages tested. |
| BT04 | Restart the app with an interrupted job, then resume | LIVE PASS (pre-1.8.0) | Recovery at 150 % scaling; dismissal round-trip. |
| BT05 | Windows restart with incomplete jobs | SIMULATED PASS | Manifest + recovery inspection. |
| BT06 | Operation band mid-scan capture and cached-listing-beside-scan capture | PENDING HARDWARE | Screenshot evidence only; no code change needed. |
| BT07 | Device-to-disk throughput benchmark with a full context block | PENDING HARDWARE | No synthetic number will be published as a device benchmark. |
| BT08 | Selected phone disappears mid-scan; the other must not be read | SIMULATED PASS; PENDING HARDWARE | `DeviceSelectionGuardTests` (6) and `ScanIdentityTests` (8) pin the refusal, the honest "other attached phone was not used" message, and `ScanResult.BelongsTo`; the cache and UI guards were compiled and unit-tested. The live disappearance drill has not run on hardware. |
| BT09 | Buffer sweep 64 KiB / 256 KiB / 1 MiB / 4 MiB | PENDING HARDWARE | Settings support all four; the sweep needs a phone. |

## Environment

| # | Item | Status |
| --- | --- | --- |
| EN1 | Windows 10 22H2 build 19045, 150 % scaling | LIVE PASS |
| EN2 | Windows 11 rendering (build 22000+) | NOT OBSERVED (no such machine) |
| EN3 | 200 % scaling | NOT OBSERVED |
| EN4 | Audible UI cues | NOT VERIFIABLE from the harness |
| EN5 | Wireless receive end-to-end from a real iOS browser | NOT RUN (no physical iOS sender) |
| EN6 | Wireless TLS-fallback refusal drill | LIVE PASS (2026-10-22, current build) |
| EN7 | Theme preview and Overview action-row drill at 150 % (real executable) | LIVE PASS (2026-10-22) |
| EN8 | Wireless private-LAN bind without elevation/URL ACL | LIVE PASS (2026-10-22, current build) |

## How a row is closed

1. Run the drill exactly as written in `TEST-PLAN.md` (or the note above).
2. Capture the diagnostics export and, for throughput rows, the full context block.
3. Replace the status with **LIVE PASS (date)** and put the evidence file or measured numbers beside it.
4. If it fails, mark **FAILED**, file it in `KNOWN_ISSUES.md`, fix, rebuild, and re-run. Never mark a row
   passed because the code "should" work.
