# I-Total Transfer 1.8.0 — OpenCode Execution-Control Instruction

**Use this together with `I-Total-Transfer-OpenCode-Master-Directive.md`.** The master directive is the complete product and engineering specification. This file controls how you execute it: continuously, safely, accurately, and with evidence.

## Paste this instruction into OpenCode

You are continuing engineering on the existing **I-Total Transfer** repository. Your controlling specification is the complete file `I-Total-Transfer-OpenCode-Master-Directive.md`. Read that entire file before implementation and follow every applicable requirement, acceptance criterion, deliverable, and non-negotiable rule in it. Do not replace it with a summary, use this execution instruction as a substitute for it, or cherry-pick only easy requirements.

### 1. Start by establishing the real state

1. Identify the actual repository root and current workspace. Confirm the master directive exists and read it completely. If it is not in the workspace, safely check the current directory and the expected user-provided/download location. Never claim to have read a file you cannot access. Record the exact blocker if unavailable; continue with safe repository inventory while resolving access to the required specification.
2. Inspect the current branch, HEAD, working-tree changes, untracked and ignored files, submodules, tags, relevant branches, recent history, solution/project files, `AGENTS.md`, build configuration, CI, tests, artifacts, and all relevant documentation and screenshots named in the master directive.
3. Preserve existing user edits. Do not use destructive reset/clean/checkout operations, force-push, overwrite unexplained work, delete unknown files, or make the current known-good release unusable. Create or use a suitable feature branch only after inspecting the state and preserving the baseline.
4. Establish and record the real build/test baseline before changing implementation. Run only commands available in the current environment. Record exact commands, exit codes, counts, logs, artifact paths and SHA-256 hashes. If Windows/.NET or physical devices are unavailable, clearly record that limitation; do not turn a static review into a claimed successful build or hardware test.

### 2. Execute; do not stop at planning

Do not answer with a roadmap alone. After the baseline, begin implementing the highest-severity correctness and data-loss risks, then continue through every applicable part of the master directive in a dependency-aware order.

- Work through bounded, meaningful phases. Each phase must contain actual code/configuration/UI/test changes where the repository and environment permit—not just recommendations or documentation updates.
- After each phase, run the most relevant available tests, inspect the diff, record evidence, update the canonical progress file, and checkpoint coherent work when appropriate. Then continue automatically to the next safe phase in the same session. Do not stop to ask whether to continue, ask for permission to perform ordinary reversible engineering work, or wait for the user to say “continue.”
- When one task is blocked, record the exact blocker and evidence, then proceed with independent, unblocked work. Do not let one unavailable phone, missing optional native library, failed test, or unavailable UI tool halt unrelated work.
- When a command/test fails, inspect the failure, determine its cause, fix it when safe, and rerun it. Use bounded retries; do not repeat a failing command indefinitely or hide the failure.
- Do not claim that this entire project can be completed in one response or session. Keep working for as long as the current session and tools support useful progress. If a session/tool/context limit forces a stop, first persist the exact state described under **Durable continuation state** below. The next invocation must resume from that state rather than starting over or repeating completed work.
- Do not terminate a turn simply because you have written a plan, completed a convenient subset, or reached a documentation milestone. The task is implementation and verification against the master directive, not production of another prompt.

### 3. Maintain one requirement ledger and evidence trail

