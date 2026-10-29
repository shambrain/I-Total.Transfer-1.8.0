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



## 2026-10-29 hardware drills (source at the time; published build after the fixes: `F3ED2CC3...`)

Both handsets were attached. Probes and scans ran first; the two defects they exposed were fixed and the copy drills then ran against the fixed source. The published build is `F3ED2CC3...`. The drills used a new reproducible harness,
`tools/DeviceDrill` (console, references the real `WpdDevice` and `TransferService`; outside the solution so
the deterministic suite never touches a phone). Source is read-only throughout.

| Drill | Result | Evidence |
| --- | --- | --- |
| iPhone probe | `Ready`, Apple, iOS 27.0.1, native WPD, hash-capable | `%TEMP%\opencode\drill-2026-10-29\probe-apple.json` |
| iPhone cold scan | **57,731 files / 194,908,848,302 bytes (181.52 GiB)**, `Complete`, 13 folders, 0 failed, 0 warnings, 0 unknown sizes, 0 zero-byte, 1,207 live-photo pairs, **1,267.2 s (21.1 min)**, 45.6 files/s | `...\scan-iphone.json` |
| iPhone warm scan (after the cold pass) | 57,731 files in 12.8-13.2 s — warm provider cache, not a cold benchmark | `...\scan-iphone2.*`, drill logs |
| iPhone HEIC/MOV copy | **8 files (6 HEIC + 2 MOV), 369,415,047 bytes**, all `SourceAndDestinationHash`, 0 failed, 0 unverified; verification 8/8, no wrong size or hash | `...\copy-iphone-heic-mov8.json` |
| Android probe | `Ready`, Android, LDN-L21, native WPD, hash-capable | `...\probe-android.json` |
| Android scan | **4,131 files / 18,653,277,179 bytes (17.37 GiB)**, `Complete`, 307 folders, 0 failed, 0 warnings, 0 unknown sizes, **263 zero-byte files**, 10.09 s, 409 files/s | `...\scan-android.json` |
| Android zero-byte copy | **20 zero-byte files copied and all 20 `SourceAndDestinationHash`**, 0 unverifiable, verification 20/20 | `...\copy-zero-android.json` |
| Android bulk copy | 2,056 files / 6,266,740,006 bytes (5.84 GiB), all `SourceAndDestinationHash`, 0 failed; verification 2,056/2,056 (2,055 live source re-hashes, one destination-digest match). This was a wider selection than planned because the harness argument containing a space was split; it is reported as what actually ran | `...\copy-camera-android.json` |
| Android rename conflict | collision preserved, copy stored as `VID_20260730_175333 (1).mp4`; verification reports `STORED AS` and passes; a third run reports `ALREADY PRESENT - NO FILES NEEDED COPYING` and adds no `(2)` copy | `...\rename-run3.json` |
| Android cancel | cancelled after 3.7 s: manifest `CANCELLED`, no `.partial`, no destination file | `...\cancel-android2.json`, `...\cancel-android2\.i-total\session.json` |
| iPhone cancel | cancelled after 3 s on a 261,575,329-byte MOV: manifest `CANCELLED`, no `.partial`, no destination file | `...\cancel-iphone.json` |
| iPhone resume | the same MOV copied to completion, `HASH-VERIFIED COMPLETE (source-to-destination)`, verification 1/1 | `...\resume-iphone.json` |
| iPhone lock | 7,336,273,708-byte MOV copied to `HASH-VERIFIED COMPLETE`; after the phone suspended it was absent from the WPD list, and an AppleFirst probe returned the HUAWEI (fallback). Not an abort: WPD removes a locked iPhone | `...\lock-iphone.json`, `probe-apple-locked.json`, `pnp-wpd-while-iphone-locked.txt` || Android buffer sweep | one 184,296,485-byte MP4, one run per buffer, all hash-verified: 64 KiB 5.29 s / 33.22 MB/s; 256 KiB 5.17 s / 34.00 MB/s; 1 MiB 5.19 s / 33.86 MB/s; 4 MiB 5.19 s / 33.86 MB/s. Single observations, not a statistical benchmark | `...\sweep-{64,256,1024,4096}.json` || Android resume | 184,296,485-byte MP4 copied to completion, `HASH-VERIFIED COMPLETE (source-to-destination)`, verification 1/1 | `...\resume-android2.json` |

