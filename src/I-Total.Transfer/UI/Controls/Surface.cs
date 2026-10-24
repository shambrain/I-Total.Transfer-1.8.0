using System.Drawing.Drawing2D;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// Shared GDI+ helpers for the rounded, bordered surfaces the Fluent shell is built from. Every
/// helper is DPI-aware: radii and line widths are multiplied by the control's scale factor so a card
/// keeps its proportions on a 150% display.
/// </summary>
internal static class Surface
{
    /// <summary>Scale factor of <paramref name="control"/> relative to the 96-DPI design baseline.</summary>
    public static float Dpi(Control control) => Math.Max(1f, control.DeviceDpi / 96f);

    /// <summary>A radius in device pixels for the given logical radius on this control.</summary>
    public static int Radius(Control control, int logicalRadius) =>
        (int)Math.Round(Math.Max(0, logicalRadius) * Dpi(control));

    /// <summary>A line width in device pixels for the given logical width.</summary>
    public static float Line(Control control, float logicalWidth) =>
        Math.Max(1f, logicalWidth * Dpi(control));

    /// <summary>Builds a rounded-rectangle path. A radius of zero (or a tiny box) yields a plain rectangle.</summary>
    public static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        if (radius <= 1 || diameter >= bounds.Width || diameter >= bounds.Height)
        {
            path.AddRectangle(bounds);
            return path;
        }
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>A fully-round path for a capsule of the given bounds.</summary>
    public static GraphicsPath Capsule(Rectangle bounds) => Rounded(bounds, Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 2));

    /// <summary>A multi-stop linear gradient brush across <paramref name="bounds"/>.</summary>
    public static LinearGradientBrush Gradient(Color[] colors, Rectangle bounds, float angle)
    {
        var safe = new Rectangle(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
        var brush = new LinearGradientBrush(safe, colors[0], colors[^1], angle);
        if (colors.Length > 2)
        {
            brush.InterpolationColors = new ColorBlend(colors.Length)
            {
                Colors = colors,
                Positions = [.. Enumerable.Range(0, colors.Length).Select(i => i / (float)(colors.Length - 1))]
            };
        }
        return brush;
    }

    /// <summary>
    /// Repaints everything outside <paramref name="roundedPath"/> with the surface behind the control,
    /// so a rounded control appears to float on its parent even though WinForms controls are square.
    /// </summary>
    public static void CutCorners(Graphics g, Rectangle bounds, GraphicsPath roundedPath, Color behind)
    {
        using var region = new Region(bounds);
        region.Exclude(roundedPath);
        using var brush = new SolidBrush(behind);
        g.FillRegion(brush, region);
    }

    /// <summary>Fills a rounded rectangle.</summary>
    public static void FillRounded(Graphics g, Rectangle bounds, int radius, Color color)
    {
        using GraphicsPath path = Rounded(bounds, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    /// <summary>Draws a rounded outline.</summary>
    public static void StrokeRounded(Graphics g, Rectangle bounds, int radius, Color color, float width)
    {
        using GraphicsPath path = Rounded(bounds, radius);
        using var pen = new Pen(color, width) { Alignment = PenAlignment.Inset };
        g.DrawPath(pen, path);
    }

    /// <summary>
    /// Paints a card: rounded fill (solid or gradient), an optional top "lit from above" highlight,
    /// corner masking against <paramref name="behind"/>, and a hairline stroke.
    /// </summary>
    public static void PaintCard(Graphics g, Control control, Rectangle bounds, Color fill, Color[]? fillGradient,
        Color stroke, Color behind, int logicalRadius, int strokeWidth, bool highlight = false)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int radius = Radius(control, logicalRadius);
        var inner = new Rectangle(bounds.X, bounds.Y, Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));
        using GraphicsPath path = Rounded(inner, radius);

        if (fillGradient is { Length: >= 2 })
        {
            using var brush = Gradient(fillGradient, inner, 0f);
            g.FillPath(brush, path);
        }
        else if (fill != Color.Empty)
        {
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);
        }

        if (highlight && fill != Color.Empty && fill.A == 255)
        {
            // A barely-there vertical sheen across the top third reads as Fluent elevation on dark
            // surfaces without needing a real drop shadow (which WinForms cannot clip to).
            Color top = Color.FromArgb(30, Color.White);
            Color bottom = Color.FromArgb(0, Color.White);
            int height = Math.Max(4, inner.Height / 3);
            var state = g.Save();
            g.SetClip(path);
            using var sheen = new LinearGradientBrush(
                new Rectangle(inner.X, inner.Y, Math.Max(1, inner.Width), height), top, bottom, 90f);
            g.FillRectangle(sheen, new Rectangle(inner.X, inner.Y, inner.Width, height));
            g.Restore(state);
        }

        if (radius > 0)
        {
            CutCorners(g, bounds, path, behind);
        }

        if (stroke != Color.Empty && strokeWidth > 0)
        {
            using var pen = new Pen(stroke, Line(control, strokeWidth)) { Alignment = PenAlignment.Inset };
            g.DrawPath(pen, path);
        }
    }

    /// <summary>Draws an icon-font glyph inside <paramref name="bounds"/>.</summary>
    public static void Glyph(Graphics g, string glyph, Font font, Rectangle bounds, Color color, ContentAlignment align = ContentAlignment.MiddleCenter)
    {
        TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        flags |= align switch
        {
            ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft => TextFormatFlags.Left | TextFormatFlags.VerticalCenter,
            ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight => TextFormatFlags.Right | TextFormatFlags.VerticalCenter,
            _ => TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
        };
        TextRenderer.DrawText(g, glyph, font, bounds, color, flags);
    }

    /// <summary>
    /// Distributes a grid's columns across its width so no header is ever clipped, using the supplied
    /// relative weights (last column takes the remainder). Columns beyond the weight list share evenly.
    /// </summary>
    public static void FillColumns(DataGridView grid, params float[] weights)
    {
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        float sum = 0;
        for (int i = 0; i < weights.Length; i++) sum += Math.Max(0.01f, weights[i]);
        float fallback = weights.Length > 0 ? sum / weights.Length : 100f;
        for (int i = 0; i < grid.Columns.Count; i++)
            grid.Columns[i].FillWeight = i < weights.Length
                ? Math.Max(1f, weights[i] / sum * 100f)
                : Math.Max(1f, fallback / sum * 100f);
    }
}
