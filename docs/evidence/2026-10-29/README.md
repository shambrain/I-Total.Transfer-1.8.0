# Hardware evidence — 2026-10-29

Raw JSON written by `tools/DeviceDrill` on this machine, with both phones attached. Nothing here is
edited by hand; the drill serialises the engine's own records.

| File | Drill | What it contains |
| --- | --- | --- |
| `probe-apple.json` | `probe --family apple` | iPhone identity, status, capabilities |
| `probe-android.json` | `probe --family android` | HUAWEI identity, status, capabilities (4 categories after the family fix) |
| `scan-iphone.json` | `scan --family apple` | 57,731 files / 181.52 GiB, 13 folders, counts and warnings |
| `scan-android.json` | `scan --family android` | 4,131 files / 17.37 GiB, 307 folders, 263 zero-byte files |
| `copy-iphone-heic-mov8.json` | filtered iPhone copy | 8/8 files at `SourceAndDestinationHash`, verification 8/8 |
| `copy-zero-android.json` | 20 zero-byte Android files | 20/20 `SourceAndDestinationHash`, verification 20/20 |
| `copy-camera-android.json` | Android bulk copy (wider selection than planned) | 2,056 files / 5.84 GiB, all hash-verified, verification 2,056/2,056 |
| `rename-run3.json` | Android rename policy, third run | `ALREADY PRESENT - NO FILES NEEDED COPYING`, no second copy |
| `cancel-android2.json` | Android cancel after 2 s | run cancelled; the manifest in the destination records `CANCELLED` |
| `resume-android2.json` | Android resume | 184,296,485 bytes, `HASH-VERIFIED COMPLETE`, verification 1/1 |
| `cancel-iphone.json` | iPhone cancel after 3 s | run cancelled; the destination manifest records `CANCELLED` |
| `resume-iphone.json` | iPhone resume | 261,575,329 bytes, `HASH-VERIFIED COMPLETE`, verification 1/1 |
| `sweep-64/256/1024/4096.json` | Android buffer sweep | one 184,296,485-byte MP4 per buffer, each hash-verified; single runs, not a benchmark |
| `lock-iphone.json` | iPhone lock drill | 7,336,273,708-byte MOV copied `HASH-VERIFIED COMPLETE` while the phone was locked |
| `probe-apple-locked.json` | probe with the iPhone locked | returns the HUAWEI (Android) — the locked iPhone is absent from WPD |
| `pnp-wpd-while-iphone-locked.txt` | Windows device list while locked | only the HUAWEI and two unrelated WPD devices |

The reports describe one run each. They are not benchmarks and do not claim statistical performance.
The companion narrative, including the two defects these drills found, is in
`docs/DEVICE_TEST_MATRIX.md` and the 2026-10-29 entry of `docs/PROGRESS.md`.
