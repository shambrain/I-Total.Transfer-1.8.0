# I-total transfer by Deveg — Architecture Review

**Review date:** 2026-10-16  
**Scope:** iPhone camera-roll media import to Windows over USB; correctness and recoverability take priority over optimistic throughput claims.

## Executive decision

Use a backend abstraction with **Windows Portable Devices (WPD) / Shell compatibility for the currently exposed photo collection**, and treat libimobiledevice/AFC as an optional, separately validated backend—not as a guaranteed replacement for the Windows photo-import path. Implement native WPD resource-stream copying before calling this a production release. Keep the current Shell backend as a compatibility implementation until native WPD behavior is validated on physical iPhones and supported iOS versions.

Do not promise that any USB backend can expose every Photos-library asset. iCloud-optimized originals may not be stored locally, and iOS only exposes the content permitted by the selected device service/protocol. The app must report the scope it observed, not claim it has independently counted the complete private Photos library.

## Important corrections to the supplied v3 concept

1. **Do not call the iPhone Windows connection “MTP” as a universal fact.** Windows exposes portable devices through WPD, which abstracts device transports and object models. Microsoft describes `IPortableDeviceContent::EnumObjects` as enumerating immediate child objects and returning object IDs. Those IDs are not guaranteed to be human-readable folder names. Implement by object ID + queried properties; never assume IDs equal paths.
2. **AFC is not a guaranteed full-Photos-library API.** `ifuse` documents that normal AFC access is rooted in the device's `/var/mobile/Media` area, not the unrestricted filesystem. AFC may expose useful media paths on supported configurations, but access to all current camera assets must be proven by device tests. Do not read private Photos databases or assume their schema/access is stable.
3. **The old `libimobiledevice-win32/imobiledevice-net` repository is archived.** The newer fork describes itself as in maintenance mode and warns that the latest commit may not compile. Do not pin a production product to that binding without a maintained, reproducible native dependency build and integration tests.
4. **Do not promise 5–10× speed or fixed MB/s values.** Throughput depends on iPhone/iOS version, host USB path, device service, file-size distribution, cable, destination storage and protocol. Benchmark on physical devices. A larger buffer can reduce overhead, but it does not guarantee higher throughput.
5. **Do not parallelize a single AFC client blindly.** Native library/client thread-safety and USB-multiplexing behavior must be verified. Start with one serialized reader per device connection; tune concurrency only using separate supported sessions and repeatable benchmarks.
6. **Two-pass enumeration is a consistency check, not a proof of completeness.** It can detect changes between scans but doubles enumeration cost and still cannot reveal files the device did not expose. Make it optional/diagnostic, not an unconditional 20-minute penalty.
7. **USB 3.x port presence is not negotiated link speed.** A blue port or USB 3 controller is not evidence that the iPhone is currently negotiating that speed. Report “USB path capability” only when supported by a trustworthy device/driver API; otherwise show “unknown”.
8. **iCloud optimization cannot be reliably inferred from DCIM counts alone.** A low DCIM count is a warning signal, not proof. The app cannot inspect the entire private Photos library through normal USB media access. Explain Apple's “Download and Keep Originals” setting and offer a user-confirmed readiness checklist.
9. **Wireless transfer requires a source-side sender.** A LAN HTTP receiver and QR code cannot autonomously enumerate an iPhone Photos library. Wireless must use an explicitly installed/configured iOS Shortcut or companion app that selects and sends files; the receiver must authenticate, restrict network exposure, validate paths/sizes, expire pairing tokens and verify received files.
10. **Do not silently install drivers or change security settings.** Use official Apple/Microsoft distribution links, detect likely missing components, request user consent, and re-run diagnostics after installation. Do not bundle unverified drivers.

## Layered target architecture

```text
WinUI 3 presentation (Transfer / Queue / Verification / Diagnostics / Settings)
    -> Application orchestrator + typed state machine
        -> Device preflight / device watcher
        -> Enumeration service + scan manifest + stability comparison
        -> Persistent queue (SQLite, transactional state transitions)
        -> Transfer scheduler (one active operation per backend unless proven safe)
        -> ITransferBackend abstraction
             -> WPD native resource-stream backend (primary Windows photo-import path)
             -> AFC/libimobiledevice backend (optional; capability-probed)
             -> Shell CopyHere backend (legacy fallback only)
        -> Destination writer (staging file, flush, verify, atomic finalize)
        -> Verification/report service (CSV + JSON + machine-readable error codes)
        -> Structured rolling logs
```

## Required invariants

