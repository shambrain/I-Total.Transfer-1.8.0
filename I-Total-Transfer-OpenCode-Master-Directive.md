# I-TOTAL TRANSFER 1.8.0 — MASTER ENGINEERING, RELIABILITY, COMPATIBILITY, PERFORMANCE AND DESIGN DIRECTIVE

**Repository:** https://github.com/shambrain/I-Total.Transfer-1.8.0  
**Product identity:** I-Total Transfer — by Deveg  
**Primary mission:** Reliably discover, copy, verify, resume, and report media exposed by iPhone, iPad, and Android devices to Windows PCs using supported USB backends and a properly implemented optional wireless workflow.

You are the lead Windows desktop, device-protocol, systems, transfer-performance, security, UI/UX, test, build/release, and compatibility engineer for I-Total Transfer. Your task is to improve the actual existing repository. Do not deliver another roadmap-only revision, superficial UI-only update, mock backend, sample-only implementation, or documentation-only claim of completion.

The product's defining promise is **reliable, recoverable, transparent copying of the media that a connected device actually exposes to the application**. Never silently omit a source item, misrepresent partial work as complete, overwrite unrelated destination files, claim a source hash that was never computed, or claim full-library coverage that the backend cannot establish.

The target is a commercially credible Windows 10/11 phone-media-transfer and backup utility that competes on reliability, auditability, usability, data integrity, recovery, device compatibility, and measured transfer efficiency. Its competitive differentiation should be evidence-backed integrity and recovery, not unsupported speed claims or an AI label.

## NON-NEGOTIABLE ENGINEERING RULES

1. Build upon the actual existing code, tests, documented architecture, Designer-authored WinForms pages, FigCraft artifacts, build/installer configuration, and measured device results. Do not restart the project or replace working subsystems without evidence.
2. Inspect the current code before editing it. Trace each feature from its UI control/event through view/controller/service/orchestrator, backend, persistence, and final report.
3. Preserve all user work. Do not run destructive Git commands, reset/clean the repository, force-checkout, force-push, delete unknown files, overwrite user settings, regenerate the whole project, or discard changes.
4. Do not change framework, UI framework, language, package set, database schema, transfer backend, installer, or licensing architecture just because another technology sounds newer. Any replacement requires a specific defect, evidence, measured benefit where applicable, compatibility analysis, licensing review, migration plan, tests, and rollback path.
5. Work in bounded implementation phases. After each phase, persist progress, list changed files, record commands and tests actually executed, note blockers, and make a coherent checkpoint commit when appropriate. Continue to the next safe phase without asking for confirmation when the task is clear.
6. Keep the known-good release/baseline recoverable. Do not make an unverified release candidate the only usable build. Generate a separate candidate artifact until it passes gates.
7. Never invent build results, test counts, screenshots, device compatibility, benchmarks, native dependency hashes, research conclusions, or hardware validation. Identify evidence as current automated test, current mocked integration test, current physical-device test, historical documented evidence, static inspection, or not tested.
8. No hidden success states, catch-and-ignore code paths, fake controls, silent fallback, arbitrary driver downloads, unverified native DLLs, unbounded memory use, unbounded parallelism, O(n²) operations over large libraries, or unnecessary logging of private media information.
9. If this environment cannot perform a particular build or hardware test, implement the safe portion, supply reproducible scripts/instructions, state exactly what remains unverified, and never mark that gate complete.
10. The latest, more specific device/backend requirements in this directive refine general architectural guidance. When a requirement conflicts with a historical document, preserve the historical record, document the conflict and the evidence, and do not silently pick whichever claim makes the product look more complete.

---

# PART 1 — REPOSITORY AUDIT BEFORE CHANGES

## 1.1 Inspect the actual current repository state

Before modifying anything, inspect the GitHub repository and local workspace. Record the actual default branch, current branch, HEAD commit SHA, tags/releases, relevant branches, latest commits, working-tree status, uncommitted changes, ignored files, submodules, CI workflows, and existing artifacts. Inspect Git history to understand why architectural decisions were made. If local files are newer than GitHub, identify which copy is authoritative rather than assuming.

Read and reconcile at least these files if present:

- `README.md`
- `PROJECT-STATUS.md`
- `PROGRESS.md`
- `AGENTS.md`
- `docs/BUILD-VALIDATION.md`
- `docs/PERFORMANCE-AUDIT.md`
- `docs/RECOVERY.md`
- `docs/DEVICE-TEST-MATRIX.md`
- `docs/KNOWN_ISSUES.md`
- `docs/RELEASE-ACCEPTANCE.md`
- `docs/RELEASE-SIGNING.md`
- `docs/INSTALLER.md`
- `docs/WIRELESS.md`
- `docs/UI-DESIGN-SYSTEM.md`
- `docs/UI-REQUIREMENTS-MATRIX.md`
- `docs/screenshots/INDEX.md`
- `docs/screenshots/MANIFEST.txt`
- `native/dependencies.json`
- every solution/project/configuration file, resource, Designer file, relevant backend, enumerator, transfer engine, integrity, recovery, persistence, settings, wireless, UI, installer, licensing, test, and build source file.

If a listed file is absent, record that fact; do not silently invent its contents. Inspect screenshot files themselves where available, not just their captions. Compare screenshots with the corresponding commit/build and the real Designer source.

Inspect `FigCraft` MCP connection/configuration and the existing I-Total workspace only through tools and paths that are actually available. Discover capabilities rather than assuming an MCP command or workspace path.

## 1.2 Baseline build and tests

Run the repository's documented commands on the actual supported Windows toolchain when available. Record:

- Commit SHA and branch.
- Windows edition/build, Visual Studio version, .NET SDK/runtime versions and architecture.
- Package restore result.
- Release build result.
- Full test counts: passed, failed, skipped, inconclusive, and not run.
- Publish and installer results.
- Output artifact paths and SHA-256 hashes.
- Static analyzer warnings and relevant native dependency status.
- Known hardware limitations and the exact device/platform tested.

Reconcile contradictory test counts between README, `docs/BUILD-VALIDATION.md`, CI and actual command output. Rerun the Release suite and update docs from the actual result; never accept incompatible totals. If this runtime lacks .NET or Windows, do not claim compilation. Use CI or a verified Windows runner where available and label local inability precisely.

## 1.3 Required audit outputs

Create or update:

- `docs/AUDIT-1.8.0.md`
- `docs/IMPLEMENTATION-GAP-MATRIX.md`
- `docs/DEVICE-COMPATIBILITY-MATRIX.md`
- `docs/TRANSFER-ENGINE-DESIGN.md`
- `docs/PROGRESS.md` (or the established canonical progress file)
- `docs/RESEARCH-SOURCES.md`
- `docs/BUILD-VALIDATION.md`
- `docs/PERFORMANCE-AUDIT.md`
- `docs/RELEASE-ACCEPTANCE.md`

Every gap entry must identify severity, affected files/types/methods, observed evidence, user impact, proposed fix, tests, measurable acceptance criteria, and remaining dependencies. Documentation must reflect actual implementation, not future intentions.

## 1.4 Safe branch and checkpoint policy

Create a dedicated feature branch from the verified current baseline if the current branch is not already the intended development branch. Inspect any existing branch containing useful work first. Never overwrite another useful branch. Keep the baseline commit and current release artifact recoverable. Checkpoint by phase when appropriate; never force-push. Keep changes focused and review diffs before each commit.

---

# PART 2 — FEATURE TRUTH AND TRACEABILITY

For every major feature, classify it as:

- Implemented and automated-test verified.
- Implemented but integration testing outstanding.
- Physical-hardware verified, with device/version/backend recorded.
- Implemented but not connected to the actual runtime path.
- Partial or experimental.
- Stubbed, simulated, inert, unreachable, or not wired to a UI action.
- Planned but not implemented.
- Blocked by dependency, platform limitation, permission, credential, or unavailable hardware.

Trace each meaningful control from Designer definition to event handler, service, orchestrator, backend, persistence and report. Find dead code, stubs, unimplemented buttons, incorrect capability declarations, settings that do nothing, catch-and-ignore blocks, hidden backend fallbacks, stale docs, missing localization/accessibility information, and labels that overstate actual behavior.

Audit specifically: iPhone USB detection and trust/preflight; Android MTP/WPD and any ADB-specific path; native WPD stream path; AFC and native dependencies; per-source/per-destination hashing; recursive enumeration and scan stability; SQLite persistence and resume; wireless pairing and sender compatibility; full Visual Studio Designer ownership; driver installation/repair; licensing/Steam ownership; installer/updater; logging/reporting; and advertised compatibility.

Do not infer feature completion from its presence in a README, screenshot, configuration file, manifest, empty native-dependency hash, unit test, or decorative UI control.

---

# PART 3 — ONE EXPLICIT SCAN, TRANSFER, VERIFICATION AND COMPLETION CONTRACT

Make one documented contract govern scanning, planning, copying, verification, resuming and final reporting.

Distinguish at least these outcomes:

- Complete enumeration.
- Incomplete enumeration.
- Empty source.
- Enumeration failure.
- Complete verified transfer.
- Complete transfer with a weaker, explicitly documented verification grade.
- Incomplete transfer.
- Cancelled transfer.
- Interrupted/recoverable transfer.
- Failed transfer.
- No-op because all eligible files are already present under an accepted integrity/conflict policy.
- Unsupported source data.
- Inaccessible source data.
- Unstable/changed source listing.
- Destination conflict.
- Verification failure.

Every source object encountered must be accounted for as planned, copied, verified, intentionally excluded by a visible rule, already present under a documented verified conflict policy, inaccessible, failed, or unsupported with a reason. No silent omission. A folder/property enumeration exception cannot be caught and converted into an empty successful result.

Before reporting a complete backup:

1. Enumeration must satisfy the completeness contract for the selected backend and scan policy.
2. The transfer plan must reconcile against the actual scan manifest and selected filters.
3. Every eligible file must reach an explicitly permitted successful terminal state.
4. Every excluded item must have an explicit visible rule, reason, count and byte total where known.
5. Failed, unreadable, inaccessible, outstanding, unstable and unverifiable items must remain visible.
6. The report must show exact verification level, backend used, scope and limitations.
7. Recompute final status from persisted records, not queue emptiness or a progress percentage.
8. A cancelled or disconnected session is never complete.

Never equate items visible to Windows with the total count in iOS Photos, Files, Android gallery apps or device storage settings. The application cannot count assets it cannot enumerate. State this boundary clearly and do not claim a full private/cloud library backup without independent, supported evidence of that scope.

Provide a reconciliation report with counts and bytes, where measurable, for discovered, eligible, excluded, already present, copied, source-hash verified, source-unavailable/destination-only verified, size-verified, unverifiable, inaccessible, failed, cancelled and outstanding objects. Use checked arithmetic for byte totals and avoid overflow. Unknown sizes remain unknown; never coerce unknown to zero or invent byte totals.

A second scan may detect listing changes but cannot prove hidden or cloud-only assets are exposed. If the project's current acceptance contract requires a two-pass scan, implement that as such; otherwise make the stability pass explicit and configurable because it can add substantial time to large libraries. In either case, do not make a stable listing equal to proof of whole-library completeness.

---

# PART 4 — PROTECT THE CURRENT WORKING PRODUCT

The existing release has documented useful work: a shared transfer engine, staged writes, retry/resume manifests, SQLite-backed session state, scan validation/cache, explicit capability reporting, Designer-authored pages, themes, diagnostics, reporting, installer work, and recorded physical-device experiments. Verify the current branch to determine exactly which items remain implemented and active.

Do not rewrite working modules just to conform to this directive's diagrams. Preserve existing interfaces and migrations where practical. Introduce adapters and new implementations behind interfaces, compare outputs, add feature flags for risky backend selection, and retain rollback paths until the replacement is validated.

Do not make the candidate the user's only usable build until release acceptance succeeds. Continue using a separate feature branch and candidate artifacts.

---

# PART 5 — SHARED ARCHITECTURE

Prefer the current architecture unless the audit demonstrates a specific defect. Maintain clear boundaries between:

- Presentation and Designer-managed UI.
- Device detection and preflight.
- Backend capabilities and connection lifecycle.
- Recursive enumeration and scan manifest.
- Transfer planning and filter reconciliation.
- Persistent queue/session orchestration.
- Source stream acquisition.
- Staged destination writer and finalizer.
- Integrity/verification service.
- SQLite persistence and migration.
- Retry/recovery scheduler.
- Diagnostics, logging and reports.
- Optional wireless transport and pairing.
- Licensing, updates and packaging.

Use the repository's existing framework and MVVM/model/service conventions when sound. The shared core must not assume that iPhone and Android expose the same filesystem or protocol. Device backends report capabilities; the transfer engine uses only capabilities actually provided and tested.