### Two defects found by these drills and fixed

1. **Every WPD copy failed at the commit.** `WpdDevice.CopyToStage` deleted the `.partial` in a `finally`
   block that ran *before* the move to the final staging name, so the move threw "could not find
   .partial"; the failure then reset the session and every later file failed with "WPD session is not
   open". The cleanup now runs only on failure, and `CopyToStage`/`OpenSourceRead` reopen the bound
   device instead of throwing. The 2,056-file Android copy, the 20 zero-byte copy, the 8-file iPhone copy
   and the 184 MB resume above are the proof.
2. **The Android family was cleared by the first session open.** `EnsureOpen` calls `ResetSession`, which
   set `_family = Unknown` after `Probe`/`Scan` had already recorded Android. `Capabilities` therefore
   advertised only Photos/Videos for the HUAWEI, and the resume path would have taken the iPhone branch.
   `ResetSession` now restores the family from the bound selection. Probe now reports four categories
   (Photos, Videos, Music, Files) for Android and two for the iPhone.

### Observation, not a product claim

Running the iPhone cold scan and an Android copy **in two processes at the same time** made the WPD
provider return "Insufficient memory" and "The requested resource is in use"; the iPhone scan aborted
safely with `DeviceStoppedAnsweringException` and wrote no report (no false completion). Re-run alone, the
same scan completed. The application is designed to read one device at a time; two app instances are not
coordinated. Recorded as KI-27.
## iPhone 13 Pro (Apple / WPD)