- A scan is either `Complete`, `Unstable`, or `Failed`; never turn an exception/inaccessible folder into an empty successful scan.
- Record every source object seen and every folder/object that could not be queried. Do not assume a second scan proves all iPhone assets were exposed.
- Persist a file state transition before/after consequential I/O: `Discovered -> Queued -> Copying -> Copied -> SizeVerified/HashVerified`, or `Failed/Inaccessible/Collision/Skipped`.
- Write to a unique `.partial` staging file. On cancellation/disconnect, retain or clean it according to a documented recovery policy. Finalize with same-volume atomic rename only after the stream is closed and the selected verification level passes.
- A destination file with matching size is only `SizeVerified`; never call it cryptographically verified. A source hash is only available if the backend can read the source bytes to hash them (which costs another read) or provides a trusted source hash. Do not invent a source hash.
- Keep both source object identity and relative destination path. Sanitize every path segment and prevent traversal, rooted paths, alternate data streams, reserved Windows names and collisions.
- “Completed” means all items in a successful scan plan have a terminal successful state and no failed/inaccessible/unverifiable items under the selected verification policy. It does **not** mean the entire private Photos library was backed up unless the app can independently establish that scope—which current USB media interfaces generally cannot.
- The app never deletes from the iPhone. Copy-only is the default and only transfer direction in v1.
- Cancellation and device disconnect must not mark the session complete. Restart must recover from SQLite and manifests without overwriting unrelated files.

## Data model

### Sessions
`Id`, `DeviceStableId` (when available), `DeviceName`, `Backend`, `DestinationRoot`, `StartedUtc`, `FinishedUtc`, `State`, `ScanId`, `PlannedFiles`, `PlannedBytes`, `CopiedFiles`, `SizeVerifiedFiles`, `HashVerifiedFiles`, `SkippedFiles`, `FailedFiles`, `InaccessibleFiles`, `WarningsJson`, `LastErrorCode`.

### ScanObjects
`ScanId`, `SourceObjectId`, `ParentObjectId`, `SourceName`, `RelativePath`, `ObjectKind`, `ReportedSize`, `ReportedModifiedUtc`, `EnumerationPass`, `Status`, `ErrorCode`, `ErrorText`.

Unique key: `(ScanId, SourceObjectId)` when IDs are stable; additionally detect duplicate normalized relative paths case-insensitively.

### TransferItems
`Id`, `SessionId`, `SourceObjectId`, `RelativePath`, `ExpectedBytes`, `State`, `Attempts`, `DestinationPath`, `StagingPath`, `StartedUtc`, `FinishedUtc`, `VerifiedUtc`, `VerificationLevel`, `DestinationHash`, `SourceHash`, `LastErrorCode`, `LastErrorText`.

Unique key: `(SessionId, RelativePath)`; do not overwrite a different source identity just because the path matches.

### Errors / Events
Timestamped structured events with severity, stable error code, session/file IDs, backend, retryability, user-facing remediation and diagnostic details. Avoid recording personal photo content or unnecessary identifiers.

## Transfer and verification pipeline

1. Preflight: device detected, unlocked/trusted/access probe succeeds, backend selected, destination writable, free space sufficient with reserve, destination is not inside the app's staging tree, no unsupported filesystem constraints.
2. Enumerate immediate children recursively by backend object IDs. Query object properties in batches where supported. Preserve source IDs and raw names; never synthesize missing objects.
3. Optionally repeat the scan and compare source IDs, paths and reported sizes. If changed, mark `Unstable`; ask the user to rescan or explicitly proceed with a warning.
4. Persist the plan in SQLite transactionally. Show planned file count, reported bytes, inaccessible objects, unknown sizes and scan duration before enabling Start.
5. Copy sequentially per device connection initially. Read source stream into a pooled 256 KiB–1 MiB buffer (configurable/benchmark-driven), write destination `.partial`, count bytes, report progress, honor cancellation, and handle short reads/writes correctly.
6. Close and flush output. Verify expected size. If both sides can be hashed, compare SHA-256; otherwise explicitly label size-only verification. Hashing the destination alone is a local integrity baseline, not source identity proof.
7. Atomically rename `.partial` into final path; persist success. Preserve existing files by default. Offer overwrite only with explicit confirmation and a backup/rollback strategy.
8. Reconcile plan against destination and export summary, CSV, JSON, logs and unresolved errors. Keep failed items resumable.

## Performance strategy