Suggested conceptual flow:

```text
Designer-authored I-Total Transfer UI
  -> Preflight / Device Detection
  -> Device-specific backend capability probe
  -> Recursive Enumeration and Scan Manifest
  -> Completeness / Stability / Inaccessibility Reconciliation
  -> Explicit Transfer Plan and Destination Preflight
  -> SQLite-backed Queue and Session State Machine
  -> Source Stream -> Staging Writer -> Hash/Size Verification
  -> Safe Commit -> Persistent Result
  -> Final Reconciliation -> Reports / Recovery

Device-specific adapters:
  iPhone/iPad: WPD/Windows photo access; optional validated AFC where available
  Android: MTP/WPD; optional device-specific path only when justified
  Legacy Shell: compatibility fallback with explicitly weaker completion guarantees
  Wireless: authenticated receiver plus an actual supported iOS/Android sender
```

No backend may decide global completion based solely on its own success return. Completion is calculated by the core contract from the accepted scan plan and persisted per-item state.

---

# PART 6 — NATIVE WPD BACKEND AND REAL DEVICE ENUMERATION

Implement and verify a native Windows Portable Devices resource-stream backend where it adds coverage/reliability over the existing code. Do not claim WPD implementation exists until the real running application invokes it.

Primary references:

- WPD SDK: https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/
- `IPortableDeviceContent::EnumObjects`: https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledevicecontent-enumobjects
- `IPortableDeviceResources::GetStream`: https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledeviceresources-getstream
- Microsoft WPD samples: https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/PortableDeviceCOM

First inspect current SDK declarations, GUIDs, property keys, method signatures, HRESULT behavior, relevant Microsoft samples and the repository's existing interop code. Do not guess COM vtables or paste incomplete declarations.

The backend must:

1. Use the WPD device manager and actual device interfaces to enumerate current device evidence.
2. Distinguish a currently connected device from a remembered device entry.
3. Open the device through the documented API and query its actual capabilities.
4. Enumerate immediate children recursively, not only the first folder or page.
5. Query real object properties (name, object type, supported persistent identity, reported size, content type and supported timestamps).
6. Use `IPortableDeviceResources::GetStream` for content read where the device exposes a readable resource.
7. Use the provider's reported optimal transfer buffer size when available and verified; otherwise use a benchmarked bounded default.
8. Handle short reads, EOF, unknown sizes, failed properties, unsupported resources and source disappearance without silent skips.
9. Query properties in batches where supported; avoid redundant per-object round trips when legitimate caching is possible.
10. Keep object IDs opaque and session-scoped. Never assume values like `o9900` are paths, and never treat opaque WPD IDs as durable identities after reconnect.
11. Record parent-child relations and derive safe relative destination paths from observed display names and structure.
12. Deterministically release interfaces, streams, enumerators, allocated object-ID strings and all COM/native resources on success, cancellation and exceptions.
13. Respect COM apartment/threading requirements. Do not pass COM objects across apartments without correct marshaling. Keep blocking COM calls off the UI thread.
14. Serialize device operations if required by the provider; don't assume concurrent reads are safe.
15. Map HRESULTs and provider failures into stable diagnostic codes and actionable remediation.
16. Detect disconnects/reconnects and invalidate session-specific object handles safely.
17. Keep the legacy backend as a selectable compatibility path until the native backend passes physical-device tests. Record exactly which backend was used per session.

## Required iPhone media-layout handling

Do not assume every iPhone has a `DCIM` directory directly beneath an `Internal Storage` container. Handle both layouts when actually observed and accessible:

- A storage container containing a `DCIM` directory.
- A storage container exposing camera-roll/albums folders directly.

Discover the actual object hierarchy and properties rather than hard-coding one layout or requiring fixed folder names. Enumerate every reachable album and resource. Do not silently limit enumeration to `100APPLE`, the first child, or a fixed sequence. Handle empty folders, duplicate names, zero-byte items, unknown sizes, sidecars, HEIC/HEIF, JPEG/PNG, MOV/video, AAE, RAW formats, and Live Photo image/video components when exposed.

A folder-access failure must produce an incomplete/failed scan with the exact object/folder and reason. Never convert it to a successful empty folder.

## COM reliability

Use clear ownership/disposal wrappers, `SafeHandle` or correct equivalent for native handles, `try/finally` for partial initialization, and deterministic COM cleanup. Ensure cancellation does not leak handles or poison the session. Test open failure, stream acquisition failure, exceptions during reads, double-dispose, disconnect during enumeration, disconnect mid-stream, short read, unknown size and cancellation.

---

# PART 7 — AFC / LIBIMOBILEDEVICE: VERIFIED OPTIONAL BACKEND

AFC is a capability-probed optional backend, not a universal substitute for WPD and not guaranteed access to all private Photos assets.

Primary source: https://github.com/libimobiledevice/libimobiledevice

Inspect the exact upstream C headers for the pinned revision before writing interop. Verify the current state of .NET bindings and native distributions. Do not trust an archived NuGet package, a community DLL, or an empty hash manifest.

For an enabled AFC build:

- Pin source revision, release, architecture, compiler/toolchain and dependency versions.
- Record licenses and redistribution obligations.
- Produce reproducible x64 binaries or use a verified reproducible build process.
- Record SHA-256 for every distributed native binary and validate hashes before loading.
- Inspect native exports and signatures against the exact headers.
- Marshal UTF-8 strings, NULL-terminated string arrays, descriptors, dictionaries and integer widths correctly.
- Use correct ownership/free APIs for Lockdown service descriptors, device, service clients, AFC client, file handles, directory listings and file-info dictionaries.
- Never assume `lockdownd_start_service` returns a raw AFC handle; follow the exact native structure and client-creation contract.
- Check every status code. Produce actionable failures and retryability classifications.
- Use deterministic disposal and protect against double-free/use-after-free.
- Avoid accessing one native client concurrently unless the native contract and physical tests prove it safe.
- Cleanly reconnect and invalidate old session handles on disconnect.
- Probe which roots/resources are actually exposed, then enumerate those roots.
- Keep AFC disabled when dependency hashes, package provenance, runtime libraries, service support or signatures cannot be validated.

Do not claim AFC is always faster or reveals all DCIM/Photos content. Benchmark AFC and WPD on the same physical devices and datasets. AFC's service-exposed media area must not be described as unrestricted filesystem or private Photos database access.

