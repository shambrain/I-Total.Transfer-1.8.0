using System.ComponentModel;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI.Pages;

/// <summary>
/// Settings page. The input controls are declared in the Designer; this file arranges them into the
/// scrolling settings table and maps them to/from <see cref="AppSettings"/>.
/// </summary>
public partial class SettingsPage : UserControl, IThemedPage
{
    public event EventHandler? SaveRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler? BrowseDestinationRequested;
    public event EventHandler? OpenSettingsFolderRequested;

    /// <summary>Raised when the operator asks for the cached device listings to be deleted.</summary>
    public event EventHandler? ForgetCacheRequested;

    /// <summary>Raised as soon as the theme combo changes, so the shell can repaint live.</summary>
    public event EventHandler? ThemePreviewRequested;

    /// <summary>Raised when the operator chooses a licence file to activate.</summary>
    public event EventHandler? ActivateLicenseRequested;

    /// <summary>Raised when the operator asks to open the purchase page.</summary>
    public event EventHandler? PurchaseLicenseRequested;

    /// <summary>Raised when the operator asks for the Steam edition's signed entitlement to be re-read.</summary>
    public event EventHandler? CheckSteamRequested;

    private Theme _theme = Theme.NeonInstance;
    private int _row;
    private readonly ToolTip _statusTip = new();

    // ---- Appearance preview -------------------------------------------------------------------
    // Three clickable swatches under the Theme combo. Each paints the real palette, so the section
    // shows what the choice means instead of only naming it. Clicking one drives the same combo the
    // shell already listens to, so live preview and persistence keep working through one path.
    private readonly FlowLayoutPanel _themePreview = new()
    {
        Name = "_themePreview",
        AutoSize = true,
        WrapContents = false,
        Margin = new Padding(0, 0, 0, 2)
    };

    private readonly Label _themeDescription = new()
    {
        Name = "_themeDescription",
        AutoSize = true,
        MaximumSize = new Size(760, 0),
        Tag = "muted",
        Text = ""
    };

    private readonly Dictionary<AppTheme, ThemeSwatch> _themeSwatches = [];

    // Built in code rather than the Designer because they belong to one row that is arranged by BuildRows,
    // and because a themed RoundedButton/FluentCheckBox is fully styled by theme.ApplyAll either way.
    private readonly FluentCheckBox _reuseLibraryCheck = new()
    {
        Name = "_reuseLibraryCheck",
        AutoSize = true,
        Text = "Show the last known library while a scan runs (a cached listing, labelled with when it was captured)"
    };

    private readonly Label _cacheStatusLabel = new()
    {
        Name = "_cacheStatusLabel",
        AutoSize = true,
        Text = "No listing is on disk."
    };

    private readonly RoundedButton _forgetCacheButton = new()
    {
        Name = "_forgetCacheButton",
        Size = new Size(190, 27),
        Text = "Forget cached listings"
    };

    // ---- Licence row -------------------------------------------------------------------------
    // Built in code for the same reason as the cache row: they belong to one section arranged by
    // BuildRows, and the shell owns the events that actually verify a file or open the store page.
    private readonly Label _licenseStatusLabel = new()
    {
        Name = "_licenseStatusLabel",
        AutoSize = true,
        Text = "Trial — checking…"
    };

    private readonly Label _licenseDetailLabel = new()
    {
        Name = "_licenseDetailLabel",
        AutoSize = true,
        MaximumSize = new Size(760, 0),
        Tag = "muted",
        Text = ""
    };

    private readonly RoundedButton _activateLicenseButton = new()
    {
        Name = "_activateLicenseButton",
        Size = new Size(170, 27),
        Text = "Enter licence…"
    };

    private readonly RoundedButton _buyLicenseButton = new()
    {
        Name = "_buyLicenseButton",
        Size = new Size(150, 27),
        Text = "Buy a licence"
    };

    private readonly RoundedButton _steamLicenseButton = new()
    {
        Name = "_steamLicenseButton",
        Size = new Size(150, 27),
        Text = "Check Steam copy",
        Visible = false
    };

    private readonly FlowLayoutPanel _licenseButtons = new()
    {
        Name = "_licenseButtons",
        AutoSize = true,
        WrapContents = true,
        Margin = new Padding(0, 4, 0, 4)
    };

