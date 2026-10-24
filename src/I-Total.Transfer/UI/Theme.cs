using ITotalTransfer.Core;
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI;

/// <summary>
/// The shared design-token set and the single place that styles a control tree. Every WinForms
/// Designer file declares structure, size and font *size/style*; this class supplies the resolved
/// Fluent colour tokens and swaps in the Fluent font family, so a control added in the Designer is
/// themed automatically without per-control copy/paste.
///
/// Palettes (see <see cref="AppTheme"/>):
///  * <b>Neon</b> (default) — the product banner: a deep navy backdrop with the banner's blue glow
///    and a cyan&#8594;sky&#8594;blue&#8594;indigo&#8594;violet&#8594;magenta accent ramp.
///  * <b>Slate</b> — neutral near-black greys with the brand blue accent (the "grey" look).
///  * <b>Light</b> — a light office palette.
///  * <b>System</b> — resolves to Light or Neon from the Windows "app mode" personalization flag.
///
/// Controls opt into styling with <see cref="Control.Tag"/> markers: "primary" (accent button),
/// "subtle" (quiet button), "danger", "nav" (navigation item), "card"/"alt" (raised surface),
/// "muted"/"ok"/"warn"/"error"/"accent" (text colour), "title"/"subtitle"/"caption" (typography),
/// "log" (console), "glyph" (Segoe icon font).
/// </summary>
public sealed class Theme
{
    // ================================================================== dark: Neon (default)
    // Colours are sampled from the product banner (I-Total Transfer_ iPhone to Windows.png).
    public static Theme NeonInstance { get; } = new()
    {
        Key = AppTheme.Neon,
        IsDark = true,

        // The banner backdrop: deep navy with a blue glow behind the phone. The blue glow is reserved
        // for the brand band, the primary action and the active navigation item; the working surfaces
        // below are deliberately desaturated navy so the interface is not painted edge-to-edge blue.
        Background = Color.FromArgb(4, 10, 30),        // #040A1E
        BackdropTop = Color.FromArgb(12, 60, 166),     // #0C3CA6  (banner glow, brightest blue)
        BackdropBottom = Color.FromArgb(4, 10, 30),    // #040A1E
        HasBackdrop = true,

        Surface = Color.FromArgb(14, 26, 51),          // #0E1A33 neutral navy card
        SurfaceAlt = Color.FromArgb(22, 35, 63),       // #16233F input / raised row
        SurfaceHover = Color.FromArgb(30, 46, 82),     // #1E2E52 hover wash
        HeaderSurface = Color.FromArgb(6, 13, 33),     // #060D21

        Border = Color.FromArgb(36, 51, 78),           // #24334E quiet separator
        BorderStrong = Color.FromArgb(53, 73, 109),    // #35496D focused / input outline

        Text = Color.FromArgb(234, 241, 255),          // #EAF1FF
        Muted = Color.FromArgb(175, 190, 220),         // #AFBEDC 5.9:1 on the card

        Accent = Color.FromArgb(11, 85, 241),          // #0B55F1 (dominant icon blue)
        AccentText = Color.White,
        AccentHover = Color.FromArgb(42, 107, 255),    // #2A6BFF
        AccentPressed = Color.FromArgb(10, 70, 201),   // #0A46C9
        AccentSubtle = Color.FromArgb(18, 34, 74),     // #12224A active-nav wash

        Ok = Color.FromArgb(61, 220, 151),             // #3DDC97
        Warn = Color.FromArgb(252, 193, 70),           // #FCC146 (banner amber)
        Error = Color.FromArgb(255, 107, 133),         // #FF6B85
        Info = Color.FromArgb(76, 184, 255),           // #4CB8FF (readable banner sky)
        Warm = Color.FromArgb(249, 148, 92),           // #F9945C (banner orange)

        LogBackground = Color.FromArgb(7, 16, 35),      // #071023
        GridBackground = Color.FromArgb(10, 20, 40),   // #0A1428
        GridHeader = Color.FromArgb(22, 35, 63),       // #16233F
        GridRowAlt = Color.FromArgb(12, 23, 48),       // #0C1730
        GridSelection = Color.FromArgb(20, 38, 79),    // #14264F

        Focus = Color.FromArgb(127, 178, 255),         // #7FB2FF keyboard focus ring
        SelectionFill = Color.FromArgb(20, 38, 79),    // #14264F
        SelectionBorder = Color.FromArgb(11, 85, 241), // #0B55F1
        DisabledFill = Color.FromArgb(22, 32, 58),     // #16203A
        DisabledText = Color.FromArgb(110, 122, 153),  // #6E7A99
        Scrim = Color.FromArgb(179, 0, 5, 14),         // #B300050E modal scrim

        Radius = 12,
        ControlRadius = 8,
        Gradient =
        [
            Color.FromArgb(20, 240, 252),   // #14F0FC cyan
            Color.FromArgb(5, 161, 252),    // #05A1FC sky
            Color.FromArgb(11, 85, 241),    // #0B55F1 brand blue
            Color.FromArgb(34, 46, 236),    // #222EEC indigo
            Color.FromArgb(111, 50, 251),   // #6F32FB violet
            Color.FromArgb(243, 52, 248)    // #F334F8 magenta
        ]
    };