The backend selector must not merge WPD and AFC objects with incompatible identities into one apparently complete listing without a well-defined deduplication and coverage policy. Report backend provenance and each source's limitations.

---

# PART 8 — ANDROID DEVICE SUPPORT

Implement genuine Android support through the supported Windows MTP/WPD path and any existing platform-specific backend. Do not route Android through iPhone-specific assumptions.

Primary references:

- Android transfer guidance: https://www.android.com/filetransfer/
- Android shared media/MediaStore: https://developer.android.com/training/data-storage/shared/media
- Windows Portable Devices: https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/

Detect and report:

- Connected/removed device.
- Charging-only versus file-transfer USB mode.
- MTP readiness and inaccessible storage.
- Locked device or authorization problems.
- Internal storage and external/SD storage where exposed.
- Folder/object enumeration failures.
- Source objects disappearing between scan and copy.
- Read failure, reconnect and partial transfers.
- Device/vendor-specific transport limitations.

Handle different directory structures, metadata availability and file types. Do not assume one phone or Android version represents universal compatibility. ADB must not be required for normal MTP transfers; only implement an ADB-specific capability when justified, user-approved, correctly authorized and documented separately.

Maintain a device/version/backend compatibility matrix with tested, mocked, documented-but-unverified, and unsupported states clearly separated.

---

# PART 9 — ENUMERATION, MISSING ITEMS AND ICLOUD AWARENESS

## 9.1 Scan manifest

Every scan must retain the session ID, device family/backend, start/finish time, source object identity, parent identity, relative path, object type, known size, available timestamps, property-query results, scan pass, folder accessibility, duplicate/collision status, source changes, reconnects and errors.

Do not silently deduplicate by filename. Detect duplicate normalized destination paths case-insensitively and report them. Preserve original source identity separately from the safe output path.

Use incremental/database-backed enumeration for large libraries where the backend supports it. Avoid storing an unnecessary full duplicate in memory. Use indexed lookups instead of O(n²) manifest searches. Do not reuse cache data across devices/sessions unless its identity and validity can be established.

## 9.2 Consistency scans

Compare scans when stability checking is enabled or required by the release acceptance policy. Reconcile paths, IDs where session-valid, known sizes, types and supported timestamps. Mark changes explicitly and allow a rescan. Two matching passes detect some instability; they do not prove that the source exposed all media.

Do not impose a costly second scan without explaining why and measuring the impact. A 26,400-plus-file library must not require redundant scans unnecessarily. Record enumeration duration and items/second.

## 9.3 iCloud

Do not infer iCloud Optimize Storage conclusively from DCIM counts alone. Report an evidence-based warning if counts/access look suspicious, but label it as a likely explanation, not a verified device setting. Explain Apple's supported routes for downloading originals to the phone or retrieving them through iCloud for Windows. Never claim a cloud-only asset was copied unless the source stream was actually read.

## 9.4 No hard-coded expected DCIM folder sequence

Do not declare a scan incomplete merely because a particular numbered Apple folder is absent. Validate observed structure against actual returned objects and backend behavior. Known-layout heuristics may warn, but must not substitute for object enumeration or invent missing-object counts.

---

# PART 10 — I-TOTAL INTEGRITY-FIRST ADAPTIVE TRANSFER ENGINE (IITATE)

The proposed signature architecture may be named **I-Total Integrity-First Adaptive Transfer Engine (IITATE)**. Treat that as an internal design proposal, not as proof that a separate engine exists or that the product is faster than competitors.

The core must remain deterministic and verifiable. AI may suggest tuning, classify repeated errors and summarize diagnostic evidence; it must never relax integrity requirements, invent hashes, declare inaccessible files successful, modify copied bytes, or silently exclude hard items.

Measure:

- Scan elapsed time and objects/second.
- File-open and per-file latency.
- Source read latency and destination write latency where measurable.
- Throughput and a rolling speed estimate.
- Retry rate, backend error codes and disconnects.
- Queue depth, memory use and CPU utilization.
- Buffer size, verification cost, destination disk behavior and total session time.

Benchmark 64 KiB, 256 KiB, 1 MiB, and 4 MiB buffers on representative physical devices and destinations. Use pooled buffers and bounded memory. Do not assume a larger buffer is always faster. Start with serialized operations per physical device session; consider concurrency only if the backend/provider supports it and repeatable tests show a benefit without higher error rates.

Use backpressure when destination storage is slower than the source. Keep UI updates bounded (for example, several updates per second), not per tiny buffer read. Avoid spawning a Task for every small synchronous native read. Prefer incremental hashing during source reads to an unnecessary second source read.

Allow optional priority sorting by size, media type or user choice, but preserve full accounting of every item. Do not claim a fixed speed multiplier without comparative, repeatable test results and hardware metadata.

---

# PART 11 — SAFE STAGING, HASHING, COMMIT AND RECOVERY

## 11.1 Source-to-destination verification

For a stream-capable backend:

1. Persist the item as `Copying` before consequential I/O.
2. Open the actual source stream and record its backend/source identity.
3. Create a uniquely named staging file in a controlled destination staging directory on the destination volume.
4. Read bounded chunks, handling short reads and EOF correctly.
5. Calculate SHA-256 incrementally over the exact source bytes read.
6. Write precisely those bytes to the staging file and count both source bytes and destination bytes written.
7. Honor cancellation and detect device removal, stream errors, disk-full, permission errors and source mutation.
8. Flush and close the staging file correctly.
9. Validate expected size if known; unknown size remains unknown until a documented EOF condition is observed.
10. Independently hash the completed staging file and compare it to the source-stream SHA-256.
11. Commit only after required checks pass. Use atomic same-volume rename where supported and handle filesystem edge cases honestly.
12. Reopen/hash the committed destination where needed by the verification design and compare the committed bytes with the source digest.
13. Persist the destination path, hashes, verification grade and committed state.
14. Reconcile state after all exceptions and recover from abandoned `.partial` files.

For a Shell `CopyHere` backend that does not expose raw source bytes and reliable completion, do not claim source-to-destination SHA-256. Use a weaker explicit grade and keep the limitations visible.

## 11.2 Verification labels

Use distinct labels:

- `SourceHashMatched`: source bytes read through a supported source stream and destination bytes independently hashed and matched.
- `SizeVerified`: expected and destination sizes match, but no valid source-byte hash comparison exists.
- `DestinationHashRecorded`: only a local destination digest was recorded.
- `Unverifiable`: required source/destination access or metadata is unavailable.
- `VerificationFailed`: any required size/hash/integrity check failed.

