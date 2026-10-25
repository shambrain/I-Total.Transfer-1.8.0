# Release test plan

The automated tests live in `tests/I-Total.Transfer.Tests`. They split deliberately in two:

- **Deterministic tests** are the default and need no hardware. They must pass on any machine, with or
  without a phone attached, and they must never wait on a device — that is why the suite completes in
  about a second and why a hanging test is treated as a product defect rather than a flaky one.
- **Hardware tests** talk to a real handset, so they are opt-in and time-bounded. Without the switch they
  are reported **inconclusive**: not passed, not failed.

```powershell
# Default: deterministic, no phone required.
dotnet test .\tests\I-Total.Transfer.Tests\I-Total.Transfer.Tests.csproj -c Release

# Hardware tests only, with a handset attached and unlocked.
$env:ITOTAL_HARDWARE_TESTS = "1"
dotnet test .\tests\I-Total.Transfer.Tests\I-Total.Transfer.Tests.csproj -c Release
```

## Automated coverage added for the 1.8.0 work

| Suite | What it pins |
| --- | --- |
| `LargeLibraryStressTests` | A **single folder with 26,400 entries** scans completely, reports progress, and stays linear; cancellation during that scan is honoured promptly; 300,000 nested entries aggregate and filter without a quadratic step |
| `ScanProgressReporterTests` | Reports are throttled, always emitted on a folder change, and formatted as the status line the operator reads |
| `CaptionButtonContrastTests` | Caption glyphs meet 3:1 on every theme, plus a guard that fails if the Light band stops being light |
| `TransferEngineTests` (new cases) | A source-hashing backend records no staged digest and still reaches `SourceAndDestinationHash`; a Shell-like backend still reads the payload back |
| `ShellBackendApartmentTests` | Opt-in; the MTA round trip completes and disposal is clean |
| `ContextFreeExecutionTests` | A blocking caller that holds a pump-required `SynchronizationContext` still scans, copies and cancels without deadlocking, and leaves nothing queued on that context |
| `OperationStatusTests` | The operation model: unmeasured phases sweep, measured ones are determinate, a total beyond `int.MaxValue` still yields a correct permille position, and idle/paused/stopped are distinct |
| `DashboardOperationBandTests` | The real controls: the band names the phase, shows no fabricated progress, and renders in both themes |
| `ThrottledProgressTests` | Updates are coalesced to the interval (fast updates, the interval boundary, a frozen clock under 2,000 parallel reports), the newest value is the one delivered, and a zero interval forwards everything |
| `ScanCacheStoreTests` | Persistence rules: a complete listing round-trips; writing leaves no temporary file and replaces in place; a partial or empty scan never replaces a good listing; another device/backend, an older schema and an over-age listing are each refused with a reason; a corrupt file is quarantined rather than used or deleted, and does not stop a later save; an unwritable folder is reported rather than thrown; the inventory reports unreadable files too; forget/forget-all behave |
| `ScanCacheTests` | The rules and the change report: the key prefers stable id → serial → model and never puts the device identity in the file name; an incomplete, empty or older-schema listing is never usable; the age limit is inclusive; the backend object id is deliberately not persisted while the timestamp is; the diff counts additions, removals and size changes, matches case-insensitively, and quotes at most five examples |
| `LibraryPageProvenanceTests` | A live listing shows plain counts with no provenance; a cached listing leads with *Last known library (captured …)* and its timestamp; clearing it returns the line to plain counts; and the provenance survives the empty state, so "nothing here" and "the filter hides everything" remain distinguishable |
| `EngineThroughputBenchmarkTests` | The real engine's throughput and progress behaviour on local storage - the benchmark whose first run exposed the per-file overhead; it also guards that a redundant full pass over the payload cannot come back |
| `SessionRecoveryTests` | The recovery rules: every entry state is counted and outstanding bytes cover exactly the non-terminal work; finished / cancelled / aborted / running / never-recorded statuses are read as meant (`INCOMPLETE — NOT A VERIFIED COMPLETE BACKUP` is not mistaken for success because it contains the word COMPLETE); a session with nothing outstanding is not offered; a record for another folder, a newer schema, a damaged file and an unreadable record are each refused with a reason and a damaged record is left in place; the plan carries only outstanding entries with their own sizes, extensions and timestamps and deliberately no object identifiers; and the compact summary stays short enough for a fixed-height bar while still separating what is done from what is not |
| `RecoveryFlowTests` | The new status stamps and the continuation through the **real engine** (mock backend): the session is stamped `RUNNING` **before the first byte is fetched**; a Stop writes `CANCELLED` recording what finished, failed and was never reached, and leaves nothing unverified on disk; a backend fault writes `ABORTED` and stays continuable; continuing fetches **exactly** the outstanding files (asserted on the backend's fetch log), leaves the verified file untouched, and reaches `HASH-VERIFIED COMPLETE`; a path that can never be copied keeps the session INCOMPLETE rather than letting a resumed pass announce a complete backup; and a stopped session can still be driven to full source-to-destination verification |
| `DashboardRecoveryTests` | The surface: the bar's numbers, the collapsed row when there is nothing to continue, the action visible-but-disabled with an explanation when no device is ready, the separate resume and dismissal events, the two-line message (with an ellipsis rather than a silent cut), the same button never doubled up, and the invariant that showing the bar **never squeezes the action panel** (the real clipping defect) |
| `FolderNameIndexTests` | The lookup a resumed run depends on, since object ids are not persisted: every file in an album resolves, a **folder is read once and never once per file** (26,100 files across three albums cost four reads), nested folders are followed, the `DCIM` segment names the root rather than being searched for, a missing file and a missing folder are told apart, matching ignores case, a nameless child is never the match, a miss does not poison a later hit, and clearing on a dropped session forces a fresh read |
| `ScanStabilityTests` | The second-pass comparison: identical listings agree, added/removed/resized paths are all reported and an unchanged file is not named, matching ignores case, the report is capped so a failure dialog cannot be flooded, a partial scan carrying a duplicate path does not throw, and 26,000 paths are compared in linear time |
| `LogRedactionTests` | Both directions of the redaction contract: a serial, a UDID and a JSON stable id are masked while the label survives so the line stays usable; long hex digests keep only a short prefix; a credential assignment loses its **value** and not just its label; and ordinary operational text — a scan summary, a source path, a folder merely called "Serial number photos" — is left byte-for-byte, including in the log file itself |
| `PairingStateTests` | The wireless credential model: the displayed secret is single-use and mints a **separate** session token; the secret is refused as a session token *and* a failed authorize does not spend it; expiry is enforced; rotation replaces the token and kills the old one immediately while widening nothing; revoking one session leaves the others; issuing a new secret invalidates the old; sessions are bounded; missing and unknown credentials are reported distinctly; tokens are random lowercase hex and never repeat |
| `ApprovalInboxTests` | The gate between a paired sender and the disk: an offer waits by default and is not committable; approving makes exactly that path committable; a refusal **sticks** against a retry of the same path; auto-approval is an explicit preference and says so; pending offers expire; approve-all/refuse-all cover every pending offer; path matching ignores case because Windows does; an unknown path changes nothing; clearing forgets every decision |
| `PrivateNetworkTests` | The bind policy: loopback and 10/8, 172.16/12, 192.168/16 bind; 172.15, 172.32, public, carrier-grade, link-local, `0.0.0.0`, `*`, `+` and empty are refused; the refusal **names** the reason; `localhost` is loopback and not private; the machine knows its own addresses |
| `QrCodeTests` + `QrDecoder` | The pairing QR is proven, not assumed: an independent decoder that shares no code with the encoder reads back every payload - format bits, mask, zig-zag walk and block de-interleaving - across every version the encoder claims (1-10, including the multi-block versions), plus the three finder patterns, a 4-module quiet zone, and refusal rather than truncation above the ceiling |
| `WirelessReceiverTests` | The real HTTP surface on a real loopback socket: unauthorized and secret-as-session uploads are `401` and write nothing; redeeming a secret twice is refused; an offer is visible **before any byte is written** and the request only completes on Approve (`200`) or Refuse (`403`); a disagreeing declared digest is `422` and leaves no staging file; a matching digest is accepted; traversal and `.i-total` are `400`; an existing file is renamed and the receipt names what was used; rotation retires the old token; health never echoes the secret; a public address, the wildcard and `WirelessEnabled = false` all refuse to start; restarting sweeps orphaned staging fragments |
| `WirelessPageTests` | The page contract on real controls: Start is disabled until wireless is enabled; a ticket renders both the URL and a **visible square** QR image and clearing it hides the QR; the approval list shows exactly the waiting files with their sender and hides decided ones; Approve/Refuse/Approve-all are enabled only when they can act and Approve raises the **selected** path; the auto-approve switch is reported when it changes; stopping clears the ticket and the list; settings round-trip through the page |
| `AttachmentDiagnosisTests` | What "connected" actually means, using this machine's **real** USB records as the fixture: only the media interface (`WUDFWpdMtp`) counts as a handset, so the support interfaces a PC keeps for every Apple cable it has ever seen are reported but never counted as phones; a cached iPhone **and** a cached iPad read as `CACHED RECORD ONLY (2)` with nothing attached and never as connected; an attached handset is never downgraded by older records beside it; a machine that has never seen an Apple device says so and names the driver/cable remedies; blank and duplicated names collapse and the singular/plural agrees with the count; Windows' indirect `@…inf,%key%;Readable Name` values resolve to the readable half; and the live registry read is safe, returns display names only, and never a raw instance key (which is where the serial number lives); and only devices the classifier called Apple count as attached Apple handsets, so an Android phone answering on the same portable-device bus never appears under a row titled "Apple handsets seen over USB" |
| `DiagnosticsPageTests` | The page on real controls: the USB evidence is the **first** row and its status is shown whole (`CACHED RECORD ONLY (2)`, `ATTACHED`, `NONE`); the three readings are coloured apart, so a remembered handset can read as neither a failure nor a pass; findings **wrap and their rows grow** instead of being silently cut off at the column edge (the defect this replaced); and a caller that supplies no evidence still gets exactly the check list it had |
| `DiagnosticsBundleTests` | The bundle always exists: it is produced **while the application is still holding the log open for writing**, which is the sharing-mode defect that made every export produce no file at all; a log that cannot be read costs its own file and nothing else; a serial in the log is masked while the attachment evidence survives redaction verbatim; and no log path means no log file rather than an empty one; and the licence/trial summary carries the counter and standing but neither the purchase id nor the ledger fingerprints, with 	rial.json left out |
| `DeviceFamilyTests` | Which storage rules apply to an attached device, and the asymmetry of getting it wrong: an iPhone, iPad or Apple-manufactured device is Apple; a device that names itself Android, or is named or manufactured by an Android maker, is Android; manufacturers are matched as **whole words**, so `Volkswagen` is not `LG` and `Microtek` is not `MTK`; Apple is decided before any maker list is consulted; a device that identifies as neither family is `Unknown` **and says why**; `Unknown` is the enum default, so an unprobed device never claims a family; and the two-phone preference only reorders the families and never makes one unreachable, with `AppleFirst` as the default value so a machine that predates Android support behaves exactly as before |
| `PortableStorageTests` | The Android/generic-MTP storage policy: `Android\data` and `Android\obb` are skipped while `Android` and `Android\media` are walked (WhatsApp keeps photos there); a user folder merely *called* `data` is not skipped; thumbnail caches are skipped case-insensitively; a volume keeps its own name as its destination folder, made safe for Windows without inventing a new one and never empty; a camera-only listing raises the PTP warning while any file-transfer-only folder clears it; the warning names the setting to change and asserts no cause; and an Android device advertises all four groups while Apple and unidentified devices keep the conservative two |
| `ConnectionDiagnosticsTests` | The diagnostics page describes the phone that is actually attached: an Android device gets the Android rows (family, USB mode, unlock state, exposed file set) and **none** of the Apple rows (trust state, iCloud originals, Apple software, Apple driver files, even when an Apple USB record exists); an unreadable Android device is told to check its USB mode and lock screen; an Apple device keeps its diagnosis; an unidentified device says `NOT IDENTIFIED` instead of guessing; and every row has a unique name, a status and a detail |
| `LicensingTests` | The commercial contract: the trial counts only a completed, uncancelled transfer that wrote at least one file; failure, cancellation, an empty plan, an all-already-present run and a partial run all cost nothing; three successes exhaust it and both a fresh start and a resume are judged correctly (resume always allowed); the same finished job reported twice costs one use; the counter survives a restart and the loss of either store; a read-only ledger still lets the job finish and says the count is memory-only; a clock moved back cannot reset it; a signed entitlement lifts the limit immediately and survives a restart; tampered, wrong-product, wrong-channel, other-key, expired, not-yet-valid and refunded entitlements are refused with distinct verdicts and leave the ledger alone; a signed revocation list revokes, an unsigned one does not; a build without a public key fails closed; one wireless sender session costs one job while a second session is refused; and the shipped build defaults to three jobs |
| `EntitlementCodecTests` | The signed document itself: round-trip; the signature covers the readable half (a differently-worded payload signed by another key is refused); both validity boundaries; ten kinds of malformed input become verdicts, not exceptions; a revocation list needs the issuer's signature and an edited payload invalidates it; short ids are screenshot-safe |
| `TrialLedgerTests` | Persistence: every ledger field round-trips through the file store; a corrupt file is quarantined and the other store still decides the count; merge takes the higher count and the union of jobs; negative counts, duplicates and empty entries are repaired; the HKCU store round-trips under a temporary key and is removed afterwards |
| `SteamEntitlementTests` | The Steam channel: a folder without `steam_appid.txt` is not the Steam edition; a Steam install without an entitlement names the missing file and app id; a signed entitlement bound to this app id licenses the service and shows `Licensed - Steam`; an entitlement for another app id, or a direct one, is refused and leaves the trial running |
| `LicenseSettingsTests` | The Settings row on real controls: the headline/detail round-trip; Buy is disabled when the build has no store URL; the Steam check is offered only on the Steam edition |
| `InstallerScriptTests` | The real per-user installer: install creates the executable, manifest and uninstaller plus an HKCU entry whose UninstallString names the uninstaller; upgrade replaces the executable and keeps `settings.json`/`trial.json`; uninstall through the installed copy removes the folder and key but keeps user data; a wrong hash exits 4 and installs nothing; uninstalling an unrelated folder exits 3 and deletes nothing; and every `installer/*.ps1` is pure ASCII, because Windows PowerShell 5.1 reads a BOM-less script as ANSI |
| `WinFormsTestBase` | Not a suite but a guard: control-creating test classes snapshot and restore the `SynchronizationContext` so the runner thread cannot be poisoned |

Timing bounds inside those tests are deliberately generous. Their purpose is to catch an accidental
`O(n²)`, not to benchmark the machine; the real device figures are in
[`PERFORMANCE-AUDIT.md`](PERFORMANCE-AUDIT.md).

## Required UI regression checks (no device needed)

Run these on both Windows 10 and Windows 11, at 100% and 150% scaling:

1. Switch Neon → Slate → Light → Follow Windows and back; every surface, the two banner bands, the
   group chips, the custom check boxes and the progress bar must re-theme in place with no restart.
2. Open each of the seven pages; nothing may clip, overlap or push the status bar off-screen, and the
   page band must show the right title/subtitle pair.
3. Media library: the five group chips filter using `MediaCategories` (a `.mp3` must appear under
   Music, not Files), the empty state distinguishes "nothing scanned" from "no matches", and the
   summary line reports the filtered and unfiltered counts.
4. With no device: the dashboard must say no iPhone is connected and every group tile must read
   "Connect your iPhone to check" — never a fabricated zero.
5. Keyboard: Tab reaches every action, Enter in the search box applies the filter, and `&` in a device
   name or path is rendered literally (mnemonics are disabled on labels and buttons).
6. **Borderless chrome.** The banner band is the title bar: the close / maximize / minimise buttons are
   legible against the gradient (each repaints the slice behind it), their accessible names are
   *Close* / *Maximize or restore* / *Minimize*, and the band double-clicks to zoom. The OS caption must
   not reappear at any scale factor.
7. **Maximize / restore is idempotent.** Maximize then restore must return the window to the *same*
   rectangle it left, at the same position, with no creep after repeated cycles; maximize must exactly
   fill the monitor working area with the top of the band on-screen and the taskbar respected; a
   double-click on the band and the system menu (Alt+Space) must go through the same path as the button.
8. **Drag.** A left-press on empty band space moves the window by exactly the pointer delta; pressing on
   a control in the band (e.g. the device name) must not start a drag.
9. **Snap / resize.** Dragging to a screen edge snaps, and the window can be resized from all eight edges
   and corners — borderless must not cost the standard resize and Snap behaviour.
10. **Sounds.** With *Play interface sounds* on, buttons tick and check boxes toggle; a transfer/scan
    completion plays a completion tone and an attention state plays a lower tone. With the setting off,
    the app is silent. (Confirm audibly - a screenshot cannot show it. On 2026-10-25 the synthesised PCM was verified as 16-bit mono 44.1 kHz with the expected durations and tones, and the default endpoint is active; a loopback capture was masked by a continuous full-scale signal, so a human listening check is still required.)
11. **Theme apply-on-save.** Changing the theme combo repaints the shell immediately *and* persists;
    reload the app and the saved theme is still applied. `"Dark"` in an existing `settings.json` must
    migrate to Neon.
12. **Caption buttons in every theme.** In Neon, Slate, Light and Follow Windows the minimize / maximize /
    close glyphs must be clearly legible against the band (≥ 3:1). On Light they were near-white on a
    white band (~1.06:1) before 1.8.0 — these are painted controls, so check them in a real capture, not
    in the Designer.
13. **Every Settings control does something.** "Copy buffer size" must change the buffer the copy path
    actually uses (it was silently ignored before 1.8.0). "File concurrency" must be **disabled** and
    labelled *fixed at 1 — an iPhone session serialises copies*, because no backend can copy
    concurrently: a control that cannot work must be visibly inert rather than adjustable-but-useless.
14. **A long scan must show progress and stay interactive.** Start a scan on a large library and confirm
    the status bar advances (`Scanning …`) and the window keeps responding (drag it, switch pages) until
    the scan finishes — the reported defect was a responsive but silent window.
15. **The Overview's *Current operation* band.** While scanning it must sweep indeterminately and name the
    phase ("Scanning") rather than showing a fabricated percentage; while copying it must become a
    determinate bar with counts, throughput and remaining time; when nothing is running it must read as
    idle. Check it in a real capture, and check it at a window height that is *shorter* than the page's
    content — the page must scroll rather than slice its own action panel (a real clipping defect seen at
    1.8.0 before the fix).
16. **The live view is paced, not strobed.** During a copy the queue page and the operation band must
    refresh several times a second — never so often that the window stops responding to a drag — and must
    end on the true final state from the summary, not on a stale intermediate value.
17. **The last known library is visibly history.** With a previous complete listing on disk, start a scan
    on a cold connection: the library page must fill immediately with *Last known library (captured …)*
    above the grid, in a colour that differs from the ordinary count line, while the run continues. When
    the live scan lands, that heading must disappear and the grid must become the fresh result; the log
    must report what changed. Start a *second* scan after cancelling the first — the cached rows must
    survive the cancellation with their heading intact.
18. **The listing cache is inspectable and removable.** On Settings, the *Cached listings on disk* row must
    match the folder (`%APPDATA%\I-Total Transfer\scan-cache\`). Pressing *Forget cached listings* must
    confirm, state that only names and sizes are kept, and then empty the folder — after which the row reads
    *No listing is on disk.* Turning the switch off must stop new listings being written.
19. **An unfinished session is offered, and refusing it is remembered.** Point the destination at a folder
    whose `.i-total\session.json` is unfinished (a seeded record is enough — no phone needed) and restart.
    The Overview must show the interrupted-session bar with the folder's own counts, it must **not** squeeze
    the action panel (scroll the page if the window is short), and with no handset attached **Resume
    transfer** must be visible but disabled with a message saying why. Clicking **Not now** must collapse the
    bar and name what is still outstanding in the status bar; after a restart the bar must stay away, and it
    must come back if the session makes further progress (edit the record's `UpdatedUtc` to simulate this).
    A record that cannot be parsed must produce the "could not be read" bar with no action, and the damaged
    file must still be on disk afterwards.

20. **A serial never reaches the log.** With *Redact sensitive data* left on (the default), connect a handset
    and read `I-total-transfer.log` in the destination: the device line must still name the model, iOS and
    link, and the **serial must be masked to a short prefix** while the label stays readable. Turn the switch
    off, click *Save*, connect again and confirm the line then carries the serial in full — the setting is the
    operator's, and the app must not silently override it. That same file is what a diagnostics bundle ships,
    so this check is what keeps the bundle's own promise true.

21. **The USB evidence separates "remembered" from "attached", and the bundle is actually produced.** On a
    PC that has hosted an iPhone or iPad before but has nothing attached, run *Run diagnostics*: the first
    row must read **CACHED RECORD ONLY (n)**, name the handset(s) Windows is holding (Device Manager still
    lists them, and their own properties say *"Currently, this hardware device is not connected to the
    computer"*), and must read as neither attached nor failed. Then press *Export diagnostics ZIP* on the
    **running** app: a bundle must be written — before 1.8.0 none ever was, because the app had the log
    open — with `logs-tail.txt` holding this run's own lines and `context.txt` carrying the same attachment
    verdict. Confirm nothing on the page is clipped: the detail column wraps and its rows grow.

22. **Nothing is called an iPhone that is not one, and an unidentified device is not called anything.**
    With **no** phone attached, the header and dashboard must say *Not connected* / *No device connected*
    rather than name a handset, and *Run diagnostics* must show a **Device family** row reading
    **NOT IDENTIFIED** (amber) above the Apple checks — those checks describe this PC and are still worth
    showing, but they must not be presented as this phone's verdict. Plug in anything that is not an
    iPhone or an Android device (a camera, a music player) and confirm the device message names it
    (*"…but Windows lists 1 portable device that identified as neither: …"*) instead of claiming nothing is
    attached — "no device found" beside a device Explorer can see is the confusion class this row removes.
    With an Android phone attached, the same page must show the **Android** family, **USB mode** and
    **Unlock state** rows and *no* trust-state, iCloud, Apple-software or Apple-driver rows, and the
    dashboard must say *Phone ready* / *Unlock the phone* rather than *iPhone ready* / *Unlock iPhone*.
    (The no-device half of this check is verifiable on this machine now; the Android half needs the phone.)

## Required hardware integration tests

Do not label a release device-tested until these tests pass on actual Windows 10 and Windows 11 machines and at least one physical iPhone:

1. iPhone absent, locked, not trusted, and ready states.
2. **Both camera-roll layouts.** Detection must reach the media on a handset that publishes a `DCIM`
   folder *and* on one that publishes the albums directly in the storage container (an iPhone 13 Pro on
   current iOS does the latter: `Internal Storage` contains `202003_a`, `202606_a`, `202608_b`,
   `201901_a`, `202608_a` and no `DCIM`). Confirm the scan lists the same files and that
   *preserve device folder structure* writes `DCIM\<album>\<file>` in both cases.
3. **Probe latency.** With a large camera roll attached (>20,000 files), a connection check must stay
   fast — it resolves the media root and reads container child names only; it must never walk file
   properties. Time it.
4. Charge-only cable, damaged cable, hub disconnect, and reconnect during scan/copy.
5. Folder enumeration exception: scan must fail, never return a complete plan.
6. 0-file listing: must not be accepted as a successful backup.
7. 26,000+ items and a multi-gigabyte video; monitor memory and scan/copy latency.
8. iOS reports a known file size; destination reaches the exact expected size and is unlocked before finalization.
9. Zero-byte file; should not hang forever.
10. Destination has a same-size file with different content: current Shell backend can only report size match, so the test must document this limitation and must not claim cryptographic integrity.
11. Destination collision with a different size: original file must remain untouched and the item must be reported failed.
12. Destination disk full and destination permission denied.
13. Cancel while waiting for a file; the final destination must not contain an incomplete final file.
14. Resume after process termination; already completed files are preserved and remaining items retry.
15. Library changes between scan and copy; missing source item is reported as failed.
16. iCloud Photos with Optimize iPhone Storage enabled and disabled; confirm that not all cloud-only originals are necessarily exposed over USB.
17. Live Photo pairs (HEIC/JPEG + MOV), edited photos, AAE sidecars, duplicate filenames, Unicode names, and long paths.
18. Verify that every incomplete/failed session reports INCOMPLETE and a fully size-matched session says SIZE-VERIFIED, not cryptographically verified.
19. **A cold-cache first scan is visible, and stoppable.** Reconnect a handset so the driver cache is
    cold, then scan. The status bar must advance through
    `Scanning <folder> · N files · X GB · N files/s · mm:ss elapsed` for the whole run — ten minutes of
    legitimate enumeration with no visible progress *was* the reported defect. Cancel mid-run and
    confirm the window stayed interactive throughout, and that the cancelled scan is never reported as
    complete.
20. **Copy overhead A/B (outstanding).** On a reconnected handset, time a bounded transfer (files
    ≤ 100 KB is a good per-file-overhead probe) before and after the 1.8.0 engine changes, with a fresh
    destination each time. The changes are the removal of the per-file object-existence probe and of the
    redundant staged read-back; the expected saving is one device round trip per file plus one full read
    pass over the data. This is **not yet measured** — the handset dropped off USB while the benchmark
    was being set up (see `PERFORMANCE-AUDIT.md` §5). The pre-change binary is kept at
    `%TEMP%\opencode\itotal-baseline-before-engine-optimisations.exe` for exactly this run.
21. **Conflict policy holds on a second pass.** With a populated destination, re-run the same transfer
    and confirm the configured policy behaves as stated (skip / keep both / replace only if different /
    replace only if newer), that nothing already verified is overwritten, and that a same-size file with
    no saved digest is reported **unverifiable** rather than verified.
22. **A real interrupted transfer is offered and can be continued (live pass 2026-10-24 on Android; per-file re-read timing and refusal cases outstanding).** Start a real transfer to
    a scratch destination, press **Stop** part-way, and confirm: the Overview shows the interrupted-session
    bar with counts that match the folder; the log records `CANCELLED`; and every file the run reported as
    verified is present and complete. Then reattach/unlock and press **Resume transfer** and confirm from
    the log that the engine fetches **only** the outstanding files — the verified files must not be re-read
    from the device or re-hashed — and that the run reaches a full source-to-destination verification and
    the session is only then declared complete. Also check the two refusal cases: a copy cancelled in the
    gap between commit and hash must leave **no** file behind (the entry stays failed), and a session with
    an entry that can never be copied must stay `INCOMPLETE` even when everything else verifies. This is
    the device-facing leg that the seeded-record live check does **not** cover; see
    [`RECOVERY.md`](RECOVERY.md) §6. **Time the continuation**: with tens of thousands of files outstanding,
    the first bytes must move within seconds of pressing Resume. A stall before the first byte means the path
    lookup is walking the album per file again, which is the defect `Core/FolderNameIndex.cs` was written to
    remove - the tests pin the logic, but only the device can confirm the saving.


    On 2026-10-24 the Android drill stopped a 612,420,912-byte copy, showed the recovery bar and resumed the
    one outstanding file to source/destination SHA-256 equality. The assertion that verified files are not
    re-read and the two refusal cases were not separately observed on hardware.
23. **A real iOS sender delivers over the LAN (outstanding).** With the handset on the same private network,
    build the Shortcut in [`WIRELESS.md`](WIRELESS.md) 5 and confirm the whole contract against a real client:
    the QR scans and `/pair` returns a session token; a second scan of the **same** ticket fails, so a
    screenshot is not a reusable credential; the first upload appears in the approval list with the sender's
    address and **nothing lands in the destination** until Approve is pressed; pressing Refuse returns `403` and
    still writes nothing; a deliberate retry of a refused path is refused again; an interrupted upload leaves no
    `.partial` file behind and re-running Start sweeps it; and `/health` reports `tls:false` honestly. Then bind
    a certificate with the elevated `netsh http add sslcert` from `WIRELESS.md` 6, restart, and confirm the
    status line, the log and `/health` all switch to TLS *and* the ticket URL becomes `https://` - the exact
    mismatch the previous build shipped. Record whichever sender actually worked, since "an iPhone can push to
    this receiver" is otherwise an assumption.

24. **The attached branch of the diagnosis on a live handset (live passes 2026-10-21 and 2026-10-24;
    locked/unplugged variants outstanding).** With both phones attached, *Run diagnostics* led with the
    selected family: Apple `ATTACHED`, family `APPLE`, software `DETECTED`, drivers `FOUND`, trust
    `ACCESSIBLE`; the summary named both handsets. The AndroidFirst bundle also named both. The earlier
    defective row `Attached and answering: HUAWEI Y7 Prime 2018` under the Apple heading was fixed by the
    family filter. Repeated on 2026-10-24 with the current build: 10 Apple checks and the summary
    `Attached: HUAWEI Y7 Prime 2018 (Android), Apple iPhone (Apple).` With only the HUAWEI attached, the same
    page showed its 8 Android rows and named only the HUAWEI. The 2026-10-24 run also exposed
    `Native AFC runtime: VERIFIED` with no pinned binary present; a repeat run after the fix showed `NOT PRESENT`
    (`AMissingAfcRuntimeIsNotReportedAsVerified`). Still to test: attach but **lock** the phone and confirm
    the row remains ATTACHED while media rows explain the lock, then unplug and confirm the row flips to
    CACHED RECORD ONLY with the same name and the page stays readable. The cross-device scan defect found on
    2026-10-21 is covered by check 27/BT08.
25. **An Android phone, end to end (live walk/copy 2026-10-20; cancel/PTP checked 2026-10-24; lock not applicable on MTP).** A HUAWEI Y7
    Prime 2018 was walked and copied through native WPD: **4,103 files / 15,612,056,299 bytes (14.54 GiB)**, on 2026-10-22 (the 2026-10-20 4,106-file / 15.01 GiB listing is historical),
    `Complete`; the filtered copy planned 520, with **412 source-and-destination SHA-256 verified, 108
    `COPIED-UNVERIFIABLE` (0-byte `Sounds\.pictures` files), 0 failed, 3,586 skipped**, ending
    `COPIED WITH UNVERIFIABLE FILES - NOT A VERIFIED COMPLETE BACKUP`. Resume preserved the 108 unverified
    entries with 0 failures, and the trial stayed at 0 jobs. The clauses below that were not covered by that
    run remain to test:
    - the device is identified as Android and the message names the phone, not an iPhone;
    - **every storage volume** is listed (internal storage *and* an SD card if fitted), each destination
      folder carrying the volume's own name, and a file on the card must not collide with the same path on
      internal storage;
    - Music and Files are **available** groups, and a `.mp3` in `Music` and a `.pdf` in `Download` actually
      arrive; the camera roll arrives under `DCIM\Camera`;
    - `Android\data` and `Android\obb` are **skipped without errors** in the report, while
      `Android\media\...` (e.g. WhatsApp photos) *is* enumerated — check the per-folder report names the
      skip rather than logging a failure per app;
    - with the phone then set to **Transfer photos (PTP)** and re-scanned, the scan must carry the
      photo-transfer warning naming the setting to change, and must not present the reduced listing as a
      complete backup;
    - lock the screen mid-scan and confirm the run degrades to an honest failure/incomplete status rather
      than a silent short listing, and that re-unlocking and re-scanning recovers;
    - record the real enumeration rate, file count and byte total, then compare against the phone's own
      Files app: any shortfall is Android's MTP view and must be stated as such, never as "everything was
      backed up".
    The walk (4,103 files on 2026-10-22) and the copy are single observations, not benchmarks. A
    612,420,912-byte MP4 was cancelled mid-copy and resumed to source/destination SHA-256 equality;
    PTP mode now shows the camera-only warning; a lock does not disable MTP, so no abort is expected; the single-file buffer sweep is recorded above; no
    multi-file Android throughput number may be published as a device benchmark.

26. **A phone unplugged mid-scan must not produce a complete scan (LIVE PASS 2026-10-24; fixed after a live failure).**
    Start a scan of a large camera roll and pull the cable while it is still walking. Expected after the fix:
    the run stops by itself with *the phone stopped answering during the scan* naming the folder and the
    reconnect instruction; the library shows no new "complete" listing; the scan cache is not overwritten;
    and no per-object warning flood appears. Before the fix the same action produced `status=Complete`,
    thousands of `Shutdown was already called on this object` warnings and a cached listing missing the tail
    of the tree (observed 2026-10-20). Also check the weaker case: one genuinely unreadable object makes the
    scan `Incomplete` and is named in the report, rather than being folded into a complete result.

27. **Two phones attached: the preference decides, the other is named (live pass 2026-10-21; disappearance drill LIVE PASS 2026-10-24).**
    With both attached, `AndroidFirst` selected the HUAWEI and `AppleFirst` selected the Apple iPhone; the
    connection message and Diagnostics named both. Choosing a family that is not attached still reads the
    attached phone (the preference only reorders). The same session found the cross-device scan defect: the
    probe could select one phone and the scan re-enumerate to the other, and the scan cache was keyed by the
    probe. Fixed by binding the backend (`DeviceSelectionGuard`), carrying `ScanResult.Family`/`StableId`,
    refusing a mismatch in `MainForm` before `_scan`, and refusing it again in the cache writer.
    **Still to test:** start a scan for one phone, make it stop answering while the other stays attached, and
    confirm the run aborts with "the other attached phone was not used" and writes no cache (matrix BT08).
28. **Licence activation and refusal on the published build (UI part live-verified 2026-10-20; activation blocked).** With no phone attached, open
    Settings > Licence at 150 % scaling and confirm the row reads *Trial — 3 of 3 transfer jobs left* with the
    rule that defines a job, that the row is reachable by scrolling and is not clipped, and that **Buy** is
    disabled with its reason when the build has no store URL. Use *Enter licence…* with (a) a corrupted file,
    (b) a file signed for another product and (c) an unsigned file: each must be refused with its own message
    and must leave the counter unchanged. Then run three successful mock/simulated jobs (or complete the real
    check 6 three times) and confirm the fourth **new** start is refused, while **Resume transfer** on the
    seeded unfinished session is still enabled. Activating a genuinely issued entitlement (valid, then
    refunded/revoked) is **blocked until the production key pair and backend exist**; record it as pending,
    never as passed. The first half of this check was performed on the published build: the row read
    *Trial — 3 of 3 transfer jobs left* with its explanation visible, Enter licence was enabled, Buy was
    disabled, and the section was reachable and unclipped at 150 % (screenshot
    %TEMP%\opencode\settings-licence.png); refusal of corrupted/foreign/unsigned files and the three-job
    exhaustion remain to be re-run when a production entitlement exists.

29. **Wireless trial gate (blocked until a sender exists).** With the trial exhausted, redeem a pairing secret
    and POST one file: the receiver must answer **402** with the trial message, write nothing, and leave the
    approval list empty; the sender must not be able to bypass it by retrying. With one job left, the first
    committed file must consume it and a **second file from the same session** must still be accepted without
    consuming another. This can be driven with the loopback client now; a real iPhone sender remains pending.

30. **Installer and uninstaller on a clean profile (automated on this machine, repeat on Windows 11).** Install
    from the portable ZIP, confirm no UAC prompt, no firewall rule and the Start Menu shortcut, run the app,
    then upgrade with a newer package and confirm settings/trial/licence survive. Uninstall and confirm the
    folder and HKCU entry are gone while `%APPDATA%\I-Total Transfer` remains. Repeat with `-RemoveUserData`
    and confirm the data folder goes only because it carries this product's files. Then repeat on Windows 11.

31. **The support bundle carries the trial counter, not the buyer (live-verified 2026-10-20).** With no phone
    attached, press *Export diagnostics ZIP* on the Diagnostics page and open the ZIP. `licence.txt` must show
    the standing, channel and jobs used/limit/remaining; `trial.json` must be absent; no purchase identifier or
    24-hex fingerprint may appear in any entry; and the README must state the same boundary. With the trial
    exhausted, repeat and confirm the standing reads `TrialExhausted` and that exporting did not change the
    counter.