    // ================================================================== dark: Slate (grey)
    public static Theme SlateInstance { get; } = new()
    {
        Key = AppTheme.Slate,
        IsDark = true,

        // A neutral charcoal stack with enough separation to read the panel boundaries without
        // outlines on every surface. The surfaces step up by roughly one visible value each so cards,
        // inputs and overlays stay distinguishable while the theme remains unmistakably grey.
        Background = Color.FromArgb(16, 17, 20),       // #101114 charcoal base
        BackdropTop = Color.FromArgb(16, 17, 20),
        BackdropBottom = Color.FromArgb(16, 17, 20),
        HasBackdrop = false,

        Surface = Color.FromArgb(23, 25, 29),          // #17191D card
        SurfaceAlt = Color.FromArgb(31, 34, 39),       // #1F2227 input / raised row
        SurfaceHover = Color.FromArgb(40, 44, 51),     // #282C33 hover wash
        HeaderSurface = Color.FromArgb(12, 13, 16),    // #0C0D10

        Border = Color.FromArgb(46, 50, 58),           // #2E323A quiet separator
        BorderStrong = Color.FromArgb(68, 74, 85),     // #444A55 input outline

        Text = Color.FromArgb(242, 243, 245),          // #F2F3F5
        Muted = Color.FromArgb(168, 171, 180),         // #A8ABB4 6.4:1 on the card

        // A restrained steel blue, deliberately calmer and greyer than Neon's electric #0B55F1, so
        // Slate keeps a recognisable neutral identity while the accent still reads as one brand.
        Accent = Color.FromArgb(74, 107, 196),         // #4A6BC4 (white label 5.0:1)
        AccentText = Color.White,
        AccentHover = Color.FromArgb(82, 114, 204),    // #5272CC (white label 4.5:1)
        AccentPressed = Color.FromArgb(60, 88, 166),   // #3C58A6
        AccentSubtle = Color.FromArgb(30, 37, 54),     // #1E2536

        Ok = Color.FromArgb(70, 209, 154),             // #46D19A
        Warn = Color.FromArgb(242, 193, 78),           // #F2C14E
        Error = Color.FromArgb(255, 112, 136),         // #FF7088
        Info = Color.FromArgb(88, 180, 240),           // #58B4F0
        Warm = Color.FromArgb(240, 160, 112),          // #F0A070

        LogBackground = Color.FromArgb(10, 11, 13),    // #0A0B0D
        GridBackground = Color.FromArgb(20, 22, 25),   // #141619
        GridHeader = Color.FromArgb(31, 34, 39),
        GridRowAlt = Color.FromArgb(25, 28, 32),
        GridSelection = Color.FromArgb(35, 43, 62),    // #232B3E

        Focus = Color.FromArgb(159, 180, 232),         // #9FB4E8
        SelectionFill = Color.FromArgb(35, 43, 62),    // #232B3E
        SelectionBorder = Color.FromArgb(74, 107, 196),
        DisabledFill = Color.FromArgb(28, 31, 36),     // #1C1F24
        DisabledText = Color.FromArgb(113, 117, 126),  // #71757E
        Scrim = Color.FromArgb(179, 0, 3, 5),          // #B3000305

        Radius = 12,
        ControlRadius = 8,
        Gradient = null
    };