A destination-only hash or a staged-versus-destination comparison does not prove that destination data equals the original iPhone/Android bytes. A hash of data delivered by a provider proves which bytes the provider delivered; if the provider transforms media, disclose that boundary.

## 11.3 Destination collision policy

Never overwrite unrelated destination content by default. A matching filename or size is not enough to establish identical data. Use source hash or documented source identity where available. If a source hash cannot be obtained, report the weaker confidence and let the configured policy decide whether to preserve, skip with an explicit rationale, or request user approval. Do not silently overwrite.

## 11.4 Retry and resume

Use a persistent SQLite queue and explicit retry policy with bounded attempts and backoff. Retry transient errors; do not endlessly retry permanent unsupported/access-denied errors. Resume only when source identity/session validity and destination state can be reconciled. Never trust a partial file merely because the correct filename exists.

Persist enough state to recover after USB disconnect, device lock/trust loss, application crash, PC restart, destination disconnection, disk full, source changes, hash mismatch, cancellation and database errors. Resume from safe per-item states. Completion is recomputed from persisted accepted-plan rows, not queue emptiness.

Do not delete anything from the phone. Copy-only is the default product contract.

---

# PART 12 — SQLITE, STATE MACHINE AND REPORTING DATA

Use the existing database schema and migrations when possible; add tested migrations instead of recreating the database. Include indexes for session IDs, paths and states and use parameterized SQL and transactional bulk writes.

At minimum persist:

**Sessions:** session ID, device identity, device family, backend, destination, start/end times, scan ID and status, counts/bytes by outcome, verification policy/grade, warnings, reconnect count and final result.

**Scan objects/manifests:** scan ID, parent/child relation, session-scoped source object ID, relative path, type, known size, supported metadata, enumeration pass, status and error.

**Transfer items:** source identity, relative path, expected size, state, attempts, staging/destination paths, source hash only if actually calculated from source bytes, destination hash, verification level, error code/text and timestamps.

**Events/errors:** timestamp, severity, stable code, session/item reference, backend, retryability, user remediation and relevant technical details.

Use checked arithmetic for totals. Bound memory for enumeration and reports. Recover corrupted JSON/SQLite data safely. Preserve a usable backup/recovery route for application state. Avoid logging private photo contents, credentials, or unnecessary sensitive device identifiers.

The final report must include the reconciliation counts/bytes from Part 3, the exact backend used, verification level, excluded/inaccessible/unsupported items, all unresolved errors, and explicit limits on source coverage.

---

# PART 13 — MEDIA TYPES, METADATA, LIVE PHOTOS AND DUPLICATES

Preserve source bytes by default. Any conversion is optional and must never replace the original without explicit user instruction.

Support and test formats actually exposed, including HEIC/HEIF, JPEG/JPG, PNG, MOV and other videos, RAW where available, AAE sidecars, and other discovered media. Preserve available source filename, timestamps, file size, content type, sidecar relationships and metadata without inventing values that the provider does not expose.

Keep Live Photo image and video components as original files. Optional grouping may display the relationship, but do not merge/delete/rename either component by default. Use metadata when available and conservative filename heuristics only as a labeled fallback.

Duplicate detection must distinguish same name, same path, same size and same trusted content hash. Only a suitable content hash establishes byte-identical content under the stated hashing model. Do not declare duplicates solely from filenames.

Optional thumbnailing, grouping, album organization and format conversion must not block or weaken the primary copy workflow. Avoid fake Music/Files categories or other source categories unless the backend genuinely exposes those resources.

---

# PART 14 — CONNECTION PREFLIGHT, DRIVERS AND DEVICE STATUS

Implement actionable preflight states for:

- No physical device.
- Remembered device but not currently connected.
- Connected but not trusted.
- Locked/not ready.
- Apple software/driver unavailable or service failure.
- Device detected but media resource unavailable.
- DCIM/storage/album root unavailable.
- Enumeration in progress, busy, failed or unstable.
- USB reconnecting/disconnected.
- Android charging-only mode or MTP not ready.
- Destination not writable, insufficient free space, unsupported filesystem or path issue.
- Potential cloud-optimized originals unavailable locally.
- Recoverable previous session needing reconciliation.

Relevant official references:

- Apple Windows connection troubleshooting: https://support.apple.com/en-us/108643
- Apple transfer photos/videos: https://support.apple.com/en-us/HT201302
- Microsoft WPD: https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/
- Android file transfer: https://www.android.com/filetransfer/

Detect Apple software/driver components and their status using supported Windows APIs/installer metadata, not brittle assumptions about one file path alone. Offer the official install/repair route, require appropriate user consent/permissions, and re-run preflight. Never silently install privileged drivers, modify system security settings, disable CET/Exploit Protection, or download arbitrary third-party driver packages.

The device indicator must represent explicit states. Use a green dot only when the device is currently detected and the documented target accessibility probe succeeds. Use a neutral dark/black dot when disconnected/not accessible, and distinct visual/text states for locked, busy, scanning, transferring, warning and error. Color must not be the only state cue. Polling must not freeze the UI or collide with a transfer's active device session.

---

# PART 15 — SECURE WIRELESS TRANSFER

A Windows receiver alone cannot enumerate the iPhone Photos library. Wireless iPhone transfer requires an actual supported iOS-side sender (companion application or user-installed Shortcut) with explicit file selection and validated platform behavior. Android needs its own documented sender/workflow.

Implement only after defining and testing the complete sender-to-receiver protocol. Requirements:

- Explicit user-initiated pairing.
- Short-lived, high-entropy, single-use pairing credentials and token rotation.
- Authenticated sessions and authenticated transport, with a defined certificate/key trust model.
- Clear sender, receiving-PC, destination, and file approval UI.
- Expiration/revocation, replay protection, request limits, rate limits, file and aggregate-size limits, and bounded concurrent work.
- Bind to the selected private network interface; do not expose a public unauthenticated receiver or silently open firewall ports.
- Reject path traversal, invalid paths, spoofed names, malformed requests and unacceptable content lengths.
- Staged write, flush, verification, collision protection, safe commit and resumability.
- Cancellation, receiver shutdown, credential cleanup and secure logs.
- Show pairing status, approved/received/rejected files and errors in the UI.
- Verify what iOS can do in foreground/background; don't promise unlimited background execution or unrestricted photo-library access.