Create/update a durable ledger such as `docs/IMPLEMENTATION-GAP-MATRIX.md` and a progress record such as `docs/PROGRESS.md` (use the repository's established canonical progress file if different). Map every master-directive requirement and deliverable to real source files, tests and acceptance criteria.

Use only these truthful statuses, as applicable:

- `NOT STARTED`
- `IN PROGRESS`
- `IMPLEMENTED — NOT VERIFIED`
- `AUTOMATED TEST VERIFIED`
- `PHYSICAL DEVICE VERIFIED`
- `BLOCKED` (include blocker and next action)
- `NOT APPLICABLE` (include reason)

For each meaningful change record: requirement ID/topic; severity and user impact; files/types/methods changed; design decision and evidence; command(s) run; actual result and exit code; test counts; artifact hash if produced; evidence class; remaining gap; next concrete action. Distinguish unit tests, mocked tests, static inspection, successful build, real-device testing, historical evidence, and not tested. Never promote a status without its required evidence. Reconcile contradictory test totals by using actual outputs and explain discrepancies.

### 4. Use the two diagrams as complementary views, not competing specifications

The image labelled **“Proposed architecture”** defines logical responsibility/components. The image labelled **“Proposed transfer pipeline”** defines the ordered runtime workflow. Do not implement them as two separate engines, two queues, two competing state machines, or duplicate layers. Implement shared services/contracts with an explicit runtime flow.

Use this mapping:

| Architecture component | Canonical runtime phase(s) | Required interpretation |
|---|---|---|
| Device preflight and identity | 1. Discover and identify | Identify the connected device and transport; distinguish detected/remembered from genuinely accessible, trusted, unlocked, busy, unsupported, or disconnected states; probe backend capability. |
| Enumeration and source inventory | 2. Enumerate and reconcile | Recursively inventory all reachable objects; capture stable session identity, metadata, inaccessible folders, duplicates/associations, unknown sizes and scope limitations; detect and report incomplete or unstable enumeration. |
| Persistent transfer planner | 3. Plan and preflight | Create/reconcile the persistent SQLite queue, conflict policy, filters, path mapping, available space, resume state and eligibility decisions before copying. |
| Adaptive copy scheduler | 4. Copy through a staged transaction | Schedule bounded, cancelable streams with measured buffer/concurrency choices, incremental source-byte hashing, progress, durable per-file state and bounded retries. |
| Staging and verification | 4. Copy through a staged transaction **and** 5. Commit and verify | Stage to a temporary file; flush and close; independently verify staged bytes against the source-byte hash where supported; commit atomically when the filesystem supports it; verify final destination and assign an honest integrity grade. This is one logical service participating in two ordered runtime phases. |
| Auditable result | 6. Report and recover | Generate per-file outcomes, reasons, byte/count reconciliation, unresolved work, resumable manifest, logs, and JSON/CSV exports. A report must describe observed results, not intended results. |

**Canonical runtime order:**

1. **Discover and identify.** Establish evidence of a real attached device, its family, session identity, transport and backend capabilities. A remembered device alone is not evidence that it is connected or usable.
2. **Enumerate and reconcile.** Build an inventory of the media this backend actually exposes. Recurse reachable containers/albums; record every relevant object and every enumeration failure. Reconcile duplicates, relationships such as Live Photo components, source changes, unknown sizes and partial scans. Never equate “scan finished” or “zero items” with a complete, healthy scan without supporting evidence.
3. **Plan and preflight.** Reconcile the inventory to eligible items; determine exclusions and reasons; ensure safe destination paths, conflict policy, free space, queue/database state, and resume strategy before committing to a transfer.
4. **Copy through a staged transaction.** Read source bytes through the capability-appropriate backend into a temporary destination file. Hash the actual source stream incrementally. Maintain durable progress/state, cancellation and bounded retries. A source hash must not be invented from a destination hash, file size, metadata, or a hash of an unrelated staging step.
5. **Commit and verify.** Flush and close handles; compare the source-byte SHA-256 with an independently computed hash of the staged bytes when source streaming and local staging permit it. Do not commit a failed or mismatched stage. Commit with a same-volume atomic rename where supported; document when it is not supported. Independently verify the final destination according to the specified policy and record the actual integrity grade. Do not label an item `VERIFIED` before the required comparisons pass.
6. **Report and recover.** Produce file-by-file outcomes and reconciled totals, reason-coded exceptions, outstanding work, safe resume state, and JSON/CSV audit output. A partial, canceled, inaccessible, or failed operation must never report “all files copied successfully.”

The six runtime phases are the workflow contract. The six architecture boxes are component boundaries. A component can participate in more than one workflow phase, and a workflow phase can call more than one component. Preserve this distinction in code, documentation and tests.

### 5. Apply the master directive as a testable contract

- Translate every requirement into a source-code location, explicit acceptance criterion and test or documented environmental limitation. Keep the matrix synchronized with implementation.
- Prioritize data-loss and false-success risks, truthful enumeration/accounting, correct backend access and stream handling, persistent/resumable transfer, integrity, collision safety and recovery before performance polish or cosmetic redesign.
- Implement only supported behavior. Capability-probe optional backends and dependencies; do not pretend a mock, stub, receiver-only feature, inactive backend or unverified native DLL is production capability.
- Keep iOS and Android device-protocol differences explicit while sharing suitable queue, planning, staging, hashing, reporting and recovery services. Do not assume that Windows-visible content equals a device's entire Photos library or that cloud-only iCloud originals are available over USB.
- Preserve opaque WPD object IDs as session-scoped identifiers; recursively enumerate using real device properties/resources; dispose COM resources deterministically; cover both specified iPhone folder layouts. Use optional AFC only when its exact dependency/capability is verified. Keep Android MTP/WPD handling distinct from any separately implemented and authorized ADB route.
- Keep source and destination hashing independent. Track whether each item received source-plus-destination SHA-256 verification, destination-only hashing, size-only verification, or no verification. Do not mislabel integrity levels.
- Keep the persistent queue, retries, cancelation, collision policy, staging, recovery and reports truthful and crash-safe. Never delete source files as part of transfer. Never overwrite unrelated destination content without the defined safe conflict policy.
- Treat wireless media transfer as incomplete until the required sender and receiver work end to end on supported platforms. A Windows receiver by itself cannot initiate iOS Photos enumeration.
- Keep the WinForms UI genuinely editable in Visual Studio Designer per the master directive. Do not substitute a runtime-only layout or a screenshot/mockup for real Designer artifacts and working control wiring. If FigCraft is required, inspect the real MCP/tool capabilities and create/edit/render actual project artifacts; do not claim it was used based on a prompt alone.
- Research current technical, platform, package-license, installer, signing and commercial claims where needed. Record URLs, access dates, claim supported and applicability in `docs/RESEARCH-SOURCES.md`. Separate source-backed facts, project evidence, inference and unverified assumptions.
- Performance changes must be measured against a repeatable baseline and must not weaken correctness, cancellation, fairness, bounded resource use or integrity checks. Do not call the engine “AI” or claim it is faster without measured evidence.

### 6. Phase gates — continue in this order, adapting only when dependencies require it

**Gate A — Baseline and audit.** Read the full master directive, inspect repository/docs/screenshots/history and establish build/test/artifact baseline. Produce the audit and gap ledger. Then proceed; do not stop after documenting gaps.

**Gate B — Correctness and inventory.** Fix false-success conditions, explicit state handling, enumeration failures, recursive object inventory, stable session scope, source-item accounting and scope warnings. Test empty, partial, failed, unstable and canceled scans.

**Gate C — Planner and recoverable transfer.** Verify SQLite queue/state transitions, path safety, free-space preflight, collisions, no-overwrite safety, staging, crash recovery, resume, retries and cancellation.

**Gate D — Integrity and audit.** Implement independent source-stream/staged/final destination verification, correct integrity grades, checked byte/count arithmetic, per-file reasons and JSON/CSV reports. Test corruption, truncation, source changes, zero-byte and unknown-size files, Unicode and long paths.

**Gate E — Device backends and preflight.** Audit and implement capability-aware WPD and Android support, the two iPhone layouts, deterministic COM cleanup, optional pinned AFC dependencies only if supported, official driver guidance, lock/trust/disconnect states and recovery. Keep untested hardware claims explicitly unverified.

**Gate F — Full Designer UI.** Trace every real control end to end. Make required pages, states, DPI/layout/accessibility, themes and status indicators genuinely Designer-editable, and fix the defects identified by screenshots/code. Link UI acceptance evidence to exact builds and source files.

**Gate G — Wireless and integrations.** Implement and test a real sender/receiver flow where feasible; secure pairing/network boundaries, explicit approvals, path-safe transfer, staging and verification. Keep unsupported platforms and missing sender components clearly marked.

**Gate H — Performance, compatibility and release.** Repeat measured benchmarks, verify Win10/11 support boundaries, automated test suites, physical-device matrix where hardware is available, installer/dependencies/licensing/signing and real licensing/Steam integration. Do not mark release-ready until the master directive's gates are met.

These gates are not permission to stop between phases. After finishing one gate, record its actual status and move to the next unblocked gate. Reorder only when a real dependency requires it, and document why.

### 7. Durable continuation state (mandatory before any forced stop)

Maintain `docs/EXECUTION-STATE.md`. Update it after every meaningful phase and before ending a session. It must contain:

- Master directive path and confirmation that its full contents were read.
- Repository root, branch, HEAD, baseline commit and whether the working tree is clean.
- Current overall status and the current gate/requirement IDs.
- What was completed, with changed-file paths and evidence references.
- Exact build/test commands run, exit codes, full counts and log/artifact paths.
- Last known passing checkpoint and its commit/hash.
- Active failure/blocker, diagnosis so far, and what is safe to do next.
- **The single next concrete action/command**, followed by the remaining ordered actions.
- Any uncommitted changes that must be preserved.

Before ending a turn, leave the repository in a recoverable state, update `docs/PROGRESS.md` (or its canonical equivalent) and `docs/EXECUTION-STATE.md`, and state precisely what is and is not verified. On the next invocation, first read these files and the master directive, inspect current Git state, confirm no external change invalidated the checkpoint, and resume at the recorded next action. Do not redo completed tasks unless new evidence shows they need revalidation.

### 8. Required completion behavior

Continue implementation and verification until all feasible requirements in the master directive have been addressed. A requirement that cannot be completed here must have a specific evidence-backed blocker, an honest status, and a concrete next action; do not conceal it by calling the overall product finished. Finish each work session with a concise evidence-based report: changes actually made, build/tests actually run and counts, artifact paths/hashes, remaining blockers, and the next exact action. Never say “done” merely because a phase plan or documentation exists.

**Begin now. Read the complete master directive and repository state, record the baseline, then execute Gate A and continue into the highest-severity implementation work. Do not wait for a separate “continue” message.**

---

## Notes about the diagrams

- **Proposed architecture** answers: “What logical components/services should exist, and what responsibilities do they own?”
- **Proposed transfer pipeline** answers: “In what order does one real scan/transfer run through the system?”
- They are complementary, not alternatives. In particular, “Staging and verification” is a logical component spanning the copy/stage phase and the commit/verify phase. “Auditable result” owns the reporting outcome, while progress and journal events are also emitted during earlier phases.
- The transfer pipeline is the clearer source of runtime sequencing because it separates staging/copy from commit/verification and report/recovery. Use it for orchestrator/state-machine ordering; use the architecture for service boundaries and ownership.
