# FigCraft design artifacts — I-Total Transfer 1.8.0

The design source is the FigCraft MCP workspace at
`C:\Users\Administrator\I-Total-Transfer-FigCraft` (headless backend; no app window attached).
Artifacts are real editable `.figcraft` documents, not screenshots used as a substitute.

## Canonical artifact

| Item | Value |
| --- | --- |
| File | `designs/i-total-transfer-design-system.figcraft` (70,375 bytes after the 2026-10-29 revision) |
| Pages | `00 · Design system`, `01 · Neon`, `02 · Light`, `03 · Slate`, `04 · 1.8.0 revision` |
| Layers | 620 at the 2026-10-22 creation; the revision page adds 6 |
| Created | 2026-10-22; revised 2026-10-29 |
| Renders | `renders/i-total-transfer-foundations.png`, `…-overview-neon.png`, `…-overview-light.png`, `…-overview-slate.png`, `renders/i-total-transfer-1.8.0-revision.png` |

The revision render is copied into this repository as
`docs/figcraft/i-total-transfer-1.8.0-revision.png`
(SHA-256 `3E85EA9F7E905BCC06687F584C78A6806011A7F53A398316157731A4FDB0D887`).

## Mapping to the application

| Design page | Application source | Notes |
| --- | --- | --- |
| `00 · Design system` — semantic colour tokens, type scale, radii | `src/I-Total.Transfer/UI/Theme.cs`, `UI/Fluent.cs` | Neon / Light / Slate token columns use the same names and hex values as the `Theme` class. |
| `01 · Neon`, `02 · Light`, `03 · Slate` — overview compositions | `UI/Pages/DashboardPage.Designer.cs` and the running shell | Composition reference for the device card, media groups, metrics and actions. |
| `04 · 1.8.0 revision` | `UI/Accessibility.cs`, `Services/TransferService.cs`, `Core/Reconciliation.cs` | Records the states added or corrected on 2026-10-29: no-op verdict, unknown-size honesty, reconciliation CSV, SQLite verification persistence, accessible names. |

Token values re-checked against `Theme.cs` on 2026-10-29:

| Token | Neon | Slate | Light |
| --- | --- | --- | --- |
| Background | `#040A1E` | `#101114` | `#F3F4F7` |
| Surface | `#0E1A33` | `#17191D` | `#FFFFFF` |
| Accent | `#0B55F1` | `#4A6BC4` | `#0B55F1` |
| Text | `#EAF1FF` | `#F2F3F5` | `#14161B` |
| OK | `#3DDC97` | `#46D19A` | `#0F7A47` |
| Warn | `#FCC146` | `#F2C14E` | `#96620A` |
| Error | `#FF6B85` | `#FF7088` | `#C42B2B` |

Card radius 12, control radius 8, type family Segoe UI Variable Text (Segoe UI fallback).

## What this does and does not prove

- The artifact is editable and renders (the PNG was produced by `file.export` from the document, and the
  page was verified with `screenshot`).
- It does **not** prove pixel parity with the WinForms build. The running application is the source of
  truth for layout and behaviour, and the runtime evidence is `docs/screenshots/`.
- The design pages predate the 2026-10-29 code changes; the revision page records the state changes but no
  screen was re-composed, because no layout changed.

## Other artifacts in the workspace

`designs/grm-*.figcraft` and `renders/grm-*` belong to a different product (`Game-Repack-Maker`) and were
left untouched. `references/` holds mood-board images, not product assets.