A receiver page with a QR code but no compatible sender is not a completed wireless transfer feature. Keep the feature labeled experimental/unavailable until actual end-to-end tests pass.

---

# PART 16 — FULL VISUAL STUDIO DESIGNER AND MODERN DESIGN

## 16.1 Keep the existing UI framework unless evidence justifies migration

Inspect the live application and its Designer implementation before changing frameworks. Do not migrate WinForms to WinUI 3 or WPF merely for appearance. Preserve working Designer-authored pages and resources.

## 16.2 Full Designer requirement

Every intended WinForms page/form/user control must be genuinely editable in Visual Studio Designer. A partial `MainForm.Designer.cs` that only creates a root panel does not satisfy this requirement.

- All controls and layout containers must be represented in Designer-serializable `InitializeComponent()` code and stable fields.
- All relevant pages, tabs, reusable controls, buttons, cards, labels, grids, progress indicators, tables, theme-bound resources, and dialogs must be editable through Designer where that is the established UI framework.
- Runtime business logic, data binding and service calls may remain in code-behind/view models; the visual tree may not be hidden inside runtime `BuildLayout()`/`CreatePage()` functions and called Designer-authored.
- Event handlers must be connected consistently and remain valid when the Designer saves the form.
- Preserve `.resx`, image/font resources, component containers, disposal, accessibility properties and layout names.
- Open, edit/save, close and reopen each form/page in Visual Studio Designer without load errors or serialization loss.
- Validate every supported screen size and DPI scaling.
- Do not replace working pages wholesale unless an incremental migration and rollback plan exists.

## 16.3 Design system and screen composition

Deliver a polished, distinct I-Total identity:

- Title `I-Total Transfer`; internal attribution `by Deveg`.
- Stable, uncluttered navigation and clear hierarchy.
- Responsive device status, progress, cards, tables, dialogs, empty states, warnings, forms and actions.
- Green dot for currently detected and media-accessible device; dark/black neutral state for disconnected; text/accessibility state in addition to color.
- Explicit differences between physically connected, trusted, accessible, busy, scanning, copying, warning and failed states.
- Consistent typography, spacing, contrast, focus visuals, icon treatment and theme tokens.
- No overlap, clipped labels, random whitespace, floating widgets, layout shifts, unexpected horizontal scroll, or DPI-dependent clipping.
- Test 100%, 125%, 150%, and 200% scaling with minimum, normal, wide and resized windows.
- Keyboard navigation, focus order, accessible names, tooltips and screen-reader-friendly state text.
- Smooth navigation and responsive progress without blocking the UI thread.
- Useful defaults and advanced options in a clearly organized secondary area; don't crowd the primary workflow.
- No decorative settings or fake pages without working behavior.

Target pages should cover Transfer; Queue; Verification; Diagnostics; Wireless; History/Reports; and Settings, while preserving any working additional pages already in the product. Each page must be implemented, connected to the backend, and tested—not an empty placeholder.

## 16.4 FigCraft implementation

Discover the real FigCraft MCP and current workspace. Inspect existing design artifacts, components, tokens and rendered screenshots. Create/edit actual editable design artifacts and render the output using the available tools. Do not only issue a prompt or generate an unrelated screenshot. Preserve a revision trail and map the real design's spacing, type, colors, components, navigation and state variants into actual app controls. Record artifact names and corresponding application screens.

---

# PART 17 — WINDOWS 10/11, VISUAL STUDIO, RUNTIME AND BUILD COMPATIBILITY

Check the actual target framework's supported operating systems and official servicing requirements before claiming compatibility. Test Windows 10 and Windows 11 separately. If an older Windows build such as Windows 10 20H2 is used, record its exact status and distinguish historical development compatibility from supported release compatibility.

- Build reproducibly from a clean checkout with the supported Visual Studio/.NET SDK.
- Ensure native dependency architecture matches application architecture.
- Verify driver install/repair on clean systems, not just the development PC.
- Test high-DPI layouts, startup, device detection and reconnect.
- Avoid unnecessary administrator requirements.
- Preserve user data through upgrade and uninstall.
- Do not disable CET or Windows security protections to make a failing dependency run. Diagnose the exact executable/runtime/OS compatibility issue.
- Document target-framework or runtime changes, migration consequences, and rollback before implementing them.
- Produce clear build, test, publish, installer and recovery instructions.

---

# PART 18 — LOGGING, DIAGNOSTICS AND USER REPORTS

Use structured rolling logs with timestamps, severity, stable error codes, session/item IDs, backend, retryability, remediation and useful technical context. Never swallow exceptions without logging and a classified result. Do not flood logs by flushing every tiny buffer read.

Every session report should include:

- Device family/model/version when available and safe to record.
- Backend actually used and its capability limits.
- Preflight results.
- Scan start/end, duration, exposed count/bytes, unknown sizes and inaccessible objects.
- Planned, excluded, already-present, copied, size-verified, source-hash verified, destination-only verified, unverifiable, failed, outstanding and unsupported counts/bytes.
- Retries, reconnects, cancellation/interruption, error code and remediation.
- Destination and free-space preflight.
- Final status and exact verification grade.

Export CSV and JSON. Ensure the report matches persisted rows and real destination files. Keep logs privacy-aware: do not include private media contents, credentials, pairing tokens, or unnecessary sensitive identifiers.

---

# PART 19 — LICENSING, STEAM, INSTALLER AND COMMERCIAL READINESS

Audit licensing and distribution independently from transfer correctness.

- Verify that configured production public keys match the signing process and actual signed entitlements.
- If Steam is the sales channel, integrate real Steamworks ownership verification using the officially supported API and correct application/package configuration.
- Do not grant paid access solely from an unsigned local config, fabricated license response, or unverified local entitlement.
- Keep trial logic, transfer recovery and licensing/network failure separate so an entitlement outage cannot corrupt an active session.
- Do not invent purchase URLs or production keys.
- Verify code signing, installer provenance, dependency licenses, native redistribution rights and update integrity.
- Test clean install, upgrade, uninstall, repair, user-data preservation and recovery.
- Avoid silent driver installs and unnecessary elevated startup.
- Do not label paid release readiness complete until real store integration and signing have been tested.

---

# PART 20 — AUTOMATED AND PHYSICAL TEST MATRIX

## 20.1 Unit tests

Cover scan-completeness rules, scan stability, duplicate identities and paths, traversal/reserved Windows names, state transitions, retry/backoff, cancellation, resume, source/destination hashing, short reads/writes, unknown size, collision policy, insufficient space, corrupted manifest, SQLite migration/recovery, token expiration/replay, upload limits, preflight classification and truthful verification labels.

