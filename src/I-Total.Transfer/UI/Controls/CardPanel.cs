using System.ComponentModel;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// A raised, rounded, hairline-bordered surface. Used (via the Designer) for every "card" region so
/// the shell reads as a set of floating panels instead of flat rectangles. <see cref="HostColor"/>
/// must match the surface behind the card so the corner cut-outs blend in.
/// </summary>
public class CardPanel : Panel
{
    private Color _fill = Color.FromArgb(20, 20, 23);

    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = _fill;
    }

    /// <summary>Corner radius in logical pixels (scaled to the current DPI when painted).</summary>
    [DefaultValue(12)]
    public int Radius { get; set; } = 12;

    /// <summary>Card fill. Kept in sync with <see cref="Control.BackColor"/> so transparent children blend.</summary>
    [DefaultValue(typeof(Color), "20, 20, 23")]
    public Color Fill
    {
        get => _fill;
        set
        {
            if (_fill == value) return;
            _fill = value;
            BackColor = value;
            Invalidate();
        }
    }

    [DefaultValue(typeof(Color), "44, 44, 51")]
    public Color Stroke { get; set; } = Color.FromArgb(44, 44, 51);

    /// <summary>The colour of the surface behind this card (used to mask the corners).</summary>
    [DefaultValue(typeof(Color), "11, 11, 13")]
    public Color HostColor { get; set; } = Color.FromArgb(11, 11, 13);

    /// <summary>Draws the subtle top sheen that reads as elevation.</summary>
    [DefaultValue(true)]
    public bool Elevate { get; set; } = true;

    /// <summary>Optional gradient used instead of <see cref="Fill"/> (a themed accent bar, for example).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color[]? FillGradient { get; set; }

    /// <summary>Optional accent ramp painted as a hairline along the card's top edge.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color[]? AccentGradient { get; set; }

    /// <summary>Height (logical px) of the <see cref="AccentGradient"/> hairline.</summary>
    [DefaultValue(3)]
    public int AccentHeight { get; set; } = 3;

    [DefaultValue(1)]
    public int StrokeWidth { get; set; } = 1;

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));

        if (FillGradient is { Length: >= 2 })
        {
            // Gradient fills are painted here; plain fills are already covered by BackColor, so the
            // card's transparent children keep sampling the right colour.
            Surface.PaintCard(e.Graphics, this, bounds, _fill, FillGradient, Color.Empty, HostColor, Radius, 0, Elevate);
        }

        if (AccentGradient is { Length: >= 2 })
        {
            int radius = Surface.Radius(this, Radius);
            var inner = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using var path = Surface.Rounded(inner, radius);
            int height = Math.Max(1, Surface.Radius(this, AccentHeight));
            var state = e.Graphics.Save();
            e.Graphics.SetClip(path);
            using var brush = Surface.Gradient(AccentGradient, new Rectangle(0, 0, inner.Width, height), 0f);
            e.Graphics.FillRectangle(brush, new Rectangle(0, 0, inner.Width, height));
            e.Graphics.Restore(state);
        }

        base.OnPaint(e);

        // The border and corner mask go last so they sit above any child-painted pixels at the edge.
        PaintBorder(e.Graphics, bounds);
    }

    private void PaintBorder(Graphics g, Rectangle bounds)
    {
        if (Stroke == Color.Empty && StrokeWidth <= 0) return;
        Surface.PaintCard(g, this, bounds, Color.Empty, null,
            StrokeWidth > 0 ? Stroke : Color.Empty, HostColor, Radius, StrokeWidth, false);
    }
}
