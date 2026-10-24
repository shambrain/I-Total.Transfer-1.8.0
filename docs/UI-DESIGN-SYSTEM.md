# UI design system - I-Total Transfer 1.8.0

This document records the editable design source, the runtime token set and the boundary between a design
render and an implementation that has actually run. It is not a claim that the FigCraft document is the
shipped UI; the shipped UI is the WinForms tree in `src/I-Total.Transfer/UI`.

## 1. Two sources of truth

| Layer | Source of truth | What it proves |
| --- | --- | --- |
| Editable design | `C:\Users\Administrator\I-Total-Transfer-FigCraft\designs\i-total-transfer-design-system.figcraft` | The palette, component states and three 1440x900 Overview screens can be inspected and edited as real FigCraft layers. |
| Design renders | `C:\Users\Administrator\I-Total-Transfer-FigCraft\renders\i-total-transfer-*.png` | What the saved design document looked like when exported. These are design renders, not application screenshots. |
| Runtime tokens | `src/I-Total.Transfer/UI/Theme.cs` | The colours, radius and semantic states the application actually paints with. |
| Runtime components | `src/I-Total.Transfer/UI/Controls/` and `src/I-Total.Transfer/UI/Pages/` | The real WinForms controls, layout and behaviour. |
| Runtime evidence | `%TEMP%\opencode\ui-capture\*.png` for this machine | DPI-aware captures of the real executable. The device state in each capture is whatever was attached at the time. |

The FigCraft document has four pages:

- `00 - Design system`: foundations, token swatches, component/state sets, badges and type/spacing notes.
- `01 - Neon`: the default banner theme.
- `02 - Light`: the light office theme.
- `03 - Slate`: the neutral charcoal theme.

A design render never proves that a control is wired, that a theme persists, or that the runtime layout
fits. Those claims come from the tests and captures listed in `UI-REQUIREMENTS-MATRIX.md`.

## 2. Themes

Exactly three themes are selectable as previews: **Neon** (default for a fresh profile), **Light** and
**Slate**. `System` remains an OS-following choice in the existing combo box and resolves only to Light or
Neon. The legacy `Dark` value is an alias for Neon. Amber is a semantic warning/attention colour, not a
fourth theme.

### Core palette

| Token | Neon | Slate | Light |
| --- | --- | --- | --- |
| `Background` | `#040A1E` | `#101114` | `#F3F4F7` |
| `Surface` | `#0E1A33` | `#17191D` | `#FFFFFF` |
| `SurfaceAlt` | `#16233F` | `#1F2227` | `#F6F7FA` |
| `SurfaceHover` | `#1E2E52` | `#282C33` | `#E9ECF2` |
| `HeaderSurface` | `#060D21` | `#0C0D10` | `#FFFFFF` |
| `Border` | `#24334E` | `#2E323A` | `#D6DAE3` |
| `BorderStrong` | `#35496D` | `#444A55` | `#BCC3D1` |
| `Text` | `#EAF1FF` | `#F2F3F5` | `#14161B` |
| `Muted` | `#AFBEDC` | `#A8ABB4` | `#59606E` |
| `Accent` | `#0B55F1` | `#4A6BC4` | `#0B55F1` |
| `AccentHover` | `#2A6BFF` | `#5272CC` | `#2A6BFF` |
| `AccentPressed` | `#0A46C9` | `#3C58A6` | `#0A46C9` |
| `AccentSubtle` | `#12224A` | `#1E2536` | `#E1EAFD` |
| `Ok` | `#3DDC97` | `#46D19A` | `#0F7A47` |
| `Warn` | `#FCC146` | `#F2C14E` | `#96620A` |
| `Error` | `#FF6B85` | `#FF7088` | `#C42B2B` |
| `Info` | `#4CB8FF` | `#58B4F0` | `#0A6FD8` |
| `Warm` | `#F9945C` | `#F0A070` | `#B4551A` |
| `Focus` | `#7FB2FF` | `#9FB4E8` | `#0B55F1` |
| `SelectionFill` | `#14264F` | `#232B3E` | `#DCE7FC` |
| `SelectionBorder` | `#0B55F1` | `#4A6BC4` | `#0B55F1` |
| `DisabledFill` | `#16203A` | `#1C1F24` | `#E7E9EE` |
| `DisabledText` | `#6E7A99` | `#71757E` | `#9AA0AD` |
| `Scrim` | `#B300050E` | `#B3000305` | `#8C000000` |

Neon also carries the six-stop banner gradient `#14F0FC -> #05A1FC -> #0B55F1 -> #222EEC -> #6F32FB ->
#F334F8`. Slate is intentionally flat. Light keeps the brand blue accent but uses darker status colours so
small status text remains readable on white.

### Semantic tokens

`Focus`, `SelectionFill`, `SelectionBorder`, `DisabledFill`, `DisabledText` and `Scrim` are shared names so
a control does not pick a raw colour for a keyboard focus ring, a selected row, a disabled field or a modal
overlay. `Theme.Luminance`, `Theme.Contrast` and `Theme.StatusColor` are the measured helpers used by the
tests and by status-aware controls.

