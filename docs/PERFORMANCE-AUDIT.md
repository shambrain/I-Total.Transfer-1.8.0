# Performance, responsiveness and integrity audit — 1.8.0

This is the evidence record for the "the app seems to hang on a huge iPhone folder" report and for the
transfer-speed work that followed it. It states what was measured, on what hardware, and — just as
importantly — what was **not** measured. Nothing in the "measured" column is extrapolated from a smaller
run, and nothing in the "changed" column is claimed to be faster on hardware unless the measurement is
listed here.

## 1. The complaint, reproduced

| Item | Observation |
| --- | --- |
| Symptom | Scanning a single iPhone album "seems to hang"; selecting the device produced a busy window that never reported progress. |
| Reproduction | Old build, live iPhone 13 Pro (iOS 27.0.1, serial `FCC4L3VYCW`, native WPD backend). Scan started `18:55:27`, manually cancelled `19:05:50`. |
| Result | **10 m 23 s with no completion and no progress indication.** Process CPU `0.8 %` — blocked waiting on the device, not spinning or deadlocked. |
| UI state | Responsive throughout (the window answered automation the whole time), so this was *not* a UI-thread freeze. |

## 2. Where the time actually goes

The largest album on this handset (`202608_a`) contains **26,320 items**. A timed enumeration of exactly
that folder, through the same Shell call the app uses, on a cold cache:

| Measurement | Value |
| --- | --- |
| First `GetFolder.Items()` + `.Count` | **723,017 ms** for 26,320 objects — **≈ 27 ms per object** |
| Warm per-item property read (`System.Size`) | 0.1 – 0.25 ms (driver and Shell cache the enumeration) |
| Full camera-roll scan, warm cache (2026-10-21, historical) | **4.9 s**, 26,335 files, 106.49 GB, `status=Complete` |
| Full camera-roll scan, cold (2026-10-22) | **23.3 min**, 55,185 files, 151.34 GiB, `status=Complete`, 0 failed folders |
| Full camera-roll scan, cold cache | ≈ 12 min by the same 27 ms/object cost |

**Conclusion: the scan is transport-bound, not algorithm-bound.** The cost is the device round trip for
each directory entry, and the Shell compatibility backend pays exactly the same cost as native WPD. Two
consequences follow, and both were acted on:

1. Micro-optimising the property *set* (WPD reads six properties per object) would have changed nothing —
   the first enumeration call dominates, and per-item reads are then nearly free.
2. The observable defect was **silence**, not slowness. Progress was routed only to the *Backup & queue*
   page, so the Overview showed a busy window while the device worked normally. A ten-minute scan is a
   legitimate outcome on a cold cache; a ten-minute scan with no visible progress is a bug.

A third consequence is that a cold pass should only have to be paid **once per device**, not once per
launch. The scan-listing cache (section 4.2) keeps the last complete listing and shows it while the cold
pass runs. It does **not** make the enumeration faster — nothing on this side of the cable can — it removes
the empty window, and it reports what changed when the fresh listing lands.

## 3. Findings matrix