    public SettingsPage()
    {
        InitializeComponent();
        _theme = Theme.NeonInstance;
        PopulateCombos();
        CreateAppearanceSwatches();
        BuildRows();
        _saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        _resetButton.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        _browseDestButton.Click += (_, _) => BrowseDestinationRequested?.Invoke(this, EventArgs.Empty);
        _openFolderButton.Click += (_, _) => OpenSettingsFolderRequested?.Invoke(this, EventArgs.Empty);
        _themeCombo.SelectedIndexChanged += (_, _) =>
        {
            SyncThemePreview();
            ThemePreviewRequested?.Invoke(this, EventArgs.Empty);
        };
        _forgetCacheButton.Click += (_, _) => ForgetCacheRequested?.Invoke(this, EventArgs.Empty);
        _activateLicenseButton.Click += (_, _) => ActivateLicenseRequested?.Invoke(this, EventArgs.Empty);
        _buyLicenseButton.Click += (_, _) => PurchaseLicenseRequested?.Invoke(this, EventArgs.Empty);
        _steamLicenseButton.Click += (_, _) => CheckSteamRequested?.Invoke(this, EventArgs.Empty);
        _licenseButtons.Controls.AddRange([_activateLicenseButton, _buyLicenseButton, _steamLicenseButton]);
        ApplyTheme(_theme);
    }

    /// <summary>The theme currently chosen in the combo, before it is saved.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AppTheme SelectedTheme => _themeCombo.SelectedIndex switch
    {
        1 => AppTheme.Slate,
        2 => AppTheme.Light,
        3 => AppTheme.System,
        _ => AppTheme.Neon
    };

    /// <summary>Records the active palette and applies it to this page's control tree.</summary>
    public void ApplyTheme(Theme theme)
    {
        _theme = theme;
        theme.ApplyAll(this);
    }

    public void SetStatus(string text)
    {
        _savedLabel.Text = text;
        // The caption elides long paths, so the full text stays reachable on hover.
        _statusTip.SetToolTip(_savedLabel, text);
    }

    private void PopulateCombos()
    {
        _directionCombo.Items.AddRange(["Phone → Windows (backup)", "Windows → iPhone (send, AFC only)"]);
        _conflictCombo.Items.AddRange(
        [
            "Preserve existing and report (never overwrite)",
            "Skip incoming file",
            "Rename incoming file",
            "Overwrite existing file"
        ]);
        _verificationCombo.Items.AddRange(
        [
            "Size only",
            "Destination hash",
            "Source and destination hash (strongest)"
        ]);
        _backendCombo.Items.AddRange(["Automatic", "Windows Portable Devices", "AFC / libimobiledevice", "Windows Shell"]);
        _handsetCombo.Items.AddRange(["the iPhone", "the Android phone"]);
        _themeCombo.Items.AddRange(["Neon (default)", "Slate grey", "Light", "Follow Windows"]);
        _logLevelCombo.Items.AddRange(["DEBUG", "INFO", "WARN", "ERROR"]);
        _bufferCombo.Items.AddRange(["64 KB", "128 KB", "256 KB", "512 KB", "1024 KB"]);
    }