- Eliminate repeated full-folder enumeration per file. Resolve and cache folder/object handles where safe; prefer backend-native object IDs over filename lookup.
- Eliminate O(n²) manifest lookups by indexing paths in a case-insensitive dictionary and using indexed SQLite queries/bulk transactions.
- Batch property reads and DB writes where backend APIs support them; update the GUI at a bounded cadence (e.g. 5–10 updates/sec), not per 4 KiB read.
- Use `ArrayPool<byte>` or pooled buffers; avoid allocating a new buffer for every chunk.
- Start with one active copy per device connection. Benchmark 64 KiB, 256 KiB, 1 MiB and 4 MiB buffers; choose a measured default. Do not promise a fixed speed.
- Do not run a second complete scan automatically after a long first scan. Offer “Check scan stability” separately and record its extra duration.
- Show scan rate, files/sec, copy throughput, current file, retry count, elapsed time and ETA only when enough samples exist. Smooth speed over a rolling window.

## Backend plan

### WPD (native)
Use `IPortableDeviceManager` to enumerate device IDs; connect through `IPortableDevice`; use `IPortableDeviceContent.EnumObjects` for object IDs; query properties using `IPortableDeviceProperties`; read file data using `IPortableDeviceResources.GetStream` and the provider's optimal read buffer size when supplied. Release COM interfaces deterministically and keep COM operations on a controlled apartment/thread. Map HRESULTs to stable error codes. Test the exact interfaces against iPhone devices; WPD providers may differ in exposed properties and object hierarchy.

### AFC (optional)
Build and pin a known libimobiledevice version from source with reproducible x64 binaries and licensing notices. Bind native signatures from the current C headers—not guessed signatures. In particular, `lockdownd_start_service` returns a `lockdownd_service_descriptor_t*` structure, not an arbitrary `IntPtr` to pass as though it were the AFC service handle; `afc_read_directory` and `afc_get_file_info` return NULL-terminated `char**` lists that must be freed correctly. Use `SafeHandle` wrappers, UTF-8 marshalling, explicit error mapping, deterministic disposal and integration tests. Probe capability and enumerate only accessible media roots. No parallel operations on a single client unless documented/tested.

### Shell fallback
Keep as compatibility only. `Shell.Application.CopyHere` is asynchronous and has no direct completion callback. Polling for file stability is heuristic, not a strong transfer-completion contract. This backend must not advertise hash verification or production-grade throughput guarantees.

### Android (added in 1.8.0): one backend, two storage policies

This review was written when only an iPhone was in scope. An Android phone needs no new transport: Microsoft
lists "Media Transfer Protocol (MTP) music players, **phones**, and cameras" among the storage devices
Windows Portable Devices covers, so the same `IPortableDevice` COM surface already reaches it. What differs
is what sits behind the transport — an iPhone publishes a camera-roll container, an Android phone publishes
whole storage volumes — and that is a *policy* choice, not a second device layer.

- **One COM implementation.** A duplicated backend would double the COM surface (interface release order,
  apartment marshalling, HRESULT mapping, stream lifetime) and every future fix would have to be made twice,
  with the second copy being the one no device has exercised. `Devices/Wpd/WpdDevice.cs` therefore keeps one
  implementation and selects its policy from the attached device's family.
- **Family from what the device says, never from `WPD_DEVICE_TYPE`.** Microsoft documents the type as being
  about "generic visual appearance" and states applications "must use the capabilities of the portable device
  to determine functionally, not the `WPD_DEVICE_TYPE` value" — and the family decides the storage policy,
  which is functional. `Core/DeviceFamily.cs` classifies from friendly name, manufacturer and model.
- **The asymmetry is deliberate.** Reading an Android phone under the iPhone policy would back up its camera
  roll and call the scan complete; reading an iPhone under the generic policy only looks in more places and
  finds nothing extra. Apple is therefore matched strictly and first, Android generously.
- **What "complete" means changes.** Android's `MtpDatabase` builds its view from `MtpStorageManager`
  filesystem operations plus `MediaProvider` metadata, so a scan proves what the device exposed, not what the
  filesystem holds. The diagnostics page says so.
- **Live now, but not exhausted.** On 2026-10-21 the app walked a HUAWEI Y7 Prime 2018 through both volumes
  (4,106 files / 15.01 GiB, `Complete`) and copied a filtered 520-file selection (412 verified, 108
  unverifiable, 0 failed); the policy is therefore exercised end to end, not only unit-tested. Still open:
  a real cancel-then-resume, a locked-screen abort, PTP-only mode, and Android throughput. Those are recorded
  in `docs/RELEASE-ACCEPTANCE.md` and `docs/DEVICE_TEST_MATRIX.md` as explicit open items rather than caveats.

## Wireless scope (implemented in 1.8.0; sender still unproven)
A receiver-only server is not enough. A separate, explicit iOS Shortcut/companion sender must choose assets and POST them. Pairing token must be random, short-lived, single-session, and bound to a destination. Bind to private interfaces only, show the listening IP/port, require explicit user consent, reject path traversal, enforce file/total-size limits, validate content length, write staging files, hash on receipt, expire sessions, and stop the server on completion/exit. Do not advertise this as "wireless Photos sync" until the sender is implemented and tested.

