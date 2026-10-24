using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// A Fluent progress indicator: a rounded track with a gradient or solid fill, smooth value easing,
/// an optional percentage caption, and an animated indeterminate segment when no total is known.
/// </summary>
public class FluentProgressBar : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private float _offset;
    private double _display;
    private bool _indeterminate;
    private int _value;
    private int _maximum = 100;

    public FluentProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        DoubleBuffered = true;
        TabStop = false;
        _timer.Tick += (_, _) => Advance();
    }

    [DefaultValue(0)]
    public int Minimum { get; set; }

    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(value, Minimum + 1); Invalidate(); }
    }

    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, Minimum, Maximum);
            EnsureTimer();
            Invalidate();
        }
    }

    /// <summary>Shows a sweeping segment instead of a determinate fill.</summary>
    [DefaultValue(false)]
    public bool IsIndeterminate
    {
        get => _indeterminate;
        set
        {
            if (_indeterminate == value) return;
            _indeterminate = value;
            _offset = -0.35f;
            EnsureTimer();
            Invalidate();
        }
    }

    /// <summary>Draws a right-aligned percentage caption.</summary>
    [DefaultValue(false)]
    public bool ShowPercentage { get; set; }

    /// <summary>Track thickness in logical pixels.</summary>
    [DefaultValue(6)]
    public int Thickness { get; set; } = 6;

    [DefaultValue(typeof(Color), "28, 28, 33")]
    public Color TrackColor { get; set; } = Color.FromArgb(28, 28, 33);

    [DefaultValue(typeof(Color), "47, 111, 224")]
    public Color Fill { get; set; } = Color.FromArgb(47, 111, 224);

    /// <summary>Optional accent ramp for the filled portion (falls back to <see cref="Fill"/>).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color[]? Gradient { get; set; }

    /// <summary>The colour of the surface behind the bar (masks nothing, but keeps transparency honest).</summary>
    [DefaultValue(typeof(Color), "11, 11, 13")]
    public Color HostColor { get; set; } = Color.FromArgb(11, 11, 13);

    /// <summary>Fraction (0..1) currently displayed, after easing.</summary>
    public double Fraction => _maximum > Minimum ? Math.Clamp((_display - Minimum) / (_maximum - Minimum), 0, 1) : 0;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        EnsureTimer();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _timer.Stop();
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Jumps straight to <see cref="Value"/> without easing (used when a run restarts).</summary>
    public void SnapToValue()
    {
        _display = _value;
        Invalidate();
    }

    private void EnsureTimer()
    {
        bool wanted = Visible && IsHandleCreated && (IsIndeterminate || Math.Abs(_display - _value) > 0.01);
        if (wanted && !_timer.Enabled) _timer.Start();
        else if (!wanted && _timer.Enabled) _timer.Stop();
    }

    private void Advance()
    {
        if (IsIndeterminate)
        {
            _offset += 0.014f;
            if (_offset > 1.35f) _offset = -0.35f;
            Invalidate();
            return;
        }

        double target = _value;
        double delta = target - _display;
        if (Math.Abs(delta) <= 0.05)
        {
            _display = target;
            _timer.Stop();
        }
        else
        {
            _display += delta * 0.20;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(HostColor);

        int thickness = Math.Max(Surface.Radius(this, 3), Surface.Radius(this, Thickness));
        int captionWidth = ShowPercentage ? Surface.Radius(this, 56) : 0;
        int barWidth = Math.Max(1, Width - captionWidth);
        int top = (Height - thickness) / 2;

        var track = new Rectangle(0, top, barWidth, thickness);
        using (var trackPath = Surface.Capsule(track))
        using (var trackBrush = new SolidBrush(TrackColor))
        {
            g.FillPath(trackBrush, trackPath);
        }

        if (IsIndeterminate)
        {
            int segment = Math.Max(thickness * 3, (int)(barWidth * 0.32));
            int x = (int)(_offset * barWidth);
            var sweep = new Rectangle(x, top, segment, thickness);
            PaintFill(g, sweep, barWidth);
        }
        else
        {
            int filled = (int)Math.Round(Fraction * barWidth);
            if (filled > 1)
            {
                PaintFill(g, new Rectangle(0, top, Math.Max(thickness, filled), thickness), barWidth);
            }
        }

        if (ShowPercentage)
        {
            string text = IsIndeterminate ? "…" : $"{(int)Math.Round(Fraction * 100)}%";
            var area = new Rectangle(barWidth + Surface.Radius(this, 6), 0, captionWidth, Height);
            TextRenderer.DrawText(g, text, Font, area,
                ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    private void PaintFill(Graphics g, Rectangle bounds, int fullWidth)
    {
        var clipped = Rectangle.Intersect(bounds, new Rectangle(0, bounds.Y, fullWidth, bounds.Height));
        if (clipped.Width <= 0) return;

        int radius = Math.Max(1, bounds.Height / 2);
        using var path = Surface.Rounded(bounds, radius);

        if (Gradient is { Length: >= 2 })
        {
            var area = new Rectangle(0, bounds.Y, Math.Max(1, fullWidth), bounds.Height);
            using var brush = Surface.Gradient(Gradient, area, 0f);
            g.FillPath(brush, path);
        }
        else
        {
            using var brush = new SolidBrush(Fill);
            g.FillPath(brush, path);
        }
    }
}
