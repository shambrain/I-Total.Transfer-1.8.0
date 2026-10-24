using System.ComponentModel;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// A small rounded status badge ("pill") with a hairline border and an optional leading icon glyph.
/// </summary>
public class PillLabel : Label
{
    public PillLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        AutoSize = false;
        TextAlign = ContentAlignment.MiddleCenter;
        UseMnemonic = false;
    }

    [DefaultValue(14)]
    public int Radius { get; set; } = 14;

    [DefaultValue(typeof(Color), "28, 28, 33")]
    public Color Fill { get; set; } = Color.FromArgb(28, 28, 33);

    [DefaultValue(typeof(Color), "44, 44, 51")]
    public Color Stroke { get; set; } = Color.FromArgb(44, 44, 51);

    /// <summary>The colour of the surface behind the badge (masks the corners).</summary>
    [DefaultValue(typeof(Color), "11, 11, 13")]
    public Color HostColor { get; set; } = Color.FromArgb(11, 11, 13);

    /// <summary>A leading Segoe icon glyph (rendered in <see cref="GlyphColor"/>).</summary>
    [DefaultValue(null)]
    public string? Glyph { get; set; }

    [DefaultValue(typeof(Color), "255, 255, 255")]
    public Color GlyphColor { get; set; } = Color.White;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        // A transparent host means the surface behind the badge is already painted (a brand band), so
        // leave it alone; otherwise clear to the host colour to mask the rounded corners.
        if (HostColor.A > 0) g.Clear(HostColor);

        var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var path = Surface.Rounded(bounds, Math.Max(4, Surface.Radius(this, Radius)));

        using (var brush = new SolidBrush(Fill))
        {
            g.FillPath(brush, path);
        }
        if (Stroke != Color.Empty)
        {
            using var pen = new Pen(Stroke, Surface.Line(this, 1)) { Alignment = System.Drawing.Drawing2D.PenAlignment.Inset };
            g.DrawPath(pen, path);
        }

        int padding = Math.Max(Padding.Left, Surface.Radius(this, 12));
        var textArea = new Rectangle(padding, 0, Math.Max(1, Width - padding * 2), Height);

        if (!string.IsNullOrEmpty(Glyph) && Fluent.HasIconFont)
        {
            int iconWidth = Math.Max(14, Surface.Radius(this, 16));
            var iconArea = new Rectangle(padding, 0, iconWidth, Height);
            Surface.Glyph(g, Glyph!, Fluent.Glyph(Font.Size + 0.5f), iconArea, GlyphColor);
            textArea = new Rectangle(iconArea.Right, 0, Math.Max(1, Width - iconArea.Right - padding), Height);
        }

        TextRenderer.DrawText(g, Text, Font, textArea, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            (UseMnemonic ? 0 : TextFormatFlags.NoPrefix));
    }
}
