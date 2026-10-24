# AGENTS.md - permanent project instructions

Read `docs/MASTER_IMPLEMENTATION_PLAN.md` and `docs/PROGRESS.md` before changing anything, then inspect the
working tree. Continue implementing the remaining plan from the actual repository state. Do not stop because
both phones are disconnected: finish every task that automated tests and simulated devices can prove, record
physical-hardware work as pending, and leave a precise continuation record. Do not merely report a plan. Make
actual code changes, verify them, and move to the next unfinished task.

## 1. Mission

I-Total Transfer is a Windows desktop application (Deveg) that moves phone media to and from a PC with
verification. It supports iPhone and Android through separate, capability-aware backends and one shared
transfer engine. Preserve and extend the existing implementation; never replace working functionality without
a recorded reason.

## 2. Non-negotiable honesty rules

- Never claim a feature works on hardware that was not tested. Simulated tests are labelled simulated.
- Never report a test as passed unless it ran in this workspace and its output is recorded.
- Never present a synthetic benchmark as a device measurement.
- Never mark an incomplete scan as complete; `EnumerationStatus.Complete` is the only value that may be
  cached or reported as a finished listing.
- Never let a licence, activation or store error abort, corrupt or abandon an in-progress transfer. The gate
  is at the start of a new job; resuming is always allowed.
- Never ship a private key, certificate, password or payment-provider secret in the application, the source
  tree or a package.
- Never weaken a test to make it pass; fix the product or correct a genuinely wrong expectation and say which.
- Never delete user data during install, upgrade or uninstall by default.

## 3. Documents of record

| File | Purpose |
| --- | --- |
| `docs/MASTER_IMPLEMENTATION_PLAN.md` | Every requirement, its state, dependencies and next action |
| `docs/PROGRESS.md` | Session-by-session record: changed files, commands, results, blockers, next task |
| `docs/DEVICE_TEST_MATRIX.md` | Physical hardware evidence and pending drills |
| `docs/KNOWN_ISSUES.md` | Open defects, limitations and technical debt |
| `docs/TEST-PLAN.md`, `docs/RELEASE-ACCEPTANCE.md`, `docs/BUILD-VALIDATION.md` | Test procedures and evidence |
| `docs/ARCHITECTURE-REVIEW.md`, `docs/RECOVERY.md`, `docs/WIRELESS.md`, `docs/PERFORMANCE-AUDIT.md` | Design notes |
| `docs/UI-DESIGN-SYSTEM.md`, `docs/UI-REQUIREMENTS-MATRIX.md` | UI tokens, components and design-to-runtime evidence |
| `docs/INSTALLER.md`, `docs/RELEASE-SIGNING.md` | Packaging and signing |
| `PROJECT-STATUS.md`, `README.md`, `CHANGELOG.md` | Shipped claims, user documentation, release history |

Task states: **NOT STARTED**, **IN PROGRESS**, **BLOCKED BY EXTERNAL REQUIREMENT**,
**IMPLEMENTED, NOT FULLY VERIFIED**, **TESTED AND PASSED**, **FAILED**. Implementation is not verification.

At the end of every session update `PROGRESS.md` and the plan with the exact build/test commands and results.
If context is running out, write the continuation record before doing anything else.

## 4. Architecture rules

- One shared engine (`Services/TransferService.cs`) for enumeration, queue, staging, verification, retries,
  cancellation and reporting.
- iPhone and Android are separate backends behind `Devices/ITransferBackend.cs`. `WpdDevice` is one native
  MTP backend with a per-family storage policy; `DeviceFamily` decides from what the device reports
  (name/manufacturer/model), never from `WPD_DEVICE_TYPE`.
- `MediaCategory` / `MediaCategories.Of(ext)` is the single source of media-group membership;
  `BackendCapabilities.Categories` gates what is offered.
- Staged writes, size checks and SHA-256 where supported. Report the weakest verification level honestly.
- Manifests are transactional and versioned; recovery rebuilds the plan from outstanding entries only and
  refuses unsafe paths.
- The scan cache is one JSON file per (backend, device), written only for complete enumerations, and never
  used to feed Overview tiles as if it were a live scan.
- Android storage: walk every volume, label with the device's own volume name, skip `Android\data`,
  `Android\obb` and `.thumbnails`, walk `Android\media`. A camera-only listing raises the PTP warning.
