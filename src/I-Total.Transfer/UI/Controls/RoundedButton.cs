using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// A fully custom-painted, rounded button used for the navigation rail and for actions. It supports
/// a flat fill, an accent fill while <see cref="Active"/>, or the brand gradient while accented,
/// plus a leading Segoe icon glyph, a trailing chevron, the Fluent selection indicator used by
/// navigation items, and hover/pressed feedback.
/// </summary>
public class RoundedButton : Button
{
    private bool _hover;
    private bool _pressed;

    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        // Captions are painted directly, so "&" is an ampersand rather than a mnemonic prefix.
        UseMnemonic = false;
    }

    [DefaultValue(8)]
    public int Radius { get; set; } = 8;

    [DefaultValue(typeof(Color), "28, 28, 33")]
    public Color Fill { get; set; } = Color.FromArgb(28, 28, 33);

    [DefaultValue(typeof(Color), "38, 38, 45")]
    public Color HoverFill { get; set; } = Color.FromArgb(38, 38, 45);

    [DefaultValue(typeof(Color), "47, 111, 224")]
    public Color ActiveFill { get; set; } = Color.FromArgb(47, 111, 224);

    [DefaultValue(typeof(Color), "44, 44, 51")]
    public Color Stroke { get; set; } = Color.FromArgb(44, 44, 51);

    [DefaultValue(typeof(Color), "White")]
    public Color ActiveForeColor { get; set; } = Color.White;

    /// <summary>The colour of the surface behind the button (masks the corners).</summary>
    [DefaultValue(typeof(Color), "11, 11, 13")]
    public Color HostColor { get; set; } = Color.FromArgb(11, 11, 13);

    /// <summary>Optional accent ramp used instead of <see cref="ActiveFill"/> while <see cref="Active"/>.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color[]? Gradient { get; set; }

    /// <summary>Paints the gradient even when the button is not <see cref="Active"/> (primary actions).</summary>
    [DefaultValue(false)]
    public bool GradientAlways { get; set; }

    /// <summary>Whether this button represents the currently selected destination.</summary>
    [DefaultValue(false)]
    public bool Active { get; set; }

    /// <summary>Left-aligns the caption with a proportional gutter (navigation items).</summary>
    [DefaultValue(false)]
    public bool AlignLeft { get; set; }

    /// <summary>An optional leading Segoe icon glyph.</summary>
    [DefaultValue(null)]
    public string? Glyph { get; set; }

    /// <summary>The font used to paint <see cref="Glyph"/> (resolved by the theme).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Font? GlyphFont { get; set; }

    /// <summary>Draws the glyph in the accent colour while the item is selected (navigation items).</summary>
    [DefaultValue(false)]
    public bool ShowGlyphAccent { get; set; }

    /// <summary>The glyph colour used while <see cref="ShowGlyphAccent"/> is set and the item is selected.</summary>
    [DefaultValue(typeof(Color), "47, 111, 224")]
    public Color GlyphActiveColor { get; set; } = Color.FromArgb(47, 111, 224);

    /// <summary>Draws the Fluent selection indicator beside the item while <see cref="Active"/>.</summary>
    [DefaultValue(true)]
    public bool ShowActiveIndicator { get; set; } = true;

    /// <summary>Draws a trailing chevron (used for "open" style navigation affordances).</summary>
    [DefaultValue(false)]
    public bool ShowChevron { get; set; }

    /// <summary>
    /// Paints the parent <see cref="BrandBand"/>'s ramp as this button's background instead of a flat
    /// <see cref="HostColor"/>, so caption buttons sit on the brand gradient without a visible box.
    /// </summary>
    [DefaultValue(false)]
    public bool HostIsBrandBand { get; set; }

    /// <summary>Fill used while the button is disabled. Empty falls back to the normal fill logic.</summary>
    [DefaultValue(typeof(Color), "Empty")]
    public Color DisabledFill { get; set; } = Color.Empty;

    /// <summary>Outline used while the button is disabled.</summary>
    [DefaultValue(typeof(Color), "Empty")]
    public Color DisabledStroke { get; set; } = Color.Empty;

    /// <summary>Caption/glyph colour used while the button is disabled.</summary>
    [DefaultValue(typeof(Color), "Empty")]
    public Color DisabledForeColor { get; set; } = Color.Empty;

    /// <summary>
    /// The keyboard focus ring drawn around the control while it has focus and the keyboard cues are on.
    /// Empty falls back to the older dotted rectangle, so a control used outside the theme still shows focus.
    /// </summary>
    [DefaultValue(typeof(Color), "Empty")]
    public Color FocusRingColor { get; set; } = Color.Empty;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnClick(EventArgs e)
    {
        // Every action button in the app is this control, so this is the one place a press needs a tick.
        if (Enabled && !HostIsBrandBand) Services.UiSound.Click();
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (HostIsBrandBand && Parent is BrandBand band)
        {
            // Reproduce the band's gradient in this control's coordinate space; alpha fills below then
            // blend against the real gradient rather than against a flat stand-in colour.
            GraphicsState state = g.Save();
            g.TranslateTransform(-Left, -Top);
            band.PaintRampInto(g, new Rectangle(Left, Top, Math.Max(1, Width), Math.Max(1, Height)));
            g.Restore(state);
        }
        else
        {
            g.Clear(HostColor);
        }

        var inner = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        int radius = Math.Max(4, Surface.Radius(this, Radius));
        using var path = Surface.Rounded(inner, radius);

        bool accented = Enabled && (Active || GradientAlways) && Gradient is { Length: >= 2 };
        if (accented)
        {
            using var brush = Surface.Gradient(Gradient!, inner, 0f);
            g.FillPath(brush, path);
        }
        else
        {
            Color fill = Active ? ActiveFill : (_pressed || _hover ? HoverFill : Fill);
            // A disabled action is inert, so the theme supplies a neutral slab. Washing the fill toward
            // the host would fail for a transparent fill (whose RGB is white) and would leave a
            // half-bright accent looking like a live primary action.
            if (!Enabled && DisabledFill.A > 0) fill = DisabledFill;
            if (fill.A > 0)
            {
                using var brush = new SolidBrush(fill);
                g.FillPath(brush, path);
            }
        }

        if (!Active && !GradientAlways && (Stroke != Color.Empty || (!Enabled && DisabledStroke.A > 0)))
        {
            // A disabled control still needs an outline to read as a control rather than as a smudge.
            Color strokeColor = !Enabled && DisabledStroke.A > 0 ? DisabledStroke : Stroke;
            using var pen = new Pen(strokeColor, Surface.Line(this, 1)) { Alignment = PenAlignment.Inset };
            g.DrawPath(pen, path);
        }

        Color fore = Active || accented ? ActiveForeColor : ForeColor;
        if (!Enabled) fore = DisabledForeColor.A > 0 ? DisabledForeColor : Theme.Blend(fore, HostColor, 0.45);

        Color glyphColor = Active && ShowGlyphAccent ? GlyphActiveColor : fore;

        int pad = AlignLeft ? Math.Max(12, Surface.Radius(this, 14)) : Math.Max(10, Surface.Radius(this, 12));
        int gap = Math.Max(6, Surface.Radius(this, 8));
        int reservedRight = ShowChevron ? Math.Max(20, Surface.Radius(this, 22)) : pad;

        if (ShowActiveIndicator && Active && AlignLeft)
        {
            int barWidth = Math.Max(3, Surface.Radius(this, 3));
            int barHeight = Math.Max(8, Height - Surface.Radius(this, 18));
            var bar = new Rectangle(0, (Height - barHeight) / 2, barWidth, barHeight);
            using var barPath = Surface.Rounded(bar, barWidth);
            using var barBrush = new SolidBrush(accented ? Theme.Blend(Color.White, ActiveFill, 0.55) : GlyphActiveColor);
            g.FillPath(barBrush, barPath);
        }

        Font? glyphFont = GlyphFont ?? (Fluent.HasIconFont ? Fluent.Glyph(Font.Size + 2f) : null);

        if (AlignLeft)
        {
            int x = pad;
            if (HasGlyph(glyphFont))
            {
                int iconWidth = Math.Max(20, Surface.Radius(this, 20));
                var iconArea = new Rectangle(x, 0, iconWidth, Height);
                Surface.Glyph(g, Glyph!, glyphFont!, iconArea, glyphColor, ContentAlignment.MiddleLeft);
                x += iconWidth + gap - Surface.Radius(this, 4);
            }

            var textArea = new Rectangle(x, 0, Math.Max(1, Width - x - reservedRight), Height);
            TextRenderer.DrawText(g, Text, Font, textArea, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                (UseMnemonic ? 0 : TextFormatFlags.NoPrefix));

            if (ShowChevron && glyphFont is not null && Fluent.HasIconFont)
            {
                var chevronArea = new Rectangle(Width - pad - gap, 0, Math.Max(10, gap + 4), Height);
                Surface.Glyph(g, Glyphs.ChevronRight, glyphFont, chevronArea, Theme.Blend(fore, HostColor, 0.4),
                    ContentAlignment.MiddleRight);
            }
        }
        else
        {
            // Centre the glyph + caption as one group.
            Size glyphSize = HasGlyph(glyphFont) ? TextRenderer.MeasureText(Glyph, glyphFont) : Size.Empty;
            Size textSize = TextRenderer.MeasureText(Text, Font);
            int totalWidth = glyphSize.Width + (glyphSize.Width > 0 ? gap : 0) + textSize.Width;
            int startX = Math.Max(pad, (Width - totalWidth) / 2);

            if (HasGlyph(glyphFont))
            {
                var iconArea = new Rectangle(startX, 0, glyphSize.Width + 4, Height);
                Surface.Glyph(g, Glyph!, glyphFont!, iconArea, glyphColor, ContentAlignment.MiddleLeft);
                startX += glyphSize.Width + gap;
            }

            var textArea = new Rectangle(startX, 0, Math.Max(1, Width - startX - pad), Height);
            TextRenderer.DrawText(g, Text, Font, textArea, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                (UseMnemonic ? 0 : TextFormatFlags.NoPrefix));
        }

        if (Focused && ShowFocusCues)
        {
            if (FocusRingColor.A > 0)
            {
                // A rounded 2 px ring reads as Fluent keyboard focus and, because it is drawn in the
                // theme's focus token, stays visible against an accent-filled primary button as well as
                // against a quiet transparent one.
                var ring = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
                using var ringPath = Surface.Rounded(ring, Math.Max(4, radius - 1));
                using var pen = new Pen(FocusRingColor, Surface.Line(this, 2)) { Alignment = PenAlignment.Inset };
                g.DrawPath(pen, ringPath);
            }
            else
            {
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(inner, -3, -3), fore, Color.Empty);
            }
        }
    }

    private bool HasGlyph(Font? glyphFont) =>
        glyphFont is not null && Fluent.HasIconFont && !string.IsNullOrEmpty(Glyph);
}