    // ================================================================== light
    public static Theme LightInstance { get; } = new()
    {
        Key = AppTheme.Light,
        IsDark = false,

        Background = Color.FromArgb(243, 244, 247),    // #F3F4F7
        BackdropTop = Color.FromArgb(243, 244, 247),
        BackdropBottom = Color.FromArgb(243, 244, 247),
        HasBackdrop = false,

        Surface = Color.White,                          // #FFFFFF
        SurfaceAlt = Color.FromArgb(246, 247, 250),     // #F6F7FA
        SurfaceHover = Color.FromArgb(233, 236, 242),   // #E9ECF2
        HeaderSurface = Color.FromArgb(255, 255, 255),

        Border = Color.FromArgb(214, 218, 227),         // #D6DAE3 visible separator
        BorderStrong = Color.FromArgb(188, 195, 209),   // #BCC3D1 input outline

        Text = Color.FromArgb(20, 22, 27),              // #14161B
        Muted = Color.FromArgb(89, 96, 110),            // #59606E 6.3:1 on white

        Accent = Color.FromArgb(11, 85, 241),           // #0B55F1
        AccentText = Color.White,
        AccentHover = Color.FromArgb(42, 107, 255),
        AccentPressed = Color.FromArgb(10, 70, 201),
        AccentSubtle = Color.FromArgb(225, 234, 253),   // #E1EAFD

        Ok = Color.FromArgb(15, 122, 71),               // #0F7A47
        Warn = Color.FromArgb(150, 98, 10),             // #96620A amber, readable on white
        Error = Color.FromArgb(196, 43, 43),            // #C42B2B
        Info = Color.FromArgb(10, 111, 216),            // #0A6FD8
        Warm = Color.FromArgb(180, 85, 26),             // #B4551A

        LogBackground = Color.FromArgb(238, 241, 246),
        GridBackground = Color.White,
        GridHeader = Color.FromArgb(231, 234, 241),     // #E7EAF1
        GridRowAlt = Color.FromArgb(247, 248, 251),
        GridSelection = Color.FromArgb(220, 231, 252),  // #DCE7FC

        Focus = Color.FromArgb(11, 85, 241),            // #0B55F1
        SelectionFill = Color.FromArgb(220, 231, 252),  // #DCE7FC
        SelectionBorder = Color.FromArgb(11, 85, 241),
        DisabledFill = Color.FromArgb(231, 233, 238),   // #E7E9EE
        DisabledText = Color.FromArgb(154, 160, 173),   // #9AA0AD
        Scrim = Color.FromArgb(140, 0, 0, 0),           // #8C000000

        Radius = 12,
        ControlRadius = 8,
        Gradient =
        [
            Color.FromArgb(5, 161, 252),
            Color.FromArgb(11, 85, 241),
            Color.FromArgb(111, 50, 251),
            Color.FromArgb(243, 52, 248)
        ]
    };

    /// <summary>Backwards-compatible alias for the default dark palette.</summary>
    public static Theme DarkInstance => NeonInstance;

    // ------------------------------------------------------------------ tokens
    public AppTheme Key { get; init; } = AppTheme.Neon;
    public bool IsDark { get; init; } = true;

    public Color Background { get; init; }
    public Color BackdropTop { get; init; }
    public Color BackdropBottom { get; init; }
    public bool HasBackdrop { get; init; }

