using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// A Fluent check box: a custom-painted rounded square with a tick, plus the caption. Kept a real
/// <see cref="CheckBox"/> so <c>Checked</c>/<c>CheckState</c>/<c>CheckedChanged</c> keep working
/// exactly as the Designer and page code expect.
/// </summary>
public class FluentCheckBox : CheckBox
{
    private bool _hover;

    public FluentCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        BackColor = Color.Transparent;
    }

    [DefaultValue(typeof(Color), "28, 28, 33")]
    public Color BoxFill { get; set; } = Color.FromArgb(28, 28, 33);

    [DefaultValue(typeof(Color), "62, 62, 72")]
    public Color BoxStroke { get; set; } = Color.FromArgb(62, 62, 72);

    [DefaultValue(typeof(Color), "38, 38, 44")]
    public Color HoverBoxFill { get; set; } = Color.FromArgb(38, 38, 44);

    [DefaultValue(typeof(Color), "47, 111, 224")]
    public Color CheckColor { get; set; } = Color.FromArgb(47, 111, 224);

    [DefaultValue(typeof(Color), "White")]
    public Color GlyphColor { get; set; } = Color.White;

    [DefaultValue(typeof(Color), "11, 11, 13")]
    public Color HostColor { get; set; } = Color.FromArgb(11, 11, 13);

    private int BoxSize => Math.Max(14, Surface.Radius(this, 18));
    private int Gap => Math.Max(4, Surface.Radius(this, 9));

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size text = TextRenderer.MeasureText(Text, Font);
        return new Size(BoxSize + Gap + text.Width + 2, Math.Max(BoxSize, text.Height));
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnClick(EventArgs e)
    {
        if (Enabled) Services.UiSound.Toggle();
        base.OnClick(e);
    }

    /// <summary>
    /// True when the caption is wider than the painted area and the control is tall enough to hold a
    /// second line. The box is top-aligned and the text wraps in that case.
    /// </summary>
    private bool WrapsCaption(int box, out int textWidth, out Size oneLine)
    {
        textWidth = Math.Max(1, Width - box - Gap);
        oneLine = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, 0), TextFormatFlags.SingleLine);
        return oneLine.Width > textWidth && Height > oneLine.Height + 2;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(HostColor);

        int box = BoxSize;
        bool wrap = WrapsCaption(box, out int textWidth, out Size oneLine);
        var rect = new Rectangle(0, wrap ? 0 : Math.Max(0, (Height - box) / 2), box, box);
        int radius = Surface.Radius(this, 5);

        bool on = Checked || CheckState == CheckState.Indeterminate;
        if (on)
        {
            Surface.FillRounded(g, rect, radius, Enabled ? CheckColor : Theme.Blend(CheckColor, HostColor, 0.5));
        }
        else
        {
            Surface.FillRounded(g, rect, radius, Enabled ? (_hover ? HoverBoxFill : BoxFill) : Theme.Blend(BoxFill, HostColor, 0.5));
            Surface.StrokeRounded(g, rect, radius, Enabled ? BoxStroke : Theme.Blend(BoxStroke, HostColor, 0.5), Surface.Line(this, 1));
        }

        if (on)
        {
            using var pen = new Pen(GlyphColor, Surface.Line(this, 2))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            float x = rect.X, y = rect.Y, s = rect.Width;
            if (CheckState == CheckState.Indeterminate)
            {
                g.DrawLine(pen, x + s * 0.26f, y + s * 0.52f, x + s * 0.74f, y + s * 0.52f);
            }
            else
            {
                g.DrawLines(pen,
                [
                    new PointF(x + s * 0.24f, y + s * 0.53f),
                    new PointF(x + s * 0.43f, y + s * 0.72f),
                    new PointF(x + s * 0.77f, y + s * 0.30f)
                ]);
            }
        }

        Color fore = Enabled ? ForeColor : Theme.Blend(ForeColor, HostColor, 0.5);
        var textArea = new Rectangle(box + Gap, 0, textWidth, Height);
        TextFormatFlags flags = TextFormatFlags.Left | (UseMnemonic ? 0 : TextFormatFlags.NoPrefix) |
            (wrap
                ? TextFormatFlags.WordBreak | TextFormatFlags.Top
                : TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, Text, Font, textArea, fore, flags);
    }
}

/// <summary>
/// A Fluent radio button: a custom-painted ring with an accent dot, plus the caption.
/// </summary>
public class FluentRadioButton : RadioButton
{
    private bool _hover;

    public FluentRadioButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        BackColor = Color.Transparent;
    }

    [DefaultValue(typeof(Color), "28, 28, 33")]
    public Color BoxFill { get; set; } = Color.FromArgb(28, 28, 33);

    [DefaultValue(typeof(Color), "62, 62, 72")]
    public Color BoxStroke { get; set; } = Color.FromArgb(62, 62, 72);

    [DefaultValue(typeof(Color), "47, 111, 224")]
    public Color DotColor { get; set; } = Color.FromArgb(47, 111, 224);

    [DefaultValue(typeof(Color), "11, 11, 13")]
    public Color HostColor { get; set; } = Color.FromArgb(11, 11, 13);

    private int BoxSize => Math.Max(14, Surface.Radius(this, 18));
    private int Gap => Math.Max(4, Surface.Radius(this, 9));

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size text = TextRenderer.MeasureText(Text, Font);
        return new Size(BoxSize + Gap + text.Width + 2, Math.Max(BoxSize, text.Height));
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    /// <summary>True when the caption needs to wrap onto more than one line in this control.</summary>
    private bool WrapsCaption(int box, out int textWidth, out Size oneLine)
    {
        textWidth = Math.Max(1, Width - box - Gap);
        oneLine = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, 0), TextFormatFlags.SingleLine);
        return oneLine.Width > textWidth && Height > oneLine.Height + 2;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(HostColor);

        int box = BoxSize;
        bool wrap = WrapsCaption(box, out int textWidth, out Size oneLine);
        var rect = new Rectangle(0, wrap ? 0 : Math.Max(0, (Height - box) / 2), box, box);
        using var path = new GraphicsPath();
        path.AddEllipse(rect);

        using (var brush = new SolidBrush(Enabled ? (_hover ? Theme.Blend(BoxStroke, BoxFill, 0.4) : BoxFill) : Theme.Blend(BoxFill, HostColor, 0.5)))
        {
            g.FillPath(brush, path);
        }

        Color ring = Checked ? DotColor : BoxStroke;
        if (!Enabled) ring = Theme.Blend(ring, HostColor, 0.5);
        using (var pen = new Pen(ring, Surface.Line(this, Checked ? 2.2f : 1f)))
        {
            g.DrawPath(pen, path);
        }

        if (Checked)
        {
            int dot = (int)Math.Round(box * 0.42);
            var inner = new Rectangle(rect.X + (box - dot) / 2, rect.Y + (box - dot) / 2, dot, dot);
            using var innerPath = new GraphicsPath();
            innerPath.AddEllipse(inner);
            using var brush = new SolidBrush(DotColor);
            g.FillPath(brush, innerPath);
        }

        Color fore = Enabled ? ForeColor : Theme.Blend(ForeColor, HostColor, 0.5);
        var textArea = new Rectangle(box + Gap, 0, textWidth, Height);
        TextFormatFlags flags = TextFormatFlags.Left | (UseMnemonic ? 0 : TextFormatFlags.NoPrefix) |
            (wrap
                ? TextFormatFlags.WordBreak | TextFormatFlags.Top
                : TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, Text, Font, textArea, fore, flags);
    }
}