| # | Area | Evidence | Verdict | Action |
| --- | --- | --- | --- | --- |
| 1 | Enumeration on the UI thread | UI answered automation during the whole 10 m 23 s scan; `TransferService.ScanAsync` runs the backend on `Task.Run` | Not a defect | None |
| 2 | Progress reporting | Progress went only to `_transfer.SetPhase`; Overview showed nothing | **Defect** | `Core/ScanProgressReporter.cs` + routing to `MainForm.SetStatus` |
| 3 | Per-item cost in the scan hot path | `files.Sum(f => f.Size)` per report would be O(n) per tick | Latent defect | Running `_scanBytes` counter (O(1)) |
| 4 | Per-file device round trip before copying | `WpdDevice.CopyToStage` → `ResolveObjectId` → `ObjectExists` = one `GetProps` per file before the stream opens | **Avoidable cost** | Trust the current session's id; re-resolve by path only if the device refuses it |
| 5 | Redundant staged read-back | Backend already returns the SHA-256 it computed while streaming; the engine re-read and re-hashed the staged file | **Avoidable cost** (a full extra pass — 106 GB here) | Compare streamed source digest with committed destination instead |
| 6 | "Copy buffer size" setting | Offered in Settings; the WPD copy hard-coded 1 MB | **Control that could not work** | Wired through `AppSettings.BufferSize` |
| 7 | "File concurrency" setting | No backend read the value; an iPhone session serialises copies | **Control that could not work** | Pinned to 1, disabled, labelled with the reason |
| 8 | Enumeration completeness reporting | `EnumerationReport` distinguishes Complete / Incomplete / Empty / Failed; a folder read failure marks the scan Incomplete and is named | Correct | None |
| 9 | Memory | Scan materialises the file list (55,185 entries current); no per-file full-file buffering anywhere | Acceptable | None — bounded by the library size, not by the file sizes |
| 10 | Database writes during enumeration | Manifest is written after the scan, and throttled/atomic | Correct | None (the *copy* path was not — see 15 and 16) |
| 11 | Caption buttons in Light theme | Live capture: glyph `#E8F0FF` on a white band ≈ **1.06:1** | **Defect** | Theme-aware glyph colour → **16.3:1** measured |
| 12 | Context capture in the service (found while chasing 13) | `TransferService.ScanAsync/CopyAsync/VerifyAsync/CopyWindowsToDeviceAsync` awaited the device with the caller's `SynchronizationContext` captured; a caller that blocked for the result instead of pumping deadlocked (whole 26,400-entry scan test wedged, host aborted) | **Product defect** | Every public entry point starts on the pool via `Services/ContextFree.cs` |
| 13 | Test-suite reliability | Creating a WinForms control installed a `WindowsFormsSynchronizationContext` on the runner thread; a later `async` test's continuation was posted to a queue nobody drained (89 passed, then nothing, 3/3 runs) | **Defect in the test harness** | Control work on a dedicated STA thread; `WinFormsTestBase` restores the context around control-testing classes; hardware tests opt-in and bounded |
| 14 | Overview progress and the page's height floor | The Overview showed nothing during a long scan (only the *Backup & queue* page did), and the page squeezed its own action panel below its content height once the new band was added - the integration line was sliced in half in a real capture | **Defect** | `Core/OperationStatus.cs`, the *Current operation* band, and `AutoScrollMinSize` on the page |
| 15 | Forced manifest write per file | Every in-flight state change called `_store.MarkDirty(force: true)`, which serialises the **entire** session manifest to disk; at tens of thousands of entries that is a megabyte-sized JSON rewritten once per small file, and the cost grows as the transfer proceeds | **Defect** — measured **87 files/s** | `MarkDirty()` (the store's own 250-entry / 2 s policy); plan, pause, cancel, abort and summary still force |
| 16 | SQLite transaction per file **and per planned entry** | `UpsertFile` committed its own transaction, twice per file in the hot loop; opening a session committed one transaction per planned entry - 26,335 of them on the real handset before the first byte moved | **Defect** | `UpsertFiles` batching (a 64-entry queue drained at every consequential boundary, and one transaction for the whole plan) |
| 17 | Cost of one log line | `LogService.Write` stat'd the log for rollover and then opened, appended and closed it **per line**, while the copy path logs a line per file | **Defect** | Writer kept open with per-line flush, rollover from a running byte count, `FileShare.ReadWrite \| Delete` so the destination folder stays deletable while the app runs |
| 18 | UI updates per file | The engine reports once per file, and every report repainted the queue page and re-rendered the Overview band - on a 26,000-item library that is 26,000 posts to the message queue | Latent defect | `Core/ThrottledProgress.cs` (250 ms pacing). Measured: the engine emitted 4,000 updates, the UI received **21** |

## 4. What changed, and the reasoning behind it

- `Core/ScanProgressReporter.cs` — throttles (250 ms, plus always on a folder change) and formats
  `Scanning <folder> · N files · X GB · N files/s · mm:ss elapsed`.
- `Devices/Wpd/WpdDevice.cs` — reports at folder entry and per file; running byte counter; optional
  copy-buffer size; `OpenSourceStream` trusts the id the scan just produced and falls back to the path
  walk when the device refuses it.
- `Devices/ShellIPhoneDevice.cs` — the same progress reporting for the compatibility backend.
- `UI/MainForm.cs` — scan progress reaches the status bar, not just the queue page.
- `Services/TransferService.cs` — the staged read-back happens only when the backend cannot supply a
  source digest. This is **stronger, not weaker**: comparing the streamed source digest with the committed
  destination also catches a bad *read*, whereas comparing staged bytes with destination bytes could only
  ever compare the copy with itself. `VerificationLevel` classification is unchanged, and the manifest
  records an absent staged digest rather than implying a read that did not happen.
- `Services/BackendFactory.cs`, `Core/AppSettings.cs` — the buffer choice reaches the backend.
- `UI/Pages/SettingsPage.cs`, `Core/AppSettings.cs` — the inert concurrency control is visibly inert.
- `UI/Theme.cs` — caption glyph/fill colours follow `Theme.IsDark`.
- `Core/OperationStatus.cs` — the phase/heading/detail/permille model behind the Overview band. The
  permille position (rather than the raw counters) is what keeps a 106 GB byte total from overflowing an
  `int`-sized progress bar, and it is unit-tested against a total beyond `int.MaxValue`.
- `UI/Pages/DashboardPage.cs`, `UI/Pages/DashboardPage.Designer.cs` — the *Current operation* band:
  indeterminate while a phase is unmeasured, determinate once a real total exists, with the phase named
  in the heading. The page also declares `AutoScrollMinSize` for its true content height, so it scrolls
  rather than clipping its own action panel on a short window.
- `UI/MainForm.cs` — scan progress and every `TransferProgress` tick now also drive the Overview band
  (phase, counts, measured throughput, elapsed, remaining), and scan/transfer completion, cancellation and
  failure each leave the band in an honest terminal state.
- `Services/ContextFree.cs` — starts the service's entry points on the thread pool so a caller's
  `SynchronizationContext` is never captured. This is the context-capture deadlock class, not a test
  artefact: any synchronous host would have hit it.
- `Services/TransferService.cs` — the per-file state writes are batched: a throttled manifest write instead
  of a forced full serialisation per file, and a 64-entry SQLite queue instead of two committed transactions
  per file, drained at every boundary that matters for recovery. The session's whole plan is also written in
  one transaction instead of one per planned entry.
- `Services/LogService.cs` — the log file is opened once and flushed per line rather than stat'd, opened and
  closed per line, because the copy path logs a line per file.
- `Core/ThrottledProgress.cs` — paces live progress so the UI is refreshed a few times a second instead of
  once per file, with the terminal state still coming from the returned summary.
- `Core/ScanCache.cs` and `Services/ScanCacheStore.cs` — the last complete listing of each device, its
  honesty rules, and the change report (section 4.2). `ScanCacheKey.From` prefers the platform's stable id,
  then the serial, then the model.

### 4.1 Engine throughput, measured on local storage (no phone required)

The device leg cannot be measured without the handset, but everything the *application* controls can be, and
that is what decides whether engine tuning is worth doing at all. `EngineThroughputBenchmarkTests` copies a
deterministic in-memory library through the real engine — staging, streaming SHA-256, atomic commit, manifest
and database updates — to a local NTFS destination, with Windows Defender enabled:

| Workload | Before this round | After | Change |
| --- | --- | --- | --- |
| 4,000 files × 8 KiB (31.2 MiB) | 45.76 s = **87 files/s**, 0.7 MiB/s | 5.69 – 6.60 s = **670 – 703 files/s**, 5.3 – 5.5 MiB/s | **≈ 8× faster** |
| 4 files × 32 MiB (128 MiB) | 0.23 s = 560 MiB/s | 0.21 s = **596 – 619 MiB/s** | unchanged (within run-to-run noise) |

Per-file engine overhead fell from **≈ 11.4 ms to ≈ 1.4–1.5 ms**. The stepwise attribution was measured, not
assumed, one change at a time:

| Step | Small-file result | What it was |
| --- | --- | --- |
| Baseline | 87 files/s (45.76 s) | Forced full-manifest write per file, SQLite transaction per file, log file opened and closed per line |
| 15 — the forced manifest write removed | 533 files/s (7.51 s) | The single dominant cost; the manifest is the growing artefact, so it got worse the longer the transfer ran |
| 16 — SQLite writes batched | 606 files/s (6.60 s) | Two committed transactions per file became one per 64 entries, and one for the whole plan |
| 17 — log writer kept open | 670 – 703 files/s (5.69 – 6.60 s) | A stat, an open, an append and a close per logged line, i.e. per file |

Large files were already fast and stayed fast: their cost is streaming and hashing, neither of which had a
per-file defect. **The conclusion that matters:** at ≈ 1.5 ms of engine work per file against the ≈ 27 ms per
object the transport itself costs on a cold camera roll (section 2), the engine is no longer the lever — the
device leg is, and it cannot be optimised away from this side of the cable. That is precisely why no "faster
transfer" number is promised here.

Durability was not traded for speed. The consequential boundaries (plan written, pause, cancel, abort, final
summary) still force a manifest flush and drain the database queue, so a crash loses at most the last few
entries — and recovery revalidates each one against the destination file rather than trusting the record
(section 3, finding 8).

### 4.2 The scan-listing cache: pay the cold pass once per device, not once per launch

The cold enumeration cost in section 2 cannot be moved from this side of the cable, so the work done here was
to stop it being a **blank window**. The last complete listing of each device is persisted under
`%APPDATA%\I-Total Transfer\scan-cache\` and shown the moment a scan starts, headed *Last known library
(captured …)*. The live scan still runs and still replaces it, then reports what changed.

The design constraint that mattered was honesty: a list of file names is exactly the kind of thing that gets
mistaken for a measurement, so the rules live in the store and the page rather than in a comment.

| Rule | Where | Why |
| --- | --- | --- |
| Only an `EnumerationStatus.Complete` scan is written | `ScanCacheStore.Save` | A partial listing must never replace a good one; an empty enumeration is a legitimate answer about a phone but is not a library worth remembering in its place |
| A listing older than `ScanCacheMaxAgeDays` (default 14) is refused, with the reason logged | `CachedLibrary.IsUsableAt` | Stale history shown without a caveat is worse than an empty page |
| A listing for another backend or device is refused | `ScanCacheKey` (hashed file per pair) + a re-check of the file's own contents | One iPhone's library must never appear as another's |
| The device identity is **hashed** into the file name | `ScanCacheKey.FileName` | A serial number must not end up in a file name that archival or support tooling copies around |
| `SourceObjectId` is deliberately not persisted | `CachedFile` | The backend documents it as not durable across reconnects; resurrecting it could address a different object. The engine re-resolves by path instead |
| A corrupt or unreadable file is moved aside, never deleted, and never throws | `ScanCacheStore.TryLoad` | Caching is an optimisation; it must not be able to fail a scan |
| Nothing is written when the feature is off; what is on disk is shown and deletable | `AppSettings.ReuseLastKnownLibrary`, Settings page | A feature that keeps a list of file names owes the operator visibility and a delete |

The scope is deliberately narrow: the **Overview's tiles are never fed from the cache**. They answer "what
does the phone expose right now", and a remembered count in a live tile would be exactly the kind of quiet
lie this project keeps removing.

**Verification status.** The rules, the store's persistence behaviour (atomic replace, quarantine, inventory,
forget) and the page's provenance line are pinned by 24 tests. The Settings surface was verified **live in
the real application**: with a seeded listing the row read *"1 listing, 1,366 B"*, and pressing *Forget cached
listings* and confirming removed it — the file was gone from disk, the row read *"No listing is on disk."*
and the status bar said *"Removed 1 cached library listing(s)."* The one part that is **not** verified live is
the cached listing appearing *beside a running scan*: `ShowLastKnownLibrary` runs only after the device probe
succeeds, and the handset was detached. It is unit-tested, and until the phone is reattached no capture of
that state is claimed.

### 4.3 Continuing an unfinished session: the record instead of a re-enumeration

The cold-pass cost in section 2 is also what made an interrupted transfer expensive, in a way that had
nothing to do with the cable. Before this round, the only route back to a job that had stopped after copying
1,203 of 26,335 files was a fresh scan of the whole phone — the ~723,000 ms enumeration of one album, paid
again before a single missing byte could move (the record existed on disk but nothing read it, so the
interface had nothing to offer).

The fix is to treat the destination's own session record as the plan: continuing rebuilds the queue from the
entries that are still outstanding and fetches only those. At device scale that is the difference between

| Route | Work before the first missing byte moves |
| --- | --- |
| Re-enumerate the device (pre-1.8.0 behaviour) | 26,335 objects at ~27 ms/object ≈ **723 s** of transport, then re-derive the plan |
| Continue from the record (1.8.0) | Read an 11.1 MB JSON manifest containing 26,335 entries, then start fetching |

Seeded at device scale (a 26,335-entry record with 1,203 verified and 25,132 outstanding, 11,143,635 B on
disk), the rebuilt plan is a **25,132-file, 172 GB** plan. Two deliberate refusals keep that saving from
becoming a lie:

- Files the record marks verified are **not** re-hashed to prove them again. Re-proving them means reading
  every destination file — 106 GB of local hashing on the test handset — and the record already holds the
  digests; an operator who wants them proven again has the explicit **Verify** action. The record decides
  *what to fetch*, never *what is true*: every file the plan does carry is fetched and hashed from scratch.
- A copy whose verification was interrupted is **discarded** rather than left in the destination. Leaving it
  would make the next attempt see a size match with no recorded digest and record it as a legacy baseline at
  **size-only** strength — turning a cheaper route into a weaker claim, silently. Discarding costs one
  re-fetch and keeps the invariant that a file in the destination is either verified or absent.

The performance claim of this subsection is therefore **the enumeration that is not paid again**, measured
from a seeded record on the published build; the transfer throughput of a continued run is the same engine
and the same device leg as a first run, and no device-side speed-up is claimed for it.

**Verification status.** The record's rules, the plan rebuild, the completeness rule and the interrupted-copy
discard are pinned by 29 tests, including a real-engine flow that asserts on the backend's fetch log that
continuing fetches *exactly* the outstanding files. The surface was verified live with a seeded record (the
bar, its counts, the dismissal fingerprint, and the action becoming enabled when the handset was attached).
A transfer that is *really* cancelled on a live device and then continued is **not** in this record: it
writes real data for minutes and was not run without the operator's go-ahead.

### 4.4 Two costs the audit found in the 1.8.0 work itself (2026-10-19)

Working through the acceptance list against the code turned up two places where the new features were slower
than they looked. Both were on paths no hardware test had exercised, which is precisely why they survived.

**A continued run re-walked the phone's folders once per file.** A resumed session has no object ids to
reuse — they are deliberately not persisted, because they do not survive a reconnect — so it locates every
file by its relative path. The first implementation did that by enumerating the album and reading each
child's properties until the name matched, **once per file**. Reading one child's name is a device
round-trip, so locating *n* files in one album cost O(*n*²) round-trips: on a 10,000-photo album, tens of
millions of them before the first byte moved — a hunt, not a transfer. `Core/FolderNameIndex.cs` now reads
each folder **once per session**, keeps its children by name, and answers every later lookup from memory. It
matches case-insensitively the way the device does, it is cleared whenever the session is dropped (a
reconnect can renumber every object id), and a nameless child is never offered as a match.

Measured property (`FolderNameIndexTests`, no device required): **26,100 files across three albums cost four
folder reads** — the media root and one per album — where the old lookup would have re-read an 8,700-file
album 8,700 times. The device-side saving is not measured and is not claimed; what *is* measured is that the
number of device reads no longer depends on the number of files sharing a folder.

**The second-pass scan check compared the two listings quadratically.** "Confirm each scan with a second full
enumeration" asked, for every path, whether the whole second listing contained it — a linear scan per path.
On the 26,000-item camera roll used throughout this document that is on the order of 340 million string
comparisons, on the UI thread, immediately after a cold scan. The comparison now indexes the second listing
once and does one dictionary lookup per path (`Core/ScanStability.cs`).

| Operation (same machine, same 26,000 paths) | Before | After |
| --- | --- | --- |
| Compare two enumerations of one camera roll | **4,705 ms** | **5.7 ms** |

The old code also built its index with `ToDictionary`, which throws on a duplicate path — and a partial scan
is exactly the case that is *allowed* to carry one — so a warned listing could surface as "Scan failed".
Duplicates are now kept as what they are: incomplete, and named in a warning. The 4,705 ms figure was
obtained by re-running the removed code inside the test harness for the purpose; the replacement is pinned by
`ScanStabilityTests` (6), including the measured case.

## 5. What is deliberately not claimed

| Not measured | Why |
| --- | --- |
| The on-device speed effect of items 4 and 5, and of the 8x engine speed-up in 4.1 | The handset dropped off USB while the before/after benchmark was being set up. The engine's own throughput *is* measured (4.1), but how much of it a real transfer sees depends on the device leg, and no end-to-end device number is claimed. |
| A cold-cache first scan with the new progress line visible on screen | Completed on 2026-10-22: **55,185 files / 151.34 GiB in 23.3 min**, with no failed folders or warnings. The 2026-10-21 26,335-file / 10.3 min pass is historical. This was a live observation, not a benchmark. |
| The Overview band caught mid-scan | The band's line and state are pinned by unit and control-state tests, and the final `Scan finished` line was captured live. A mid-scan screenshot capture remains missing. |
| A device-to-disk transfer benchmark (MB/s before and after) | Needs the handset attached; the machine has no second iPhone library. The band shows measured throughput so a benchmark is a case of running one, not of building the meter. |
| A cached listing shown beside a running scan, captured live | `ShowLastKnownLibrary` runs after the successful device probe, and the handset was detached. The path and its provenance text are pinned by tests; the *store* and its Settings surface were verified live instead (section 4.2). |
| A real cancelled-then-continued transfer on a live device (4.3) | It writes real data to a real disk for minutes and was not run without the operator's go-ahead. The record, the plan rebuild, the completeness rule, the interrupted-copy discard and the interface are pinned by tests, and the surface was verified live against a seeded record with the handset attached; the device-facing leg of the continuation is not claimed. |
| Windows 11 rendering (DWM rounded corners, native dark scrollbars, `Application.SetColorMode`) | The test machine is Windows 10 Pro 19045. |
| Audible UI sounds | The harness cannot listen to the machine's speakers. |

## 6. How to reproduce the measurements

```powershell
# Tests: the deterministic suite needs no phone.
dotnet test tests\I-Total.Transfer.Tests -c Release

# Engine throughput, with the printed numbers (MB/s, files/s, progress counts).
dotnet test tests\I-Total.Transfer.Tests -c Release --filter "FullyQualifiedName~EngineThroughputBenchmarkTests" --logger "console;verbosity=detailed"

# Hardware tests, with a handset attached and unlocked.
$env:ITOTAL_HARDWARE_TESTS = "1"
dotnet test tests\I-Total.Transfer.Tests -c Release

# Enumeration cost of one album (STA is required for Shell COM).
# See the helper used for the 723,017 ms figure in the session notes: it times
# GetFolder.Items() and .Count for one album folder and nothing else.
```

## 7. Sources consulted

Platform constraints and API behaviour:

- Microsoft Learn — [File buffering and I/O performance](https://learn.microsoft.com/en-us/windows/win32/fileio/file-buffering):
  buffer sizes interact with the cache manager, so the copy buffer is a *tunable*, not "bigger is better".
- Microsoft Learn — [I/O completion ports](https://learn.microsoft.com/en-us/windows/win32/fileio/i-o-completion-ports):
  overlapped completion is the Windows-native way to keep many reads in flight. It applies to the
  *destination* file, not to the device: a WPD resource stream is a serialised COM conversation.
- Microsoft Learn — [Windows Portable Devices portal](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/portal):
  WPD is a device-access framework over MTP/PTP; properties are fetched per object and the API is not a
  filesystem. This is the documented basis for "the round trip is the cost".
- Microsoft Learn — [Processes and threads](https://learn.microsoft.com/en-us/windows/win32/procthread/about-processes-and-threads)
  and the .NET `SynchronizationContext` behaviour behind the test deadlock: a context whose `Post` needs a
  message pump will deadlock an `async` continuation on a thread that never pumps.
- Apple — [Transfer photos from iPhone to a computer](https://support.apple.com/en-gb/HT201302): Windows
  access to iPhone media is through the device/Photos interface, not arbitrary filesystem access.
- Android Developers — [Shared media / Storage Access Framework](https://developer.android.com/training/data-storage/shared/media):
  Android's media access model differs from iPhone's; a future Android backend must not assume WPD
  semantics carry over.

Design references for a copy engine (patterns only — nothing copied, and none of it lifts the device cost):

- TeraCopy (`teracopy.org`, Code Sector): queued jobs, verification, retry and pause/resume as product
  behaviours worth matching. Its parallel copy engine is a filesystem technique; it does not apply to a
  single serialised MTP session, which is why concurrency is pinned to 1 here.
- [libimobiledevice](https://github.com/libimobiledevice/libimobiledevice): an alternative iPhone access
  path. Its Windows packaging, licences and capability limits are recorded in `docs/RESEARCH-SOURCES.md`;
  the AFC backend stays inert until a pinned, hash-verified runtime is present.

## 8. Result

- Release build: **0 warnings / 0 errors**.
- Tests: **460 passed, 0 failed, 2 skipped (opt-in hardware), 462 total** in about thirty seconds
  without a phone. That total includes the 26,400-entry scan, the 300,000-entry nested walk, the
  context-capture
  regression tests, the engine-throughput benchmarks, the scan-cache rules and persistence, the
  unfinished-session recovery rules, the real-engine continuation flow, the recovery surface's layout
  contract, log redaction in both directions, the second-pass scan comparison, the resumed-run path
  lookup, and (added after the performance work) the wireless credential/receiver/page suites, the
  hand-written QR encoder with an independent decoder, and the USB-evidence diagnosis with the diagnostics
  page and support-bundle contracts.
- Live: detection confirmed, **55,185 files / 151.34 GiB** enumerated as `Complete` in 23.3 min.
- Live: Android device-to-disk, one 612,420,912-byte MP4, native WPD, hash-verified source-to-destination:
  | Buffer | Elapsed | Throughput | CPU | Peak working set |
  | ---: | ---: | ---: | ---: | ---: |
  | 64 KiB | 18.246 s | 33.56 MB/s | 2.94 s | 70.1 MB |
  | 256 KiB | 16.911 s | 36.21 MB/s | 4.39 s | 77.4 MB |
  | 1 MiB | 16.369 s | 37.41 MB/s | 5.45 s | 77.4 MB |
  | 4 MiB | 16.187 s | 37.83 MB/s | 6.50 s | 85.5 MB |
  One run per buffer, one file; this is a device observation, not a multi-file benchmark or a statistical claim.
- Live: Android mixed-size batch, same device/native WPD, 30 files / 111,135,111 bytes
  (10 tiny 8-24 B, 10 about 1 MB, 10 about 10 MB): **4.681 s, 6.41 files/s, 23.74 MB/s**,
  CPU **1.55 s**, peak working set **69.9 MB**, 30/30 copied and `SourceAndDestinationHash`.
  This is one batch, not a statistical benchmark.
- Live: the scan-cache Settings surface read the real cache folder and removed a listing on request.
- Engine throughput on local storage, measured: **≈ 8x faster on small files** (87 → ≈ 700 files/s) with
  verification level unchanged, and large files unchanged at ~600 MiB/s. Per-file engine overhead is now
  ≈ 1.5 ms against ≈ 27 ms of transport cost per object.
- Scanning now reports a moving, honest status line on both the Overview and the status bar; the Overview
  band distinguishes a sweeping scan from a measured copy; the transfer engine no longer pays for a
  redundant staged read-back or an unnecessary per-file existence probe; the library can no longer
  deadlock a blocking host; and two Settings controls that could not work have been made real or made
  visibly inert.
- An interrupted transfer is now shown and can be continued **without re-enumerating the phone**: the
  destination's session record is the plan, so a 26,335-entry record with 25,132 outstanding rebuilds as a
  25,132-file / 172 GB plan instead of paying the ~723 s cold enumeration again. Nothing is trusted on the
  strength of the record — every planned file is fetched and hashed from scratch — and a run can only be
  declared complete when nothing anywhere in the session is failed, unreadable, unverified or untried.
- An audit of the acceptance list (2026-10-19) found and fixed three defects on paths no hardware test had
  reached, two of them cost: a resumed run re-read the album once **per file** (now one read per folder —
  26,100 files cost four reads) and the second-pass scan comparison was quadratic on the UI thread
  (**4,705 ms → 5.7 ms** for 26,000 paths); and a device **serial** was written into the log — which ships
  inside the backup folder and inside a diagnostics bundle whose README said it carried no full device
  identifiers — while the credential rule consumed a label and left its value. All three are pinned now
  (`FolderNameIndexTests`, `ScanStabilityTests`, `LogRedactionTests`).
- Not yet done, and deliberately not claimed: the **transfer-throughput** work itself. The handset was
  detached before a device-to-disk benchmark could be run, so no before/after MB/s figure exists. What is
  in place is the instrumentation to produce one (measured throughput, elapsed and remaining time are
  already surfaced) and the two measured-cost fixes listed above.

## 2026-10-29 candidate: no performance claim

The master-directive changes do not touch the copy buffer policy, the staging loop's read/write pattern or
the hashing path. The one resource-relevant change is COM cleanup in the WPD enumeration path: the folder
enumerator, the per-object key collection and the per-object values object are now released deterministically
instead of accumulating until finalization. That should reduce provider-side handle pressure on a large
walk, but **it was not measured** — no phone was attached and no benchmark was run for this candidate.
No speed claim is made here.