using System.Diagnostics;
using System.Reflection;
using ITotalTransfer.Core;
using ITotalTransfer.Devices;
using ITotalTransfer.Licensing;
using ITotalTransfer.Services;
using ITotalTransfer.UI.Controls;
using ITotalTransfer.UI.Pages;

namespace ITotalTransfer.UI;

/// <summary>
/// Application shell. The Designer owns the layout (brand band, navigation rail, page frame, status
/// bar); this file owns behaviour: it loads settings, selects a backend, drives the transfer engine
/// and keeps every page in sync. Nothing is advertised that a backend cannot actually do.
/// </summary>
public sealed partial class MainForm : Form
{
    private readonly DashboardPage _dashboard = new();
    private readonly TransferPage _transfer = new();
    private readonly LibraryPage _library = new();
    private readonly WirelessPage _wirelessPage = new();
    private readonly HistoryPage _history = new();
    private readonly SettingsPage _settingsPage = new();
    private readonly DiagnosticsPage _diagnostics = new();

    /// <summary>A navigation entry: its rail button, the page it shows, and its page-header copy.</summary>
    private sealed record NavItem(RoundedButton Button, Control Page, string Title, string Subtitle, string Glyph);

    private readonly List<NavItem> _navItems = [];

    private readonly System.Windows.Forms.Timer _pollTimer = new() { Interval = 5000 };

    private readonly SettingsService _settingsService = new();
    private LicenseService? _license;
    private AppSettings _settings = AppSettings.Defaults();
    private LogService? _log;
    private string _logDestination = "";
    private ITransferBackend? _device;
    private TransferService? _service;
    private ScanResult? _scan;

    /// <summary>
    /// Persisted listings, one per (backend, device). Used to fill the library view while a cold scan runs,
    /// never to replace one: see <see cref="ShowLastKnownLibrary"/>.
    /// </summary>
    private readonly ScanCacheStore _scanCache = new();
    private IReadOnlyList<MediaFile> _cachedFiles = [];
    private CachedLibrary? _cachedLibrary;

    private BackendSelection? _selection;
    private Control? _currentPage;
    private WirelessReceiver? _wireless;
    private CancellationTokenSource? _cts;
    private bool _busy;

    /// <summary>The handset noun for the family actually on screen, so a prompt never calls an Android phone an iPhone.</summary>
    private string PhoneNoun => DeviceCopy.Noun(_deviceInfo.Family);

    /// <summary>The same noun with its article, for sentences such as "Scan the Android phone".</summary>
    private string PhoneWithArticle => DeviceCopy.WithArticle(_deviceInfo.Family);

    /// <summary>
    /// Ceiling on how often the live transfer view is refreshed. The engine reports per file, but the UI only
    /// needs to look alive: at 250 ms a 26,000-file copy repaints about four times a second instead of posting
    /// tens of thousands of updates to the message queue, and the final state comes from the returned summary.
    /// </summary>
    private static readonly TimeSpan UiProgressInterval = TimeSpan.FromMilliseconds(250);
    private bool _deviceReady;
    private bool _pollInProgress;
    private bool _closing;
    private bool _presencePresent;
    private int _pollTicks;

    private Theme _theme = Theme.NeonInstance;
    private DeviceInfo _deviceInfo = new(DeviceStatus.NotFound, "", "Not probed yet.");

    public MainForm()
    {
        InitializeComponent();
        Icon = AppIcons.WindowIcon();
        _settings = _settingsService.Load();
        // The licence service reads the trial ledger before any page exists, so the first screen already
        // carries the real counter instead of a placeholder that changes a moment later.
        _license = LicenseService.ForThisBuild(message => _log?.Write("Licence: " + message, "INFO"));
        BuildPages();
        WireNavigation();
        WirePageEvents();
        InstallChromeBehaviour();
        ApplyTheme();
        ApplyPreferences();
        EnsureLogger();
        _versionLabel.Text = "I-Total Transfer " + VersionText();
        _settingsPage.LoadSettings(_settings);
        _settingsPage.SetStatus("Loaded from " + _settingsService.SettingsPath);
        RefreshLicenseSurface();
        RefreshCacheInventory();
        // Ask the destination folder what it already holds before anything else happens: an interrupted
        // job has to be visible on the very first screen, and it is knowable with no iPhone attached.
        RefreshRecovery();
        _dashboard.Destination = _settings.Destination;
        _dashboard.ConfirmScan = _settings.ConfirmScanWithSecondPass;
        _wirelessPage.LoadSettings(_settings);
        UpdatePolicyText();
        _dashboard.SetState(false, false, false, false);
        PushMediaGroups();
        UiSound.WarmUp();
        Log("Welcome to I-Total Transfer. Connect and unlock your phone to begin.", "INFO");
        SetStatus("Ready. Connect and unlock your phone, then check the connection.");

        _pollTimer.Tick += async (_, _) => await ProbeStatusAsync();
        _pollTimer.Start();
        FormClosing += OnFormClosing;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        Shown += async (_, _) => await CheckConnectionAsync(silent: true);
    }

    /// <summary>Applies the preferences that are not part of the visual theme.</summary>
    private void ApplyPreferences()
    {
        UiSound.Enabled = _settings.PlayUiSounds;
        TopMost = _settings.AlwaysOnTop;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Round the window corners and tint the caption as soon as a handle exists so the chrome never
        // flashes the default light caption before the first theme application.
        Fluent.ApplyWindowChrome(this, _theme);
    }