### Contrast contract

`ThemeTests.EveryPaletteKeepsTextStatusAndFocusReadable` measures the real token pairs, not a copied table:

- body text on page and card >= 7.0:1;
- muted text, primary button label and error text >= 4.5:1;
- warning text, success marks and focus rings >= 3.0:1.

A palette change that breaks one of those pairs fails the suite. `CaptionButtonContrastTests` separately
keeps the window caption glyphs at or above 3:1.

## 3. Typography, shape and spacing

- UI text: `Segoe UI Variable Text`, falling back to `Segoe UI`. Display text prefers `Segoe UI Variable
  Display`.
- Icons: `Segoe Fluent Icons`, falling back to `Segoe MDL2 Assets`, then `Segoe UI Symbol`. Every glyph used
  also exists in MDL2 so Windows 10 keeps working.
- Card radius: 12 px. Control radius: 8 px. The higher-radius card is the layer; the lower-radius control
  is the object inside it.
- The page uses a 250 px navigation rail, a 78 px brand band and a 32 px status bar at 100 % scaling.
- The Overview stack at 1440x900 is: device hero, media groups, four headline metrics, current-operation
  band, then the action card. The action card must stay above the fold at 1440x900 and 150 % scaling.
  `DashboardPage.AutoScrollMinSize` is the anti-clipping floor (690 px); it is a scroll fallback, not the
  preferred layout.

## 4. Components and states

| Component | Runtime file | States and rules |
| --- | --- | --- |
| `RoundedButton` | `UI/Controls/RoundedButton.cs` | primary, subtle, danger, disabled; 2 px keyboard focus ring drawn from `Focus`; dotted fallback when the control cannot draw the ring. |
| `ThemeSwatch` | `UI/Controls/ThemeSwatch.cs` | rest, hover, selected check badge, keyboard focus; miniature header/nav/card/action and a dedicated caption strip. Clicking or pressing Space/Enter calls the same theme-selection path as the combo box. |
| `MetricCard` | `UI/Controls/MetricCard.cs` | tinted glyph chip, title, value, footnote; fills the column and repaints on resize so the rounded right edge never leaves a seam. |
| `CardPanel` | `UI/Controls/CardPanel.cs` | rounded surface, hairline border, optional accent top rule; used for every page card. |
| `PillLabel` | `UI/Controls/PillLabel.cs` | state pill with a glyph, tinted fill and contrast-checked text; does not invent a status. |
| `FluentCheckBox` / `FluentComboBox` | `UI/Controls/` | themed check state and drop-down; keyboard reachable. |
| `BrandBand` | `UI/Controls/BrandBand.cs` | the header band; Neon paints the banner gradient, Slate and Light paint a flat header surface. |
| Navigation | `UI/MainForm.Designer.cs` | selected item uses `AccentSubtle` plus a 3 px accent rail; unselected items use muted text. |
| Caption buttons | `UI/Controls/` + `MainForm` | min/max/close are real window commands; contrast is tested per theme. |

## 5. Theme selection and persistence

1. `SettingsPage` owns the Appearance section: a `Theme` combo box, three `ThemeSwatch` previews and a
   one-line description of the selected theme.
2. Selecting a swatch updates the combo box and raises `ThemePreviewRequested` exactly once.
3. The shell applies the preview through the existing theme path; the saved `settings.json` value is only
   overwritten by the normal Save action.
4. A fresh profile defaults to Neon. A saved Light or Slate value is restored and the matching swatch is
   highlighted. `System` remains a combo option and resolves to Light or Neon.
5. The user's current saved preference in this workspace is `Light`; capture scripts back it up and restore
   it byte for byte.

## 6. What the design does not decide

- The FigCraft screens show an iPhone-ready state. The runtime captures from 2026-10-22 show the real
  disconnected state (`No device connected`, `NO DEVICE`) with neutral phone wording because no handset was
  attached. Neither image is used to claim a device action happened.
- The design screens are 1440x900. The runtime is DPI-scaled; the real window on this machine is
  2152x1380 physical at 150 %, which is 1434x920 logical.
- Windows 11 rendering has not been observed; the 200 % layout was rendered on 2026-10-24. See `KNOWN_ISSUES.md`.
  (KI-11) and `DEVICE_TEST_MATRIX.md` (EN2/EN3).

## 7. Changing the design

1. Edit the `.figcraft` document first, or edit `Theme.cs` first if the change is a token rule; record
   which came first.
2. Re-export the affected render and save the document. Never replace the editable document with a flat
   image.
3. Update `Theme.cs` and the control that consumes the token. Do not add a second theming path.
4. Add or update a test that fails without the change. Contrast changes belong in `ThemeTests`; control
   behaviour belongs in the page's test class.
5. Build, run the targeted tests, then the full Release suite, then capture the real window at 150 %.
6. Update `UI-REQUIREMENTS-MATRIX.md`, `PROGRESS.md` and, if a shipped claim changed, `PROJECT-STATUS.md`.