| # | Drill | Status | Evidence / note |
| --- | --- | --- | --- |
| IP01 | Attach, unlock, trust; app reports "attached and answering" | LIVE PASS (2026-10-20, pre-fix build) | Detection root cause fixed earlier: this phone exposes no `DCIM` container; either container is accepted. |
| IP02 | Full camera-roll scan | LIVE PASS (cold, final fixed build, 2026-10-22) | **55,185 files / 162,499,726,120 bytes (151.34 GiB)**, `status=Complete`, **23.3 min**, 11 folders, 0 failed folders, 0 warnings, 0 unknown sizes, 1,167 live-photo pairs. The scan report is `%TEMP%\opencode\physical-heic-mov\destination\I-total-enumeration-report.json`. The 2026-10-21 26,335-file / 106.49 GiB / 10.3 min listing and the 2026-10-20 57,371-file / 188.81 GB warm pass are historical: the phone now exposes more albums. |
| IP03 | Copy a verified selection to disk | LIVE PASS (pre-1.8.0 build) | Per-file SHA-256; completion report captured. |
| IP04 | HEIC + MOV from this real roll, metadata preserved | LIVE PASS (2026-10-22, shared engine, filtered) | Eight files selected from the last complete listing: 3 `.HEIC` + 5 `.MOV`, 2,868,790 bytes, copied through the real native WPD backend and the shared engine. Every entry ended `IntegrityVerified` with `SourceAndDestinationHash`; all eight source/destination SHA-256 pairs are identical, 0 failed, 0 unverified. Report: `%TEMP%\opencode\physical-copy\destination\I-total-session-report.json`. This was a filtered engine fetch, not the UI copy path; the separate live UI scan is IP02. |
| IP05 | Cancel a transfer, then resume from the session record | LIVE PASS (2026-10-29, current source) | A 261,575,329-byte MOV was cancelled after 3 s: manifest `CANCELLED`, no `.partial`, no destination file. The resumed copy ended `HASH-VERIFIED COMPLETE (source-to-destination)`, verification 1/1. Reports: `docs/evidence/2026-10-29/cancel-iphone.json`, `resume-iphone.json`. |
| IP06 | Unplug mid-scan: scan must end `Incomplete`, no cache written, no false completion | LIVE PASS (2026-10-24, current published build) | iPhone unplugged during a live WPD scan while the HUAWEI stayed attached. The scan returned **2,326 files / 8.05 GB, `Incomplete`**, with **1,739 files** reporting no usable size. The post-scan probe rejected it with the real dialog **“the iPhone stopped answering during the scan. The listing is incomplete and was not accepted.”** No `I-total-enumeration-report.json` and no cache were written. Log: `%TEMP%\opencode\disconnect-drill2\destination\I-total-transfer.log`; screenshot: `...\02-rejected-after-disconnect.png`. |
| IP07 | Unplug mid-file: partial file not marked copied, resume re-reads it | SIMULATED PASS; PENDING HARDWARE | Fault injection covers read/commit failures. |
| IP08 | Lock the phone mid-transfer | LIVE PASS (2026-10-29; observed, not an abort) | A 7,336,273,708-byte MOV copy completed `HASH-VERIFIED COMPLETE` while the operator locked the iPhone (the exact lock instant was not timestamped). Once the phone suspended, it **disappeared from the WPD device list** (PnP showed only the HUAWEI); a new `--family apple` probe then returned the HUAWEI, so the app falls back to the other attached phone and names it. A locked iPhone is therefore not an "attached but not answering" abort on this stack: WPD removes it. Evidence: `docs/evidence/2026-10-29/lock-iphone.json`, `probe-apple-locked.json`, `pnp-wpd-while-iphone-locked.txt`. |
| IP09 | Reconnect the same phone: resume targets the right source | PENDING HARDWARE | Stable identity checks are in the manifest. |
| IP10 | Connect a different phone after disconnect: resume must not bind to it | SIMULATED PASS; PENDING HARDWARE | Device identity comparison is tested. |
| IP11 | Diagnostics row `Attached and answering: Apple iPhone` | LIVE PASS (2026-10-21) | With both attached, the AppleFirst Diagnostics showed 10 checks, Apple `ATTACHED`, family `APPLE`, software `DETECTED`, drivers `FOUND`, trust `ACCESSIBLE`; the summary named both phones. The AndroidFirst bundle named both as well. The locked-phone variant remains pending. Repeated on 2026-10-24 with the current build: the same 10 checks and the summary `Attached: HUAWEI Y7 Prime 2018 (Android), Apple iPhone (Apple).` With only the HUAWEI attached, the page showed its 8 Android rows and no Apple rows. That run also found the AFC row reading `VERIFIED` with no binary verified; it now reads `NOT PRESENT`. |
| IP12 | AFC backend availability | NOT VERIFIED (fail-closed) | `native/dependencies.json` ships empty hashes; AFC is inert by design. The Diagnostics row reads `NOT PRESENT` when nothing is verified; until 2026-10-24 it said `VERIFIED` because the required set is empty (`AMissingAfcRuntimeIsNotReportedAsVerified`). |
| IP13 | Apple driver/software missing or needing repair | PENDING HARDWARE | Diagnostics explain the required Apple software; repair drill not run. |

## Android - HUAWEI Y7 Prime 2018 (WPD/MTP)