    /// <summary>Adds every declared input control to the scrolling table in labelled rows.</summary>
    private void BuildRows()
    {
        _table.SuspendLayout();
        _table.Controls.Clear();
        _table.RowStyles.Clear();
        _row = 0;

        AddSection("Destination & layout");
        AddRow("Destination folder", _destBox, _browseDestButton);
        AddCheck(_autoOrganizeCheck);
        AddCheck(_preserveStructureCheck);
        AddCheck(_preserveTimestampsCheck);

        AddSection("Direction, scope & filters");
        AddRow("Transfer direction", _directionCombo);
        AddRow("Include only extensions", _includeExtensionsBox);
        AddRow("Exclude folders", _excludeFoldersBox);
        AddRow("Exclude names containing", _excludeNamesBox);
        AddCheck(_excludeSidecarsCheck);
        AddRow("Minimum file size (bytes)", _minSizeBox);
        AddRow("Maximum file size (0 = no limit)", _maxSizeBox);

        AddSection("Conflict & incrementality");
        AddRow("When a destination file exists", _conflictCombo);
        AddCheck(_skipUnchangedCheck);
        AddCheck(_verifyAfterCopyCheck);

        AddSection("Performance");
        AddRow("Copy buffer size", _bufferCombo);
        AddRow("Retries per file", _maxRetriesBox);
        AddRow("Per-file timeout (seconds)", _timeoutBox);
        AddRow("File concurrency (fixed at 1 - a phone session serialises copies)", _concurrencyBox);
        AddCheck(_keepAwakeCheck);

        AddSection("Verification");
        AddRow("Claims to make after copy", _verificationCombo);
        AddCheck(_confirmScanCheck);

        AddSection("Scan listing cache");
        AddCheck(_reuseLibraryCheck);
        AddRow("Cached listings on disk", _cacheStatusLabel, _forgetCacheButton);

        AddSection("Backend");
        AddRow("Preferred backend", _backendCombo);
        AddRow("When both phones are attached, read", _handsetCombo);
        AddCheck(_allowShellFallbackCheck);
        AddCheck(_allowAfcCheck);
        AddRow("", _backendStatusLabel);

        AddSection("Interface & logging");
        AddRow("Theme", _themeCombo);
        AddFullWidth(_themePreview);
        AddFullWidth(_themeDescription);
        AddRow("Minimum log level", _logLevelCombo);
        AddCheck(_showCompletionCheck);
        AddCheck(_playSoundsCheck);
        AddCheck(_alwaysOnTopCheck);
        AddCheck(_redactCheck);

        AddSection("Licence");
        AddRow("Status", _licenseStatusLabel);
        AddRow("", _licenseButtons);
        AddRow("", _licenseDetailLabel);

        _table.ResumeLayout(true);
    }

    /// <summary>
    /// Adds the clickable appearance preview under the Theme combo. The swatches are built from the
    /// real <see cref="Theme"/> tokens; selecting one selects the matching combo entry, so the shell's
    /// existing live-preview path runs and nothing is themed by a second code path.
    /// </summary>
    private void CreateAppearanceSwatches()
    {
        foreach (AppTheme option in new[] { AppTheme.Neon, AppTheme.Slate, AppTheme.Light })
        {
            var swatch = new ThemeSwatch
            {
                Name = "_themeSwatch" + option,
                SwatchTheme = option,
                Caption = option switch
                {
                    AppTheme.Slate => "Slate",
                    AppTheme.Light => "Light",
                    _ => "Neon (default)"
                },
                AccessibleName = "Preview the " + option + " theme",
                AccessibleDescription = ThemeDescription(option),
                Margin = new Padding(0, 0, 10, 0)
            };
            AppTheme captured = option;
            swatch.Click += (_, _) => SelectThemeFromPreview(captured);
            _themeSwatches[option] = swatch;
            _themePreview.Controls.Add(swatch);
        }

        SyncThemePreview();
    }

    private void SelectThemeFromPreview(AppTheme option)
    {
        _themeCombo.SelectedIndex = option switch
        {
            AppTheme.Slate => 1,
            AppTheme.Light => 2,
            AppTheme.System => 3,
            _ => 0
        };
    }

    private void SyncThemePreview()
    {
        AppTheme chosen = SelectedTheme;
        foreach ((AppTheme option, ThemeSwatch swatch) in _themeSwatches)
        {
            // "Follow Windows" shows the palette it currently resolves to, so the swatch is never blank.
            swatch.Selected = option == chosen
                || (chosen == AppTheme.System && Theme.For(AppTheme.System).Key == option);
        }
        _themeDescription.Text = ThemeDescription(chosen);
    }

    private static string ThemeDescription(AppTheme theme) => theme switch
    {
        AppTheme.Slate =>
            "Slate — neutral charcoal and graphite surfaces with a restrained steel-blue accent. " +
            "Distinct from Neon, for operators who prefer a quieter, greyer desk.",
        AppTheme.Light =>
            "Light — bright white cards on a soft grey page, for a well-lit room. " +
            "Designed and tested on its own, not an inverted Neon.",
        AppTheme.System =>
            "Follow Windows — uses Neon when Windows apps are dark and Light when they are light. " +
            "Changes with the Windows personalisation setting.",
        _ =>
            "Neon — the product identity: a deep navy page with the banner's electric-blue accent. " +
            "The default for a fresh profile; a saved preference is never overwritten."
    };

