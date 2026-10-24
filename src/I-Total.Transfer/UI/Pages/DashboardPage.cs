using System.ComponentModel;
using ITotalTransfer.Core;
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI.Pages;

/// <summary>
/// Dashboard: at-a-glance device state, the four media groups the banner advertises, headline metrics
/// and the primary transfer actions. Every value is pushed in by <see cref="MainForm"/> so this page
/// stays free of service dependencies, and a group the active backend cannot read is shown as
/// unavailable rather than as an empty folder.
/// </summary>
public partial class DashboardPage : UserControl, IThemedPage
{
    public event EventHandler? CheckConnectionRequested;
    public event EventHandler? ScanRequested;
    public event EventHandler? BrowseDestinationRequested;
    public event EventHandler? StartBackupRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? StopRequested;

    /// <summary>Raised when the operator asks to continue the unfinished session in the destination.</summary>
    public event EventHandler? RecoveryResumeRequested;

    /// <summary>Raised when the operator asks the app to stop offering the unfinished session.</summary>
    public event EventHandler? RecoveryDismissRequested;

    private Theme _theme = Theme.NeonInstance;

    // The last state the shell pushed, kept so a live theme switch can repaint the tinted tiles
    // without the shell having to push everything again.
    private IReadOnlyDictionary<MediaCategory, CategoryStat>? _categories;
    private IReadOnlyCollection<MediaCategory>? _available;
    private DeviceStatus _deviceStatus = DeviceStatus.NotFound;
    /// <summary>
    /// The family of the device on screen, kept so the wording can name it correctly. An Android phone
    /// is not an iPhone, and telling its owner to "tap Trust" describes a prompt their phone will never
    /// show. Before anything has been classified the iPhone wording is used, which is what this app said
    /// when it was an iPhone-only product.
    /// </summary>
    private DeviceFamily _family = DeviceFamily.Unknown;
    private int _metricFiles;
    private int _metricVerified;
    private int _metricFailed;
    private int _metricUnverified;
    private long _metricBytes;
    private OperationStatus _operation = OperationStatus.Idle;

    public DashboardPage()
    {
        InitializeComponent();
        _checkButton.Click += (_, _) => CheckConnectionRequested?.Invoke(this, EventArgs.Empty);
        _scanButton.Click += (_, _) => ScanRequested?.Invoke(this, EventArgs.Empty);
        _browseButton.Click += (_, _) => BrowseDestinationRequested?.Invoke(this, EventArgs.Empty);
        _startButton.Click += (_, _) => StartBackupRequested?.Invoke(this, EventArgs.Empty);
        _pauseButton.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        _stopButton.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
        _recoveryBar.ActionClicked += (_, _) => RecoveryResumeRequested?.Invoke(this, EventArgs.Empty);
        _recoveryBar.DismissClicked += (_, _) => RecoveryDismissRequested?.Invoke(this, EventArgs.Empty);
        ApplyTheme(_theme);
        SetCategories(null, null);
        SetMetrics(0, 0, 0, 0, 0);
        SetState(false, false, false, false);
        SetOperation(OperationStatus.Idle);
        SetRecovery(null, false, "");
    }

    /// <summary>The destination folder shown in the action panel.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Destination
    {
        get => _destinationBox.Text.Trim();
        set => _destinationBox.Text = value;
    }

    /// <summary>Whether the operator asked for the confirming second scan pass.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ConfirmScan
    {
        get => _confirmScanCheck.Checked;
        set => _confirmScanCheck.Checked = value;
    }

    /// <summary>Records the active palette and repaints the page's own tinted surfaces.</summary>
    public void ApplyTheme(Theme theme)
    {
        _theme = theme;
        theme.ApplyAll(this);

        // ApplyAll deliberately leaves MetricCard chip tints alone because they carry meaning, so set
        // them here: the four banner groups keep the banner ramp and verification keeps its colour.
        foreach ((MetricCard tile, MediaCategory category) in CategoryTiles())
        {
            Color accent = theme.CategoryAccent(category);
            tile.ChipColor = accent;
            tile.AccentGradient = [accent, Theme.Blend(accent, theme.Surface, 0.62)];
            tile.Invalidate();
        }

        _filesCard.ChipColor = theme.Accent;
        _sizeCard.ChipColor = theme.Accent;
        _verifiedCard.ChipColor = theme.Ok;
        _failedCard.ChipColor = theme.Warn;
        foreach (MetricCard tile in new[] { _filesCard, _sizeCard, _verifiedCard, _failedCard }) tile.Invalidate();
        _failedCard.AccentGradient = [theme.Warn, Theme.Blend(theme.Warn, theme.Surface, 0.62)];
        _verifiedCard.AccentGradient = [theme.Ok, Theme.Blend(theme.Ok, theme.Surface, 0.62)];

        SetCategories(_categories, _available, _deviceStatus);
        SetMetrics(_metricFiles, _metricBytes, _metricVerified, _metricFailed, _metricUnverified);

        // The operation band carries a phase tint (accent / warn / error), so it has to be re-applied
        // after ApplyAll, which would otherwise repaint the heading with the plain text colour.
        SetOperation(_operation);
    }

