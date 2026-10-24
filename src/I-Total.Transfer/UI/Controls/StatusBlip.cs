using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ITotalTransfer.UI.Controls;

/// <summary>
/// The connection blip: a small filled circle with a soft halo, used in the brand band in place of a
/// text badge. Green means media is readable, amber means the iPhone answered but still needs the
/// screen unlocked or Trust accepted, grey means nothing is attached.
/// </summary>
public class StatusBlip : Control
{
    private System.Windows.Forms.Timer? _pulse;
    private float _phase;

    public StatusBlip()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        TabStop = false;
    }

    [DefaultValue(typeof(Color), "116, 137, 178")]
    public Color BlipColor { get; set; } = Color.FromArgb(116, 137, 178);

    /// <summary>Draws a breathing halo, so "waiting for the handset" reads as an activity, not a fault.</summary>
    [DefaultValue(false)]
    public bool Pulsing
    {
        get => _pulse is not null;
        set
        {
            if (value == Pulsing) return;
            if (value)
            {
                _pulse = new System.Windows.Forms.Timer { Interval = 40 };
                _pulse.Tick += (_, _) =>
                {
                    _phase += 0.07f;
                    if (_phase > MathF.Tau) _phase -= MathF.Tau;
                    Invalidate();
                };
                _pulse.Start();
            }
            else
            {
                _pulse?.Stop();
                _pulse?.Dispose();
                _pulse = null;
                Invalidate();
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        float radius = Math.Max(2f, Math.Min(Width, Height) / 2f - 2f * scale);
        float cx = Width / 2f;
        float cy = Height / 2f;

        // Halo: brightest when idle, breathing while the app is waiting on the handset.
        float haloAlpha = 0.30f + (Pulsing ? 0.22f * (1f + MathF.Sin(_phase)) / 2f : 0f);
        float haloRadius = radius + (Pulsing ? 3.4f * scale + 1.6f * scale * (1f + MathF.Sin(_phase)) / 2f : 2.6f * scale);
        using (var halo = new GraphicsPath())
        {
            halo.AddEllipse(cx - haloRadius, cy - haloRadius, haloRadius * 2, haloRadius * 2);
            using var brush = new PathGradientBrush(halo)
            {
                CenterColor = Color.FromArgb((int)(haloAlpha * 255), BlipColor),
                SurroundColors = [Color.FromArgb(0, BlipColor)],
                CenterPoint = new PointF(cx, cy)
            };
            g.FillPath(brush, halo);
        }

        // Core: a slightly off-centre highlight gives the dot a soft, lit look.
        using (var core = new GraphicsPath())
        {
            core.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);
            using var brush = new PathGradientBrush(core)
            {
                CenterColor = Theme.Blend(Color.White, BlipColor, 0.55),
                SurroundColors = [BlipColor],
                CenterPoint = new PointF(cx - radius * 0.25f, cy - radius * 0.3f)
            };
            g.FillPath(brush, core);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pulse?.Stop();
            _pulse?.Dispose();
            _pulse = null;
        }
        base.Dispose(disposing);
    }
}