    public Color Surface { get; init; }
    public Color SurfaceAlt { get; init; }
    public Color SurfaceHover { get; init; }
    public Color HeaderSurface { get; init; }

    public Color Border { get; init; }
    public Color BorderStrong { get; init; }

    public Color Text { get; init; }
    public Color Muted { get; init; }

    public Color Accent { get; init; }
    public Color AccentText { get; init; }
    public Color AccentHover { get; init; }
    public Color AccentPressed { get; init; }
    public Color AccentSubtle { get; init; }

    public Color Ok { get; init; }
    public Color Warn { get; init; }
    public Color Error { get; init; }
    public Color Info { get; init; }
    public Color Warm { get; init; }

    /// <summary>The keyboard focus ring. Never the same value as <see cref="Accent"/> in a dark theme,
    /// so a focused primary button still shows a distinguishable ring against its own fill.</summary>
    public Color Focus { get; init; }

    /// <summary>The background of a selected row or item (grid selection, list selection).</summary>
    public Color SelectionFill { get; init; }

    /// <summary>The border of a selected item where a border is needed to separate it from neighbours.</summary>
    public Color SelectionBorder { get; init; }

    /// <summary>Fill used by disabled interactive controls.</summary>
    public Color DisabledFill { get; init; }

    /// <summary>Text/glyph colour of a disabled control.</summary>
    public Color DisabledText { get; init; }

    /// <summary>A modal scrim painted over the page behind a blocking dialog.</summary>
    public Color Scrim { get; init; }

    public Color LogBackground { get; init; }
    public Color GridBackground { get; init; }
    public Color GridHeader { get; init; }
    public Color GridRowAlt { get; init; }
    public Color GridSelection { get; init; }

    /// <summary>Corner radius (logical px) for large surfaces (cards).</summary>
    public int Radius { get; init; } = 12;

    /// <summary>Corner radius (logical px) for controls (buttons, fields, pills).</summary>
    public int ControlRadius { get; init; } = 8;

    /// <summary>Optional accent ramp used for gradients (primary buttons, active nav, header rule).</summary>
    public Color[]? Gradient { get; init; }

    public bool HasGradient => Gradient is { Length: >= 2 };

    /// <summary>
    /// The banner ramp painted by the app's brand band: the banner's glow blue fading into the page
    /// background. Flat themes fall back to their header surface so the band still reads as a band.
    /// </summary>
    public Color[] BandRamp => HasGradient
        ? [BackdropTop, Blend(BackdropTop, Background, 0.58), Background]
        : [HeaderSurface, Background];

    /// <summary>A quieter version of the ramp for the page-header band.</summary>
    public Color[] BandRampSubtle => HasGradient
        ? [Blend(BackdropTop, Background, 0.80), Background]
        : [HeaderSurface, Background];

    /// <summary>The DWM caption tint: the band's leading colour, so the title bar reads as part of it.</summary>
    public Color CaptionColor => HasBackdrop ? BackdropTop : HeaderSurface;

    // ------------------------------------------------------------------ resolution
    /// <summary>System theme follows the OS "apps use light theme" personalization flag.</summary>
    public static Theme For(AppTheme theme) => theme switch
    {
        AppTheme.Light => LightInstance,
        AppTheme.Slate => SlateInstance,
        AppTheme.System => UsesLightSystemTheme() ? LightInstance : NeonInstance,
        _ => NeonInstance
    };