    private IEnumerable<(MetricCard Tile, MediaCategory Category)> CategoryTiles()
    {
        yield return (_photosTile, MediaCategory.Photos);
        yield return (_videosTile, MediaCategory.Videos);
        yield return (_musicTile, MediaCategory.Music);
        yield return (_filesTile, MediaCategory.Files);
    }

    /// <summary>Replaces the integration-policy sentence shown in the action panel.</summary>
    public void SetPolicyText(string text) => _integrationLabel.Text = text;

    /// <summary>Shows the connected device, or the reason there is not one.</summary>
    public void SetDevice(DeviceInfo info, string backendDescription)
    {
        // A probe's family is not evidence that a phone of that family is on the cable. When the status
        // is NotFound, keep the neutral wording: a missing iPhone must not tell an Android owner to
        // "connect your iPhone" or head the media card "What your iPhone exposes".
        _family = info.Status == DeviceStatus.NotFound ? DeviceFamily.Unknown : info.Family;

        // These two labels are designed with wording, not filled by SetCategories, so they must follow
        // the family as well. An Android phone that reads "What your iPhone exposes" looks like the app
        // picked up the wrong device even when every number below it is correct.
        _mediaTitle.Text = _family switch
        {
            DeviceFamily.Android => "What your Android phone exposes",
            DeviceFamily.Apple => "What your iPhone exposes",
            _ => "What your phone exposes"
        };
        _hintLabel.Text = _family == DeviceFamily.Apple
            ? "Files are copied, then read back and hashed against the source where the backend can produce one. Nothing is ever deleted from the iPhone."
            : "Files are copied, then read back and hashed against the source where the backend can produce one. Nothing is ever deleted from the phone.";

        bool reachable = info.Status.IsUsable();
        Color color = info.Status switch
        {
            DeviceStatus.Ready or DeviceStatus.MediaAvailable => _theme.Ok,
            DeviceStatus.MediaUnavailable or DeviceStatus.LockedOrUntrusted or DeviceStatus.Connected => _theme.Warn,
            DeviceStatus.NotFound => _theme.Muted,
            _ => _theme.Error
        };
        bool locked = info.Status is DeviceStatus.LockedOrUntrusted or DeviceStatus.MediaUnavailable;

        _stateLabel.Text = info.Status switch
        {
            DeviceStatus.Ready => _family == DeviceFamily.Apple ? "IPHONE READY" : "DEVICE READY",
            DeviceStatus.MediaAvailable => "MEDIA AVAILABLE",
            // Android never asks the phone's owner to trust this PC: it asks which USB mode to use.
            // An unidentified family gets the neutral unlock wording rather than an Apple-only prompt.
            DeviceStatus.MediaUnavailable or DeviceStatus.LockedOrUntrusted =>
                _family == DeviceFamily.Apple ? "UNLOCK / TRUST REQUIRED" : "UNLOCK REQUIRED",
            DeviceStatus.Connected => _family == DeviceFamily.Apple ? "IPHONE DETECTED" : "DEVICE DETECTED",
            DeviceStatus.NotFound => "NO DEVICE",
            _ => "CHECK NEEDED"
        };
        _stateLabel.ForeColor = color;
        _stateLabel.GlyphColor = color;
        _stateLabel.Fill = Theme.Blend(color, _theme.SurfaceAlt, _theme.IsDark ? 0.18 : 0.12);
        _stateLabel.Stroke = Theme.Blend(color, _theme.Border, 0.5);
        _stateLabel.Glyph = locked ? Glyphs.Lock : Glyphs.Phone;
        _stateLabel.Invalidate();

        _deviceGlyph.ForeColor = reachable ? _theme.Accent : _theme.Muted;
        _deviceGlyph.Text = locked ? Glyphs.Lock : Glyphs.Phone;

        _nameLabel.Text = string.IsNullOrWhiteSpace(info.Name) ? "No device connected" : info.Name;
        _detailLabel.Text = BuildDetail(info);
        _backendLabel.Text = "Backend: " + backendDescription;
    }