    /// <summary>
    /// Re-applies the theme when Windows flips between light and dark while "Follow Windows" is
    /// selected. <see cref="Microsoft.Win32.SystemEvents"/> raises this on its own thread, so the
    /// re-theme is marshalled back to the UI thread.
    /// </summary>
    private void OnSystemPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
        if (_closing || IsDisposed || !IsHandleCreated) return;
        if (_settings.Theme != AppTheme.System) return;
        try { BeginInvoke(ApplyTheme); } catch (InvalidOperationException) { }
    }

    /// <summary>
    /// WinForms scales the designed client size by the monitor DPI. On a 150% display the 1400x880
    /// design becomes 2100x1320, which is taller than the working area and would hide the status bar
    /// and lower controls. The scale factor is applied as the window is created (after OnLoad), so the
    /// clamp runs in OnShown and again on DPI change to keep everything reachable at any scale factor.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitToWorkingArea(center: true);
        RememberNormalBounds();
        UpdateNavSelection();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        // Windows supplies the rectangle a DPI change should adopt, so the clamp must not re-centre a
        // window the operator has already placed — it only guarantees the window still fits.
        BeginInvoke(() => FitToWorkingArea(center: false));
    }

    private void FitToWorkingArea(bool center)
    {
        if (_closing || !IsHandleCreated)
        {
            return;
        }
        // A maximized or minimized window is positioned by the system; touching its bounds here would
        // fight the maximize/restore bookkeeping.
        if (IsZoomed(Handle) || WindowState != FormWindowState.Normal)
        {
            return;
        }
        var wa = Screen.FromControl(this).WorkingArea;
        int w = Math.Min(Width, wa.Width);
        int h = Math.Min(Height, wa.Height);
        if (w != Width || h != Height)
        {
            Size = new Size(w, h);
        }
        if (!center)
        {
            return;
        }
        Left = wa.Left + Math.Max(0, (wa.Width - Width) / 2);
        Top = wa.Top + Math.Max(0, (wa.Height - Height) / 2);
    }

    private static string VersionText() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.8.0";

    // ------------------------------------------------------------------ setup
    private void BuildPages()
    {
        _contentBody.SuspendLayout();
        foreach (Control page in new Control[] { _dashboard, _transfer, _library, _wirelessPage, _history, _settingsPage, _diagnostics })
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _contentBody.Controls.Add(page);
        }
        _contentBody.ResumeLayout();
    }

    private void WireNavigation()
    {
        _navItems.Add(new NavItem(_navDashboard, _dashboard, "Overview",
            "Device state, headline numbers and the primary transfer actions.", Glyphs.Home));
        _navItems.Add(new NavItem(_navTransfer, _transfer, "Backup & queue",
            "Live progress, throughput, the per-file queue and the run log.", Glyphs.Download));
        _navItems.Add(new NavItem(_navLibrary, _library, "Media library",
            "Photos, videos, music and files the phone exposes, with verification results.", Glyphs.Photo));
        _navItems.Add(new NavItem(_navWireless, _wirelessPage, "Wireless import",
            "Receive media over your private network while the phone is untethered.", Glyphs.Wifi));
        _navItems.Add(new NavItem(_navHistory, _history, "History",
            "Past sessions and the files that needed attention.", Glyphs.History));
        _navItems.Add(new NavItem(_navSettings, _settingsPage, "Settings",
            "Destination, transfer scope, verification, backend and appearance.", Glyphs.Settings));
        _navItems.Add(new NavItem(_navDiagnostics, _diagnostics, "Diagnostics",
            "Connection checks, native dependencies and support bundles.", Glyphs.Repair));

        foreach (NavItem item in _navItems) item.Button.Click += (_, _) => ShowPage(item.Page);
    }

    private void WirePageEvents()
    {
        _dashboard.CheckConnectionRequested += async (_, _) => await CheckConnectionAsync();
        _dashboard.ScanRequested += async (_, _) => await ScanAsync();
        _dashboard.BrowseDestinationRequested += (_, _) => BrowseDestination();
        _dashboard.StartBackupRequested += async (_, _) => await StartTransferAsync();
        _dashboard.PauseRequested += (_, _) => TogglePause();
        _dashboard.StopRequested += (_, _) => _cts?.Cancel();
        _dashboard.RecoveryResumeRequested += async (_, _) => await ResumeTransferAsync();
        _dashboard.RecoveryDismissRequested += (_, _) => DismissRecovery();

        _transfer.OpenDestinationRequested += (_, _) => OpenDestination();
        _transfer.OpenLogRequested += (_, _) => OpenLog();
        _transfer.ExportReportRequested += (_, _) => ExportReport();
        _transfer.ClearLogRequested += (_, _) => _transfer.ClearLog();

        _library.ApplyFilterRequested += (_, _) => ApplyLibraryFilter();
        _library.VerifyRequested += async (_, _) => await VerifyAsync();
        _library.ExportScanRequested += (_, _) => ExportScan();

        _wirelessPage.StartRequested += (_, _) => StartWireless();
        _wirelessPage.StopRequested += (_, _) => StopWireless();
        _wirelessPage.PairRequested += (_, _) => CreatePairingTicket();
        _wirelessPage.ApproveRequested += ApproveWirelessOffer;
        _wirelessPage.RefuseRequested += RefuseWirelessOffer;
        _wirelessPage.ApproveAllRequested += (_, _) => ApproveAllWirelessOffers();
        _wirelessPage.RefuseAllRequested += (_, _) => RefuseAllWirelessOffers();
        _wirelessPage.AutoApproveChanged += OnWirelessAutoApproveChanged;

        _history.RefreshRequested += (_, _) => RefreshHistory();
        _history.OpenDestinationRequested += (_, _) => OpenDestination();

        _settingsPage.SaveRequested += (_, _) => SaveSettings();
        _settingsPage.ResetRequested += (_, _) => ResetSettings();
        _settingsPage.BrowseDestinationRequested += (_, _) => BrowseDestination();
        _settingsPage.OpenSettingsFolderRequested += (_, _) => OpenSettingsFolder();
        _settingsPage.ForgetCacheRequested += (_, _) => ForgetCacheFromSettings();
        // Picking a theme repaints the whole shell straight away; saving is what makes it stick.
        _settingsPage.ThemePreviewRequested += (_, _) => PreviewTheme();
        _settingsPage.ActivateLicenseRequested += (_, _) => ActivateLicense();
        _settingsPage.PurchaseLicenseRequested += (_, _) => OpenPurchasePage();
        _settingsPage.CheckSteamRequested += (_, _) => CheckSteamLicence();

        _diagnostics.RunRequested += async (_, _) => await RunDiagnosticsAsync();
        _diagnostics.ExportBundleRequested += async (_, _) => await ExportDiagnosticsBundleAsync();
        _diagnostics.OpenAppleGuideRequested += (_, _) => OpenUrl("https://support.apple.com/en-us/108643");
        _diagnostics.OpenAppleDevicesRequested += (_, _) => OpenUrl("https://apps.microsoft.com/detail/9np83lwlpz9k");
    }

    private void ShowPage(Control page)
    {
        _currentPage = page;
        foreach (Control child in _contentBody.Controls) child.Visible = ReferenceEquals(child, page);
        page.BringToFront();
        NavItem? item = _navItems.Find(i => ReferenceEquals(i.Page, page));
        if (item is not null)
        {
            _pageTitle.Text = item.Title;
            _pageSubtitle.Text = item.Subtitle;
            _pageGlyph.Text = item.Glyph;
        }
        UpdateNavSelection();
        if (ReferenceEquals(page, _library)) ApplyLibraryFilter();
        if (ReferenceEquals(page, _history)) RefreshHistory();
    }

    private void UpdateNavSelection()
    {
        foreach (NavItem item in _navItems)
        {
            // Page.Visible only reports true once the form itself is shown, so the selected page is
            // tracked explicitly; otherwise nothing is highlighted during construction.
            item.Button.Active = ReferenceEquals(item.Page, _currentPage);
            item.Button.Invalidate();
        }
    }

    /// <summary>Updates the status bar caption and its leading glyph tint.</summary>
    private void SetStatus(string message, string level = "INFO")
    {
        _statusLabel.Text = message;
        _statusDot.Text = level.ToUpperInvariant() switch
        {
            "OK" => Glyphs.Check,
            "WARN" => Glyphs.Warning,
            "ERROR" => Glyphs.Error,
            "DEBUG" => Glyphs.Info,
            _ => Glyphs.Info
        };
        _statusDot.ForeColor = _theme.LevelColor(level);
    }

    private void ApplyTheme()
    {
        Theme theme = Theme.For(_settings.Theme);
        _theme = theme;
        theme.ApplyAll(this);

        // Pages own their token lookups (status colours, category accents), so push the palette into
        // each of them too; ApplyAll above only walks the visual control tree.
        foreach (Control page in _contentBody.Controls)
            if (page is IThemedPage themed) themed.ApplyTheme(theme);

        _logoImage.Image ??= AppIcons.Badge();

        // Keep the native Win32 common controls (scroll bars, combo boxes, list views) in the same
        // light/dark mode as the custom-painted surfaces. Best-effort: some builds only honour this
        // before the first window exists, so a failure here must never break a live theme switch.
        try { Application.SetColorMode(Program.ColorModeFor(theme.Key)); }
        catch (InvalidOperationException) { }

        // Fluent window chrome: immersive dark caption, rounded corners, caption tinted to the theme.
        Fluent.ApplyWindowChrome(this, theme);

        // The brand band and the page-header band paint the banner ramp; the rule under the brand band
        // is the one place the full accent ramp shows.
        _headerRule.FillGradient = theme.HasGradient ? theme.Gradient : null;
        _headerRule.Fill = theme.Background;
        _headerRule.Stroke = Color.Empty;
        _headerRule.Invalidate();

        _pageHeaderRule.Fill = theme.Border;
        _pageHeaderRule.Stroke = Color.Empty;
        _pageHeaderRule.Invalidate();

        // Text on the painted ramp has to read against the band, not against the page background.
        Color onBand = Theme.Blend(theme.Text, theme.CaptionColor, 0.72);
        _titleLabel.ForeColor = theme.HasBackdrop ? Color.White : theme.Text;
        _subtitleLabel.ForeColor = onBand;
        _deviceNameHeader.ForeColor = onBand;

        SetDeviceHeader(_deviceInfo);
        UpdateNavSelection();
        ShowPage(_currentPage ?? _dashboard);
        Invalidate(true);
    }

    // ------------------------------------------------------------------ logging
    private void EnsureLogger()
    {
        string destination = CurrentDestination();
        try { Directory.CreateDirectory(destination); } catch { }
        if (_log is not null && string.Equals(_logDestination, destination, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            if (_log is not null) _log.LineWritten -= OnLogLine;
            _log = new LogService(destination, _settings.RedactSensitiveData, _settings.LogLevel);
            _log.LineWritten += OnLogLine;
            _logDestination = destination;
        }
        catch (Exception ex)
        {
            _log = null;
            AppendUiLog("Log file unavailable: " + ex.Message, "WARN");
        }
    }

    private void OnLogLine(string line, string level) => AppendUiLog(line, level);

    private void Log(string message, string level = "INFO")
    {
        if (_log is not null) _log.Write(message, level);
        else AppendUiLog(message, level);
    }

    private void AppendUiLog(string line, string level)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => AppendUiLog(line, level))); return; }
        _transfer.AppendLog(line, level);
    }

    private string CurrentDestination()
    {
        string value = _settingsPage.Destination;
        if (string.IsNullOrWhiteSpace(value)) value = _dashboard.Destination;
        if (string.IsNullOrWhiteSpace(value)) value = _settings.Destination;
        return value.Trim();
    }

    // ------------------------------------------------------------------ device status
    /// <summary>
    /// The background poll. It deliberately runs two checks at different frequencies:
    /// <list type="bullet">
    /// <item>every tick, a cheap "is a handset on the bus?" probe that only reads device names, so the
    /// indicator reacts within seconds of a plug-in without ever touching the media tree;</item>
    /// <item>a full media probe (which walks the device's object tree and is slow on MTP) only on a
    /// plug-in event, and afterwards every third tick while the iPhone is still not readable.</item>
    /// </list>
    /// Hammering the device tree every five seconds is what makes an app fight Explorer, Apple's
    /// services and its own transfer, so it is not done.
    /// </summary>
    private async Task ProbeStatusAsync()
    {
        if (_busy || _pollInProgress || _closing || IsDisposed) return;
        _pollInProgress = true;
        try
        {
            DevicePresence presence = await Task.Run(DevicePresenceMonitor.Check);
            bool changed = presence.Present != _presencePresent;
            _presencePresent = presence.Present;
            _pollTicks++;

            if (!presence.Present)
            {
                if (!changed && !_deviceReady) return;
                var absent = new DeviceInfo(DeviceStatus.NotFound, "", "No phone is attached.");
                _deviceReady = false;
                SetDeviceHeader(absent);
                _dashboard.SetDevice(absent, "not selected");
                _dashboard.SetState(_busy, false, _scan is not null, _service?.Pause.IsPaused == true);
                PushMediaGroups();
                RefreshRecovery();
                return;
            }

            // Present: probe the media on a plug-in event, then periodically while it is still unreadable.
            if (!changed && _deviceReady) return;
            if (!changed && _pollTicks % 3 != 0) return;

            // One authoritative source for the band *and* the dashboard: the backend actually in use.
            // Probing every candidate here let the two describe different backends in the same breath —
            // a handset can be "connected, no camera roll" to WPD and "not found" to a Shell probe that
            // could not see portable devices — and the screen contradicted itself. It also sent the
            // device double the traffic for one answer.
            if (_device is null)
            {
                _selection = await Task.Run(() => BackendFactory.Create(_settings, _log!));
                _device = _selection.Backend;
            }
            DeviceInfo info = await Task.Run(() => _device.Probe());
            DeviceStatus status = info.Status;
            DeviceStatus previous = _deviceInfo.Status;
            SetDeviceHeader(info);
            AnnounceStatus(previous, status);
            _settingsPage.SetBackendStatus(BuildBackendStatusText(info, _selection));
            if (!_busy)
            {
                _deviceReady = status.IsUsable();
                _dashboard.SetDevice(info, _selection?.Description ?? (status.IsUsable() ? "detected — run a connection check" : "not selected"));
                _dashboard.SetState(_busy, _deviceReady, _scan is not null, _service?.Pause.IsPaused == true);
                PushMediaGroups();
                RefreshRecovery();
            }
        }
        catch (Exception ex) { Log("Device poll failed: " + ex.Message, "DEBUG"); }
        finally { _pollInProgress = false; }
    }

    /// <summary>The Settings page's one-line backend summary: the backend actually in use, and its verdict.</summary>
    private static string BuildBackendStatusText(DeviceInfo info, BackendSelection? selection)
    {
        string name = selection?.Backend.Name ?? info.BackendName ?? "not selected";
        string state = info.Status.IsUsable() ? "available" : info.Status.ToString();
        return $"{name}: {state} — {Shorten(info.Message, 120)}";
    }

    /// <summary>
    /// Plays the one sound the situation deserves when the connection state changes: a rising chime when
    /// the iPhone becomes readable, a short blip when it appears but still needs the screen unlocked.
    /// Unplugging is silent — there is nothing to do about it.
    /// </summary>
    private static void AnnounceStatus(DeviceStatus previous, DeviceStatus current)
    {
        if (previous == current) return;
        if (current.IsUsable() && !previous.IsUsable())
            UiSound.Success();
        else if (current.NeedsAttention() && !previous.IsPresent())
            UiSound.Notify();
    }

    /// <summary>
    /// Refreshes the band's connection indicator. The header carries one quiet signal — a blip whose
    /// colour is the whole story (green: media readable, amber: handset answered but needs the screen
    /// unlocked, grey: nothing attached) — so instructions are never repeated in chrome.
    /// </summary>
    private void SetDeviceHeader(DeviceInfo info)
    {
        _deviceInfo = info;
        Theme theme = _theme;

        Color blip = info.Status switch
        {
            DeviceStatus.Ready or DeviceStatus.MediaAvailable => theme.Ok,
            DeviceStatus.MediaUnavailable or DeviceStatus.LockedOrUntrusted or DeviceStatus.Connected => theme.Warn,
            DeviceStatus.Error => theme.Error,
            _ => theme.Muted
        };

        _deviceBlip.BlipColor = blip;
        _deviceBlip.Pulsing = info.Status.NeedsAttention();
        _deviceBlip.Invalidate();

        _deviceStateHeader.Text = info.Status switch
        {
            DeviceStatus.Ready => info.Family == DeviceFamily.Android ? "Phone ready" : "iPhone ready",
            DeviceStatus.MediaAvailable => "Media available",
            DeviceStatus.MediaUnavailable or DeviceStatus.LockedOrUntrusted =>
                info.Family == DeviceFamily.Android ? "Unlock the phone" : "Unlock iPhone",
            DeviceStatus.Connected => info.Family == DeviceFamily.Android ? "Phone detected" : "iPhone detected",
            DeviceStatus.NotFound => "Not connected",
            DeviceStatus.Error => "Check device",
            _ => "Checking…"
        };
        _deviceStateHeader.ForeColor = blip;

        _deviceNameHeader.Text = string.IsNullOrWhiteSpace(info.Name)
            ? info.Family switch
            {
                DeviceFamily.Android => "Plug in by USB, unlock, then choose File transfer",
                DeviceFamily.Apple => "Plug in by USB, unlock, then tap Trust",
                _ => "Plug in by USB and unlock the phone"
            }
            // The banner caption identifies the handset and its link. The long readiness sentence belongs
            // in the device card, not in a right-aligned caption that truncates to an ellipsis.
            : info.Name
              + (string.IsNullOrWhiteSpace(info.OperatingSystem) ? "" : " · " + info.OperatingSystem)
              + " · " + (string.IsNullOrWhiteSpace(info.ConnectionType) ? "USB" : info.ConnectionType);
        _backendStatusLabel.Text = "Backend: " + (_selection?.Backend.Name ?? info.BackendName ?? "not selected");
    }

    /// <summary>
    /// Pushes the banner's four media groups to the dashboard: how many files and bytes each one holds,
    /// and which of them the active connection can actually read. The available set is null until a
    /// device is reachable, so the tiles say "connect your iPhone" instead of claiming zero.
    /// </summary>
    private void PushMediaGroups()
    {
        IReadOnlyDictionary<MediaCategory, CategoryStat>? stats = _scan is null ? null : MediaSummary.From(_scan.Files);
        IReadOnlyCollection<MediaCategory>? available = _deviceReady
            ? _selection?.Backend.Capabilities.Categories ?? _device?.Capabilities.Categories
            : null;
        _dashboard.SetCategories(stats, available, _deviceInfo.Status);
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..max] + "…";

    private async Task CheckConnectionAsync(bool silent = false)
    {
        if (!BeginOperation()) return;
        try
        {
            EnsureLogger();
            ReleaseDevice();
            DeviceStatus previous = _deviceInfo.Status;
            _selection = await Task.Run(() => BackendFactory.Create(_settings, _log!));
            _device = _selection.Backend;
            DeviceInfo info = await Task.Run(() => _device.Probe());
            _deviceReady = info.Status.IsUsable();
            _dashboard.SetDevice(info, _selection.Description);
            SetDeviceHeader(info);
            AnnounceStatus(previous, info.Status);
            SetStatus($"Connection check: {info.Status} — {info.Message}", _deviceReady ? "OK" : "WARN");
            Log($"Connection check: {info.Status} — {info.Message} ({_selection.Description})", _deviceReady ? "OK" : "WARN");
            foreach (string attempt in _selection.Attempts) Log("  backend probe " + attempt, "DEBUG");
            if (_selection.IsFallback && !_deviceReady)
                Log("No streaming backend is available. The Windows Shell compatibility backend can copy but cannot claim source-to-destination integrity.", "WARN");
            BuildService();
            _dashboard.SetState(_busy, _deviceReady, _scan is not null, _service?.Pause.IsPaused == true);
            PushMediaGroups();
            RefreshRecovery();
        }
        catch (Exception ex)
        {
            Log("Connection check failed: " + ex.Message, "ERROR");
            SetStatus("Connection check failed: " + ex.Message, "ERROR");
            if (!silent)
                MessageBox.Show(this, ex.Message, "Connection check failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { EndOperation(); }
    }

    private void BuildService()
    {
        if (_log is null || _device is null) { _service = null; return; }
        _service = new TransferService(_device, _log, TransferOptions.FromSettings(_settings), TransferRules.BuildFilter(_settings));
    }

    private void ReleaseDevice()
    {
        try { _device?.Dispose(); } catch { }
        _device = null;
        _service = null;
        // The selection describes the backend that was just disposed, so it must not outlive it.
        _selection = null;
    }

    // ------------------------------------------------------------------ scan
    private async Task ScanAsync()
    {
        if (!BeginOperation()) return;
        try
        {
            EnsureLogger();
            if (_device is null)
            {
                _selection = await Task.Run(() => BackendFactory.Create(_settings, _log!));
                _device = _selection.Backend;
                BuildService();
            }
            DeviceInfo preflight = await Task.Run(() => _device.Probe());
            _deviceReady = preflight.Status.IsUsable();
            _dashboard.SetDevice(preflight, _selection?.Description ?? _device.Name);
            SetDeviceHeader(preflight);
            if (!_deviceReady) throw new InvalidOperationException("Connection preflight failed: " + preflight.Message);

            Directory.CreateDirectory(CurrentDestination());

            // Show the last complete listing for this device right away. A cold camera roll is enumerated by
            // the device at roughly 27 ms per object (measured), so a first pass is minutes of work during
            // which this page used to be simply empty. The live scan below still runs and still replaces
            // this; the provenance line above the grid says where the rows came from.
            ShowLastKnownLibrary(preflight);

            // Scan progress goes to the transfer page, the always-visible status bar *and* the Overview's
            // operation band. Enumerating a large camera roll takes minutes and the device, not the app,
            // sets the pace, so a scan that reported only to the Backup & queue page looked like a hang
            // from every other page — including the Overview the operator watches.
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Scanning,
                "Enumerating the device. A first pass over a large camera roll is limited by the phone."));
            var progress = new Progress<string>(message =>
            {
                _transfer.SetPhase(message);
                SetStatus(message, "INFO");
                _dashboard.SetOperation(new OperationStatus(OperationPhase.Scanning, message));
            });
            ScanResult scan = await _service!.ScanAsync(_cts!.Token, progress);
            EnsureScanCameFromSelectedDevice(preflight, scan);

            if ((_dashboard.ConfirmScan || _settingsPage.ConfirmScan) && scan.Files.Count > 0)
            {
                Log("Second scan enabled: re-enumerating to detect a listing that changes mid-scan.", "WARN");
                ScanResult second = await _service.ScanAsync(_cts!.Token, progress);
                EnsureScanCameFromSelectedDevice(preflight, second);
                IReadOnlyList<string> changed = ScanStability.ChangedPaths(scan, second);
                if (changed.Count > 0)
                    throw new InvalidOperationException($"The {PhoneNoun} listing changed between scans. No transfer plan was accepted. First changed paths: " + string.Join(", ", changed));
                scan = second;
                Log("Second scan matched the first by path and size. This checks listing stability, not iCloud completeness.", "OK");
            }

            _scan = scan;
            LogScan(scan);
            FinishScanCache(preflight, scan);
            SetStatus($"Scan finished: {scan.Files.Count:N0} files · {FormatBytes(scan.TotalBytes)}.", scan.IsComplete ? "OK" : "WARN");
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Idle,
                scan.IsComplete
                    ? $"Scan finished: {scan.Files.Count:N0} files · {MediaSummary.FormatBytes(scan.TotalBytes)}. Review the groups below."
                    : $"Scan finished with warnings: {scan.Files.Count:N0} files · {MediaSummary.FormatBytes(scan.TotalBytes)}. The listing is partial — read the warnings before copying.",
                0, 0, Indeterminate: false));
            ApplyLibraryFilter();
            _dashboard.SetMetrics(scan.Files.Count, scan.TotalBytes, 0, 0, 0,
                unknownSize: scan.Report?.UnknownSizeCount ?? 0);
            PushMediaGroups();
            _transfer.SetPhase("Scan complete — review warnings before copying.");
            RefreshQueue();
            SaveScanReports(scan);
            if (scan.IsComplete) UiSound.Success(); else UiSound.Notify();
        }
        catch (OperationCanceledException)
        {
            _scan = null;
            Log("Scan cancelled. No complete scan is available; a partial listing is never treated as complete.", "WARN");
            DescribeCachedListingState("the scan was cancelled before it completed");
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Idle,
                "Scan cancelled — no complete listing was accepted.", 0, 0, Indeterminate: false));
        }
        catch (Exception ex)
        {
            _scan = null;
            UiSound.Error();
            Log("Scan failed: " + ex.Message, "ERROR");
            DescribeCachedListingState("the live scan failed");
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Failed, "Scan failed: " + ex.Message));
            MessageBox.Show(this, ex.Message, "Scan failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { EndOperation(); }
    }

    /// <summary>
    /// Refuses a listing that came from a different phone than the one the session selected. A backend
    /// that re-enumerates the bus can return the other attached handset when the selected one briefly
    /// stops answering; accepting those rows would display (and cache, and copy) one phone's files under
    /// the other phone's name. Nothing is accepted, cached or copied when this check fails.
    /// </summary>
    private static void EnsureScanCameFromSelectedDevice(DeviceInfo selected, ScanResult scan)
    {
        if (scan.BelongsTo(selected)) return;
        throw new InvalidOperationException(
            "The live scan read a different phone than the one selected. Selected: " + selected.Name +
            " (" + selected.Family + "); the scan returned: " + scan.DeviceName + " (" + scan.Family +
            "). Nothing was accepted, cached or queued. Reconnect the phone you want, choose Check device, and scan again.");
    }

    private void LogScan(ScanResult scan)
    {
        string level = scan.IsComplete ? "OK" : "WARN";
        Log($"Scan finished: {scan.Files.Count:N0} files, {FormatBytes(scan.TotalBytes)}, status={scan.Status}, duration={scan.Duration.TotalMinutes:N1} min, backend={_device?.Name}.", level);
        foreach (string warning in scan.Warnings) Log(warning, "WARN");
    }

    private void SaveScanReports(ScanResult scan)
    {
        try
        {
            TransferService.SaveScanReport(scan, CurrentDestination(), _device?.Name ?? "unknown");
        }
        catch (Exception ex) { Log("Could not write enumeration report: " + ex.Message, "WARN"); }
    }

    private void ApplyLibraryFilter()
    {
        // The grid shows the live scan once there is one, and otherwise the last complete listing that was
        // loaded for this device. Both go through the same filter, so the group chips behave identically
        // whether the rows are live or remembered.
        IReadOnlyList<MediaFile> source = _scan?.Files ?? _cachedFiles;
        if (source.Count == 0)
        {
            _library.SetFiles([], 0);
            return;
        }
        string kind = _library.KindFilter;
        string text = _library.FilterText;
        IEnumerable<MediaFile> query = source;
        // Group membership comes from MediaCategories so the filter, the tiles and the tests agree.
        if (!string.Equals(kind, "All", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse(kind, ignoreCase: true, out MediaCategory category))
            query = query.Where(file => file.Category == category);
        if (!string.IsNullOrWhiteSpace(text))
            query = query.Where(f => f.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || f.Folder.Contains(text, StringComparison.OrdinalIgnoreCase));
        _library.SetFiles(query.ToList(), source.Count);
    }

    // ------------------------------------------------------------------ last known library
    private ScanCacheKey CurrentScanCacheKey(DeviceInfo info) =>
        ScanCacheKey.From(_device?.Name ?? _selection?.Description, info.StableId, info.SerialNumber, info.Model);

    /// <summary>
    /// Fills the library view from the last complete listing of this device, if the operator has that
    /// behaviour on and the listing is still allowed. Only the library view is touched: the Overview's tiles
    /// answer "what does the phone expose right now" and must not be fed history.
    /// </summary>
    private void ShowLastKnownLibrary(DeviceInfo info)
    {
        _cachedLibrary = null;
        _cachedFiles = [];
        if (!_settings.ReuseLastKnownLibrary)
        {
            _library.SetProvenance(null);
            return;
        }

        CachedLibrary? cached = _scanCache.TryLoad(CurrentScanCacheKey(info), DateTimeOffset.UtcNow,
            TimeSpan.FromDays(Math.Max(1, _settings.ScanCacheMaxAgeDays)));

        if (cached is null)
        {
            // A refused listing is worth saying out loud, but it is not a failure: the live scan decides.
            if (_scanCache.LastNote is { Length: > 0 } note)
            {
                Log($"No last known library was shown: {note}. The live scan decides what is listed.", "WARN");
                SetStatus($"Ignoring the cached listing ({note}); the live scan is running.", "WARN");
            }
            _library.SetProvenance(null);
            return;
        }

        _cachedLibrary = cached;
        _cachedFiles = cached.ToMediaFiles();
        _library.SetProvenance(
            $"Last known library (captured {cached.CapturedLocal}) — the live scan is still running and will replace this");
        ApplyLibraryFilter();

        string unknown = cached.UnknownSizeCount > 0 ? $", {cached.UnknownSizeCount:N0} of unknown size" : "";
        Log($"Showing the last known library for this device while the live scan runs: {cached.Files.Count:N0} files, " +
            $"{FormatBytes(cached.TotalBytes)}{unknown}, captured {cached.CapturedLocal} from backend '{cached.BackendName}'. " +
            "This is a cached listing, not the result of the current scan.", "WARN");
        SetStatus($"Showing the last known library captured {cached.CapturedLocal} while the live scan runs…", "INFO");
    }

    /// <summary>
    /// Reports how the fresh scan differs from the cached listing and, when the scan is fit to remember,
    /// replaces it. A partial or empty enumeration never overwrites a complete listing.
    /// </summary>
    private void FinishScanCache(DeviceInfo info, ScanResult scan)
    {
        // Defence in depth: the caller already refuses a mismatched scan, but a cache key is built from
        // the *selected* device. Writing a listing from another phone under that key would feed the wrong
        // rows to the next session, so a mismatch never reaches the store.
        if (!scan.BelongsTo(info))
        {
            Log("The listing was not cached because it came from a different phone than the selected one.", "ERROR");
            return;
        }

        CachedLibrary? previous = _cachedLibrary;
        if (previous is not null)
        {
            ScanCacheDiff diff = ScanCacheDiff.Between(previous, scan);
            Log($"Compared with the last known library (captured {previous.CapturedLocal}): {diff.Describe()}. " +
                "A difference is normal — the phone can change between scans — and is reported because the cached rows were on screen.", 
                diff.IsIdentical ? "OK" : "WARN");
        }

        // The live listing is authoritative from here, whether or not it ends up being written.
        _cachedLibrary = null;
        _cachedFiles = [];
        _library.SetProvenance(null);

        if (!_settings.ReuseLastKnownLibrary) return;

        if (_scanCache.Save(CurrentScanCacheKey(info), scan, VersionText(), out string? note))
        {
            Log($"Cached this complete listing ({scan.Files.Count:N0} files) so the next cold scan is not a blank window.", "INFO");
            return;
        }
        if (note is not null) Log($"The listing was not cached: {note}.", "WARN");
    }

    /// <summary>
    /// Keeps a cached listing on screen after a run that did not produce a live one, and says plainly that
    /// what is on screen is the last time the phone was read rather than its state now.
    /// </summary>
    private void DescribeCachedListingState(string reason)
    {
        if (_cachedLibrary is null) return;
        _library.SetProvenance(
            $"Last known library (captured {_cachedLibrary.CapturedLocal}) — {reason}, so these rows are the last completed scan, not the current state of the phone");
        SetStatus($"Showing the last known library from {_cachedLibrary.CapturedLocal}; " + reason + ".", "WARN");
    }

    /// <summary>Deletes every cached listing. Returns how many were removed, for the confirmation message.</summary>
    private int ForgetCachedListings()
    {
        int removed = _scanCache.ForgetAll();
        _cachedLibrary = null;
        _cachedFiles = [];
        _library.SetProvenance(null);
        Log($"Removed {removed} cached library listing(s) at the operator's request.", "WARN");
        return removed;
    }

    // ------------------------------------------------------------------ transfer
    private async Task StartTransferAsync()
    {
        if (_settings.Direction == TransferDirection.WindowsToDevice)
        {
            await SendToDeviceAsync();
            return;
        }
        if (_scan is null)
        {
            MessageBox.Show(this, $"Scan {PhoneWithArticle} successfully before starting a transfer.", "No scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        LicenseDecision licence = _license!.TryStartJob(resuming: false);
        if (!licence.Allowed) { ShowLicenseStop(licence.Message); return; }
        if (!BeginOperation()) return;
        try
        {
            EnsureLogger();
            if (_device is null || _service is null) { _selection = await Task.Run(() => BackendFactory.Create(_settings, _log!)); _device = _selection.Backend; BuildService(); }
            DeviceInfo preflight = await Task.Run(() => _device!.Probe());
            if (!preflight.Status.IsUsable())
                throw new InvalidOperationException($"Transfer blocked because {PhoneWithArticle} is no longer ready: " + preflight.Message);

            string destination = CurrentDestination();
            (int plannedCount, long plannedBytes) = _service!.PlanSummary(_scan);
            if (plannedCount == 0)
            {
                MessageBox.Show(this,
                    "Every scanned file is excluded by the current filter; nothing would be copied.\n\n" +
                    "Clear or widen the include/exclude/size filters in Settings, then start again.",
                    "Nothing to transfer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string message = $"Copy {plannedCount:N0} files ({FormatBytes(plannedBytes)}) from {PhoneWithArticle} to:\n{destination}\n\n" +
                             $"Conflict policy: {_settings.ConflictPolicy}\nVerification: {_settings.VerificationPolicy}";
            if (MessageBox.Show(this, message, "Start backup", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

            var progress = new ThrottledProgress<TransferProgress>(
                new Progress<TransferProgress>(UpdateProgress), UiProgressInterval);
            if (_settings.KeepAwake) PowerManagement.KeepAwake(true);
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Transferring, "Starting the copy…"));
            TransferSummary summary;
            try
            {
                summary = await _service!.CopyAsync(_scan, destination, _settings.MaxRetries, progress, _cts!.Token);
            }
            finally { if (_settings.KeepAwake) PowerManagement.KeepAwake(false); }

            await ReportTransferSummaryAsync(summary);
        }
        catch (OperationCanceledException)
        {
            _transfer.SetPhase("Stopped — run again to resume; completed files are preserved.");
            Log("Transfer stopped by the user. Completed files are preserved and the manifest allows a resume.", "WARN");
            SetStatus("Transfer stopped. Completed files are preserved; run again to resume.", "WARN");
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Idle,
                "Transfer stopped. Completed files are preserved; run again to resume.", 0, 0, Indeterminate: false));
            RefreshRecovery();
        }
        catch (Exception ex)
        {
            _transfer.SetPhase("Transfer failed");
            Log("Transfer error: " + ex, "ERROR");
            SetStatus("Transfer failed: " + ex.Message, "ERROR");
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Failed, "Transfer failed: " + ex.Message));
            RefreshRecovery();
            MessageBox.Show(this, ex.Message, "Transfer failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { EndOperation(); }
    }

    // ------------------------------------------------------------------ interrupted-session recovery

    private RecoveryState? _recoveryState;
    private string _recoveryKey = "";
    private DateTime _recoveryStampUtc;
    private long _recoveryStampLength = -1;
    private string _recoveryPushed = "\0";
    private bool _recoveryDismissedThisRun;
    private string _recoveryLogged = "";

    /// <summary>
    /// Keeps the Overview's unfinished-work bar in step with the destination folder's own session
    /// record.
    ///
    /// <para>
    /// This reads one small file beside the copied files - the same record the copy loop writes - and
    /// nothing else: no device, no scan, no cache, no memory of a previous screen. That is why it can
    /// answer "did my last backup finish?" before an iPhone is even attached, which is exactly when the
    /// operator wants to know. The parsed result is reused until the file's timestamp or size changes,
    /// so a 26,000-entry record is not re-parsed on every poll.
    /// </para>
    /// </summary>
    private void RefreshRecovery()
    {
        string destination = CurrentDestination();
        RecoveryState? state = null;

        if (!string.IsNullOrWhiteSpace(destination))
        {
            try
            {
                string manifestPath = SessionRecovery.ManifestPath(destination);
                bool exists = File.Exists(manifestPath);
                DateTime stamp = exists ? File.GetLastWriteTimeUtc(manifestPath) : default;
                long length = exists ? new FileInfo(manifestPath).Length : -1;
                if (!string.Equals(destination, _recoveryKey, StringComparison.OrdinalIgnoreCase) ||
                    stamp != _recoveryStampUtc || length != _recoveryStampLength)
                {
                    _recoveryKey = destination;
                    _recoveryStampUtc = stamp;
                    _recoveryStampLength = length;
                    _recoveryState = SessionRecovery.Inspect(destination);
                }
                state = _recoveryState;
            }
            catch (Exception ex)
            {
                Log("Could not read the destination's session record: " + ex.Message, "WARN");
                state = null;
            }
        }

        bool dismissed = _recoveryDismissedThisRun ||
            (state is { CanResume: true } &&
             string.Equals(state.Fingerprint, _settings.DismissedRecovery, StringComparison.Ordinal));
        RecoveryState? pushed = dismissed ? null : state;

        // Only touch the page when something actually changed: this runs from the device poll.
        string key = $"{pushed?.Fingerprint ?? pushed?.Problem ?? "-"}|{_deviceReady}";
        if (key != _recoveryPushed)
        {
            _recoveryPushed = key;
            _dashboard.SetRecovery(pushed, _deviceReady, "");
        }

        if (pushed is { CanResume: true } && pushed.Fingerprint != _recoveryLogged)
        {
            _recoveryLogged = pushed.Fingerprint;
            Log($"Unfinished session in {pushed.Destination}: {pushed.Describe()} It did not finish because {pushed.WhyStopped}.", "WARN");
        }
    }

    /// <summary>
    /// Records that the operator does not want to continue the unfinished session now.
    ///
    /// <para>
    /// A resumable session is remembered by its own fingerprint, so any later progress, any new entry
    /// or a different destination brings the offer back - a dismissal can never hide a new problem. An
    /// unreadable record is only hidden for this run, because a folder whose recovery record cannot be
    /// read should not be quietly forgotten.
    /// </para>
    /// </summary>
    private void DismissRecovery()
    {
        RecoveryState? state = _recoveryState;
        _recoveryDismissedThisRun = true;

        if (state is { CanResume: true })
        {
            _settings.DismissedRecovery = state.Fingerprint;
            try { _settingsService.Save(_settings); }
            catch (Exception ex) { Log("Could not save the dismissal: " + ex.Message, "WARN"); }

            string note = $"Unfinished session set aside. {state.Outstanding:N0} file(s), {FormatBytes(state.OutstandingBytes)}, are still outstanding in the destination.";
            SetStatus(note, "WARN");
            Log($"Unfinished session in {state.Destination} was set aside by the operator. {state.Outstanding:N0} file(s) remain outstanding.", "WARN");
        }

        RefreshRecovery();
    }

    /// <summary>
    /// Continues the unfinished session recorded in the destination folder.
    ///
    /// <para>
    /// The plan is rebuilt from that record rather than from a fresh enumeration, because the whole
    /// point of the record is that a 106 GB camera roll does not have to be walked again to know what
    /// is left. Nothing is trusted on the strength of the record alone: every file the plan carries is
    /// fetched from the device and hashed again, and files it marks as already verified are left in
    /// place instead of being re-read - an explicit verification action exists for proving those again.
    /// </para>
    /// </summary>
    private async Task ResumeTransferAsync()
    {
        string destination = CurrentDestination();
        SessionManifest? manifest = SessionRecovery.Load(destination, out string? problem);
        if (manifest is null)
        {
            MessageBox.Show(this, problem ?? "This destination folder holds no unfinished session.",
                "Nothing to continue", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshRecovery();
            return;
        }

        RecoveryState state = SessionRecovery.Inspect(destination);
        ScanResult plan = SessionRecovery.BuildResumePlan(manifest, out int unsafeSkipped);
        if (plan.Files.Count == 0)
        {
            MessageBox.Show(this,
                $"The recorded session has nothing left to fetch: {state.Verified:N0} file(s) are already verified and {state.Skipped:N0} were skipped by policy or filter.\n\n" +
                $"Scan {PhoneWithArticle} and start a transfer to pick up anything new.",
                "Nothing to continue", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshRecovery();
            return;
        }

        if (!BeginOperation()) return;
        try
        {
            EnsureLogger();
            if (_device is null || _service is null)
            {
                _selection = await Task.Run(() => BackendFactory.Create(_settings, _log!));
                _device = _selection.Backend;
                BuildService();
            }
            DeviceInfo preflight = await Task.Run(() => _device!.Probe());
            if (!preflight.Status.IsUsable())
                throw new InvalidOperationException($"Continuing is blocked because {PhoneWithArticle} is not ready: " + preflight.Message);

            if (!SessionRecovery.MatchesCurrentDevice(manifest, preflight, out string? identityProblem))
                throw new InvalidOperationException(identityProblem);

            // A legacy record has no hardware identity. Once the name check has agreed, adopt the live
            // identity so the engine records it and future resumes are decided by hardware, not a name.
            if (string.IsNullOrWhiteSpace(plan.StableId) && !string.IsNullOrWhiteSpace(preflight.StableId))
                plan = plan with { StableId = preflight.StableId.Trim() };

            string device = string.IsNullOrWhiteSpace(state.DeviceName) ? preflight.Name : state.DeviceName;
            var message = new System.Text.StringBuilder();
            message.AppendLine("Continue the unfinished session in:");
            message.AppendLine(destination);
            message.AppendLine();
            message.AppendLine($"Device: {device}    Interrupted: {state.WhyStopped}.");
            message.AppendLine($"Left to fetch: {plan.Files.Count:N0} file(s), {FormatBytes(plan.TotalBytes)}.");
            message.AppendLine($"Already copied and verified: {state.Verified:N0} file(s) — left alone, not re-checked or copied again.");
            if (state.Failed > 0 || state.Inaccessible > 0 || state.CopiedUnverified > 0)
                message.AppendLine($"Recorded problems that will be retried: {state.Failed:N0} failed, {state.Inaccessible:N0} unreadable, {state.CopiedUnverified:N0} unverified.");
            if (unsafeSkipped > 0)
                message.AppendLine($"{unsafeSkipped:N0} recorded path(s) were refused as unsafe and are left out.");
            message.AppendLine();
            message.AppendLine($"Files added to {PhoneWithArticle} since this session was planned are not part of it; scan again to include them.");
            if (MessageBox.Show(this, message.ToString(), "Continue unfinished session",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
                return;

            var progress = new ThrottledProgress<TransferProgress>(
                new Progress<TransferProgress>(UpdateProgress), UiProgressInterval);
            if (_settings.KeepAwake) PowerManagement.KeepAwake(true);
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Transferring,
                $"Continuing the unfinished session: {plan.Files.Count:N0} file(s) outstanding."));

            TransferSummary summary;
            try { summary = await _service!.CopyAsync(plan, destination, _settings.MaxRetries, progress, _cts!.Token); }
            finally { if (_settings.KeepAwake) PowerManagement.KeepAwake(false); }

            await ReportTransferSummaryAsync(summary);
        }
        catch (OperationCanceledException)
        {
            _transfer.SetPhase("Stopped — the session is still unfinished; continue again when ready.");
            Log("Continued transfer stopped by the user. Completed files are preserved and the session record still holds what is left.", "WARN");
            SetStatus("Stopped. The session is still unfinished; continue again when ready.", "WARN");
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Idle,
                "Stopped. The session is still unfinished; continue again when ready.", 0, 0, Indeterminate: false));
            RefreshRecovery();
        }
        catch (Exception ex)
        {
            _transfer.SetPhase("Continuing failed");
            Log("Could not continue the session: " + ex, "ERROR");
            SetStatus("Continuing failed: " + ex.Message, "ERROR");
            _dashboard.SetOperation(new OperationStatus(OperationPhase.Failed, "Continuing failed: " + ex.Message));
            RefreshRecovery();
            MessageBox.Show(this, ex.Message, "Continuing failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { EndOperation(); }
    }

    /// <summary>
    /// Reports a finished transfer the same way whether it was started from a fresh scan or continued
    /// from the destination's session record, so a continued job is never described in other terms.
    /// </summary>
    private async Task ReportTransferSummaryAsync(TransferSummary summary)
    {
        _transfer.SetSummary(summary);
        RefreshQueue();
        // A pass that processed every planned file but left unverifiable entries is not "Stopped": the
        // run ended, nothing is moving, and the operator has a decision to make. A genuine failure or
        // cancellation keeps the Failed phase.
        OperationPhase phase = summary.Completed
            ? OperationPhase.Idle
            : summary.Failed == 0 && summary.Inaccessible == 0
                ? OperationPhase.Incomplete
                : OperationPhase.Failed;
        string level = phase == OperationPhase.Incomplete ? "WARN" : summary.Completed ? "OK" : "ERROR";
        Log(summary.Status, level);
        SetStatus(summary.Status, level);
        _dashboard.SetOperation(new OperationStatus(
            phase, summary.Status,
            phase == OperationPhase.Incomplete ? summary.Total : 0,
            phase == OperationPhase.Incomplete ? summary.Total : 0,
            Indeterminate: false));
        if (summary.Completed) UiSound.Success();
        else if (phase == OperationPhase.Incomplete) UiSound.Notify();
        else UiSound.Error();
        _dashboard.SetMetrics(summary.Total, summary.ExpectedBytes,
            Math.Max(0, summary.Copied + summary.AlreadyPresent - summary.Unverifiable), summary.Failed, summary.Unverifiable,
            runPlan: true);
        RefreshRecovery();

        if (_settings.VerifyAfterCopy && summary.Completed)
        {
            if (_scan is null)
                Log("Post-copy verification was not run: it checks a scan listing, and this run continued from the session record instead. Use Verify on the Library page to re-prove those files.", "INFO");
            else
                await VerifyCoreAsync(repair: false);
        }

        JobRecordResult licence = RecordFinishedJob(summary, LicenseJobKind.DeviceToWindows);

        if (_settings.ShowCompletionDialog)
        {
            string licenceLine = licence.Snapshot.Standing == LicenseStanding.Licensed
                ? ""
                : $"\n\n{licence.Snapshot.Headline}.\n{licence.Detail}";
            MessageBox.Show(this,
                $"{summary.Status}\n\nCopied: {summary.Copied:N0}\nAlready present: {summary.AlreadyPresent:N0}\nSkipped: {summary.Skipped:N0}\n" +
                $"Failed: {summary.Failed:N0}\nUnverified: {summary.Unverifiable:N0}\nWeakest verification: {summary.WeakestLevel}\n" +
                $"Elapsed: {summary.Elapsed:hh\\:mm\\:ss}\n\nReport: {summary.ReportPath ?? summary.ManifestPath}" + licenceLine,
                "Transfer result", MessageBoxButtons.OK, summary.Completed ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// Records a finished job with the licence service and repaints the Settings row. Recording is
    /// idempotent: the engine's <see cref="TransferSummary.JobId"/> makes a repeated report free.
    /// </summary>
    private JobRecordResult RecordFinishedJob(TransferSummary summary, LicenseJobKind kind)
    {
        JobRecordResult result = _license!.RecordJob(new JobFacts(
            summary.JobId, kind, summary.Completed, summary.Copied, summary.BytesCopied,
            summary.Failed, Cancelled: false));
        RefreshLicenseSurface();
        Log("Licence: " + result.Detail, result.Counted ? "OK" : "INFO");
        return result;
    }

    /// <summary>Rebuilds the Settings licence row from the service's current view.</summary>
    private void RefreshLicenseSurface()
    {
        if (_license is null) return;
        LicenseSnapshot snapshot = _license.Snapshot();
        _settingsPage.SetLicenseStatus(snapshot.Headline, snapshot.Detail);
        _settingsPage.SetLicenseActions(
            purchaseConfigured: !string.IsNullOrWhiteSpace(_license.Options.PurchaseUrl),
            steamEdition: _license.Options.SteamEditionDetected);
    }

    /// <summary>
    /// Opens a licence file, verifies it and reports the verdict. A rejected file never changes the current
    /// entitlement or the trial counter, and the message says which check failed.
    /// </summary>
    private void ActivateLicense()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose your I-Total Transfer licence",
            Filter = "Licence files (*.lic)|*.lic|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        LicenseActivationResult result = _license!.ActivateFromFile(dialog.FileName);
        RefreshLicenseSurface();
        Log("Licence activation from " + dialog.FileName + ": " + result.Message, result.Accepted ? "OK" : "WARN");
        MessageBox.Show(this,
            (result.Accepted ? "This copy is now licensed.\n\n" : "The licence was not accepted.\n\n") + result.Message,
            result.Accepted ? "Licence accepted" : "Licence not accepted",
            MessageBoxButtons.OK, result.Accepted ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (result.Accepted) UiSound.Success(); else UiSound.Notify();
    }

    private void OpenPurchasePage()
    {
        string url = _license!.Options.PurchaseUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(this,
                "This build has no purchase page configured, so it cannot open one.\n\n" +
                "A licence file received from the store can still be entered with \"Enter licence…\".",
                "No purchase page in this build", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        OpenUrl(url);
        Log("Opened the purchase page.", "INFO");
    }

    private void CheckSteamLicence()
    {
        LicenseSnapshot snapshot = _license!.Refresh();
        RefreshLicenseSurface();
        Log("Steam licence check: " + snapshot.Headline + " — " + snapshot.Detail,
            snapshot.Standing == LicenseStanding.Licensed ? "OK" : "WARN");
        MessageBox.Show(this, snapshot.Headline + "\n\n" + snapshot.Detail,
            "Steam licence", MessageBoxButtons.OK,
            snapshot.Standing == LicenseStanding.Licensed ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    /// <summary>
    /// The one place that refuses a new job. It opens the licence row instead of only complaining, and it is
    /// only reached before an operation begins: an in-flight transfer is never interrupted by a licence
    /// decision.
    /// </summary>
    private void ShowLicenseStop(string message)
    {
        Log("A new transfer was not started: " + message.Replace("\n\n", " "), "WARN");
        SetStatus("Trial finished — activate a licence to start another transfer.", "WARN");
        DialogResult answer = MessageBox.Show(this,
            message + "\n\nOpen the licence options now?", "Licence required",
            MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (answer != DialogResult.Yes) return;
        ShowPage(_settingsPage);
        ActivateLicense();
    }

    private async Task SendToDeviceAsync()
    {
        LicenseDecision licence = _license!.TryStartJob(resuming: false);
        if (!licence.Allowed) { ShowLicenseStop(licence.Message); return; }
        if (!BeginOperation()) return;
        try
        {
            EnsureLogger();
            if (_device is null) { _selection = await Task.Run(() => BackendFactory.Create(_settings, _log!)); _device = _selection.Backend; BuildService(); }
            if (!_device!.Capabilities.CanWriteToDevice)
                throw new NotSupportedException($"The active backend cannot write to {PhoneWithArticle}. Writing to a phone requires an iPhone with the AFC backend and its verified native runtime; enable it in Settings and install the pinned runtime.");

            // Do not ask the operator to choose files for a phone that is locked, unplugged or not
            // exposing its media container. The write backend declares capability from its verified
            // runtime, not from the state of the currently attached handset.
            DeviceInfo preflight = await Task.Run(() => _device.Probe());
            if (!preflight.Status.IsUsable())
                throw new InvalidOperationException($"Sending is blocked because {PhoneWithArticle} is not ready: " + preflight.Message);

            using var dialog = new OpenFileDialog { Multiselect = true, Title = "Select files to send to " + PhoneWithArticle };
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FileNames.Length == 0) return;

            string deviceFolder = "DCIM/I-Total";
            var progress = new ThrottledProgress<TransferProgress>(
                new Progress<TransferProgress>(UpdateProgress), UiProgressInterval);
            TransferSummary summary = await _service!.CopyWindowsToDeviceAsync(dialog.FileNames, deviceFolder, progress, _cts!.Token);
            _transfer.SetSummary(summary);
            Log(summary.Status, summary.Completed ? "OK" : "ERROR");
            RecordFinishedJob(summary, LicenseJobKind.WindowsToDevice);
        }
        catch (OperationCanceledException) { Log("Send to device cancelled.", "WARN"); }
        catch (Exception ex)
        {
            Log("Send to device failed: " + ex.Message, "ERROR");
            MessageBox.Show(this, ex.Message, "Send to device failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { EndOperation(); }
    }

    private void TogglePause()
    {
        if (_service is null) return;
        if (_service.Pause.IsPaused) { _service.Pause.Resume(); Log("Transfer resumed.", "OK"); }
        else { _service.Pause.Pause(); Log("Transfer paused after the current file.", "WARN"); }
        _dashboard.SetState(_busy, _deviceReady, _scan is not null, _service.Pause.IsPaused);
    }

    private void UpdateProgress(TransferProgress p)
    {
        _transfer.UpdateProgress(p);
        _dashboard.SetOperation(Describe(p));
    }

    /// <summary>
    /// Renders the live copy into the Overview's operation band: phase, counts, measured throughput and
    /// the remaining time. The bar is measured in bytes, so a single large video still moves, and the
    /// percentage stays a copy percentage rather than a mixture of copy, hash and verification work.
    /// </summary>
    private static OperationStatus Describe(TransferProgress p)
    {
        var parts = new List<string> { p.Phase };
        if (!string.IsNullOrWhiteSpace(p.Message)) parts.Add(p.Message);
        if (p.Total > 0) parts.Add($"{p.Current:N0} of {p.Total:N0} files");
        if (p.BytesPerSecond > 0) parts.Add($"{MediaSummary.FormatBytes((long)p.BytesPerSecond)}/s");
        if (p.Eta is { } eta && eta > TimeSpan.Zero) parts.Add($"~{eta:hh\\:mm\\:ss} remaining");
        if (p.Elapsed > TimeSpan.Zero) parts.Add($"{p.Elapsed:hh\\:mm\\:ss} elapsed");
        if (p.Failed > 0) parts.Add($"{p.Failed:N0} failed");

        return new OperationStatus(
            p.IsPaused ? OperationPhase.Paused : OperationPhase.Transferring,
            string.Join(" · ", parts),
            p.BytesDone,
            p.TotalBytes,
            Indeterminate: p.TotalBytes <= 0);
    }

    private void RefreshQueue()
    {
        if (_log is null) return;
        try
        {
            string destination = CurrentDestination();
            // Reading the queue is not a resume. It must not enforce (or claim) a source identity,
            // and it must not create a manifest just because the page was painted. The destination's
            // own record is enough for a read-only display.
            SessionManifest? manifest = SessionRecovery.Load(destination, out _);
            _transfer.SetQueue((manifest?.Entries ?? [])
                .OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase).ToList());
        }
        catch (Exception ex) { Log("Could not read the queue manifest: " + ex.Message, "DEBUG"); }
    }

    // ------------------------------------------------------------------ verification
    private async Task VerifyAsync()
    {
        if (_scan is null || _service is null)
        {
            MessageBox.Show(this, "Run a successful scan first.", "No scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!BeginOperation()) return;
        try { await VerifyCoreAsync(_library.RepairRequested); }
        catch (OperationCanceledException) { Log("Verification cancelled.", "WARN"); }
        catch (Exception ex)
        {
            Log("Verification failed: " + ex.Message, "ERROR");
            MessageBox.Show(this, ex.Message, "Verification failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { EndOperation(); }
    }

    private async Task VerifyCoreAsync(bool repair)
    {
        if (_scan is null || _service is null) return;
        var progress = new ThrottledProgress<TransferProgress>(
            new Progress<TransferProgress>(UpdateProgress), UiProgressInterval);
        VerificationResult result = await _service.VerifyAsync(_scan, CurrentDestination(), progress, _cts?.Token ?? CancellationToken.None, repair);
        _library.SetVerification(result);
        ShowPage(_library);
        _dashboard.SetMetrics(_scan.Files.Count, _scan.TotalBytes, result.Matching, result.Missing + result.WrongSize, result.Unverifiable);
        Log($"Verification: {result.Matching:N0}/{result.Total:N0} matched; source+dest hashed={result.SourceHashVerified:N0}; " +
            $"missing={result.Missing:N0}; size mismatches={result.WrongSize:N0}; unverifiable={result.Unverifiable:N0}; repaired={result.Repaired:N0}.",
            result.Completed ? "OK" : "ERROR");
        RefreshQueue();
    }

    // ------------------------------------------------------------------ wireless
    private void StartWireless()
    {
        try
        {
            StopWireless();
            ApplySettingsFromPages(save: true);
            EnsureLogger();
            string destination = CurrentDestination();
            _wireless = new WirelessReceiver(destination, _settings, _log!, _license);
            _wireless.Activity += OnWirelessActivity;
            _wireless.OfferArrived += OnWirelessOffer;
            string url = _wireless.Start();
            _wirelessPage.SetRunning(true, url, _wireless.UsingTls);
            CreatePairingTicket();
            _wirelessPage.AppendLog($"Receiver started at {url} (TLS={_wireless.UsingTls}).", "OK");
            if (!_wireless.UsingTls)
                _wirelessPage.AppendLog(_wireless.TlsNote, "WARN");
            if (_settings.WirelessAutoApprove)
                _wirelessPage.AppendLog("Automatic approval is ON: incoming files are written without asking.", "WARN");
        }
        catch (Exception ex)
        {
            _wirelessPage.SetStatus("Could not start: " + ex.Message, warn: true);
            _wirelessPage.AppendLog("Wireless start failed: " + ex.Message, "ERROR");
        }
    }

    private void StopWireless()
    {
        if (_wireless is null) { _wirelessPage.SetRunning(false, "", false); return; }
        try
        {
            _wireless.Activity -= OnWirelessActivity;
            _wireless.OfferArrived -= OnWirelessOffer;
            _wireless.Dispose();
        }
        catch { }
        _wireless = null;
        _wirelessPage.SetRunning(false, "", false);
    }

    private void CreatePairingTicket()
    {
        if (_wireless is null) { _wirelessPage.SetStatus("Start the receiver before creating a ticket.", warn: true); return; }
        PairingTicket ticket = _wireless.CreatePairingTicket();
        _wirelessPage.SetTicket(ticket);
    }

    private void OnWirelessActivity(WirelessEvent evt)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnWirelessActivity(evt))); return; }
        _wirelessPage.AppendLog($"[{evt.Kind}] {evt.Message}", evt.Success ? "OK" : "WARN");
        _wirelessPage.SetReceived(_wireless?.Received ?? []);
        if (evt.Kind == "paired" && _wireless is not null)
            _wirelessPage.MarkTicketSpent();
        RefreshWirelessPending();
    }

    private void OnWirelessOffer(OfferRecord record)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnWirelessOffer(record))); return; }
        RefreshWirelessPending();
    }

    private void RefreshWirelessPending()
    {
        _wirelessPage.SetPending(_wireless?.Inbox.Pending ?? []);
    }

    /// <summary>
    /// Applies the approval preference to the running receiver immediately. A switch that only takes
    /// effect after a restart would be a control that appears to work and does not.
    /// </summary>
    private void OnWirelessAutoApproveChanged(bool autoApprove)
    {
        _settings.WirelessAutoApprove = autoApprove;
        if (_wireless is not null) _wireless.Inbox.AutoApprove = autoApprove;
        _wirelessPage.AppendLog(autoApprove
            ? "Automatic approval is ON: incoming files are written without asking."
            : "Automatic approval is OFF: incoming files wait for your decision.", autoApprove ? "WARN" : "OK");
    }

    private void ApproveWirelessOffer(string relativePath)
    {
        if (_wireless is null) return;
        bool ok = _wireless.Approve(relativePath);
        _wirelessPage.AppendLog(ok ? $"Approved {relativePath}." : $"No waiting offer named {relativePath}.",
            ok ? "OK" : "WARN");
        RefreshWirelessPending();
    }

    private void RefuseWirelessOffer(string relativePath)
    {
        if (_wireless is null) return;
        bool ok = _wireless.Refuse(relativePath);
        _wirelessPage.AppendLog(ok ? $"Refused {relativePath}." : $"No waiting offer named {relativePath}.",
            ok ? "OK" : "WARN");
        RefreshWirelessPending();
    }

    private void ApproveAllWirelessOffers()
    {
        if (_wireless is null) return;
        int changed = _wireless.ApproveAll();
        _wirelessPage.AppendLog($"Approved {changed} waiting file(s).", changed > 0 ? "OK" : "WARN");
        RefreshWirelessPending();
    }

    private void RefuseAllWirelessOffers()
    {
        if (_wireless is null) return;
        int changed = _wireless.RefuseAll();
        _wirelessPage.AppendLog($"Refused {changed} waiting file(s).", changed > 0 ? "OK" : "WARN");
        RefreshWirelessPending();
    }

    // ------------------------------------------------------------------ history
    private void RefreshHistory()
    {
        if (_log is null) return;
        try
        {
            string destination = CurrentDestination();
            string dbPath = Path.Combine(destination, ".i-total", "session.db");
            _history.SetDatabasePath(dbPath);
            if (!File.Exists(dbPath)) { _history.SetSessions([]); _history.SetProblems([]); return; }
            using var db = new SessionDatabase(dbPath, _log);
            db.Initialize();
            _history.SetSessions(db.QueryRecentSessions(100));
            _history.SetProblems(db.QueryProblemFiles(destination, 500));
        }
        catch (Exception ex) { Log("Could not read transfer history: " + ex.Message, "WARN"); }
    }

    // ------------------------------------------------------------------ settings
    private void BrowseDestination()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the backup destination", SelectedPath = CurrentDestination() };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settingsPage.Destination = dialog.SelectedPath;
        _dashboard.Destination = dialog.SelectedPath;
        // A different folder is a different session: ask it what it holds straight away.
        _recoveryDismissedThisRun = false;
        RefreshRecovery();
    }

    private void SaveSettings()
    {
        ApplySettingsFromPages(save: true);
        ApplyTheme(); // a theme chosen in Settings should take effect immediately, not on the next launch
        ApplyPreferences();
        _settingsPage.SetStatus("Saved to " + _settingsService.SettingsPath + " at " + DateTime.Now.ToString("HH:mm:ss"));
        Log("Settings saved.", "OK");
        UiSound.Success();
        RefreshRecovery();
    }

    /// <summary>
    /// Repaints with the theme currently selected in Settings without persisting it. The choice is only
    /// written to disk when the operator presses Save, so an accidental selection is not committed.
    /// </summary>
    private void PreviewTheme()
    {
        AppTheme chosen = _settingsPage.SelectedTheme;
        if (chosen == _settings.Theme) return;
        _settings.Theme = chosen;
        ApplyTheme();
    }

    private void ResetSettings()
    {
        if (MessageBox.Show(this, "Reset all settings to their defaults?", "Reset settings", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
        _settings = _settingsService.ResetToDefaults();
        _settingsPage.LoadSettings(_settings);
        RefreshCacheInventory();
        _dashboard.Destination = _settings.Destination;
        _dashboard.ConfirmScan = _settings.ConfirmScanWithSecondPass;
        _wirelessPage.LoadSettings(_settings);
        ApplyTheme();
        EnsureLogger();
        UpdatePolicyText();
        _settingsPage.SetStatus("Reset to defaults.");
        Log("Settings reset to defaults.", "WARN");
        _recoveryDismissedThisRun = false;
        RefreshRecovery();
    }

    /// <summary>Reads every page's controls into the settings object and optionally persists it.</summary>
    private void ApplySettingsFromPages(bool save)
    {
        _settingsPage.ApplyTo(_settings);
        _settings.WirelessEnabled = _wirelessPage.WirelessEnabled;
        _settings.WirelessBindAddress = _wirelessPage.BindAddress;
        _settings.WirelessPort = _wirelessPage.Port;
        _settings.WirelessRequireTls = _wirelessPage.RequireTls;
        _settings.PairingTokenMinutes = _wirelessPage.TokenMinutes;
        _settings.WirelessAutoApprove = _wirelessPage.AutoApprove;
        _settings.ConfirmScanWithSecondPass = _dashboard.ConfirmScan || _settingsPage.ConfirmScan;
        _settings.Destination = CurrentDestination();
        _settings.Normalize();
        if (save)
        {
            try { _settingsService.Save(_settings); } catch (Exception ex) { Log("Settings save failed: " + ex.Message, "ERROR"); }
        }
        if (_log is not null) { _log.Redact = _settings.RedactSensitiveData; _log.MinimumLevel = _settings.LogLevel; }
        UiSound.Enabled = _settings.PlayUiSounds;
        TopMost = _settings.AlwaysOnTop;
        UpdatePolicyText();
        RefreshCacheInventory();
    }

    /// <summary>
    /// Says what the listing cache is actually holding, next to the switch that controls it. The cache is a
    /// record of the operator's filenames, so "how much is on disk?" and "remove it" belong together.
    /// </summary>
    private void RefreshCacheInventory()
    {
        try
        {
            IReadOnlyList<CachedListing> listings = _scanCache.List();
            if (listings.Count == 0)
            {
                _settingsPage.SetCacheInventory("No listing is on disk.");
                return;
            }
            long bytes = listings.Sum(listing => listing.Bytes);
            string described = listings.Count == 1
                ? "1 listing"
                : $"{listings.Count} listings";
            string unreadable = listings.Any(listing => !listing.IsReadable) ? " (one is unreadable and will be ignored)" : "";
            _settingsPage.SetCacheInventory($"{described}, {FormatBytes(bytes)}{unreadable}");
        }
        catch (Exception ex)
        {
            _settingsPage.SetCacheInventory("The cache folder could not be read: " + ex.Message);
        }
    }

    private void ForgetCacheFromSettings()
    {
        IReadOnlyList<CachedListing> listings = _scanCache.List();
        if (listings.Count == 0)
        {
            RefreshCacheInventory();
            SetStatus("There is no cached library listing to remove.", "INFO");
            return;
        }
        if (MessageBox.Show(this,
                $"Delete {listings.Count} cached library listing(s)?\n\nThe listings hold file names and sizes from past scans, " +
                "never file contents. Scanning again recreates them if the setting stays on.",
                "Forget cached listings", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        int removed = ForgetCachedListings();
        RefreshCacheInventory();
        SetStatus($"Removed {removed} cached library listing(s).", "INFO");
    }

    private void OpenSettingsFolder()
    {
        try
        {
            string folder = Path.GetDirectoryName(_settingsService.SettingsPath)!;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }

    private void UpdatePolicyText()
    {
        _dashboard.SetPolicyText("Verification policy: " + _settings.VerificationPolicy +
            ". Backend preference: " + _settings.Backend + "." +
            " A file is only reported as verified when the active backend can produce the matching hash.");
    }

    // ------------------------------------------------------------------ diagnostics
    private async Task RunDiagnosticsAsync()
    {
        if (!BeginOperation()) return;
        try
        {
            EnsureLogger();
            if (_device is null) { _selection = await Task.Run(() => BackendFactory.Create(_settings, _log!)); _device = _selection.Backend; BuildService(); }
            DeviceInfo info = await Task.Run(() => _device!.Probe());
            DependencyReport dependencies = await Task.Run(() => DependencyVerifier.Verify(Path.Combine(AppContext.BaseDirectory, "native")));
            // The USB evidence is read independently of the media probe: a handset can be attached and
            // answering while its media is unreadable, and Windows can hold records for a handset that
            // is not attached at all. Those are different findings and the page must show both.
            (AttachmentEvidence evidence, IReadOnlyList<DevicePresence> attached) =
                await Task.Run(ReadAttachmentEvidence);
            AttachmentReport attachment = AttachmentDiagnosis.Decide(evidence);
            IReadOnlyList<DiagnosticItem> items = await Task.Run(() => ConnectionDiagnostics.Collect(info, _device!.Name, dependencies, attachment));
            _diagnostics.SetDiagnostics(items);
            _diagnostics.SetDependencies(dependencies);
            // The Apple diagnosis is deliberately Apple-only, but this summary sits above an Android
            // device's rows too. Printing its headline unconditionally made an Android page announce
            // "Attached and answering: Apple iPhone" while the HUAWEI was the selected device. Name
            // every attached phone instead, and leave the Apple-specific verdict to its own row/bundle
            // field.
            _diagnostics.SetSummary(DescribeAttachedPhones(attached) + "  |  " + (_selection?.Description ?? info.Message));
        }
        catch (Exception ex)
        {
            Log("Diagnostics failed: " + ex.Message, "ERROR");
            MessageBox.Show(this, ex.Message, "Diagnostics failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { EndOperation(); }
    }

    /// <summary>
    /// Gathers the USB evidence on demand: a live "is it answering?" probe for every handset plus what
    /// this PC remembers. Called from the diagnostics path and from the bundle, never from the status poll.
    /// </summary>
    private static (AttachmentEvidence Evidence, IReadOnlyList<DevicePresence> Attached) ReadAttachmentEvidence()
    {
        // Every answering handset, then the Apple ones only: an Android phone on the same bus must not
        // appear under an Apple-specific row. The unfiltered list is returned beside it so the summary
        // can still name both phones without pretending the Apple row speaks for both.
        IReadOnlyList<DevicePresence> attached = DevicePresenceMonitor.CheckAll();
        return (AppleDeviceHistory.Evidence(attached), attached);
    }

    private static string DescribeAttachedPhones(IReadOnlyList<DevicePresence> attached)
    {
        List<string> phones = attached.Where(p => p.Present)
            .Select(p => $"{p.Name} ({(p.Family == DeviceFamily.Android ? "Android" : p.Family == DeviceFamily.Apple ? "Apple" : "unidentified")})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return phones.Count == 0 ? "No phone identified." : "Attached: " + string.Join(", ", phones) + ".";
    }

    private static string Describe(IReadOnlyList<string> names) =>
        names.Count == 0 ? "none" : string.Join(", ", names);

    private async Task ExportDiagnosticsBundleAsync()
    {
        try
        {
            EnsureLogger();
            IReadOnlyList<BackendCandidate> candidates = BackendFactory.Describe(_settings);
            DependencyReport dependencies = await Task.Run(() => DependencyVerifier.Verify(Path.Combine(AppContext.BaseDirectory, "native")));
            // The same USB evidence the Diagnostics page shows, minus anything identifying: display names
            // and counts only, never the instance key Windows stores the serial number in.
            (AttachmentEvidence evidence, IReadOnlyList<DevicePresence> attached) =
                await Task.Run(ReadAttachmentEvidence);
            AttachmentReport attachment = AttachmentDiagnosis.Decide(evidence);
            var extra = new List<string>
            {
                "Selected backend: " + (_selection?.Description ?? "none"),
                "Device ready: " + _deviceReady,
                "Scanned files: " + (_scan?.Files.Count ?? 0),
                "Verification policy: " + _settings.VerificationPolicy,
                "Required dependencies satisfied: " + dependencies.RequiredDependenciesSatisfied,
                "Phones attached: " + DescribeAttachedPhones(attached),
                "Attachment verdict: " + attachment.Status,
                "Apple handsets attached: " + Describe(attachment.AttachedNames),
                "Apple handset records cached by Windows: " + Describe(attachment.CachedHandsetNames),
                "Apple USB support records cached: " + attachment.CachedSupportNames.Count.ToString("N0"),
                "Apple attachment diagnosis: " + attachment.Headline,
                "Apple attachment guidance: " + attachment.Guidance
            };
            string path = DiagnosticsBundle.Create(CurrentDestination(), _settings, _selection, candidates,
                _log?.LogPath, extra, _license?.Snapshot());
            Log("Diagnostics bundle written to " + path, "OK");
            MessageBox.Show(this, "Diagnostics bundle written to:\n" + path, "Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Diagnostics export failed: " + ex.Message, "ERROR");
            MessageBox.Show(this, ex.Message, "Diagnostics export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ------------------------------------------------------------------ reports / shell
    private void OpenDestination()
    {
        try
        {
            string destination = CurrentDestination();
            Directory.CreateDirectory(destination);
            Process.Start(new ProcessStartInfo("explorer.exe", destination) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }

    private void OpenLog()
    {
        try
        {
            string path = _log?.LogPath ?? Path.Combine(CurrentDestination(), "I-total-transfer.log");
            if (File.Exists(path)) Process.Start(new ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
            else MessageBox.Show(this, "No log file exists yet.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }

    private void ExportReport()
    {
        if (_scan is null) { MessageBox.Show(this, "Scan the device first."); return; }
        using var dialog = new SaveFileDialog { Filter = "JSON report (*.json)|*.json", FileName = "I-total-enumeration-report.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { TransferService.SaveScanReport(_scan, Path.GetDirectoryName(dialog.FileName)!, _device?.Name ?? "unknown"); Log("Enumeration report exported to " + dialog.FileName, "OK"); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }

    private void ExportScan()
    {
        if (_scan is null) { MessageBox.Show(this, "Scan the device first."); return; }
        using var dialog = new SaveFileDialog { Filter = "JSON report (*.json)|*.json", FileName = "I-total-scan.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { LogService.ExportJson(dialog.FileName, _scan); Log("Scan exported to " + dialog.FileName, "OK"); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }

    private static void OpenUrl(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { } }

    // ------------------------------------------------------------------ operation gate
    private bool BeginOperation()
    {
        if (_busy) return false;
        _busy = true;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        UseWaitCursor = true;
        _dashboard.SetState(true, _deviceReady, _scan is not null, _service?.Pause.IsPaused == true);
        return true;
    }

    private void EndOperation()
    {
        _busy = false;
        UseWaitCursor = false;
        _dashboard.SetState(false, _deviceReady, _scan is not null, _service?.Pause.IsPaused == true);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_busy && !_closing)
        {
            _cts?.Cancel();
            e.Cancel = true;
            Log("Close requested during an operation. Cancellation requested; wait for the operation to stop, then close again.", "WARN");
            return;
        }
        _closing = true;
        try { Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged; } catch { }
        try { _pollTimer.Stop(); _pollTimer.Dispose(); } catch { }
        StopWireless();
        ReleaseDevice();
        try { _cts?.Dispose(); } catch { }
    }

    private static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024
        ? $"{bytes / (1024d * 1024 * 1024):N2} GB"
        : bytes >= 1024L * 1024 ? $"{bytes / (1024d * 1024):N1} MB" : $"{bytes:N0} B";
}
