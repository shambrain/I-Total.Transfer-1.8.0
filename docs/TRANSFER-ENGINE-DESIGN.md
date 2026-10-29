# Transfer engine design — components and runtime order

This document maps the master directive's two diagrams onto the code that exists. The architecture diagram
answers "which component owns what"; the pipeline diagram answers "in which order one run executes". They
describe one engine, not two.

## Components (architecture view)

| Component | Code | Responsibility |
| --- | --- | --- |
| Device preflight and identity | `Devices/DevicePresence.cs`, `WpdDevice.Probe`, `ShellIPhoneDevice.Probe`, `ConnectionDiagnostics`, `DeviceSelectionGuard` | Decide detected / trusted / accessible / busy / disconnected and bind a backend to one phone. |
| Enumeration and source inventory | `WpdDevice.Scan`, `ShellIPhoneDevice.Scan`, `AfcDevice.Scan`, `CameraRoll`, `PortableStorage`, `ScanAccounting` | Walk every reachable object, record per-folder outcomes, classify completeness. |
| Persistent transfer planner | `TransferService.CopyCoreAsync` (plan section), `TransferRules.ApplyFilter`, `ManifestStore`, `SessionDatabase` | Reconcile scan → eligible plan, apply conflict policy, check free space, persist the plan. |
| Adaptive copy scheduler | `CopyCoreAsync` loop, `PauseController`, `WpdDevice.CopyToStage`, `AfcDevice.CopyToStage`, `ShellIPhoneDevice.CopyToStage` | Bounded, cancelable staging with retries and per-file durable state. |
| Staging and verification | `TransferService` commit path, `TransferRules.ClassifyVerification`, `VerifyCoreAsync` | Stage → flush → commit → independently verify → assign the honest grade. Spans pipeline phases 4 and 5. |
| Auditable result | `ExportReports`, `ManifestStore`, `SessionDatabase`, `LogService` | Per-file outcomes, reasons, reconciliation, JSON/CSV, recovery state. |

## Runtime order (pipeline view)

1. **Discover and identify.** `Probe()` returns a `DeviceInfo` with an explicit `DeviceStatus`. A remembered
   device alone is never `Ready`; `WpdDevice` binds the selected device id and refuses a different one
   (`DeviceSelectionGuard`).
2. **Enumerate and reconcile.** `Scan()` walks containers recursively. Each folder records `Succeeded` and an
   error; 20 consecutive unreadable objects abort the walk as a device disappearance; duplicates are counted.
   `ScanAccounting.BuildReport` produces the single completeness verdict used by every backend.
3. **Plan and preflight.** `CopyCoreAsync` refuses `Failed`/`Empty` scans and duplicate paths, applies the
   filter (excluded files are recorded `Skipped`, never dropped), computes required bytes from the plan,
   checks free space plus a reserve, writes `RUNNING` to the manifest, and opens SQLite.
4. **Copy through a staged transaction.** For each item: mark `Copying`, increment attempts, ask the backend
   to stage. WPD/AFC hash the raw source stream while writing; Shell copies through the shell and returns no
   source hash. The engine validates the staged size only when the source size is known, then commits with
   `File.Move` (same volume, atomic on NTFS), backing up any replaced file first.
5. **Commit and verify.** The engine hashes the committed destination and compares it with the source digest
   when one exists (strongest), otherwise with the staged read-back (Shell), and classifies the grade. A
   mismatch discards the new copy and restores the previous destination. Unknown sizes are accepted only
   when a source-to-destination hash proves the bytes.
6. **Report and recover.** The final status is recomputed from persisted states, not from the loop counters:
   any `Failed`, `Inaccessible`, `CopiedUnverified`, `Discovered`, `Queued`, `Copying` or `Paused` entry, any
   unverifiable item, or an incomplete enumeration forces a non-complete verdict. JSON and CSV reports are
   written into the destination.

## State machine (`TransferState`)

```text
Discovered -> Queued -> Copying -> IntegrityVerified   (strongest available grade)
                            |-> SizeVerified            (legacy baseline only)
                            |-> CopiedUnverified        (no size and no source digest)
                            |-> Failed                  (retries exhausted, verification failed, collision)
                            |-> Inaccessible            (unsafe source path)
         Queued -> Skipped                              (filter or conflict policy)
         Copying -> Paused -> Copying                  (user pause)
```

`SourceChanged` is an overlay, not a state: a later scan that sees a different size, modification time or
size-known flag marks the entry, and `PreserveAndReport` refuses to treat the destination as current.

## Verification grades

| Grade | Meaning | Where assigned |
| --- | --- | --- |
| `SourceAndDestinationHash` | Source bytes were read through the backend and hashed while streaming; the committed destination hashes equal that digest. | `ClassifyVerification` with a source hash |
| `DestinationHash` | Only a staged-payload digest and the destination digest match; the backend cannot expose raw source bytes. | Shell path |
| `SizeOnly` | Sizes match, no digest comparison. | legacy destination baseline |
| `None` | Nothing was proven. | failures and unknown-size items without a digest |

The weakest grade across the session is reported in the summary and the session report.

## Recovery invariants

- The JSON manifest is the recovery plan; SQLite is the queryable history. If SQLite cannot be opened the run
  continues on JSON and logs the downgrade.
- A destination is either verified or absent. A copy whose verification fails is deleted and, if it replaced
  a file, the previous file is restored from `.i-total/backup`.
- Resume never trusts a filename: it compares the recorded digest, and only falls back to size when no digest
  exists, in which case the entry is recorded at size-only strength.
- `LastSessionStatus` is stamped `RUNNING` before the first byte so an interrupted job is recognisable.

## Concurrency and resource bounds

- One device operation at a time: the WPD backend serialises through `_gate`; staging runs on a worker thread.
- Buffers: 64 KiB–4 MiB, chosen by settings (`WpdDevice._copyBufferBytes`), one buffer per copy, not pooled
  globally (a single stream per device session).
- SQLite writes are batched (64 entries) inside one transaction; the JSON manifest is throttled (250 changes
  or 2 s) with forced flushes at every consequential boundary.
- UI progress is throttled by the scan reporter and by the engine's per-file report.

## Known limits recorded honestly

- Shell cannot provide a source hash; its strongest claim is destination-hash plus size.
- A same-size source change with no modification timestamp is undetectable without re-reading the source.
- Two stable scans detect instability; they do not prove whole-library coverage.
- Unknown sizes cannot contribute to byte totals; the report counts only measured bytes and names the
  unknown-size count separately.