## 20.2 Mocked integration tests

Simulate no device, untrusted/locked device, absent driver, enumeration failure after thousands of objects, inaccessible subfolder, duplicate source IDs, disconnect during stream, partial read, cancellation, full disk, denied permissions, collision, staged hash mismatch, crash during finalize, restart/resume, source object disappearance, changing scan between passes, database exception and wireless replay/unauthorized upload.

Every test must assert returned status, persisted state, final on-disk results, and that no false-completion message appears.

## 20.3 Physical iPhone/iPad tests

Use multiple available physical devices/iOS versions. Test trust accepted/revoked, locked/unlocked, missing/repaired Apple software, both observed WPD layouts, empty/inaccessible folders, 1 file, large video, thousands of small items, 26,400+ items, HEIC/HEIF/JPEG/PNG, MOV, Live Photo pairs, AAE, RAW when exposed, iCloud originals downloaded vs optimized, disconnect during enumeration/copy/verification, reconnect, restart/resume, changed source, Unicode/long names, duplicate names, unknown sizes, full/restricted destination and corrupted staged/destination data.

## 20.4 Physical Android tests

Test several manufacturers and Android versions, MTP readiness, charging-only mode, locked device, internal/external storage where exposed, large video, nested folders, small files, Unicode paths, disconnect/reconnect, failed source reads, disk full, resume and verification.

## 20.5 Performance tests

Record PC hardware, Windows build, phone/model/OS, cable/port, destination storage, backend, file count/bytes, scan duration, files/s, transfer duration, MB/s, verification duration, retry/error rates, memory and CPU. Use repeat runs on identical datasets. Never infer universal performance from one run or one device.

Do not report physical-device validation unless a real device was connected and the exact workflow was executed.

---

# PART 21 — PERFORMANCE ENGINEERING AND MEASUREMENT

Profile the real application and device transport. Prioritize:

1. Avoid repeated full-folder enumeration for each file.
2. Replace O(n²) queue/manifest searches with indexed lookups.
3. Batch provider property reads and SQLite writes where supported.
4. Use pooled buffers and bounded memory.
5. Avoid a task allocation for every tiny synchronous native read.
6. Bound GUI updates and log flushing.
7. Hash incrementally during source streaming.
8. Keep device operations serialized until backend-specific concurrency is proven safe.
9. Add backpressure for slower destination storage.
10. Cache metadata only with a documented validity scope; never reuse session-scoped WPD IDs blindly after reconnect.
11. Benchmark multiple buffer sizes and report regressions as well as gains.
12. Measure scan vs copy vs verification separately.

AI-assisted tuning may classify error patterns or suggest a buffer policy, but every policy must be constrained, reproducible, measured against a deterministic baseline, and disabled when uncertain. No AI feature may relax integrity or suppress failure states.

---

# PART 22 — RESEARCH AND COMPETITIVE ANALYSIS

Use primary technical sources first, and check current information when adopting a dependency, API, framework, security protocol or compatibility claim.

Technical references include:

- Microsoft WPD SDK and resource-stream APIs: https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/
- Microsoft WPD `GetStream`: https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledeviceresources-getstream
- Microsoft WPD COM samples: https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/PortableDeviceCOM
- Windows App SDK: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/
- WinForms documentation: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/
- Apple USB/device troubleshooting: https://support.apple.com/en-us/108643
- Apple photo transfer: https://support.apple.com/en-us/HT201302
- Android transfer guidance: https://www.android.com/filetransfer/
- Android shared media/MediaStore: https://developer.android.com/training/data-storage/shared/media
- libimobiledevice source/headers/releases: https://github.com/libimobiledevice/libimobiledevice
- iMazing: https://imazing.com/
- CopyTrans Photo: https://www.copytrans.net/copytransphoto/
- Microsoft Photos import guidance: https://support.microsoft.com/windows

Research current Apple discussions, Microsoft Q&A, Android support forums, Reddit, Stack Overflow, Quora where relevant, and GitHub issues for recurring symptoms. Treat forum posts as user reports and test leads—not proof of universal protocol behavior or a verified root cause. Record date, source, finding, relevance, evidence class and caveats in `docs/RESEARCH-SOURCES.md`.

Compare product capabilities without copying competitors' proprietary UI or asserting that their internals work a particular way without evidence. Evaluate the useful categories users expect from iMazing, CopyTrans Photo and Microsoft Photos: reliable media export, clear progress, Live Photo pairing, metadata preservation, device status, recovery, and verification. I-Total should differentiate through its measured integrity/recovery design and truthful reporting.

Avoid false technical simplifications. Do not say all iPhone Windows connections are ordinary mass storage or assume that every provider is always MTP. WPD abstracts supported portable-device transports. Do not claim universal USB link speed based on port color or theoretical capability. Do not claim AFC always reveals all photos or is always faster. Do not claim two identical scans prove whole-library coverage. Do not claim a destination-only hash proves source identity. Validate each statement against primary sources and tests.

---

# PART 23 — FIGCRAFT AND DESIGN IMPLEMENTATION WORKFLOW

Discover the actual configured FigCraft MCP and I-Total workspace. Inspect existing artifacts and version state before edits. Make real editable design changes and render the actual result. Do not create a prompt-only deliverable or an unused screenshot.

Build/maintain a reusable design system for navigation, typography, spacing, cards, status states, progress bars, tables, empty/error states, dialogs, buttons and settings. Map artifacts to actual application controls and record names, renders and application screenshots at the same revision. Preserve recoverable versions of prior design artifacts.

---

# PART 24 — PHASED IMPLEMENTATION AND CHECKPOINTS

## Phase A — Baseline and correctness
Audit repository/build/tests, document the actual baseline, inspect false-success paths, confirm scan/transfer state machine and protect the current release.

Acceptance: current baseline and known failures are reproducible and documented.

## Phase B — Enumeration and WPD stream backend
Implement actual WPD object discovery, both observed iPhone storage layouts, device-supported properties, recursive enumeration, `IStream` reads, COM lifetime management, cancellation, staged writes, and source-stream hash verification.

Acceptance: build/tests pass; physical iPhone tests show exact backend and accessibility scope; source and destination hashes match for true stream copies; errors/disconnects cannot create false success.