    public static bool UsesLightSystemTheme()
    {
        try
        {
            using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch { return false; }
    }

    /// <summary>The banner colour used for a media group, keeping the four groups distinguishable.</summary>
    public Color CategoryAccent(MediaCategory category) => category switch
    {
        MediaCategory.Photos => Color.FromArgb(183, 56, 252),   // #B738FC (banner purple)
        MediaCategory.Videos => Color.FromArgb(5, 161, 252),    // #05A1FC (banner sky)
        MediaCategory.Music => Color.FromArgb(243, 52, 248),    // #F334F8 (banner magenta)
        _ => Color.FromArgb(20, 240, 252)                       // #14F0FC (banner cyan)
    };

    /// <summary>The glyph that represents a media group.</summary>
    public static string CategoryGlyph(MediaCategory category) => category switch
    {
        MediaCategory.Photos => Glyphs.Photo,
        MediaCategory.Videos => Glyphs.Video,
        MediaCategory.Music => Glyphs.Audio,
        _ => Glyphs.Folder
    };

    /// <summary>Blends <paramref name="a"/> over <paramref name="b"/> by <paramref name="t"/> (0..1).</summary>
    public static Color Blend(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(a.R * t + b.R * (1 - t)),
            (int)Math.Round(a.G * t + b.G * (1 - t)),
            (int)Math.Round(a.B * t + b.B * (1 - t)));
    }

    /// <summary>An alpha-blended overlay of <paramref name="over"/> on <paramref name="under"/>.</summary>
    public static Color Overlay(Color under, Color over, double alpha) => Blend(over, under, alpha);

    /// <summary>
    /// The WCAG relative luminance of an opaque colour channel. Exposed so tests and diagnostics can
    /// measure a palette instead of trusting an eyeballed hex value.
    /// </summary>
    public static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            double s = value / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    /// <summary>The WCAG contrast ratio between two opaque colours (1.0 … 21.0).</summary>
    public static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        double lighter = Math.Max(la, lb), darker = Math.Min(la, lb);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>The semantic colour for a status word, shared by labels, badges and the status bar.</summary>
    public Color StatusColor(string level) => level.ToUpperInvariant() switch
    {
        "ERROR" or "FAILED" => Error,
        "WARN" or "WARNING" or "ATTENTION" => Warn,
        "OK" or "READY" or "VERIFIED" or "DONE" => Ok,
        "INFO" or "RUNNING" or "PROGRESS" => Info,
        "DEBUG" or "IDLE" => Muted,
        _ => Text
    };

    // ------------------------------------------------------------------ application
    public void ApplyAll(Control root) => ApplyAll(root, Background);

    private void ApplyAll(Control control, Color host)
    {
        Color background = ApplyOne(control, host);
        foreach (Control child in control.Controls) ApplyAll(child, background);
    }

