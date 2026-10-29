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
- Gate B: **complete for the defects found** — D1/D2/D3/D6 fixed and tested.
- Gate C/D: **complete for the defects found** — D4 (SQLite hashes + migration), D5 (retry
  classification), D8 (reconciliation report) fixed and tested.
- Gate E: **WPD COM leaks fixed** (enumerator, key collection, values, manager); static evidence only.
- Gate F: **Designer coverage verified statically; accessible names added and tested**; Visual Studio
  round-trip not run (not installed).
- Gate H: **candidate built and packaged** — exe `1C62B2B1...`, source-ZIP rebuild identical, installer
  install/uninstall re-verified. Physical hardware, Windows 11 and signing remain blocked.

## Evidence — commands actually run

```text
dotnet build I-Total.Transfer.sln -c Release --no-incremental
  -> 0 warnings, 0 errors (baseline, before changes: 6.27 s)

dotnet test tests\I-Total.Transfer.Tests -c Release --no-build --blame-hang --blame-hang-timeout 240s
  baseline (17c0b1a): Passed! Failed: 0, Passed: 460, Skipped: 2, Total: 462, Duration: 32 s
  after D1-D3:        Passed! Failed: 0, Passed: 472, Skipped: 2, Total: 474, Duration: 30 s
  after D4/D8:        Passed! Failed: 0, Passed: 481, Skipped: 2, Total: 483, Duration: 29 s
  after D5:           Passed! Failed: 0, Passed: 482, Skipped: 2, Total: 484, Duration: 31 s
  after D6:           Passed! Failed: 0, Passed: 485, Skipped: 2, Total: 487, Duration: 31 s
  after Gate E/F:     Passed! Failed: 0, Passed: 488, Skipped: 2, Total: 490, Duration: 31 s
```

Artifacts at baseline (unchanged by this session so far):

| Artifact | SHA-256 |
| --- | --- |
| `publish/I-Total.Transfer.exe` (candidate) | `1C62B2B18AA488939E3DC747C9D7B5772436C376E6AEC27C10ECE5986EC97D7E` (112,191,302 B) |
| `dist/installer/I-Total.Transfer-1.8.0-portable-win-x64.zip` | `96E773D3B4B1217750648E39B577F07084E880EB9B1B7E70FD240798F70CE0DD` |
| `dist/installer/I-Total.Transfer-1.8.0-setup.exe` | `41A2F4A3FE00A7DA9C34CDCAEB3926529D6B7EE080D23DDCF6BAF68C2DE51A36` |
| `dist/I-Total.Transfer-1.8.0-win-x64.zip` and source ZIP | see `dist/SHA256SUMS.txt` after the final rebuild |

The baseline artifacts (`8FCB2B50...` exe, `0E41DEC3...` deployment, `21742374...` portable,
`2E34EED0...` setup, `3412D03B...` source) remain recoverable in the repository history at `17c0b1a`.

The candidate executable **has** been published to `publish/` and packaged. It is not signed and has not
been run against a phone.

## Last passing checkpoint

- Commits on `work/master-directive`: `0c13df2` (Gate A + size truth), `2917be1` (SQLite, reconciliation,
  retries, stability), and the final commit that carries Gate E/F/H and the rebuilt archives.
- State: build 0/0, 488 passed, 2 skipped, 490 total, candidate exe `1C62B2B1...`.

## Blockers and their evidence

| Blocker | Evidence | Unblocked by |
| --- | --- | --- |
| Physical device drills (iPhone lock, reconnect, 4 GB+, mid-copy pull) | no phone attached; `Get-CimInstance Win32_PnPEntity` shows no handset | attach hardware, run `docs/DEVICE_TEST_MATRIX.md` rows |
| AFC backend | `src/I-Total.Transfer/native/dependencies.json` has empty hashes; `DependencyVerifier` refuses | audited native binaries + licence review |
| Windows 11 | host is Windows 10 22H2 19045 | a Windows 11 machine |
| Visual Studio Designer open/save/reopen | Visual Studio is not installed on this host | run the Designer round-trip on a VS machine |
| Trusted signing / Steam | no certificate, no Steamworks SDK | external credentials |

## Single next concrete action

Rebuild the deployment and source archives from the final tree, refresh `dist/SHA256SUMS.txt`, run the
full suite once more, commit the checkpoint, and push `work/master-directive`. After that, the next
unblocked work is hardware: attach the phones and run IP02/IP03/AN02/AN05/AN11 plus the 4 GB+ and
mid-copy-pull drills on the candidate build.

## Ordered remaining actions

1. Final archive rebuild + hashes + suite + commit + push.
2. Physical drills on the candidate (blocked: no phone).
3. Windows 11 visual/device drills (blocked: no Windows 11 host).
4. Visual Studio Designer open/save/reopen (blocked: VS not installed).
5. Trusted signing, Steam, AFC native binaries (blocked externally).

## Uncommitted changes to preserve

At the time of writing, the phase-1 source changes and the new documents are staged for the checkpoint
commit; nothing is discarded. If the session is interrupted before the commit, `git status` lists them.
