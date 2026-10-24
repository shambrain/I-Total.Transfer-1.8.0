using System.ComponentModel;
using System.Drawing.Drawing2D;
using ITotalTransfer.Core;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// A small, clickable preview of one application theme. It paints a miniature of the real shell using
/// the theme's own tokens, so the Appearance section shows the actual palette rather than a label.
/// Selecting it raises the normal <see cref="Control.Click"/> event, which the Settings page maps onto
/// the theme combo, so switching here runs the same live-preview path as the combo.
/// </summary>
public class ThemeSwatch : Control
{
    private bool _hover;
    private bool _selected;

    public ThemeSwatch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        DoubleBuffered = true;
        Size = new Size(168, 116);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    /// <summary>The theme represented by this swatch.</summary>
    [DefaultValue(AppTheme.Neon)]
    public AppTheme SwatchTheme { get; set; } = AppTheme.Neon;

    /// <summary>The short name shown on the swatch.</summary>
    [DefaultValue("")]
    public string Caption { get; set; } = "";

    /// <summary>Whether this swatch is the theme currently chosen in Settings.</summary>
    [DefaultValue(false)]
    public bool Selected
    {
        get => _selected;
        set { if (_selected != value) { _selected = value; Invalidate(); } }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    /// <summary>Runs the same action as a click or Enter/Space, for keyboard and test callers.</summary>
    public void Activate() => OnClick(EventArgs.Empty);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            Activate();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Theme theme = Theme.For(SwatchTheme);

        var outer = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Surface.Radius(this, 8);
        using (var path = Surface.Rounded(outer, radius))
        {
            using var hostBrush = new SolidBrush(Parent?.BackColor ?? theme.Background);
            g.FillPath(hostBrush, path);
        }

        // The miniature: a header band, a nav strip, a card and an accent action. The bottom 30 px
        // are reserved for the caption, so the name never sits on top of the artwork.
        var mini = new Rectangle(6, 6, Math.Max(1, Width - 12), Math.Max(1, Height - 36));
        using (var path = Surface.Rounded(mini, Math.Max(4, radius - 3)))
        {
            using var bg = new SolidBrush(theme.Background);
            g.FillPath(bg, path);
        }

        int headerH = Math.Max(12, mini.Height / 5);
        var header = new Rectangle(mini.X, mini.Y, mini.Width, headerH);
        using (var brush = new LinearGradientBrush(header,
                   theme.HasBackdrop ? theme.BackdropTop : theme.HeaderSurface,
                   theme.HasBackdrop ? theme.Background : theme.HeaderSurface, LinearGradientMode.Horizontal))
        {
            g.SetClip(mini);
            g.FillRectangle(brush, header);
        }

        var nav = new Rectangle(mini.X, mini.Y + headerH, Math.Max(24, mini.Width / 4), mini.Height - headerH);
        using (var brush = new SolidBrush(theme.HeaderSurface)) g.FillRectangle(brush, nav);
        var navItem = new Rectangle(nav.X + 4, nav.Y + 5, nav.Width - 8, 8);
        using (var brush = new SolidBrush(theme.AccentSubtle)) g.FillRectangle(brush, navItem);
        using (var pen = new Pen(theme.Accent, 2)) g.DrawLine(pen, navItem.Left, navItem.Top, navItem.Left, navItem.Bottom);

        var card = new Rectangle(nav.Right + 6, nav.Y + 5, mini.Right - nav.Right - 12, mini.Height - headerH - 11);
        using (var path = Surface.Rounded(card, 5))
        {
            using var brush = new SolidBrush(theme.Surface);
            g.FillPath(brush, path);
            using var pen = new Pen(theme.Border, 1);
            g.DrawPath(pen, path);
        }

        int lineX = card.X + 6, lineW = Math.Max(10, card.Width - 12);
        using (var brush = new SolidBrush(theme.Muted))
        {
            g.FillRectangle(brush, lineX, card.Y + 7, (int)(lineW * 0.6), 3);
            g.FillRectangle(brush, lineX, card.Y + 15, (int)(lineW * 0.42), 3);
        }

        var accent = new Rectangle(lineX, card.Bottom - 14, Math.Max(22, lineW / 3), 9);
        using (var brush = new SolidBrush(theme.Accent)) g.FillRectangle(brush, accent);

        // Status dots: verified green, warning amber, error red — the semantic trio every theme must keep.
        int dot = 5, dy = card.Bottom - 12;
        int dx = accent.Right + 5;
        using (var brush = new SolidBrush(theme.Ok)) g.FillEllipse(brush, dx, dy, dot, dot);
        using (var brush = new SolidBrush(theme.Warn)) g.FillEllipse(brush, dx + dot + 3, dy, dot, dot);
        using (var brush = new SolidBrush(theme.Error)) g.FillEllipse(brush, dx + (dot + 3) * 2, dy, dot, dot);

        g.ResetClip();

        // Caption strip below the miniature, not laid over it.
        var caption = new Rectangle(6, Height - 26, Math.Max(1, Width - 12), 20);
        using (var path = Surface.Rounded(caption, 4))
        {
            using var brush = new SolidBrush(theme.SurfaceAlt);
            g.FillPath(brush, path);
            using var pen = new Pen(theme.Border, 1);
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, Caption, Fluent.Text(8.5f, FontStyle.Bold), caption,
            theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        // Selection / hover outline.
        Color outline = _selected ? theme.Accent : _hover ? theme.BorderStrong : theme.Border;
        float weight = _selected ? 2f : 1f;
        using (var path = Surface.Rounded(Rectangle.Inflate(outer, -1, -1), radius))
        using (var pen = new Pen(outline, weight))
        {
            g.DrawPath(pen, path);
        }

        if (_selected)
        {
            var badge = new Rectangle(Width - 25, 12, 14, 14);
            using (var brush = new SolidBrush(theme.Accent)) g.FillEllipse(brush, badge);
            TextRenderer.DrawText(g, "✓", Fluent.Text(7f, FontStyle.Bold), badge, theme.AccentText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        if (Focused && ShowFocusCues)
        {
            using var path = Surface.Rounded(Rectangle.Inflate(outer, -3, -3), Math.Max(3, radius - 2));
            using var pen = new Pen(theme.Focus, 2) { Alignment = PenAlignment.Inset };
            g.DrawPath(pen, path);
        }
    }
}