- Two attached phones are ordered by `AppSettings.PreferredHandset`, not by Windows enumeration order.
- Native dependencies are fail-closed: empty hashes mean the backend stays inert.

## 5. Build and test commands

```powershell
# Release build
dotnet build I-Total.Transfer.sln -c Release --no-incremental

# Full regression suite (hang detection is mandatory)
dotnet test tests\I-Total.Transfer.Tests -c Release --blame-hang --blame-hang-timeout 240s

# Publish
dotnet publish src\I-Total.Transfer\I-Total.Transfer.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# Package (portable ZIP; setup only when Inno Setup is present)
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1 -Version 1.8.0
```

Target: 0 warnings and 0 errors. Kill `I-Total.Transfer`/`testhost` before building; a running process locks
the output. Any source edit changes the executable hash, so rebuild, retest, republish, update
`PROJECT-STATUS.md`, repackage and refresh `dist\SHA256SUMS.txt`.

## 6. Testing rules

- MSTest 3.6.4: use `Assert.ThrowsException<T>`, not `Assert.ThrowsExactly`.
- There is no `InternalsVisibleTo`; anything under test must be public.
- Tests that create WinForms controls derive from `WinFormsTestBase` (it restores the synchronization
  context). Otherwise later async tests hang.
- Use simulated backends and fault injection for disconnects, timeouts, permission errors, no space and
  collisions. Both adapters must satisfy the same engine contract.
- A new commercial rule needs a test that fails without it: every kind of unsuccessful job free, duplicate
  report free, restart persistence, single-store loss, tampered signature, expired/refunded/revoked
  entitlement, wrong channel/product/app id, resume allowed after exhaustion.
- Installer changes are tested by executing the real script against temporary folders and a temporary HKCU
  key.

## 7. Licensing rules (v1.8.0)

- Trial: default 3 meaningful jobs, set at build time with `-p:TrialJobLimit=`. A job counts only when the
  transfer completed, was not cancelled, had no failures, and wrote at least one file with non-zero bytes.
- Entitlements are RSA-PSS/SHA-256 JSON envelopes signed by the issuing backend; the app embeds at most a
  public key (`src/I-Total.Transfer/Licensing/public-key.pem`). Missing key means `NotConfigured`, never
  acceptance.
- Channels are `direct` and `steam`. A Steam entitlement is bound to the app id in `steam_appid.txt`.
- Revocation and refunds use a signed list or the `Refunded` flag; an unsigned list changes nothing.
- The trial ledger lives in `%APPDATA%\I-Total Transfer\trial.json` and HKCU, merged pessimistically. Keep it
  local; do not send it anywhere.
- Wireless reception costs one trial job per sender session, charged after the first committed file.
- No licence check may run while a transfer is in progress.

## 8. Packaging and signing rules

- Per-user installation only: `%LOCALAPPDATA%\Programs\I-Total Transfer`, `PrivilegesRequired=lowest`.
- No firewall rule, service or scheduled task, ever. Wireless receiving is opt-in and binds to a private
  interface only.
- Uninstall removes only manifest-recorded files and keeps `%APPDATA%` unless `-RemoveUserData` is passed.
- Never package an executable whose hash does not match the expected hash.
- Signing is explicit: `installer\Sign-Artifacts.ps1`, then verify with `signtool verify /pa`. Never embed a
  certificate or password in the source, the installer or the app.
- PowerShell 5.1 reads BOM-less files as ANSI: keep `.ps1` files ASCII-only, or write them with a BOM.

## 9. Versioning and release

- Deliverable version: **1.8.0** until the commercial release is actually cut. Update `CHANGELOG.md` under
  the existing heading, not a new unreleased version, unless a decision to bump is recorded.
- The shipped hash table in `PROJECT-STATUS.md` must match the files in `dist` and `publish`.
- Keep the source zip reproducible from the tree, and verify that the embedded executable is byte-identical.

## 10. Session continuity

Before stopping:

1. Update `PROGRESS.md` with completed work, files changed, commands and exact results.
2. Update the master plan states, the device matrix and `KNOWN_ISSUES.md` if affected.
3. Record blockers with the external thing that would unblock them (certificate, toolchain, hardware, SDK).
4. Write the exact next task and its dependencies so the next session can resume without this conversation.