| # | Drill | Status | Evidence / note |
| --- | --- | --- | --- |
| AN01 | Attach, unlock, choose "File transfer (MTP)" | LIVE PASS (2026-10-20, Explorer only) | Windows Explorer read "SD card" and "Internal storage"; the 1,209-entry `music` folder recursed for >15 min. |
| AN02 | App detects the phone as Android and enumerates every volume | LIVE PASS (2026-10-22, current source) | Native WPD reported the Android family; both volumes were walked. **4,103 files / 15,612,056,299 bytes (14.54 GiB)**, `Complete`, 307 folders, 0 failed folders. The 2026-10-20 4,106-file / 16,114,057,157-byte listing is historical. |
| AN03 | Volume names come from the device ("SD card", "Internal storage") | LIVE PASS (2026-10-20) | The destination roots are `SD card` and `Internal storage`, taken from the phone's own volume labels. |
| AN04 | `Android\data` and `Android\obb` skipped, `Android\media` walked, `.thumbnails` skipped | LIVE PASS (2026-10-29) | The 4,131-file scan contains two `Android\media` folder entries and **zero** `Android\data`, `Android\obb` or `.thumbnails` entries. Evidence: `docs/evidence/2026-10-29/scan-android.json` (folder list). |
| AN05 | Copy a small selection with verified bytes | LIVE PASS (2026-10-20; honest unverifiable result) | Filtered copy planned 520 files: **412 source-and-destination SHA-256 verified, 108 `COPIED-UNVERIFIABLE` (0-byte `Sounds\.pictures` files), 0 failed, 3,586 skipped**; ending `COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP`. Resume preserved the 108 unverified entries (`PRESERVED UNVERIFIED`), 0 failed. The trial was not charged because the run was not a meaningful completed job. |
| AN06 | PTP-only device raises the camera-only warning | LIVE PASS (2026-10-24, current published build) | The HUAWEI was switched to **Photo transfer (PTP)**; Windows enumerated it with PID `1083`. The app scan returned 57 files / 7.5 MB and showed the visible Overview warning **"Camera-only connection (PTP): only DCIM and Pictures are visible. If its Files app shows more, switch the USB notification to File transfer and scan again."** The activity log carried the same warning. Screenshot: `%TEMP%\opencode\ptp-drill3\02-ptp-result.png`. |
| AN07 | Unplug mid-scan / mid-file | SIMULATED PASS; PENDING HARDWARE | Same engine guards as iPhone; real pull pending. |
| AN08 | Lock the phone mid-transfer | NOT APPLICABLE on this MTP stack (observed 2026-10-24) | Locking the HUAWEI screen does **not** disable file transfer: the phone remains visible to Windows and the 584.1 MB copy completed with source/destination SHA-256 equality. A screen lock is not a disconnect. The abort path is covered by the backend-fault tests, not by this lock. |
| AN09 | Two phones at once: `PreferredHandset = AndroidFirst` reorders selection | LIVE PASS (2026-10-21; with caveat) | With both attached, `AndroidFirst` selected the HUAWEI and `AppleFirst` selected the Apple iPhone. The same session exposed a cross-device scan defect: the probe could select one phone and the scan re-enumerate to the other. `DeviceSelectionGuard` now binds the backend and refuses that switch; 14 tests cover the rule. The physical "selected phone disappears mid-scan" drill is still pending. |
| AN10 | Windows-to-Android send (device write) | NOT SUPPORTED on Android (WPD/MTP is read-only) | The Settings wording is `Windows -> iPhone (send, AFC only)`; with an Android/WPD backend a send is refused with an explicit message. Writing to an iPhone needs the AFC backend and its pinned runtime, which is fail-closed until installed. No live write is claimed. |
| AN11 | Cancel a transfer, then resume it | LIVE PASS (2026-10-22, shared engine) | A live Android scan found 4,103 files; one 612,420,912-byte MP4 was copied and cancelled after more than 5 MB. The manifest recorded `CANCELLED`, one outstanding entry and 612,420,912 bytes. `SessionRecovery.BuildResumePlan` produced one file; the resumed copy ended `HASH-VERIFIED COMPLETE (source-to-destination)`, 1/1 copied, 0 failed, 0 unverified, with source and destination SHA-256 both `4DADB8CC6A2E6412581167E0233CB495C62BDE570C7CE03743BA128EB140CD1B`. Destination: `%TEMP%\opencode\physical-cancel-resume\destination`. |
| AN12 | Filtered Start confirmation names the real plan | LIVE PASS (2026-10-24, current published build) | AndroidFirst live scan of 4,103 files; filter `.mp4` with a 500 MB minimum selected one 584.1 MB file. The Start-backup dialog read **`Copy 1 files (584.1 MB)`**, not the full scan, and was cancelled without writing. Screenshot: `%TEMP%\opencode\ui-android-confirm\02-confirm.png`. |
| AN13 | UI Stop and recovery bar after a real cancellation | LIVE PASS (2026-10-24, current published build) | After the one-file Start, **Stop** was pressed. The Overview recovery bar appeared with `1 planned - 0 copied and verified - 1 outstanding - 4,102 skipped by filter`; **Resume transfer** opened `Continue the unfinished session` naming the same 584.1 MB file and `Interrupted: you cancelled it`. The resume was cancelled, so no trial job was consumed. Screenshot: `%TEMP%\opencode\ui-android-cancel\01-cancelled-recovery.png`. |
| AN14 | Filtered copy must not auto-verify the whole scan | LIVE PASS (2026-10-24, current published build) | After a filtered one-file copy, the pre-fix app auto-ran destination verification over all 4,103 scan files and reported 4,102 `MISSING` files that the filter had excluded. The app now skips that whole-library pass when the filter planned fewer files (`Post-copy verification of the whole library was skipped: the filter planned 1 of 4,103 file(s)`). A no-op rerun skipped the already-present file, did not show the false verification report, and did not charge a trial job. |