    /// <summary>Styles a single control and returns the colour its children sit on.</summary>
    private Color ApplyOne(Control control, Color host)
    {
        string tag = control.Tag as string ?? "";
        bool Has(string marker) => tag.Contains(marker, StringComparison.OrdinalIgnoreCase);

        // Re-resolve the typography family while preserving the Designer's size and style.
        ApplyFont(control, tag);

        switch (control)
        {
            case CardPanel card:
                card.HostColor = host;
                if (Has("card"))
                {
                    card.Fill = Has("alt") ? SurfaceAlt : Surface;
                    card.Stroke = Border;
                    card.Radius = Radius;
                    card.AccentGradient = null;
                    card.Elevate = !Has("flat");
                }
                else if (!Has("gradient"))
                {
                    // Untagged cards keep their Designer colours but still track the host surface.
                    card.Fill = host;
                    card.Stroke = Color.Empty;
                }
                return card.Fill;

            case MetricCard metric:
                metric.HostColor = host;
                metric.Fill = Surface;
                metric.Stroke = Border;
                metric.Radius = Radius;
                metric.AccentGradient = null;
                metric.TitleColor = Muted;
                metric.FootnoteColor = Muted;
                return metric.Fill;

            case InfoBar bar:
                bar.HostColor = host;
                bar.Apply(this);
                return bar.Fill;

            case EmptyState empty:
                empty.HostColor = host;
                empty.TitleColor = Text;
                empty.SubtitleColor = Muted;
                empty.GlyphColor = Muted;
                empty.Invalidate();
                return host;

            case FluentProgressBar progress:
                progress.HostColor = host;
                // A visible groove: SurfaceAlt is too close to the card to read as a track.
                progress.TrackColor = Has("alt") ? SurfaceAlt : Border;
                progress.Gradient = HasGradient ? Gradient : null;
                progress.Fill = Accent;
                progress.ForeColor = Text;
                return host;

            case PillLabel pill:
                pill.HostColor = host.A > 0 ? host : Color.Empty;
                pill.BackColor = host.A > 0 ? host : Color.Transparent;
                pill.Fill = SurfaceAlt;
                pill.Stroke = Border;
                pill.Radius = Math.Max(8, ControlRadius + 2);
                pill.ForeColor = Has("muted") ? Muted : Text;
                return host;

            case StatusBlip blip:
                blip.BackColor = Color.Transparent;
                return host;

            case BrandBand band:
                // The band paints its own ramp; tell it which one, then hand children the mid stop so
                // anything that masks a rounded corner lands on a close-enough colour.
                band.BackColor = Background;
                band.Ramp = Has("page") ? BandRampSubtle : BandRamp;
                return band.MidColor;

            case RoundedButton button:
                StyleButton(button, tag, host);
                return host;

            case FluentCheckBox check:
                check.HostColor = host;
                check.BoxFill = SurfaceAlt;
                check.BoxStroke = BorderStrong;
                check.HoverBoxFill = SurfaceHover;
                check.CheckColor = Accent;
                check.GlyphColor = AccentText;
                check.ForeColor = Text;
                check.Invalidate();
                return host;

            case FluentRadioButton radio:
                radio.HostColor = host;
                radio.BoxFill = SurfaceAlt;
                radio.BoxStroke = BorderStrong;
                radio.DotColor = Accent;
                radio.ForeColor = Text;
                radio.Invalidate();
                return host;

            case Button plainButton:
                bool plainPrimary = Has("primary");
                bool plainSubtle = Has("subtle");
                plainButton.FlatStyle = FlatStyle.Flat;
                plainButton.FlatAppearance.BorderSize = plainSubtle ? 0 : 1;
                plainButton.FlatAppearance.BorderColor = Border;
                plainButton.BackColor = plainPrimary ? Accent : plainSubtle ? Color.Transparent : SurfaceAlt;
                plainButton.ForeColor = plainPrimary ? AccentText : Has("danger") ? Error : Text;
                plainButton.UseVisualStyleBackColor = false;
                plainButton.Cursor = Cursors.Hand;
                return host;

            case RichTextBox rich:
                rich.BackColor = LogBackground;
                rich.ForeColor = Text;
                rich.BorderStyle = BorderStyle.None;
                rich.Font = new Font("Consolas", rich.Font.Size);
                return host;

            case TextBox textBox:
                textBox.BackColor = SurfaceAlt;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                return host;

            case NumericUpDown numeric:
                numeric.BackColor = SurfaceAlt;
                numeric.ForeColor = Text;
                numeric.BorderStyle = BorderStyle.FixedSingle;
                return host;

            case ComboBox combo:
                // Windows 11 paints the common controls with the app's colour mode, so the native
                // (Standard) style is the best-looking option there. Windows 10 has no Win32 dark mode,
                // so a native combo would keep a light drop button inside a dark card; the flat style
                // paints the whole control with our colours instead.
                combo.FlatStyle = Fluent.IsWindows11 ? FlatStyle.Standard : FlatStyle.Flat;
                combo.BackColor = SurfaceAlt;
                combo.ForeColor = Text;
                return host;

            case CheckBox check:
                check.BackColor = Color.Transparent;
                check.ForeColor = Text;
                check.FlatStyle = FlatStyle.Flat;
                return host;

            case RadioButton radio:
                radio.BackColor = Color.Transparent;
                radio.ForeColor = Text;
                radio.FlatStyle = FlatStyle.Flat;
                return host;

            case ProgressBar bar:
                bar.BackColor = SurfaceAlt;
                bar.ForeColor = HasGradient && Gradient is { Length: > 0 } ? Gradient[^1] : Accent;
                bar.Style = ProgressBarStyle.Continuous;
                return host;

            case DataGridView grid:
                StyleGrid(grid);
                return host;

            case GroupBox group:
                group.BackColor = Color.Transparent;
                group.ForeColor = Text;
                return host;

            case LinkLabel link:
                link.BackColor = Color.Transparent;
                link.LinkColor = Info;
                link.ActiveLinkColor = Accent;
                link.VisitedLinkColor = Info;
                return host;

            case Label label:
                label.BackColor = Color.Transparent;
                // Device names, folder paths and captions are data: "&" is an ampersand, not a mnemonic.
                label.UseMnemonic = false;
                if (Has("muted")) label.ForeColor = Muted;
                else if (Has("ok")) label.ForeColor = Ok;
                else if (Has("warn")) label.ForeColor = Warn;
                else if (Has("error")) label.ForeColor = Error;
                else if (Has("accent")) label.ForeColor = Accent;
                else label.ForeColor = Text;
                return host;

            case TabPage page:
                page.BackColor = Background;
                page.ForeColor = Text;
                page.UseVisualStyleBackColor = false;
                return Background;

            case TableLayoutPanel table:
                table.BackColor = Has("header") ? HeaderSurface : Has("card") ? Surface : Background;
                return table.BackColor;

            case FlowLayoutPanel flow:
                flow.BackColor = Has("card") ? Surface : Background;
                return flow.BackColor;

            case Panel panel:
                panel.BackColor = Has("card") ? Surface : Background;
                return panel.BackColor;

            case UserControl user:
                user.BackColor = Background;
                user.ForeColor = Text;
                return Background;

            case Form form:
                form.BackColor = Background;
                form.ForeColor = Text;
                return Background;

            default:
                return host;
        }
    }

