using System.ComponentModel;

namespace ITotalTransfer.UI.Controls;

/// <summary>The severity of an <see cref="InfoBar"/> message, mirroring the Fluent InfoBar.</summary>
public enum InfoBarTone { Info, Success, Warning, Error }

/// <summary>
/// A Fluent inline message bar: a tinted, rounded surface with a leading icon, a title, a message and
/// an optional action. Used for guidance that must not interrupt the user with a modal dialog.
/// </summary>
public partial class InfoBar : Panel
{
    private InfoBarTone _tone = InfoBarTone.Info;
    private string _glyph = Glyphs.Info;

    public InfoBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        InitializeComponent();
        _action.Click += (_, _) => ActionClicked?.Invoke(this, EventArgs.Empty);
        _dismiss.Click += (_, _) => DismissClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the optional action button is clicked.</summary>
    public event EventHandler? ActionClicked;

    /// <summary>
    /// Raised when the optional second button is clicked. It exists so a bar that reports a problem can
    /// offer both "do something about it" and "not now" without a modal dialog.
    /// </summary>
    public event EventHandler? DismissClicked;

    [DefaultValue(InfoBarTone.Info)]
    public InfoBarTone Tone
    {
        get => _tone;
        set { _tone = value; _glyph = GlyphFor(value); Apply(_theme ?? Theme.NeonInstance); }
    }

    [DefaultValue("Title")]
    public string Title
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    [DefaultValue("Message")]
    public string Message
    {
        get => _message.Text;
        set => _message.Text = value;
    }

    /// <summary>Shows the action button with this caption.</summary>
    public void SetAction(string? text)
    {
        _action.Text = text ?? "";
        _action.Visible = !string.IsNullOrEmpty(text);
        LayoutButtons();
    }

    /// <summary>
    /// Shows the secondary button with this caption. Both buttons keep their theme roles: the action is
    /// the accent button, the dismissal is quiet.
    /// </summary>
    public void SetDismiss(string? text)
    {
        _dismiss.Text = text ?? "";
        _dismiss.Visible = !string.IsNullOrEmpty(text);
        LayoutButtons();
    }

    /// <summary>
    /// Enables or disables the action button without hiding it, so the reason it cannot be used stays
    /// on screen instead of the action silently disappearing.
    /// </summary>
    public void SetActionEnabled(bool enabled)
    {
        _action.Enabled = enabled;
        _action.Invalidate();
    }

    private bool _layingOut;

    /// <summary>
    /// Places the buttons against the right edge and shrinks the message so text can never run beneath
    /// them. The title is short by design; only the message carries the detail.
    /// </summary>
    private void LayoutButtons()
    {
        if (_layingOut) return;
        _layingOut = true;
        try
        {
            int right = Width - 20;
            if (_action.Visible)
            {
                _action.Location = new Point(Math.Max(60, right - _action.Width), 15);
                right = _action.Left - 8;
            }
            if (_dismiss.Visible)
            {
                _dismiss.Location = new Point(Math.Max(60, right - _dismiss.Width), 15);
                right = _dismiss.Left - 8;
            }
            int messageWidth = Math.Max(140, right - _message.Left - 8);
            if (_message.Width != messageWidth) _message.Width = messageWidth;
        }
        finally { _layingOut = false; }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutButtons();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        LayoutButtons();
    }

    [DefaultValue(typeof(Color), "11, 11, 13")]
    public Color HostColor { get; set; } = Color.FromArgb(11, 11, 13);

    /// <summary>The resolved tinted fill (set by <see cref="Apply"/>).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Fill { get; private set; } = Color.FromArgb(28, 28, 33);

    /// <summary>The resolved hairline stroke (set by <see cref="Apply"/>).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Stroke { get; private set; } = Color.FromArgb(44, 44, 51);

    /// <summary>The resolved tone colour (set by <see cref="Apply"/>).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ToneColor { get; private set; } = Color.FromArgb(5, 161, 252);

    private Theme? _theme;

    /// <summary>Re-tints the bar for the supplied theme.</summary>
    public void Apply(Theme theme)
    {
        _theme = theme;
        ToneColor = _tone switch
        {
            InfoBarTone.Success => theme.Ok,
            InfoBarTone.Warning => theme.Warn,
            InfoBarTone.Error => theme.Error,
            _ => theme.Info
        };

        Fill = Theme.Blend(ToneColor, theme.Surface, 0.16);
        Stroke = Theme.Blend(ToneColor, theme.Border, 0.55);
        _glyph = GlyphFor(_tone);

        _title.ForeColor = theme.Text;
        _message.ForeColor = theme.Muted;
        _action.HostColor = Fill;
        _action.ForeColor = theme.Text;
        _action.Tag = "primary";
        _action.Invalidate();
        _dismiss.HostColor = Fill;
        _dismiss.ForeColor = theme.Muted;
        _dismiss.Tag = "subtle";
        _dismiss.Invalidate();
        LayoutButtons();
        Invalidate();
    }

    private static string GlyphFor(InfoBarTone tone) => tone switch
    {
        InfoBarTone.Success => Glyphs.Check,
        InfoBarTone.Warning => Glyphs.Warning,
        InfoBarTone.Error => Glyphs.Error,
        _ => Glyphs.Info
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        var bounds = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));
        Surface.PaintCard(g, this, bounds, Fill, null, Stroke, HostColor, 10, 1);

        int pad = Surface.Radius(this, 16);
        var iconArea = new Rectangle(pad, 0, Surface.Radius(this, 26), Height);
        if (Fluent.HasIconFont)
        {
            Surface.Glyph(g, _glyph, Fluent.Glyph(14f), iconArea, ToneColor, ContentAlignment.MiddleLeft);
        }

        base.OnPaint(e);
    }
}