All of those rules are now implemented and pinned by tests, with one substitution and one gap. Certificate-pinned TLS is deliberately absent (an iOS Shortcut cannot pin a self-signed certificate), so the receiver uses the authenticated session the plan allowed as an equivalent: a single-use secret redeemed once for an expiring, rotatable session token, private-only binding, and an approval gate that writes nothing until the operator accepts. The substitution and the gap - **no physical iOS sender has been run against the receiver** - are stated in `docs/WIRELESS.md` and left unticked in the acceptance checklist rather than papered over. "Wireless Photos sync" is still not advertised: iOS offers no LAN API for the Photos library, so a sender must always choose the files.

## Commercial gate, trial ledger and installer (implemented in 1.8.0)

- **The gate sits at the start of a new job, never inside one.** `LicenseService.TryStartJob(resuming)` is
  consulted before a new device-to-disk or disk-to-device run; `resuming: true` is always allowed. No timer,
  no background poll and no check on a running transfer exists. A licence failure can therefore delay a new
  job or refuse it, but can never abandon, corrupt or roll back work in flight.
- **The engine, not the UI, defines "a job".** `TransferSummary.JobId` is generated once per run and copied
  into `JobFacts`; `JobFacts.IsMeaningful` requires completed, not cancelled, no failures, at least one
  file copied and at least one byte. The UI reports the summary; it does not decide whether the trial was
  consumed. `RecordJob` is idempotent through a truncated SHA-256 fingerprint, so a replayed report is
  `AlreadyRecorded` rather than a second charge.
- **Entitlements are data, verified before they are trusted.** `EntitlementCodec` treats the envelope as
  `{payload, signature}`, verifies RSA-PSS/SHA-256 over the exact encoded payload bytes, and only then
  deserialises and applies product, channel, app-id, not-before and expiry rules. Every malformed input is a
  verdict. The private key belongs to the issuing backend and is never part of the build; the desktop app
  embeds at most a public key, and a build without one is `NotConfigured` rather than permissive.
- **The trial ledger is redundant, not secret.** `CompositeTrialStore` merges the JSON file and the HKCU
  value pessimistically: higher use count, union of job fingerprints, earliest first-seen, latest last-seen.
  Writes are atomic; a corrupt file is quarantined; a clock rollback is detected but not treated as proof.
  This raises the cost of an accidental reset and keeps a single lost store from restarting the trial, while
  remaining honest that a local-only scheme with no secret is not DRM (`KNOWN_ISSUES.md` KI-04).
- **Wireless has one extra metering point.** A sender session is the unit of work, because the protocol has
  no batch-complete signal. `TryStartWirelessUpload` checks the gate when a session first tries to send and
  `RecordWirelessReceipt` charges one job after the first committed and verified file. Refusal is HTTP 402;
  an already-received file is never deleted, and the receiver keeps running.
- **The installer is per-user and manifest-scoped.** No elevation, no firewall rule, no service, no scheduled
  task. It installs under `%LOCALAPPDATA%`, writes an HKCU uninstall entry, and records the exact file list
  in `install-manifest.json`. Uninstall deletes only that list, refuses a folder that is not an installation,
  and keeps `%APPDATA%\I-Total Transfer` - settings, trial and licence - unless explicitly told otherwise.
- **Signing is a separate, fail-closed release step.** The build never signs implicitly. The signing script
  requires an explicit credential, timestamps with RFC 3161, and verifies every artifact; a missing tool,
  credential or verification stops the release instead of producing an unsigned "success".
## Release gates

1. Unit tests: path traversal/reserved names, state transitions, resume reconciliation, collision handling, scan stability comparison, size/hash verification semantics.
2. Integration tests: mocked backend disconnect/short read/exception, destination full, permissions failure, interrupted writes, crash/restart, duplicate IDs/paths, changed scan.
3. Windows device tests: Windows 10/11, Apple Devices installed/not installed, trust accepted/revoked, locked/unlocked, multiple iOS versions, iCloud optimization both settings, HEIC/JPEG + MOV pairs, large videos, 25k+ small files, long names/non-ASCII names.
4. Performance tests with measured environment details; no hard-coded throughput acceptance threshold until baseline is collected across hardware.
5. Release build, dependency/license audit, crash-log redaction review, clean install/uninstall and upgrade/resume tests (now automated); code signing remains an explicit external step (`docs/RELEASE-SIGNING.md`) and is **not** satisfied in this workspace.