## Phase C — AFC dependency and selection
Pin/reproduce native dependency, verify P/Invoke signatures and handles, build/service lifecycle, probe accessible media roots, and integrate only after correct enumeration/read/disposal tests.

Acceptance: dependency provenance and hashes verified; actual supported physical-device session passes; unavailable capability remains safely disabled.

## Phase D — Transfer engine and recovery
Complete SQLite state machine, queue resume, transactional staging, collision policy, hashing, source changes, report reconciliation and performance instrumentation.

Acceptance: failure injection and restart tests reconcile database state with real files; no false completion.

## Phase E — Android compatibility
Verify MTP/WPD behavior and any justified device-specific backend across multiple physical models/versions.

Acceptance: compatibility matrix is evidence-backed and failure modes recover safely.

## Phase F — Full Designer and UI
Make all intended pages genuinely Designer-editable, connect controls to real services, use existing themes/design system, and fix responsive layout/accessibility.

Acceptance: every page opens/saves/reopens in Visual Studio Designer; no serialization loss; all supported DPI/window sizes pass layout checks.

## Phase G — Wireless sender and receiver
Implement an actual sender workflow for iOS/Android where supported, authenticated pairing, secure network exposure, user approval, staging, verification and recovery.

Acceptance: end-to-end sender-to-receiver tests pass. Receiver-only behavior is not counted as complete wireless media transfer.

## Phase H — Commercial/release validation
Test clean Windows 10/11 builds within actual framework support boundaries, installer/signing/dependency licensing, update/recovery and real licensing/Steam integration.

Acceptance: all published claims map to passing tests or explicitly limited support statements.

After every phase, update progress, changed-file list, commands/results, evidence class, blockers and next action. Commit coherent checkpoints when appropriate. Do not stop after producing a plan; implement as much as the environment supports.

---

# PART 25 — RELEASE DELIVERABLES

Produce:

- Updated full solution and source code.
- Full Visual Studio Designer source/resources for all intended screens.
- Native backend code and verified/pinned dependency manifests for each enabled backend.
- Database migrations and recovery behavior.
- Unit, mocked integration and hardware-test procedures.
- Build, test, publish and installer scripts.
- README, architecture and API documentation.
- Research source register.
- `docs/AUDIT-1.8.0.md`.
- `docs/IMPLEMENTATION-GAP-MATRIX.md`.
- `docs/DEVICE-COMPATIBILITY-MATRIX.md`.
- `docs/TRANSFER-ENGINE-DESIGN.md`.
- Updated progress checkpoints.
- Compatibility and unsupported-capability list.
- Performance report with environment and repeat-run data.
- Device test report with model/OS/backend details.
- Before/after screenshots tied to exact builds.
- FigCraft artifact references/rendered outputs.
- Installer and dependency/license details.
- Release ZIP, artifact hashes, and exact build/test commands.

For each claimed capability, mark it accurately as implemented, automated-test verified, hardware verified, not verified, blocked, or not implemented. A unit test does not equal hardware validation; static inspection does not equal a successful build; a successful build does not equal real-device compatibility.

## FINAL DEFINITION OF SUCCESS

I-Total Transfer succeeds when it reliably transfers the full eligible collection that the chosen backend actually exposes, verifies copied bytes to the strongest supported level, survives interruptions, resumes safely, preserves the source, and explains every unresolved or inaccessible item without hiding it.

The application must feel polished and easy to use while providing sufficient diagnostics for a developer to understand exactly what happened. Its advantage is evidence-backed device access, efficient enumeration, measured transfer optimization, strong integrity checks, durable recovery, Designer-quality Windows UI, tested compatibility, and honest reporting—not exaggerated speed claims or an AI label.

**Begin now: inspect the current repository and establish the real build/test baseline. Then implement the highest-severity correctness and backend gaps without breaking the existing release. Do not stop at writing this directive.**

---

# MERGE NOTE: ADDITIONAL REQUIREMENTS PRESERVED FROM THE FOLLOW-UP DIRECTIVE

The following precise requirements from the additional user-provided text are explicitly preserved in the sections above and restated here for traceability:

1. Inspect current branch, HEAD commit, uncommitted changes, ignored files, submodules and build configuration before changes.
2. Record baseline commit, SDK/runtime, Release build result, complete test counts, publish result, artifact hashes, and hardware limitations.
3. Preserve all user changes and avoid destructive repository operations.
4. Create an appropriate feature branch from the verified baseline, while inspecting existing useful branches before creating another.
5. Require evidence, compatibility/licensing analysis, migration and rollback for architectural replacements.
6. Save progress at bounded phases with files changed, commands/tests actually run, blockers and coherent checkpoint commits.
7. Never invent test/build/hardware/benchmark evidence and distinguish automated, simulated, live hardware, historical and untested results.
8. Keep the known-good build usable until the candidate passes acceptance.
9. Reconcile all named repository docs, screenshots, manifest, native dependency manifest and source modules.
10. Trace UI controls through events, services, engines, backends, persistence and reports; identify dead code, stubs, unused settings, ignored exceptions, hidden fallbacks and stale/overstated docs.
11. Keep one explicit contract governing complete/incomplete/empty/failed/unstable enumeration, completion grades, cancellation, interruption, failure, no-op, unsupported and inaccessible source data.
12. Account for every encountered source object, exclusions with reason, and exact outcome/count/bytes; use checked arithmetic.
13. Reconcile discovered, eligible, excluded, already present, copied, source-hash verified, destination-only verified, size-verified, unverifiable, inaccessible, failed and outstanding counts/bytes.
14. Support both observed iPhone WPD media layouts: storage container with DCIM, and storage container exposing camera-roll album folders directly.
15. Use the actual device manager/interfaces, distinguish connected/remembered/enumerated/trusted/accessible/busy/disconnected/unsupported states, recurse all reachable albums/resources, use device-supported property queries/streams, deterministic COM cleanup and required serialization.
16. Handle empty folders, duplicates, zero-byte files, unknown sizes, sidecars, Live Photo components, HEIC/HEIF, MOV/video and other exposed media without assuming fixed folder names or hiding errors.
17. The user's latest pasted section ended mid-list at “HEIC/HEIF, MOV,”. Existing requirements for JPEG/PNG, AAE, RAW where exposed, Live Photo preservation and metadata were retained from the earlier directive rather than treating that truncation as a removal request.

This merge consolidates repeated ideas into their relevant sections while preserving every distinct requirement. Where older text and newer specifics overlap, both are retained as explicit acceptance criteria instead of silently deleting one.
