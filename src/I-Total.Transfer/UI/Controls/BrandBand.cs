using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// A band that paints the banner's blue ramp as its own background — the app's brand strip. The ramp
/// is painted in <see cref="OnPaintBackground"/> rather than in <c>OnPaint</c> so that transparent
/// children (labels and the status pill) reproduce it correctly when they repaint themselves.
/// </summary>
public class BrandBand : Panel
{
    private Color[] _ramp = [Color.FromArgb(12, 60, 166), Color.FromArgb(4, 10, 30)];

    public BrandBand()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
    }

    /// <summary>The colours of the ramp, in order. Fewer than two stops falls back to a flat fill.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color[] Ramp
    {
        get => _ramp;
        set
        {
            if (value is not { Length: >= 2 }) return;
            _ramp = value;
            Invalidate();
        }
    }

    /// <summary>Ramp direction in degrees: 0 runs left to right, 90 runs top to bottom.</summary>
    [DefaultValue(90f)]
    public float Angle { get; set; } = 90f;

    /// <summary>
    /// The middle stop of the ramp. Children that mask their own rounded corners use this as a
    /// close-enough solid; genuinely transparent children show the ramp itself.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color MidColor => _ramp[_ramp.Length / 2];

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var bounds = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));
        e.Graphics.SmoothingMode = SmoothingMode.None;

        if (_ramp.Length < 2)
        {
            using var flat = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(flat, bounds);
            return;
        }

        using var brush = BuildRampBrush(bounds);
        e.Graphics.FillRectangle(brush, bounds);
    }

    /// <summary>
    /// Paints the ramp into <paramref name="target"/> as it would appear inside the band. Children that
    /// paint themselves opaque (the caption buttons) call this with the band's coordinate space so the
    /// gradient continues seamlessly behind them instead of being masked by a flat colour.
    /// </summary>
    public void PaintRampInto(Graphics g, Rectangle target)
    {
        var bounds = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));
        g.SmoothingMode = SmoothingMode.None;
        if (_ramp.Length < 2)
        {
            using var flat = new SolidBrush(BackColor);
            g.FillRectangle(flat, target);
            return;
        }
        using var brush = BuildRampBrush(bounds);
        g.FillRectangle(brush, target);
    }

    private LinearGradientBrush BuildRampBrush(Rectangle bounds)
    {
        var brush = new LinearGradientBrush(bounds, _ramp[0], _ramp[^1], Angle)
        {
            WrapMode = WrapMode.TileFlipXY
        };
        if (_ramp.Length > 2)
        {
            var positions = new float[_ramp.Length];
            for (int i = 0; i < positions.Length; i++) positions[i] = i / (float)(positions.Length - 1);
            brush.InterpolationColors = new ColorBlend { Colors = _ramp, Positions = positions };
        }
        return brush;
    }
}
