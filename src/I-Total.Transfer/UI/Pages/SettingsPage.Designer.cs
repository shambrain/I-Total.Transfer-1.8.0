#nullable enable
using ITotalTransfer.UI.Controls;
namespace ITotalTransfer.UI.Pages;

partial class SettingsPage
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel _root = null!;

    private FlowLayoutPanel _toolbar = null!;
    private RoundedButton _saveButton = null!;
    private RoundedButton _resetButton = null!;
    private RoundedButton _openFolderButton = null!;
    private Label _savedLabel = null!;

    private CardPanel _scrollHost = null!;
    private TableLayoutPanel _table = null!;

    // Destination & layout
    private TextBox _destBox = null!;
    private RoundedButton _browseDestButton = null!;
    private FluentCheckBox _autoOrganizeCheck = null!;
    private FluentCheckBox _preserveStructureCheck = null!;
    private FluentCheckBox _preserveTimestampsCheck = null!;

    // Scope & filters
    private ComboBox _directionCombo = null!;
    private TextBox _includeExtensionsBox = null!;
    private TextBox _excludeFoldersBox = null!;
    private TextBox _excludeNamesBox = null!;
    private FluentCheckBox _excludeSidecarsCheck = null!;
    private NumericUpDown _minSizeBox = null!;
    private NumericUpDown _maxSizeBox = null!;

    // Conflict & incrementality
    private ComboBox _conflictCombo = null!;
    private FluentCheckBox _skipUnchangedCheck = null!;
    private FluentCheckBox _verifyAfterCopyCheck = null!;

    // Performance
    private ComboBox _bufferCombo = null!;
    private NumericUpDown _maxRetriesBox = null!;
    private NumericUpDown _timeoutBox = null!;
    private NumericUpDown _concurrencyBox = null!;
    private FluentCheckBox _keepAwakeCheck = null!;

    // Verification
    private ComboBox _verificationCombo = null!;
    private FluentCheckBox _confirmScanCheck = null!;

    // Backend
    private ComboBox _backendCombo = null!;
    private ComboBox _handsetCombo = null!;
    private FluentCheckBox _allowShellFallbackCheck = null!;
    private FluentCheckBox _allowAfcCheck = null!;
    private Label _backendStatusLabel = null!;

    // Interface
    private ComboBox _themeCombo = null!;
    private ComboBox _logLevelCombo = null!;
    private FluentCheckBox _showCompletionCheck = null!;
    private FluentCheckBox _redactCheck = null!;
    private FluentCheckBox _playSoundsCheck = null!;
    private FluentCheckBox _alwaysOnTopCheck = null!;

    /// <summary>Designer-owned settings page: action bar plus a scrolling settings table.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _root = new TableLayoutPanel();
        _toolbar = new FlowLayoutPanel();
        _saveButton = new RoundedButton();
        _resetButton = new RoundedButton();
        _openFolderButton = new RoundedButton();
        _savedLabel = new Label();
        _scrollHost = new CardPanel();
        _table = new TableLayoutPanel();
        _destBox = new TextBox();
        _browseDestButton = new RoundedButton();
        _autoOrganizeCheck = new FluentCheckBox();
        _preserveStructureCheck = new FluentCheckBox();
        _preserveTimestampsCheck = new FluentCheckBox();
        _directionCombo = new ComboBox();
        _includeExtensionsBox = new TextBox();
        _excludeFoldersBox = new TextBox();
        _excludeNamesBox = new TextBox();
        _excludeSidecarsCheck = new FluentCheckBox();
        _minSizeBox = new NumericUpDown();
        _maxSizeBox = new NumericUpDown();
        _conflictCombo = new ComboBox();
        _skipUnchangedCheck = new FluentCheckBox();
        _verifyAfterCopyCheck = new FluentCheckBox();
        _bufferCombo = new ComboBox();
        _maxRetriesBox = new NumericUpDown();
        _timeoutBox = new NumericUpDown();
        _concurrencyBox = new NumericUpDown();
        _keepAwakeCheck = new FluentCheckBox();
        _verificationCombo = new ComboBox();
        _confirmScanCheck = new FluentCheckBox();
        _backendCombo = new ComboBox();
        _handsetCombo = new ComboBox();
        _allowShellFallbackCheck = new FluentCheckBox();
        _allowAfcCheck = new FluentCheckBox();
        _backendStatusLabel = new Label();
        _themeCombo = new ComboBox();
        _logLevelCombo = new ComboBox();
        _showCompletionCheck = new FluentCheckBox();
        _redactCheck = new FluentCheckBox();
        _playSoundsCheck = new FluentCheckBox();
        _alwaysOnTopCheck = new FluentCheckBox();
        _root.SuspendLayout();
        _toolbar.SuspendLayout();
        _scrollHost.SuspendLayout();
        _table.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_minSizeBox).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_maxSizeBox).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_maxRetriesBox).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_timeoutBox).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_concurrencyBox).BeginInit();
        SuspendLayout();
        //
        // _root
        //
        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Controls.Add(_toolbar, 0, 0);
        _root.Controls.Add(_scrollHost, 0, 1);
        _root.Dock = DockStyle.Fill;
        _root.Name = "_root";
        _root.RowCount = 2;
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.Size = new Size(1100, 640);
        _root.TabIndex = 0;
        //
        // _toolbar
        //
        _toolbar.Controls.Add(_saveButton);
        _toolbar.Controls.Add(_resetButton);
        _toolbar.Controls.Add(_openFolderButton);
        _toolbar.Controls.Add(_savedLabel);
        _toolbar.Dock = DockStyle.Fill;
        _toolbar.Name = "_toolbar";
        _toolbar.Padding = new Padding(0, 10, 0, 0);
        _toolbar.Size = new Size(1100, 52);
        _toolbar.TabIndex = 0;
        //
        // toolbar items
        //
        _saveButton.Margin = new Padding(0, 0, 10, 0);
        _saveButton.Name = "_saveButton";
        _saveButton.Size = new Size(160, 32);
        _saveButton.TabIndex = 0;
        _saveButton.Tag = "primary";
        _saveButton.Text = "Save settings";
        _resetButton.Margin = new Padding(0, 0, 10, 0);
        _resetButton.Name = "_resetButton";
        _resetButton.Size = new Size(140, 32);
        _resetButton.TabIndex = 1;
        _resetButton.Tag = "subtle";
        _resetButton.Text = "Reset to defaults";
        _openFolderButton.Margin = new Padding(0, 0, 10, 0);
        _openFolderButton.Name = "_openFolderButton";
        _openFolderButton.Size = new Size(150, 32);
        _openFolderButton.TabIndex = 2;
        _openFolderButton.Text = "Open settings folder";
        _savedLabel.AutoEllipsis = true;
        _savedLabel.Margin = new Padding(0, 9, 0, 0);
        _savedLabel.Name = "_savedLabel";
        _savedLabel.Size = new Size(520, 15);
        _savedLabel.TabIndex = 3;
        _savedLabel.Tag = "muted";
        _savedLabel.Text = "";
        _savedLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _scrollHost
        //
        _scrollHost.AutoScroll = true;
        _scrollHost.Controls.Add(_table);
        _scrollHost.Dock = DockStyle.Fill;
        _scrollHost.Name = "_scrollHost";
        _scrollHost.Size = new Size(1100, 588);
        _scrollHost.TabIndex = 1;
        _scrollHost.Tag = "card";
        //
        // _table
        //
        _table.AutoSize = true;
        _table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _table.ColumnCount = 2;
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _table.Dock = DockStyle.Top;
        _table.Location = new Point(0, 0);
        _table.Name = "_table";
        _table.Padding = new Padding(18, 12, 18, 18);
        _table.RowCount = 1;
        _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _table.Size = new Size(1100, 60);
        _table.TabIndex = 0;
        //
        // input controls (defaults; LoadSettings overrides from the saved file)
        //
        _destBox.Name = "_destBox";
        _destBox.Size = new Size(600, 27);
        _destBox.TabIndex = 0;
        _browseDestButton.Name = "_browseDestButton";
        _browseDestButton.Size = new Size(110, 27);
        _browseDestButton.TabIndex = 1;
        _browseDestButton.Text = "Browse…";
        _autoOrganizeCheck.Name = "_autoOrganizeCheck";
        _autoOrganizeCheck.AutoSize = true;
        _autoOrganizeCheck.Text = "Organise destination by capture date (YYYY\\MM)";
        _preserveStructureCheck.Name = "_preserveStructureCheck";
        _preserveStructureCheck.AutoSize = true;
        _preserveStructureCheck.Text = "Preserve device folder structure (DCIM\\…)";
        _preserveTimestampsCheck.Name = "_preserveTimestampsCheck";
        _preserveTimestampsCheck.AutoSize = true;
        _preserveTimestampsCheck.Text = "Preserve source modification timestamps";
        _directionCombo.Name = "_directionCombo";
        _directionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _directionCombo.Size = new Size(240, 27);
        _includeExtensionsBox.Name = "_includeExtensionsBox";
        _includeExtensionsBox.Size = new Size(600, 27);
        _includeExtensionsBox.PlaceholderText = ".heic;.jpg;.mov (empty = all files)";
        _excludeFoldersBox.Name = "_excludeFoldersBox";
        _excludeFoldersBox.Size = new Size(600, 27);
        _excludeFoldersBox.PlaceholderText = "Thumbnails; PhotoStream";
        _excludeNamesBox.Name = "_excludeNamesBox";
        _excludeNamesBox.Size = new Size(600, 27);
        _excludeNamesBox.PlaceholderText = ".aae; IMG_0001";
        _excludeSidecarsCheck.Name = "_excludeSidecarsCheck";
        _excludeSidecarsCheck.AutoSize = true;
        _excludeSidecarsCheck.Text = "Exclude Live Photo sidecars and helper files (.aae, .AAE)";
        _minSizeBox.Name = "_minSizeBox";
        _minSizeBox.Maximum = new decimal(new int[] { 1000000000, 0, 0, 0 });
        _minSizeBox.Size = new Size(140, 27);
        _maxSizeBox.Name = "_maxSizeBox";
        _maxSizeBox.Maximum = new decimal(new int[] { 1000000000, 0, 0, 0 });
        _maxSizeBox.Size = new Size(140, 27);
        _conflictCombo.Name = "_conflictCombo";
        _conflictCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _conflictCombo.Size = new Size(420, 27);
        _skipUnchangedCheck.Name = "_skipUnchangedCheck";
        _skipUnchangedCheck.AutoSize = true;
        _skipUnchangedCheck.Text = "Skip files already present with a matching verified hash";
        _verifyAfterCopyCheck.Name = "_verifyAfterCopyCheck";
        _verifyAfterCopyCheck.AutoSize = true;
        _verifyAfterCopyCheck.Text = "Run verification automatically after a completed transfer";
        _bufferCombo.Name = "_bufferCombo";
        _bufferCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _bufferCombo.Size = new Size(160, 27);
        _maxRetriesBox.Name = "_maxRetriesBox";
        _maxRetriesBox.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _maxRetriesBox.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
        _maxRetriesBox.Size = new Size(120, 27);
        _timeoutBox.Name = "_timeoutBox";
        _timeoutBox.Minimum = new decimal(new int[] { 30, 0, 0, 0 });
        _timeoutBox.Maximum = new decimal(new int[] { 7200, 0, 0, 0 });
        _timeoutBox.Size = new Size(140, 27);
        _concurrencyBox.Name = "_concurrencyBox";
        _concurrencyBox.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _concurrencyBox.Maximum = new decimal(new int[] { 4, 0, 0, 0 });
        _concurrencyBox.Size = new Size(120, 27);
        _keepAwakeCheck.Name = "_keepAwakeCheck";
        _keepAwakeCheck.AutoSize = true;
        _keepAwakeCheck.Text = "Prevent the PC from sleeping or hibernating during a transfer";
        _verificationCombo.Name = "_verificationCombo";
        _verificationCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _verificationCombo.Size = new Size(460, 27);
        _confirmScanCheck.Name = "_confirmScanCheck";
        _confirmScanCheck.AutoSize = true;
        _confirmScanCheck.Text = "Confirm each scan with a second full enumeration (slow but detects a changing listing)";
        _backendCombo.Name = "_backendCombo";
        _backendCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _backendCombo.Size = new Size(400, 27);
        _handsetCombo.Name = "_handsetCombo";
        _handsetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _handsetCombo.Size = new Size(400, 27);
        _allowShellFallbackCheck.Name = "_allowShellFallbackCheck";
        _allowShellFallbackCheck.AutoSize = true;
        _allowShellFallbackCheck.Text = "Allow the Windows Shell compatibility fallback";
        _allowAfcCheck.Name = "_allowAfcCheck";
        _allowAfcCheck.AutoSize = true;
        _allowAfcCheck.Text = "Allow the AFC / libimobiledevice backend when its native runtime verifies";
        _backendStatusLabel.Name = "_backendStatusLabel";
        _backendStatusLabel.AutoSize = true;
        _backendStatusLabel.Tag = "muted";
        _backendStatusLabel.Text = "Backend availability is probed when the app starts and after device changes.";
        _themeCombo.Name = "_themeCombo";
        _themeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _themeCombo.Size = new Size(240, 27);
        _logLevelCombo.Name = "_logLevelCombo";
        _logLevelCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _logLevelCombo.Size = new Size(240, 27);
        _showCompletionCheck.Name = "_showCompletionCheck";
        _showCompletionCheck.AutoSize = true;
        _showCompletionCheck.Text = "Show a completion dialog after a transfer";
        _redactCheck.Name = "_redactCheck";
        _redactCheck.AutoSize = true;
        _redactCheck.Text = "Redact device identifiers and pairing tokens from logs";
        _playSoundsCheck.Name = "_playSoundsCheck";
        _playSoundsCheck.AutoSize = true;
        _playSoundsCheck.Text = "Play interface sounds (button ticks and notification chimes)";
        _alwaysOnTopCheck.Name = "_alwaysOnTopCheck";
        _alwaysOnTopCheck.AutoSize = true;
        _alwaysOnTopCheck.Text = "Keep the window above other windows";
        //
        // SettingsPage
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(_root);
        Name = "SettingsPage";
        Padding = new Padding(0, 4, 0, 0);
        Size = new Size(1100, 640);
        _root.ResumeLayout(false);
        _toolbar.ResumeLayout(false);
        _toolbar.PerformLayout();
        _scrollHost.ResumeLayout(false);
        _scrollHost.PerformLayout();
        _table.ResumeLayout(false);
        _table.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_minSizeBox).EndInit();
        ((System.ComponentModel.ISupportInitialize)_maxSizeBox).EndInit();
        ((System.ComponentModel.ISupportInitialize)_maxRetriesBox).EndInit();
        ((System.ComponentModel.ISupportInitialize)_timeoutBox).EndInit();
        ((System.ComponentModel.ISupportInitialize)_concurrencyBox).EndInit();
        ResumeLayout(false);
    }
}