## Both phones / cross-cutting

| # | Drill | Status | Evidence / note |
| --- | --- | --- | --- |
| BT01 | Both phones attached; iPhone preferred by default | LIVE PASS (2026-10-21, current build) | Both attached. `AppleFirst` selected the Apple iPhone; `AndroidFirst` selected the HUAWEI. The cross-device scan defect above was found here and fixed. |
| BT02 | One phone unplugged while the other stays: only the lost family shows unavailable | SIMULATED PASS; PENDING HARDWARE | Family-filtered presence evidence. |
| BT03 | Both unplugged while jobs are queued | SIMULATED PASS | Queue state and messages tested. |
| BT04 | Restart the app with an interrupted job, then resume | LIVE PASS (pre-1.8.0) | Recovery at 150 % scaling; dismissal round-trip. |
| BT05 | Windows restart with incomplete jobs | SIMULATED PASS | Manifest + recovery inspection. |
| BT06 | Operation band mid-scan capture and cached-listing-beside-scan capture | PENDING HARDWARE | Screenshot evidence only; no code change needed. |
| BT07 | Device-to-disk throughput benchmark with a full context block | LIVE PASS (single-file + mixed-batch observations, 2026-10-24) | Single file: one 612,420,912-byte MP4, native WPD, SourceAndDestinationHash, four buffer sizes (33.56-37.83 MB/s). Mixed batch: 30 files / 111,135,111 bytes (10 tiny, 10 about 1 MB, 10 about 10 MB), 4.681 s, 6.41 files/s, 23.74 MB/s, CPU 1.55 s, peak working set 69.9 MB, 30/30 hash-verified. One run each; real device observations, not a statistical benchmark. |
| BT08 | Selected phone disappears mid-scan; the other must not be read | LIVE PASS (2026-10-24, current published build) | The iPhone was unplugged while the HUAWEI remained attached. The app showed “the phone selected for this session (Apple iPhone) is no longer answering. Windows now lists HUAWEI Y7 Prime 2018, but this session will not switch phones automatically; that phone was not used,” then rejected the scan as incomplete. The HUAWEI tree was not accepted, cached or queued. This is the live disappearance drill. |
| BT09 | Buffer sweep 64 KiB / 256 KiB / 1 MiB / 4 MiB | LIVE PASS (single file, 2026-10-24) | Same context as BT07. 4 MiB was 37.83 MB/s versus 37.41 MB/s at 1 MiB (about 1.1% faster), with higher CPU time and peak working set; 1 MiB remains the default. One run each, one file only. |
| BT10 | Mixed-size batch throughput | LIVE PASS (2026-10-24) | Same HUAWEI/native WPD context: 30 files / 111,135,111 bytes = 10 tiny (8-24 B), 10 about 1 MB and 10 about 10 MB; 4.681 s, 6.41 files/s, 23.74 MB/s, CPU 1.55 s, peak 69.9 MB; 30/30 copied, 0 failed, 0 unverified, `SourceAndDestinationHash`. One run only. |

## Environment

| # | Item | Status |
| --- | --- | --- |
| EN1 | Windows 10 22H2 build 19045, 150 % scaling | LIVE PASS |
| EN2 | Windows 11 rendering (build 22000+) | NOT OBSERVED (no such machine) |
| EN3 | 200 % scaling | LIVE PASS (2026-10-24; real 192-DPI render at minimum and design sizes plus 200 % window captures of all seven pages from the published exe) |
| EN4 | Audible UI cues | PARTIAL (PCM verified; endpoint active; loopback masked; no listening test) |
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
