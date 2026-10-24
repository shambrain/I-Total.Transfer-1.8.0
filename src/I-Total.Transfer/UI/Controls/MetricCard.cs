using System.ComponentModel;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// A single read-only dashboard metric tile: a tinted icon chip, a title, a big value and a footnote,
/// painted as a rounded, hairline-bordered card so the dashboard reads as a grid of floating tiles.
/// </summary>
public partial class MetricCard : UserControl
{
    private Color _fill = Color.FromArgb(20, 20, 23);

    public MetricCard()
    {
        InitializeComponent();
        // Matches every other hand-painted surface in the shell, and it matters more here than
        // anywhere else: the dashboard creates these tiles at their design width and then stretches
        // them to fill their column. Without ResizeRedraw only the newly exposed strip is repainted,
        // so the tile's old rounded right edge survives in the middle of the card and the accent ramp
        // visibly restarts against it.
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    [DefaultValue("Metric")]
    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    [DefaultValue("—")]
    public string Value
    {
        get => _valueLabel.Text;
        set => _valueLabel.Text = value;
    }

    [DefaultValue("Footnote")]
    public string Footnote
    {
        get => _footnoteLabel.Text;
        set => _footnoteLabel.Text = value;
    }

    /// <summary>Corner radius in logical pixels.</summary>
    [DefaultValue(12)]
    public int Radius { get; set; } = 12;

    /// <summary>Tile fill; kept in sync with <see cref="Control.BackColor"/> so transparent children blend.</summary>
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

    /// <summary>The colour of the surface behind the tile (masks the corners).</summary>
    [DefaultValue(typeof(Color), "11, 11, 13")]
    public Color HostColor { get; set; } = Color.FromArgb(11, 11, 13);

    /// <summary>The Segoe icon glyph drawn in the tinted chip.</summary>
    [DefaultValue(null)]
    public string? Glyph { get; set; }

    /// <summary>The tint of the icon chip and the value text.</summary>
    [DefaultValue(typeof(Color), "47, 111, 224")]
    public Color ChipColor { get; set; } = Color.FromArgb(47, 111, 224);

    [DefaultValue(typeof(Color), "141, 160, 199")]
    public Color TitleColor { get; set; } = Color.FromArgb(141, 160, 199);

    [DefaultValue(typeof(Color), "141, 160, 199")]
    public Color FootnoteColor { get; set; } = Color.FromArgb(141, 160, 199);

    /// <summary>Draws the subtle top sheen that reads as elevation.</summary>
    [DefaultValue(true)]
    public bool Elevate { get; set; } = true;

    /// <summary>Optional accent ramp painted as a hairline along the tile's top edge.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color[]? AccentGradient { get; set; }

    /// <summary>Sets the glyph, caption, value and footnote in one call, optionally tinting the chip.</summary>
    public void SetMetric(string glyph, string title, string value, string footnote, Color? chip = null)
    {
        Glyph = glyph;
        Title = title;
        Value = value;
        Footnote = footnote;
        if (chip.HasValue) ChipColor = chip.Value;
        _valueLabel.ForeColor = ChipColor;
        _footnoteLabel.ForeColor = FootnoteColor;
        Invalidate();
    }

    public void SetValue(string value, string footnote, Color? color = null)
    {
        _valueLabel.Text = value;
        _footnoteLabel.Text = footnote;
        _valueLabel.ForeColor = color ?? ChipColor;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));
        Surface.PaintCard(e.Graphics, this, bounds, _fill, null, Stroke, HostColor, Radius, 1, Elevate);
        base.OnPaint(e);

        if (AccentGradient is { Length: >= 2 })
        {
            var inner = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using var path = Surface.Rounded(inner, Surface.Radius(this, Radius));
            int height = Math.Max(1, Surface.Radius(this, 3));
            var state = e.Graphics.Save();
            e.Graphics.SetClip(path);
            using var brush = Surface.Gradient(AccentGradient, new Rectangle(0, 0, inner.Width, height), 0f);
            e.Graphics.FillRectangle(brush, new Rectangle(0, 0, inner.Width, height));
            e.Graphics.Restore(state);
        }

        DrawChip(e.Graphics);
    }

    private void DrawChip(Graphics g)
    {
        if (string.IsNullOrEmpty(Glyph) || !Fluent.HasIconFont) return;
        int size = Surface.Radius(this, 36);
        var chip = new Rectangle(Surface.Radius(this, 16), Surface.Radius(this, 16), size, size);
        Surface.FillRounded(g, chip, Surface.Radius(this, 10), Theme.Blend(ChipColor, _fill, 0.18));
        Surface.Glyph(g, Glyph!, Fluent.Glyph(13f), chip, ChipColor);
    }
}