    private void StyleButton(RoundedButton button, string tag, Color host)
    {
        bool isPrimary = tag.Contains("primary", StringComparison.OrdinalIgnoreCase);
        bool isSubtle = tag.Contains("subtle", StringComparison.OrdinalIgnoreCase);
        bool isDanger = tag.Contains("danger", StringComparison.OrdinalIgnoreCase);
        bool isNav = tag.Contains("nav", StringComparison.OrdinalIgnoreCase);
        bool isChrome = tag.Contains("chrome", StringComparison.OrdinalIgnoreCase);
        bool isClose = tag.Contains("close", StringComparison.OrdinalIgnoreCase);

        if (isChrome)
        {
            // Caption buttons: no box until hovered, and the ramp shows through so the band stays whole.
            //
            // The band is a dark ramp in Neon/Slate but a *light* one in the Light theme, so both the
            // glyph and the hover wash have to follow the theme. A hard-coded near-white glyph left the
            // minimize / maximize / close buttons painted white on white — visible in a Light-theme
            // capture as three barely-there outlines. CaptionButtonContrastTests pins this per theme.
            button.Radius = 0;
            button.HostColor = host;
            button.HostIsBrandBand = true;
            button.Fill = Color.Transparent;
            button.HoverFill = IsDark
                ? Color.FromArgb(isClose ? 210 : 34, isClose ? 196 : 255, isClose ? 43 : 255, isClose ? 28 : 255)
                : Color.FromArgb(isClose ? 210 : 30, 0, 0, 0);
            button.ActiveFill = IsDark
                ? Color.FromArgb(isClose ? 235 : 52, isClose ? 160 : 255, isClose ? 32 : 255, isClose ? 20 : 255)
                : Color.FromArgb(isClose ? 235 : 48, 0, 0, 0);
            button.ActiveForeColor = isClose || IsDark ? Color.White : Text;
            button.ForeColor = IsDark ? Color.FromArgb(232, 240, 255) : Text;
            button.Stroke = Color.Empty;
            button.GlyphFont = Fluent.Glyph(button.Font.Size + 1f);
            button.GlyphActiveColor = button.ForeColor;
            button.ShowGlyphAccent = false;
            button.ShowActiveIndicator = false;
            button.Gradient = null;
            button.GradientAlways = false;
            button.Cursor = Cursors.Hand;
            return;
        }

        button.HostIsBrandBand = false;
        button.HostColor = host;
        button.Radius = isNav ? ControlRadius : Math.Max(6, ControlRadius);
        button.GlyphFont = Fluent.Glyph(button.Font.Size + 2f);
        button.GlyphActiveColor = Accent;
        button.Cursor = Cursors.Hand;
        button.FocusRingColor = Focus;
        // Disabled actions get a neutral, low-contrast slab so "cannot press" never reads as "primary".
        // The colour comes from the shared disabled tokens so every theme disables the same way.
        button.DisabledFill = DisabledFill;
        button.DisabledStroke = Overlay(host, Border, 0.75);
        button.DisabledForeColor = DisabledText;

        if (isNav)
        {
            button.Fill = Color.Transparent;
            button.Stroke = Color.Empty;
            button.HoverFill = SurfaceHover;
            button.ActiveFill = AccentSubtle;
            button.ActiveForeColor = Text;
            button.ForeColor = Muted;
            button.Gradient = null;
            button.GradientAlways = false;
            button.ShowGlyphAccent = true;
            return;
        }

        button.Gradient = HasGradient && isPrimary ? Gradient : null;
        button.GradientAlways = isPrimary;
        button.ShowGlyphAccent = false;

        if (isPrimary)
        {
            button.Fill = Accent;
            button.HoverFill = AccentHover;
            button.ActiveFill = AccentPressed;
            button.ActiveForeColor = AccentText;
            button.ForeColor = AccentText;
            button.Stroke = Color.Empty;
        }
        else if (isSubtle)
        {
            button.Fill = Color.Transparent;
            button.HoverFill = SurfaceHover;
            button.ActiveFill = SurfaceAlt;
            button.ActiveForeColor = isDanger ? Error : Text;
            button.ForeColor = isDanger ? Error : Muted;
            button.Stroke = Color.Empty;
        }
        else
        {
            button.Fill = SurfaceAlt;
            button.HoverFill = SurfaceHover;
            button.ActiveFill = SurfaceHover;
            button.ActiveForeColor = isDanger ? Error : Text;
            button.ForeColor = isDanger ? Error : Text;
            button.Stroke = Border;
        }
    }

