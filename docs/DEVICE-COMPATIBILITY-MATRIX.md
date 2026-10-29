# Device compatibility matrix — 1.8.0

Consolidates `docs/DEVICE_TEST_MATRIX.md` (drill-level record) into a device/backend view. Statuses here
describe what the product can do with a device class, not what one drill observed.

Evidence classes: **LIVE** recorded physical run in this repository, **SIM** automated simulation,
**STATIC** code inspection only, **NONE** no evidence.

| Device | OS | Transport / backend | Detection | Enumeration | Stream copy + source hash | Write to device | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| iPhone 13 Pro | iOS 27.0.1 | WPD (native, `WpdDevice`) | LIVE | LIVE: 55,185 files / 151.34 GiB, `Complete` (2026-10-22) | LIVE: 8/8 HEIC+MOV `SourceAndDestinationHash` (2026-10-22) | not supported by WPD | PHYSICAL DEVICE VERIFIED |
| iPhone 13 Pro | iOS 27.0.1 | AFC (`AfcDevice`) | STATIC | STATIC only; backend inert | STATIC only | STATIC only | BLOCKED (empty dependency hashes) |
| iPhone 13 Pro | iOS 27.0.1 | Shell (`ShellIPhoneDevice`) | LIVE (fallback) | LIVE historical | size-only by design (no source stream) | not supported | IMPLEMENTED — NOT VERIFIED after the latest changes |
| HUAWEI Y7 Prime 2018 | Android 8 (MTP) | WPD | LIVE | LIVE: 4,103 files / 14.54 GiB (2026-10-22) | LIVE: 612,420,912-byte MP4 resumed to hash equality | not supported (read-only MTP path) | PHYSICAL DEVICE VERIFIED |
| Android phone in PTP mode | — | WPD | LIVE (historical, 2026-10-24) | camera-only warning shown (2026-10-24); revalidation on the current candidate is pending | n/a (warning state) | n/a | HISTORICAL PHYSICAL PASS; CURRENT-CANDIDATE REVALIDATION PENDING |
| Android, other vendors/versions | — | WPD | SIM | SIM | SIM | not supported | NOT VERIFIED — no second Android model tested |
| iPhone/iPad, other models | — | WPD | SIM | SIM | SIM | n/a | NOT VERIFIED |
| Any device, wireless | — | HTTP receiver | LIVE receiver only | sender-side enumeration not implemented | receiver stores + verifies uploads | n/a | BLOCKED — no sender application |

## Backend capability declarations (source of truth: `BackendCapabilities`)

| Backend | Enumerate | Read source | Hash source bytes | Read back committed | Write to device | Categories |
| --- | --- | --- | --- | --- | --- | --- |
| WPD | yes | yes | yes | no | no | Photos, Videos (Android adds Music/Files where volumes expose them) |
| Shell | yes | via CopyHere | no | no | no | Photos, Videos |
| AFC | yes (when active) | yes | yes | yes | yes | Photos, Videos, Music, Files |
| Mock (tests) | yes | yes | configurable | no | no | Photos, Videos |

## Explicit limits

- Windows-visible content is not the whole Photos library. Cloud-only iCloud originals cannot be read over
  USB; the app warns when the exposed listing looks suspicious but cannot prove the setting.
- A lock/trust failure is reported as a preflight state; only the iPhone has a documented "not answering"
  path (the HUAWEI keeps MTP alive while locked, so AN08 is not applicable there).
- ADB is not used and is not required. No Android write path exists in this release.
- AFC remains disabled until audited binaries with pinned SHA-256 and a licence review are shipped.

## What would change a row

| Row | Evidence needed |
| --- | --- |
| AFC rows | pinned native binaries, hash verification, a physical read/write drill |
| Other Android vendors | attach the device, run the standard Android drill set (AN01–AN14) |
| Other iPhones/iPads | attach, run IP01–IP13 |
| Wireless sender | a real sender app/Shortcut plus the end-to-end drill in `docs/WIRELESS.md` |
| Windows 11 | run the visual and device drills on a Windows 11 machine |