    private string BuildDetail(DeviceInfo info)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(info.Model)) parts.Add("Model: " + info.Model);
        if (!string.IsNullOrWhiteSpace(info.OperatingSystem))
            parts.Add((_family switch
            {
                DeviceFamily.Android => "Firmware: ",
                DeviceFamily.Apple => "iOS: ",
                _ => "OS: "
            }) + info.OperatingSystem);
        if (!string.IsNullOrWhiteSpace(info.SerialNumber)) parts.Add("Serial: " + info.SerialNumber);
        if (!string.IsNullOrWhiteSpace(info.ConnectionType)) parts.Add("Link: " + info.ConnectionType);
        if (!string.IsNullOrWhiteSpace(info.Message)) parts.Add(info.Message);
        return parts.Count == 0
            ? _family switch
            {
                DeviceFamily.Android =>
                    "Connect your Android phone by USB, then choose \"File transfer\" on the phone when it asks.",
                DeviceFamily.Apple =>
                    "Connect your iPhone by USB, unlock it, then tap Trust on the device.",
                _ =>
                    "Connect your phone by USB and unlock it — an iPhone asks you to tap Trust, an Android phone asks you to choose File transfer."
            }
            : string.Join("   ·   ", parts);
    }

    /// <summary>
    /// Shows what each banner group actually holds.
    /// <paramref name="stats"/> is null before a scan and <paramref name="available"/> is null while
    /// no device is reachable; both cases say so instead of showing a misleading zero. When the handset
    /// is attached but its media is not exposed yet, the tiles say that instead of "connect your iPhone",
    /// because telling someone to plug in a phone that is already plugged in is the one message that is
    /// guaranteed to be wrong.
    /// </summary>
    public void SetCategories(
        IReadOnlyDictionary<MediaCategory, CategoryStat>? stats,
        IReadOnlyCollection<MediaCategory>? available,
        DeviceStatus status = DeviceStatus.NotFound)
    {
        _categories = stats;
        _available = available;
        _deviceStatus = status;

        bool waitingForUnlock = available is null && status.NeedsAttention();
        string pending = status switch
        {
            DeviceStatus.MediaUnavailable or DeviceStatus.LockedOrUntrusted =>
                _family == DeviceFamily.Apple ? "Unlock iPhone to read media" : "Unlock the phone to read media",
            DeviceStatus.Connected =>
                _family == DeviceFamily.Apple ? "Waiting for iPhone storage" : "Waiting for phone storage",
            _ => _family == DeviceFamily.Apple ? "Connect your iPhone to check" : "Connect your phone to check"
        };

        foreach ((MetricCard tile, MediaCategory category) in CategoryTiles())
        {
            if (available is null)
            {
                tile.SetValue("—", pending, _theme.Muted);
                continue;
            }
            if (!available.Contains(category))
            {
                tile.SetValue("—", "Not exposed by this connection", _theme.Muted);
                continue;
            }

            CategoryStat stat = stats is not null && stats.TryGetValue(category, out CategoryStat found)
                ? found
                : CategoryStat.Empty;

            tile.SetValue(
                stat.Files == 0 ? "0" : stat.Files.ToString("N0"),
                stat.Files == 0 ? "None found" : $"{MediaSummary.FormatBytes(stat.Bytes)} · {category.Tagline()}",
                _theme.CategoryAccent(category));
        }

        // The media card's hint doubles as the honest availability note.
        int exposed = available?.Count ?? 0;
        _mediaHint.Text = available is null
            ? waitingForUnlock
                ? _family switch
                {
                    DeviceFamily.Android =>
                        "Your phone is connected but has not published its storage yet. Unlock the screen, set the USB notification to \"File transfer\", then check the connection.",
                    DeviceFamily.Apple =>
                        "Your iPhone is connected but has not published its media yet. Unlock the screen, tap Trust, then check the connection.",
                    _ =>
                        "Your phone is connected but has not published its storage yet. Unlock the screen and accept any prompt it shows — an iPhone asks you to tap Trust, an Android phone asks you to choose File transfer."
                }
                : _family switch
                {
                    DeviceFamily.Android => "Connect and unlock your Android phone to see which groups it exposes.",
                    DeviceFamily.Apple => "Connect and unlock your iPhone to see which groups it exposes.",
                    _ => "Connect and unlock your phone to see which groups it exposes."
                }
            : exposed == MediaCategories.All.Length
                ? "Every group is reachable through the active backend. A group with no files is reported as empty."
                : $"This connection exposes {exposed} of {MediaCategories.All.Length} groups. Groups it cannot read are shown as unavailable, never as empty folders.";
    }

    /// <summary>
    /// Shows the one operation the app is running: where it is, how far it has got, and how fast.
    ///
    /// <para>
    /// The band sweeps while the work is open-ended and becomes measurably determinate only once a real
    /// total exists, so an unmeasured phase never displays a fabricated percentage. The heading names
    /// the phase, which keeps scan progress and transfer progress distinguishable on one screen.
    /// </para>
    /// </summary>
    public void SetOperation(OperationStatus operation)
    {
        _operation = operation;
        _operationTitle.Text = operation.Heading;
        _operationText.Text = operation.Detail;

        bool determinate = operation.HasMeasuredTotal;
        _operationBar.IsIndeterminate = operation.IsBusy && !determinate;
        _operationBar.Maximum = determinate ? 1000 : 1;
        _operationBar.Value = determinate ? operation.Permille : 0;
        _operationBar.Invalidate();

        _operationTitle.ForeColor = operation.Phase switch
        {
            OperationPhase.Failed => _theme.Error,
            OperationPhase.Incomplete => _theme.Warn,
            OperationPhase.Paused => _theme.Warn,
            OperationPhase.Idle => _theme.Muted,
            _ => _theme.Accent
        };
    }

    /// <summary>
    /// Shows, updates or hides the unfinished-work bar for the chosen destination.
    ///
    /// <para>
    /// <paramref name="state"/> is read from the destination folder's own session record and nothing
    /// else: no cache, no remembered scan, no inference from a previous screen. The bar appears only
    /// when the folder actually holds something to continue, or when its record exists but cannot be
    /// read (which the operator needs to know, because the record is also the recovery plan). A
    /// destination with nothing to do costs no space at all: the row collapses to zero.
    /// </para>
    ///
    /// <para>
    /// When no device is ready the action stays visible but disabled, and the bar says why, instead of
    /// offering a button that cannot work.
    /// </para>
    /// </summary>
    public void SetRecovery(RecoveryState? state, bool deviceReady, string detail)
    {
        bool unusable = state is not null && state.Problem is not null;
        bool resumable = state is not null && state.CanResume;
        bool show = unusable || resumable;

        _recoveryShown = show;
        _recoveryBar.Visible = show;
        ApplyRecoveryLayout();
        if (!show) return;

        if (unusable)
        {
            _recoveryBar.Tone = InfoBarTone.Warning;
            _recoveryBar.Title = "This folder's session record could not be read";
            _recoveryBar.Message = $"{state!.Problem} The copied files were left untouched.";
            _recoveryBar.SetAction(null);
            _recoveryBar.SetDismiss("Dismiss");
        }
        else
        {
            _recoveryBar.Tone = InfoBarTone.Warning;
            _recoveryBar.Title = state!.Headline;
            string device = string.IsNullOrWhiteSpace(state.DeviceName) ? "Unknown device" : state.DeviceName;
            string tail = !string.IsNullOrWhiteSpace(detail)
                ? detail
                : deviceReady
                    ? "Only outstanding files are copied."
                    : "Connect and unlock the device to continue.";
            _recoveryBar.Message = $"{device} · {state.Compact()}  {tail}";
            _recoveryBar.SetAction("Resume transfer");
            _recoveryBar.SetActionEnabled(deviceReady);
            _recoveryBar.SetDismiss("Not now");
        }

        _recoveryBar.Apply(_theme);
    }

    /// <summary>The row that holds the unfinished-work bar; zero-height until there is something to continue.</summary>
    private const int RecoveryRow = 3;

    /// <summary>
    /// The bar is 84 logical px tall with a 10 logical px bottom margin, so its row needs 94. Both are
    /// logical: the designer's values are scaled for the display and a value assigned at run time is not,
    /// so the row is converted where it is applied rather than trusted as a raw number.
    /// </summary>
    private const int RecoveryRowUnits = 94;

    /// <summary>Whether the unfinished-work bar is currently occupying its row.</summary>
    private bool _recoveryShown;

    /// <summary>Guards the layout pass that keeps the row and the page's floor in step with the display.</summary>
    private bool _applyingRecoveryLayout;

    /// <summary>
    /// Sizes the unfinished-work row and the page's own scroll floor for the display this window is on.
    ///
    /// These numbers are logical pixels and the designer's are scaled for the current DPI, but a value
    /// assigned at run time is taken as *device* pixels. At 150% scaling that made the row 94 device pixels
    /// tall - about 63 logical - while the bar's own designer-scaled content was 126, so the second line of
    /// the two-line message was painted past the bottom edge of its own card, into the row below.
    ///
    /// It is applied from the layout pass as well as when the bar is switched, because the DPI is only known
    /// once there is a window, and the page must also stay correct when it is dragged to another monitor.
    ///
    /// The page has to be tall enough for the row that is actually there, or the extra row is paid for out
    /// of the last one - which is the action panel, whose text then slices in half. The scroll floor (for
    /// the scrollbar) and the table's own minimum (because a docked child inside a scrollable panel keeps
    /// the client height until the scroll area is recalculated) both have to agree with it.
    /// </summary>
    private void ApplyRecoveryLayout()
    {
        int row = _recoveryShown ? LogicalToDeviceUnits(RecoveryRowUnits) : 0;
        int floor = LogicalToDeviceUnits(742 + (_recoveryShown ? RecoveryRowUnits : 0));

        if (Math.Abs(_root.RowStyles[RecoveryRow].Height - row) > 0.5f)
            _root.RowStyles[RecoveryRow].Height = row;
        if (_root.MinimumSize.Height != floor)
            _root.MinimumSize = new Size(0, floor);
        if (AutoScrollMinSize.Height != floor)
            AutoScrollMinSize = new Size(0, floor);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (!_applyingRecoveryLayout)
        {
            _applyingRecoveryLayout = true;
            try { ApplyRecoveryLayout(); }
            finally { _applyingRecoveryLayout = false; }
        }
        base.OnLayout(levent);
    }

    /// <summary>Updates the headline tiles with the latest scan and verification numbers.</summary>
    public void SetMetrics(int files, long bytes, int verified, int failed, int unverified, bool runPlan = false,
        int unknownSize = 0)
    {
        _metricFiles = files;
        _metricBytes = bytes;
        _metricVerified = verified;
        _metricFailed = failed;
        _metricUnverified = unverified;

        // A scan counts everything on the device; a finished run counts the plan it was given. The
        // captions have to say which one is on screen, otherwise a filtered run shows its smaller file
        // count beside the device's total size and the two numbers silently contradict each other.
        _filesCard.SetValue(files.ToString("N0"),
            files == 0 ? "Waiting for a scan" : runPlan ? "In this transfer plan" : "Enumerated on the device",
            _theme.Accent);
        _sizeCard.SetValue(MediaSummary.FormatBytes(bytes),
            bytes == 0 ? "Not calculated yet"
            : runPlan ? "Size of this transfer plan"
            : unknownSize > 0 ? $"Known size · {unknownSize:N0} files unknown"
            : "Total exposed media size",
            _theme.Accent);
        _verifiedCard.SetValue(verified.ToString("N0"), "Hash-verified against the source", _theme.Ok);

        int attention = failed + unverified;
        _failedCard.SetValue(attention.ToString("N0"),
            unverified > 0 ? $"{failed:N0} failed · {unverified:N0} unverified" : "No failures",
            attention > 0 ? _theme.Error : _theme.Muted);
    }

    /// <summary>Enables or disables the actions for the current shell state.</summary>
    public void SetState(bool busy, bool deviceReady, bool hasScan, bool paused)
    {
        _checkButton.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _destinationBox.Enabled = !busy;
        _scanButton.Enabled = !busy && deviceReady;
        _startButton.Enabled = !busy && deviceReady && hasScan;
        _pauseButton.Enabled = busy;
        _stopButton.Enabled = busy;
        _pauseButton.Text = paused ? "Resume" : "Pause";
        _pauseButton.Glyph = paused ? Glyphs.Play : Glyphs.Pause;
        _pauseButton.Invalidate();
    }
}