    private void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = GridBackground;
        grid.GridColor = Border;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = grid.ColumnHeadersHeight < 40 ? 40 : grid.ColumnHeadersHeight;
        grid.ColumnHeadersDefaultCellStyle.BackColor = GridHeader;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = GridHeader;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Muted;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.Font = Fluent.Text(grid.ColumnHeadersDefaultCellStyle.Font?.Size ?? 9f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
        grid.DefaultCellStyle.BackColor = GridBackground;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = SelectionFill.A == 255 ? SelectionFill : GridSelection;
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
        grid.DefaultCellStyle.Font = Fluent.Text(grid.DefaultCellStyle.Font?.Size ?? 9f);
        grid.AlternatingRowsDefaultCellStyle.BackColor = GridRowAlt;
        grid.RowHeadersVisible = false;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.RowTemplate.Height = grid.RowTemplate.Height < 32 ? 32 : grid.RowTemplate.Height;
        grid.ScrollBars = ScrollBars.Both;
    }

    /// <summary>Swaps in the resolved Fluent font family while preserving the Designer size and style.</summary>
    private static void ApplyFont(Control control, string tag)
    {
        if (control is Form) return; // the form font is inherited by children that do not set one.
        Font current = control.Font;
        try
        {
            Font next = tag.Contains("glyph", StringComparison.OrdinalIgnoreCase)
                ? Fluent.Glyph(current.Size)
                : Fluent.Text(current.Size, current.Style);
            if (!ReferenceEquals(next, current)) control.Font = next;
        }
        catch (ArgumentException) { }
    }

    public Color LevelColor(string level) => StatusColor(level);
}
