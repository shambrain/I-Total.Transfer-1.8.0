# Screenshot evidence

These are runtime captures of **I-Total Transfer 1.8.0**, not design mockups. Each set says what it is and
what it does not prove.

`MANIFEST.txt` lists every one of the 82 PNGs with its size and SHA-256, so a copy of the gallery can be
checked against this index. It is generated from the files, not typed by hand.

Environment: Windows 10 Pro 22H2 (19045), .NET 10.0.302, app version 1.8.0. The display available during
the 200 % pass was 3840x2160 at 192 DPI; the 96 DPI set is a DPI-unaware harness render of the real
`MainForm`. The physical handsets were an iPhone 13 Pro (iOS 27.0.1, native WPD) and a HUAWEI Y7 Prime 2018
(Android, native WPD/MTP).

| Set | What it is | What it does not prove |
| --- | --- | --- |
| `96dpi/` | DPI-unaware harness rendering the real `MainForm` at `DeviceDpi=96`, client root 1416x919. All seven pages in Neon, Slate and Light. | Not a native 100 % desktop screenshot; no 100 % display exists on the host. |
| `150dpi-min/` | PerMonitorV2 harness at 150 % with a 1740x1110 device window (the shell's minimum logical size). Seven pages, Light. | A harness render, not a desktop screenshot. |
| `200dpi-min/`, `200dpi-design/` | Real 192 DPI (200 %) harness at 2320x1480 (minimum logical size) and 2840x1800 (design size). Seven pages each, Light. | The window is larger than the logical desktop (1920x1080), so these are 200 % DPI renders, not full-desktop screenshots. |
| `200dpi-published/` | The **published exe**, driven through UI Automation and captured with a DPI-aware `PrintWindow` at 192 DPI (2866x1871 device). Seven pages. | Window captures, not a composited desktop. |
| `live-150/` | The published build at the host's native 150 %, `PrintWindow` plus UI Automation dumps (2026-10-22). | Some pages predate the later caption/minimum-window fixes; they are dated evidence. |
| `themes/` | Overview in all three themes and the Appearance section at 150 %. | Palette evidence only. |
| `drills/` | Physical-device drills: iPhone scan, unplug mid-scan rejection, filtered plan, Android cancel/recovery, PTP warning, Android lock, filtered no-op. | No iPhone lock or revoked-permission drill; the HUAWEI lock does not disable MTP. |
| `diagnostics/` | Attached-diagnostics page with both phones and the matching row dump. | The iPhone was briefly absent in one earlier run; this capture is the two-phone run. |
| `published/` | The published exe at 200 % (DPI-aware `PrintWindow`) and maximized at 150 %. | Not a 200 % full-desktop capture. |

## 96 DPI — all pages, three themes

`96dpi/<theme>/<page>.png`, theme in `neon`, `slate`, `light`; page in `dashboard`, `transfer`, `library`,
`wireless`, `history`, `settings`, `diagnostics`. The header keeps its 78 px band, and the reflowed
Wireless/Library/Transfer/History toolbars stay inside their cards.

## 150 % minimum window

`150dpi-min/<page>.png` for the same seven pages. This is the size at which the Wireless card reflows, the
Library toolbar uses two rows, the Transfer log toolbar wraps and the History database path gets its own row.

## 200 % — minimum and design sizes

`200dpi-min/<page>.png` and `200dpi-design/<page>.png`. Text, cards, chips and the reflowed toolbars scale
uniformly with no clipping.

## 200 % — published exe

`200dpi-published/<page>.png`. The real shipped binary, navigated with UI Automation, captured with a
DPI-aware `PrintWindow` (2866x1871 device).

## Live 150 % audit

`live-150/` contains the 2026-10-22 published-build captures and the matching `.txt` UI Automation dumps
were kept in the source workspace:

| File | Shows |
| --- | --- |
| `backup-queue-150.png` | Live progress, queue and log on the published build. |
| `media-library-150.png` | Library with the scan actions and the empty state. |
| `history-150.png`, `history-refreshed-150.png` | History before and after Refresh. |
| `diagnostics-before-150.png`, `diagnostics-run-150.png` | Diagnostics before and after Run. |
| `dialog-verify-with-no-scan.png`, `dialog-export-scan-with-no-scan.png` | The real no-scan dialogs. |
| `dialog-diagnostics-export.png` | The diagnostics bundle save dialog. |
| `wireless-idle-150.png`, `wireless-enabled-150.png` | Receiver disabled and enabled. |
| `wireless-listening-150.png` | Listening status and pairing ticket. |
| `wireless-new-ticket-150.png` | A freshly issued ticket. |
| `wireless-stopped-150.png` | Receiver stopped and ticket cleared. |

## Themes

`themes/overview-{neon,slate,light}-150.png` and `themes/settings-appearance-light-150.png`.

## Hardware drills

| File | What it proves |
| --- | --- |
| `iphone-scan-01-ready.png`, `iphone-scan-02-scanning.png`, `iphone-scan-03-finished.png` | The iPhone is detected over native WPD, the scan runs, and the finished listing is shown. |
| `iphone-unplug-midscan-rejected.png` (+ `.txt`) | A mid-scan unplug ends the listing as incomplete; no cache or completion report is written. |
| `android-filtered-01-scan-finished.png`, `android-filtered-02-confirm.png` | The filtered plan and the confirmation naming the planned file/bytes. |
| `android-cancel-01-recovery.png` | A cancelled Android copy and the recovery bar. |
| `android-ptp-01-check.png`, `android-ptp-02-result.png` | The PTP camera-only warning. |
| `android-locked-01-copy-running.png`, `android-locked-02-result.png` | A copy running while the HUAWEI screen is locked; MTP stays active, so a lock is not a disconnect. |
| `android-filtered-noop-result.png` | A no-op filtered rerun skips the whole-library verification. |

## Diagnostics

`diagnostics/attached-apple-rows.png` shows the attached branch with both phones; the matching
`attached-apple-rows.txt` lists the 10 rows and the summary.

## Published exe

`published/exe-200-dashboard.png` is the shipped binary on the 200 % desktop (DPI-aware window capture);
`published/exe-150-maximized.png` is the maximized 150 % window.

## Gaps

- No native 100 % desktop capture (the host has no 100 % display); the 96 DPI set is a logical render.
- The 200 % sets are DPI renders/window captures, not a 200 % full-desktop screenshot.
- Windows 11, a real iOS wireless sender, an iPhone lock/revoked-permission drill and audible-cue listening
  are not captured here.
