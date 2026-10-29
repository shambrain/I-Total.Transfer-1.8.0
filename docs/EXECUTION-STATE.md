# Execution state — master directive

Updated: 2026-10-29 (session 1). Read this first on resume.

## Specification

- Master directive: `I-Total-Transfer-OpenCode-Master-Directive.md` — read in full (799 lines) on 2026-10-29.
- Execution instruction: `I-Total-Transfer-OpenCode-Execution-Instruction.md` — read in full (132 lines).
- Both files are untracked in the repository; they are specification inputs, not product source.

## Repository

| Item | Value |
| --- | --- |
| Root | `C:\Users\Administrator\Documents\GitHub\I-Total.Transfer-v1.5.0-source` |
| Baseline branch / commit | `main` / `17c0b1adff502ffa4b70fa83e19dff8f8e782676` |
| Working branch | `work/master-directive` |
| Working tree | see `git status`; changes from phase 1 are committed at the checkpoint recorded below |
| Remote | `origin` = `https://github.com/shambrain/I-Total.Transfer-1.8.0.git` (private) |

## Current status

- Gate A: **complete for the audit portion** — `docs/AUDIT-1.8.0.md`, this file,
  `docs/IMPLEMENTATION-GAP-MATRIX.md`, `docs/DEVICE-COMPATIBILITY-MATRIX.md`,
  `docs/TRANSFER-ENGINE-DESIGN.md` written from inspection and executed commands.
- Gate B: **in progress** — D1/D2/D3 fixed and tested (zero-byte vs unknown size, shared enumeration
  accounting, unknown-size resume). Remaining: stability timestamps (D6).
- Gate C/D: **next** — SQLite hash/verification columns (D4), reconciliation report (D8), retry
  classification (D5).

## Evidence — commands actually run

```text
dotnet build I-Total.Transfer.sln -c Release --no-incremental
  -> 0 warnings, 0 errors (baseline, before changes: 6.27 s)

dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s
  baseline (17c0b1a): Passed! Failed: 0, Passed: 460, Skipped: 2, Total: 462, Duration: 32 s
  after D1-D3:        Passed! Failed: 0, Passed: 472, Skipped: 2, Total: 474, Duration: 30 s
```

Artifacts at baseline (unchanged by this session so far):

| Artifact | SHA-256 |
| --- | --- |
| `publish/I-Total.Transfer.exe` | `8FCB2B5095365995813AF028EFE4A27E6EC538A32ADEA12F153919AD0E61CABB` |
| `dist/I-Total.Transfer-1.8.0-win-x64.zip` | `0E41DEC310CEFAD3FD71C5181407B236E14F52F9E3684D4FE7F1C951F7513374` |
| `dist/installer/I-Total.Transfer-1.8.0-portable-win-x64.zip` | `21742374A48BC0FA6A7A5DDD3291CD87A8BC2ED472300F5DFE08628F53C21653` |
| `dist/installer/I-Total.Transfer-1.8.0-setup.exe` | `2E34EED05F69B04AD019C6E1641CB846BC7B08D6BE3C44BCE644314B49AEBC76` |
| `dist/I-Total.Transfer-1.8.0-source.zip` | `3412D03B111CCF6CD5740A1AB011F18E92198EB45AC58059A67F2C8D3F112BE7` |

A candidate executable has **not** been published from this session's source yet. The shipped 1.8.0 build
therefore still lacks D1–D3 and must not be described as containing them.

## Last passing checkpoint

- Commit: see `git log --oneline -3` on `work/master-directive`; the checkpoint message is
  `Record the Gate A audit and fix zero-byte/unknown-size accounting`.
- State at checkpoint: build 0/0, 472 passed, 2 skipped, 474 total.

## Blockers and their evidence

| Blocker | Evidence | Unblocked by |
| --- | --- | --- |
| Physical device drills (iPhone lock, reconnect, 4 GB+, mid-copy pull) | no phone attached; `Get-CimInstance Win32_PnPEntity` shows no handset | attach hardware, run `docs/DEVICE_TEST_MATRIX.md` rows |
| AFC backend | `src/I-Total.Transfer/native/dependencies.json` has empty hashes; `DependencyVerifier` refuses | audited native binaries + licence review |
| Windows 11 | host is Windows 10 22H2 19045 | a Windows 11 machine |
| Visual Studio Designer open/save/reopen | Visual Studio is not installed on this host | run the Designer round-trip on a VS machine |
| Trusted signing / Steam | no certificate, no Steamworks SDK | external credentials |

## Single next concrete action

Implement D4: add `SourceSha256`, `StagedPayloadSha256`, `DestinationSha256`, `Verification`, `SizeKnown` and
`BytesWritten` to `SessionDatabase.Files` with an idempotent `ALTER TABLE` migration, write them in
`UpsertFiles`, and add a migration + round-trip test. Then D8 (reconciliation report), D5 (retry
classification), D6 (stability timestamps), updating the ledger and running the full suite after each.

## Ordered remaining actions

1. D4 SQLite columns + migration tests.
2. D8 reconciliation JSON/CSV + test.
3. D5 retry classification + test.
4. D6 stability timestamps + unknown-size honesty + test.
5. Gate E audit of WPD/COM/backends; fix concrete defects found.
6. Gate F Designer verification (static + documentation; Visual Studio unavailable).
7. Gate H: rebuild, full suite, publish candidate, update `PROJECT-STATUS.md`, `BUILD-VALIDATION.md`,
   `PERFORMANCE-AUDIT.md`, `RELEASE-ACCEPTANCE.md`, refresh archives and hashes.
8. Update `docs/PROGRESS.md`, this file and the ledger before any stop.

## Uncommitted changes to preserve

At the time of writing, the phase-1 source changes and the new documents are staged for the checkpoint
commit; nothing is discarded. If the session is interrupted before the commit, `git status` lists them.