    /// <summary>Adds a control that spans both settings columns on its own row.</summary>
    private void AddFullWidth(Control control)
    {
        control.Margin = new Padding(0, 2, 0, 6);
        _table.Controls.Add(control, 0, _row);
        _table.SetColumnSpan(control, 2);
        _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _row++;
    }

    private void AddSection(string title)
    {
        var header = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10.5F),
            Margin = new Padding(0, 16, 0, 2)
        };
        _table.Controls.Add(header, 0, _row);
        _table.SetColumnSpan(header, 2);
        _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _row++;
    }

    private void AddRow(string label, Control control) => AddRow(label, control, null);

    private void AddRow(string label, Control control, Control? trailing)
    {
        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 0),
            Tag = "muted"
        };
        _table.Controls.Add(caption, 0, _row);
        if (trailing is null)
        {
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(0, 4, 0, 4);
            _table.Controls.Add(control, 1, _row);
        }
        else
        {
            // The button is scaled by hand because controls managed by the settings table are laid out by
            // that table and miss the UserControl's DPI auto-scale, which would otherwise leave the
            // caption clipped. The ratio is used directly rather than as an integer factor so that
            // fractional scalings (125%, 150%) stay correct.
            double dpiScale = DeviceDpi / 96.0;
            int height = Math.Max(24, control.Height);
            trailing.Width = (int)Math.Round(trailing.Width * dpiScale);
            trailing.Height = height;
            var host = new Panel
            {
                Height = height,
                Margin = new Padding(0, 4, 0, 4),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            // Dock order is z-order driven: the button is added last so it claims the right edge
            // first, and the field then fills whatever is left.
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0);
            trailing.Dock = DockStyle.Right;
            trailing.Margin = new Padding((int)Math.Round(8 * dpiScale), 0, 0, 0);
            host.Controls.Add(control);
            host.Controls.Add(trailing);
            _table.Controls.Add(host, 1, _row);
        }
        _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _row++;
    }

    private void AddCheck(CheckBox box)
    {
        box.Margin = new Padding(0, 6, 0, 4);
        _table.Controls.Add(box, 1, _row);
        _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _row++;
    }

    public void SetBackendStatus(string text) => _backendStatusLabel.Text = text;

    public void LoadSettings(AppSettings settings)
    {
        _destBox.Text = settings.Destination;
        _autoOrganizeCheck.Checked = settings.AutoOrganizeByDate;
        _preserveStructureCheck.Checked = settings.PreserveFolderStructure;
        _preserveTimestampsCheck.Checked = settings.PreserveTimestamps;

        _directionCombo.SelectedIndex = settings.Direction == TransferDirection.WindowsToDevice ? 1 : 0;
        _includeExtensionsBox.Text = settings.IncludeExtensions;
        _excludeFoldersBox.Text = settings.ExcludeFolders;
        _excludeNamesBox.Text = settings.ExcludeNameContains;
        _excludeSidecarsCheck.Checked = settings.ExcludeSidecars;
        _minSizeBox.Value = Clamp(settings.MinFileSizeBytes, _minSizeBox);
        _maxSizeBox.Value = Clamp(settings.MaxFileSizeBytes, _maxSizeBox);

        _conflictCombo.SelectedIndex = settings.ConflictPolicy switch
        {
            ConflictPolicy.Skip => 1,
            ConflictPolicy.RenameIncoming => 2,
            ConflictPolicy.Overwrite => 3,
            _ => 0
        };
        _skipUnchangedCheck.Checked = settings.SkipUnchanged;
        _verifyAfterCopyCheck.Checked = settings.VerifyAfterCopy;

        _bufferCombo.SelectedIndex = settings.BufferSizeKb switch
        {
            <= 64 => 0,
            <= 128 => 1,
            <= 256 => 2,
            <= 512 => 3,
            _ => 4
        };
        _maxRetriesBox.Value = Clamp(settings.MaxRetries, _maxRetriesBox);
        _timeoutBox.Value = Clamp(settings.CopyTimeoutSeconds, _timeoutBox);
        // An iPhone session serialises copies: one device conversation at a time. The engine therefore
        // always copies one file at a time, and this value cannot be honoured. Rather than present a
        // control that silently does nothing, it is pinned and disabled with the reason shown beside it.
        _concurrencyBox.Value = 1;
        _concurrencyBox.Enabled = false;
        _keepAwakeCheck.Checked = settings.KeepAwake;

        _verificationCombo.SelectedIndex = settings.VerificationPolicy switch
        {
            VerificationPolicy.SizeOnly => 0,
            VerificationPolicy.DestinationHash => 1,
            _ => 2
        };
        _confirmScanCheck.Checked = settings.ConfirmScanWithSecondPass;
        _reuseLibraryCheck.Checked = settings.ReuseLastKnownLibrary;

        _backendCombo.SelectedIndex = settings.Backend switch
        {
            BackendPreference.Wpd => 1,
            BackendPreference.Afc => 2,
            BackendPreference.Shell => 3,
            _ => 0
        };
        _allowShellFallbackCheck.Checked = settings.AllowShellFallback;
        _allowAfcCheck.Checked = settings.AllowAfcBackend;
        _handsetCombo.SelectedIndex = settings.PreferredHandset == HandsetPreference.AndroidFirst ? 1 : 0;

        _themeCombo.SelectedIndex = settings.Theme switch
        {
            AppTheme.Slate => 1,
            AppTheme.Light => 2,
            AppTheme.System => 3,
            _ => 0
        };
        _logLevelCombo.SelectedIndex = settings.LogLevel switch
        {
            "DEBUG" => 0, "WARN" => 2, "ERROR" => 3, _ => 1
        };
        _showCompletionCheck.Checked = settings.ShowCompletionDialog;
        _redactCheck.Checked = settings.RedactSensitiveData;
        _playSoundsCheck.Checked = settings.PlayUiSounds;
        _alwaysOnTopCheck.Checked = settings.AlwaysOnTop;
    }

    /// <summary>Copies the current control values onto <paramref name="target"/> and returns it.</summary>
    public AppSettings ApplyTo(AppSettings target)
    {
        target.Destination = _destBox.Text.Trim();
        target.AutoOrganizeByDate = _autoOrganizeCheck.Checked;
        target.PreserveFolderStructure = _preserveStructureCheck.Checked;
        target.PreserveTimestamps = _preserveTimestampsCheck.Checked;

        target.Direction = _directionCombo.SelectedIndex == 1 ? TransferDirection.WindowsToDevice : TransferDirection.DeviceToWindows;
        target.IncludeExtensions = _includeExtensionsBox.Text.Trim();
        target.ExcludeFolders = _excludeFoldersBox.Text.Trim();
        target.ExcludeNameContains = _excludeNamesBox.Text.Trim();
        target.ExcludeSidecars = _excludeSidecarsCheck.Checked;
        target.MinFileSizeBytes = (long)_minSizeBox.Value;
        target.MaxFileSizeBytes = (long)_maxSizeBox.Value;

        target.ConflictPolicy = _conflictCombo.SelectedIndex switch
        {
            1 => ConflictPolicy.Skip,
            2 => ConflictPolicy.RenameIncoming,
            3 => ConflictPolicy.Overwrite,
            _ => ConflictPolicy.PreserveAndReport
        };
        target.SkipUnchanged = _skipUnchangedCheck.Checked;
        target.VerifyAfterCopy = _verifyAfterCopyCheck.Checked;

        target.BufferSizeKb = _bufferCombo.SelectedIndex switch
        {
            0 => 64, 1 => 128, 2 => 256, 3 => 512, _ => 1024
        };
        target.MaxRetries = (int)_maxRetriesBox.Value;
        target.CopyTimeoutSeconds = (int)_timeoutBox.Value;
        // Pinned: see LoadSettings. No backend honours a value above 1, so an edited value would be a lie.
        target.FileConcurrency = 1;
        target.KeepAwake = _keepAwakeCheck.Checked;

        target.VerificationPolicy = _verificationCombo.SelectedIndex switch
        {
            0 => VerificationPolicy.SizeOnly,
            1 => VerificationPolicy.DestinationHash,
            _ => VerificationPolicy.SourceAndDestinationHash
        };
        target.ConfirmScanWithSecondPass = _confirmScanCheck.Checked;
        target.ReuseLastKnownLibrary = _reuseLibraryCheck.Checked;

        target.Backend = _backendCombo.SelectedIndex switch
        {
            1 => BackendPreference.Wpd,
            2 => BackendPreference.Afc,
            3 => BackendPreference.Shell,
            _ => BackendPreference.Auto
        };
        target.AllowShellFallback = _allowShellFallbackCheck.Checked;
        target.AllowAfcBackend = _allowAfcCheck.Checked;
        target.PreferredHandset = _handsetCombo.SelectedIndex == 1
            ? HandsetPreference.AndroidFirst
            : HandsetPreference.AppleFirst;

        target.Theme = _themeCombo.SelectedIndex switch
        {
            1 => AppTheme.Slate,
            2 => AppTheme.Light,
            3 => AppTheme.System,
            _ => AppTheme.Neon
        };
        target.LogLevel = _logLevelCombo.SelectedIndex switch
        {
            0 => "DEBUG", 2 => "WARN", 3 => "ERROR", _ => "INFO"
        };
        target.ShowCompletionDialog = _showCompletionCheck.Checked;
        target.RedactSensitiveData = _redactCheck.Checked;
        target.PlayUiSounds = _playSoundsCheck.Checked;
        target.AlwaysOnTop = _alwaysOnTopCheck.Checked;
        return target;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Destination
    {
        get => _destBox.Text.Trim();
        set => _destBox.Text = value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ConfirmScan
    {
        get => _confirmScanCheck.Checked;
        set => _confirmScanCheck.Checked = value;
    }

    /// <summary>Whether the last known library may be shown while a scan runs.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ReuseLastKnownLibrary
    {
        get => _reuseLibraryCheck.Checked;
        set => _reuseLibraryCheck.Checked = value;
    }

    /// <summary>
    /// Describes what is actually on disk, so the switch above is never the only thing the operator can see
    /// about a feature that keeps a list of their filenames.
    /// </summary>
    public void SetCacheInventory(string text) => _cacheStatusLabel.Text = text;

    /// <summary>
    /// Shows what the operator is entitled to, in the same words the shell uses when it refuses a job.
    /// The detail is written by <see cref="Licensing.LicenseService"/> and explains what counts as a job.
    /// </summary>
    public void SetLicenseStatus(string headline, string detail)
    {
        _licenseStatusLabel.Text = headline;
        _licenseDetailLabel.Text = detail;
        _statusTip.SetToolTip(_licenseStatusLabel, headline + "\n\n" + detail);
    }

    /// <summary>
    /// Enables the purchase action only when this build has a store URL, and shows the Steam check only on
    /// the Steam edition. A disabled button with a reason is honest; a button that opens nothing is not.
    /// </summary>
    public void SetLicenseActions(bool purchaseConfigured, bool steamEdition)
    {
        _buyLicenseButton.Enabled = purchaseConfigured;
        _statusTip.SetToolTip(_buyLicenseButton, purchaseConfigured
            ? "Opens the purchase page in your browser."
            : "No purchase page is configured in this build. Use Enter licence… with the file you received.");
        _steamLicenseButton.Visible = steamEdition;
    }

    /// <summary>The headline currently shown, exposed so the shell and its tests can read it back.</summary>
    public string LicenseHeadline => _licenseStatusLabel.Text;

    /// <summary>The explanatory line currently shown under the licence actions.</summary>
    public string LicenseDetail => _licenseDetailLabel.Text;

    /// <summary>True when the purchase action can be used (this build has a store URL).</summary>
    public bool PurchaseActionEnabled => _buyLicenseButton.Enabled;

    /// <summary>True when the Steam re-check action is offered (Steam edition only).</summary>
    public bool SteamActionVisible => _steamLicenseButton.Visible;

    private static decimal Clamp(long value, NumericUpDown control) =>
        Math.Clamp(value, control.Minimum, control.Maximum);
}
