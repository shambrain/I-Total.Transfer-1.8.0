using System.ComponentModel;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// A centred, friendly placeholder shown when a collection is empty: a large icon glyph, a title and
/// a one-line explanation. Keeps every "nothing here yet" state purposeful instead of blank.
/// </summary>
public partial class EmptyState : UserControl
{
    private Rectangle _glyphArea = Rectangle.Empty;

    public EmptyState()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = true;
        InitializeComponent();
        SetState(Glyphs.Folder, "Nothing here yet", "");
    }

    [DefaultValue(typeof(Color), "11, 11, 13")]
    public Color HostColor { get; set; } = Color.FromArgb(11, 11, 13);

    [DefaultValue(typeof(Color), "234, 241, 255")]
    public Color TitleColor { get; set; } = Color.FromArgb(234, 241, 255);

    [DefaultValue(typeof(Color), "156, 178, 228")]
    public Color SubtitleColor { get; set; } = Color.FromArgb(156, 178, 228);

    [DefaultValue(typeof(Color), "156, 178, 228")]
    public Color GlyphColor { get; set; } = Color.FromArgb(156, 178, 228);

    [DefaultValue(null)]
    public string? Glyph { get; set; }

    public void SetState(string glyph, string title, string subtitle)
    {
        Glyph = glyph;
        int maxWidth = Math.Max(120, Width - 48);
        _titleLabel.MaximumSize = new Size(maxWidth, 0);
        _subtitleLabel.MaximumSize = new Size(maxWidth, 0);
        _titleLabel.Text = title;
        _subtitleLabel.Text = subtitle;
        _subtitleLabel.Visible = !string.IsNullOrWhiteSpace(subtitle);
        Relayout();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        int maxWidth = Math.Max(120, Width - 48);
        _titleLabel.MaximumSize = new Size(maxWidth, 0);
        _subtitleLabel.MaximumSize = new Size(maxWidth, 0);
        Relayout();
    }

    private void Relayout()
    {
        int glyph = Surface.Radius(this, 52);
        int gapAfterGlyph = Surface.Radius(this, 14);
        int gapBetween = Surface.Radius(this, 6);

        int titleHeight = _titleLabel.Height;
        int subtitleHeight = _subtitleLabel.Visible ? _subtitleLabel.Height : 0;
        int total = glyph + gapAfterGlyph + titleHeight + (subtitleHeight > 0 ? gapBetween + subtitleHeight : 0);

        int top = Math.Max(0, (Height - total) / 2);
        _glyphArea = new Rectangle((Width - glyph) / 2, top, glyph, glyph);

        int titleTop = top + glyph + gapAfterGlyph;
        _titleLabel.Location = new Point(Math.Max(0, (Width - _titleLabel.Width) / 2), titleTop);

        if (subtitleHeight > 0)
        {
            _subtitleLabel.Location = new Point(Math.Max(0, (Width - _subtitleLabel.Width) / 2), titleTop + titleHeight + gapBetween);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(HostColor);
        if (!string.IsNullOrEmpty(Glyph) && Fluent.HasIconFont && _glyphArea.Width > 0)
        {
            Surface.Glyph(e.Graphics, Glyph!, Fluent.Glyph(38f), _glyphArea, GlyphColor);
        }
        base.OnPaint(e);
    }
}
