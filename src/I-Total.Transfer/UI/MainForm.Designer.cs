#nullable enable
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel _root = null!;

    // ---- brand band -------------------------------------------------------------------
    private BrandBand _header = null!;
    private PictureBox _logoImage = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private StatusBlip _deviceBlip = null!;
    private Label _deviceStateHeader = null!;
    private Label _deviceNameHeader = null!;
    private RoundedButton _minimizeButton = null!;
    private RoundedButton _maximizeButton = null!;
    private RoundedButton _closeButton = null!;
    private CardPanel _headerRule = null!;

    // ---- navigation rail --------------------------------------------------------------
    private TableLayoutPanel _navHost = null!;
    private FlowLayoutPanel _nav = null!;
    private Label _navGroupTransfer = null!;
    private Label _navGroupActivity = null!;
    private Label _navGroupSystem = null!;
    private RoundedButton _navDashboard = null!;
    private RoundedButton _navTransfer = null!;
    private RoundedButton _navLibrary = null!;
    private RoundedButton _navWireless = null!;
    private RoundedButton _navHistory = null!;
    private RoundedButton _navSettings = null!;
    private RoundedButton _navDiagnostics = null!;
    private Label _versionLabel = null!;

    // ---- page frame -------------------------------------------------------------------
    private TableLayoutPanel _contentHost = null!;
    private BrandBand _pageHeader = null!;
    private Label _pageGlyph = null!;
    private Label _pageTitle = null!;
    private Label _pageSubtitle = null!;
    private CardPanel _pageHeaderRule = null!;
    private Panel _contentBody = null!;

    // ---- status bar -------------------------------------------------------------------
    private TableLayoutPanel _statusStrip = null!;
    private Label _statusDot = null!;
    private Label _statusLabel = null!;
    private Label _backendStatusLabel = null!;

    /// <summary>Designer-owned Fluent shell: brand band, navigation rail, page frame and status bar.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _root = new TableLayoutPanel();
        _header = new BrandBand();
        _logoImage = new PictureBox();
        _titleLabel = new Label();
        _subtitleLabel = new Label();
        _deviceBlip = new StatusBlip();
        _deviceStateHeader = new Label();
        _deviceNameHeader = new Label();
        _minimizeButton = new RoundedButton();
        _maximizeButton = new RoundedButton();
        _closeButton = new RoundedButton();
        _headerRule = new CardPanel();
        _navHost = new TableLayoutPanel();
        _nav = new FlowLayoutPanel();
        _navGroupTransfer = new Label();
        _navDashboard = new RoundedButton();
        _navTransfer = new RoundedButton();
        _navLibrary = new RoundedButton();
        _navWireless = new RoundedButton();
        _navGroupActivity = new Label();
        _navHistory = new RoundedButton();
        _navGroupSystem = new Label();
        _navSettings = new RoundedButton();
        _navDiagnostics = new RoundedButton();
        _versionLabel = new Label();
        _contentHost = new TableLayoutPanel();
        _pageHeader = new BrandBand();
        _pageGlyph = new Label();
        _pageTitle = new Label();
        _pageSubtitle = new Label();
        _pageHeaderRule = new CardPanel();
        _contentBody = new Panel();
        _statusStrip = new TableLayoutPanel();
        _statusDot = new Label();
        _statusLabel = new Label();
        _backendStatusLabel = new Label();
        _root.SuspendLayout();
        _header.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_logoImage).BeginInit();
        _navHost.SuspendLayout();
        _nav.SuspendLayout();
        _contentHost.SuspendLayout();
        _pageHeader.SuspendLayout();
        _statusStrip.SuspendLayout();
        SuspendLayout();
        //
        // _root — brand band on top, rail and content in the middle, status bar at the bottom.
        //
        _root.ColumnCount = 2;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Controls.Add(_header, 0, 0);
        _root.Controls.Add(_navHost, 0, 1);
        _root.Controls.Add(_contentHost, 1, 1);
        _root.Controls.Add(_statusStrip, 0, 2);
        _root.Dock = DockStyle.Fill;
        _root.Margin = new Padding(0);
        _root.Name = "_root";
        _root.RowCount = 3;
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _root.SetColumnSpan(_header, 2);
        _root.SetColumnSpan(_statusStrip, 2);
        _root.Size = new Size(1420, 900);
        _root.TabIndex = 0;
        //
        // _header — the brand band: the banner ramp, the glossy mark and the device state.
        //
        _header.Controls.Add(_logoImage);
        _header.Controls.Add(_titleLabel);
        _header.Controls.Add(_subtitleLabel);
        _header.Controls.Add(_deviceBlip);
        _header.Controls.Add(_deviceStateHeader);
        _header.Controls.Add(_deviceNameHeader);
        _header.Controls.Add(_minimizeButton);
        _header.Controls.Add(_maximizeButton);
        _header.Controls.Add(_closeButton);
        _header.Controls.Add(_headerRule);
        _header.Dock = DockStyle.Fill;
        _header.Margin = new Padding(0);
        _header.Name = "_header";
        _header.Size = new Size(1420, 78);
        _header.TabIndex = 0;
        //
        // _logoImage — the glossy squircle from the app artwork, on the painted ramp.
        //
        _logoImage.BackColor = Color.Transparent;
        _logoImage.Location = new Point(18, 16);
        _logoImage.Margin = new Padding(0);
        _logoImage.Name = "_logoImage";
        _logoImage.Size = new Size(46, 46);
        _logoImage.SizeMode = PictureBoxSizeMode.Zoom;
        _logoImage.TabIndex = 0;
        _logoImage.TabStop = false;
        //
        // _titleLabel
        //
        _titleLabel.AutoSize = true;
        _titleLabel.BackColor = Color.Transparent;
        _titleLabel.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
        _titleLabel.Location = new Point(76, 12);
        _titleLabel.Margin = new Padding(0);
        _titleLabel.Name = "_titleLabel";
        _titleLabel.Size = new Size(240, 32);
        _titleLabel.TabIndex = 1;
        _titleLabel.Text = "I-Total Transfer";
        //
        // _subtitleLabel
        //
        _subtitleLabel.AutoSize = true;
        _subtitleLabel.BackColor = Color.Transparent;
        _subtitleLabel.Font = new Font("Segoe UI", 9F);
        _subtitleLabel.Location = new Point(78, 46);
        _subtitleLabel.Margin = new Padding(0);
        _subtitleLabel.Name = "_subtitleLabel";
        _subtitleLabel.Size = new Size(320, 15);
        _subtitleLabel.TabIndex = 2;
        _subtitleLabel.Text = "Phone to Windows · verified media transfer";
        //
        // _deviceBlip — the connected / attention blip that replaced the repeated state pill.
        //
        _deviceBlip.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _deviceBlip.Location = new Point(1020, 30);
        _deviceBlip.Margin = new Padding(0);
        _deviceBlip.Name = "_deviceBlip";
        _deviceBlip.Size = new Size(16, 16);
        _deviceBlip.TabIndex = 3;
        //
        // _deviceStateHeader — a quiet caption beside the blip; it never repeats the body copy.
        //
        _deviceStateHeader.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _deviceStateHeader.AutoEllipsis = true;
        _deviceStateHeader.BackColor = Color.Transparent;
        _deviceStateHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _deviceStateHeader.Location = new Point(1044, 27);
        _deviceStateHeader.Margin = new Padding(0);
        _deviceStateHeader.Name = "_deviceStateHeader";
        _deviceStateHeader.Size = new Size(220, 22);
        _deviceStateHeader.TabIndex = 4;
        _deviceStateHeader.Text = "Checking device…";
        _deviceStateHeader.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _deviceNameHeader
        //
        _deviceNameHeader.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _deviceNameHeader.AutoEllipsis = true;
        _deviceNameHeader.BackColor = Color.Transparent;
        _deviceNameHeader.Font = new Font("Segoe UI", 8.5F);
        _deviceNameHeader.Location = new Point(640, 50);
        _deviceNameHeader.Margin = new Padding(0);
        _deviceNameHeader.Name = "_deviceNameHeader";
        _deviceNameHeader.Size = new Size(624, 16);
        _deviceNameHeader.TabIndex = 5;
        _deviceNameHeader.Text = "Connect your phone by USB and unlock it";
        _deviceNameHeader.TextAlign = ContentAlignment.MiddleRight;
        //
        // window-chrome buttons — the app draws its own caption, so these are the only caption controls.
        //
        _minimizeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _minimizeButton.Glyph = Glyphs.ChromeMinimize;
        _minimizeButton.Location = new Point(1264, 0);
        _minimizeButton.Margin = new Padding(0);
        _minimizeButton.Name = "_minimizeButton";
        _minimizeButton.Size = new Size(48, 34);
        _minimizeButton.TabIndex = 6;
        _minimizeButton.Tag = "chrome";
        _minimizeButton.Text = "";
        _minimizeButton.TabStop = false;
        //
        _maximizeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _maximizeButton.Glyph = Glyphs.ChromeMaximize;
        _maximizeButton.Location = new Point(1312, 0);
        _maximizeButton.Margin = new Padding(0);
        _maximizeButton.Name = "_maximizeButton";
        _maximizeButton.Size = new Size(48, 34);
        _maximizeButton.TabIndex = 7;
        _maximizeButton.Tag = "chrome";
        _maximizeButton.Text = "";
        _maximizeButton.TabStop = false;
        //
        _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _closeButton.Glyph = Glyphs.ChromeClose;
        _closeButton.Location = new Point(1360, 0);
        _closeButton.Margin = new Padding(0);
        _closeButton.Name = "_closeButton";
        _closeButton.Size = new Size(48, 34);
        _closeButton.TabIndex = 8;
        _closeButton.Tag = "chrome close";
        _closeButton.Text = "";
        _closeButton.TabStop = false;
        //
        // _headerRule — the accent ramp painted as a hairline under the band.
        //
        _headerRule.Dock = DockStyle.Bottom;
        _headerRule.Location = new Point(0, 75);
        _headerRule.Margin = new Padding(0);
        _headerRule.Name = "_headerRule";
        _headerRule.Radius = 0;
        _headerRule.Size = new Size(1420, 3);
        _headerRule.StrokeWidth = 0;
        _headerRule.TabIndex = 9;
        //
        // _navHost
        //
        _navHost.ColumnCount = 1;
        _navHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _navHost.Controls.Add(_nav, 0, 0);
        _navHost.Controls.Add(_versionLabel, 0, 1);
        _navHost.Dock = DockStyle.Fill;
        _navHost.Margin = new Padding(0);
        _navHost.Name = "_navHost";
        _navHost.RowCount = 2;
        _navHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _navHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        _navHost.Size = new Size(250, 790);
        _navHost.TabIndex = 1;
        //
        // _nav — the grouped navigation rail.
        //
        _nav.Controls.Add(_navGroupTransfer);
        _nav.Controls.Add(_navDashboard);
        _nav.Controls.Add(_navTransfer);
        _nav.Controls.Add(_navLibrary);
        _nav.Controls.Add(_navWireless);
        _nav.Controls.Add(_navGroupActivity);
        _nav.Controls.Add(_navHistory);
        _nav.Controls.Add(_navGroupSystem);
        _nav.Controls.Add(_navSettings);
        _nav.Controls.Add(_navDiagnostics);
        _nav.Dock = DockStyle.Fill;
        _nav.FlowDirection = FlowDirection.TopDown;
        _nav.Margin = new Padding(0);
        _nav.Name = "_nav";
        _nav.Padding = new Padding(14, 16, 14, 8);
        _nav.Size = new Size(250, 756);
        _nav.TabIndex = 0;
        _nav.WrapContents = false;
        //
        // navigation group captions
        //
        _navGroupTransfer.AutoSize = true;
        _navGroupTransfer.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        _navGroupTransfer.Margin = new Padding(8, 4, 0, 6);
        _navGroupTransfer.Name = "_navGroupTransfer";
        _navGroupTransfer.Size = new Size(64, 12);
        _navGroupTransfer.TabIndex = 0;
        _navGroupTransfer.Tag = "muted";
        _navGroupTransfer.Text = "TRANSFER";
        //
        _navGroupActivity.AutoSize = true;
        _navGroupActivity.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        _navGroupActivity.Margin = new Padding(8, 16, 0, 6);
        _navGroupActivity.Name = "_navGroupActivity";
        _navGroupActivity.Size = new Size(58, 12);
        _navGroupActivity.TabIndex = 5;
        _navGroupActivity.Tag = "muted";
        _navGroupActivity.Text = "ACTIVITY";
        //
        _navGroupSystem.AutoSize = true;
        _navGroupSystem.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        _navGroupSystem.Margin = new Padding(8, 16, 0, 6);
        _navGroupSystem.Name = "_navGroupSystem";
        _navGroupSystem.Size = new Size(50, 12);
        _navGroupSystem.TabIndex = 8;
        _navGroupSystem.Tag = "muted";
        _navGroupSystem.Text = "SYSTEM";
        //
        // _navDashboard
        //
        _navDashboard.AlignLeft = true;
        _navDashboard.Glyph = Glyphs.Home;
        _navDashboard.Margin = new Padding(0, 0, 0, 2);
        _navDashboard.Name = "_navDashboard";
        _navDashboard.Size = new Size(222, 40);
        _navDashboard.TabIndex = 1;
        _navDashboard.Tag = "nav";
        _navDashboard.Text = "Overview";
        //
        // _navTransfer
        //
        _navTransfer.AlignLeft = true;
        _navTransfer.Glyph = Glyphs.Download;
        _navTransfer.Margin = new Padding(0, 0, 0, 2);
        _navTransfer.Name = "_navTransfer";
        _navTransfer.Size = new Size(222, 40);
        _navTransfer.TabIndex = 2;
        _navTransfer.Tag = "nav";
        _navTransfer.Text = "Backup & queue";
        //
        // _navLibrary
        //
        _navLibrary.AlignLeft = true;
        _navLibrary.Glyph = Glyphs.Photo;
        _navLibrary.Margin = new Padding(0, 0, 0, 2);
        _navLibrary.Name = "_navLibrary";
        _navLibrary.Size = new Size(222, 40);
        _navLibrary.TabIndex = 3;
        _navLibrary.Tag = "nav";
        _navLibrary.Text = "Media library";
        //
        // _navWireless
        //
        _navWireless.AlignLeft = true;
        _navWireless.Glyph = Glyphs.Wifi;
        _navWireless.Margin = new Padding(0, 0, 0, 2);
        _navWireless.Name = "_navWireless";
        _navWireless.Size = new Size(222, 40);
        _navWireless.TabIndex = 4;
        _navWireless.Tag = "nav";
        _navWireless.Text = "Wireless import";
        //
        // _navHistory
        //
        _navHistory.AlignLeft = true;
        _navHistory.Glyph = Glyphs.History;
        _navHistory.Margin = new Padding(0, 0, 0, 2);
        _navHistory.Name = "_navHistory";
        _navHistory.Size = new Size(222, 40);
        _navHistory.TabIndex = 6;
        _navHistory.Tag = "nav";
        _navHistory.Text = "History";
        //
        // _navSettings
        //
        _navSettings.AlignLeft = true;
        _navSettings.Glyph = Glyphs.Settings;
        _navSettings.Margin = new Padding(0, 0, 0, 2);
        _navSettings.Name = "_navSettings";
        _navSettings.Size = new Size(222, 40);
        _navSettings.TabIndex = 9;
        _navSettings.Tag = "nav";
        _navSettings.Text = "Settings";
        //
        // _navDiagnostics
        //
        _navDiagnostics.AlignLeft = true;
        _navDiagnostics.Glyph = Glyphs.Repair;
        _navDiagnostics.Margin = new Padding(0, 0, 0, 2);
        _navDiagnostics.Name = "_navDiagnostics";
        _navDiagnostics.Size = new Size(222, 40);
        _navDiagnostics.TabIndex = 10;
        _navDiagnostics.Tag = "nav";
        _navDiagnostics.Text = "Diagnostics";
        //
        // _versionLabel
        //
        _versionLabel.AutoEllipsis = true;
        _versionLabel.Dock = DockStyle.Fill;
        _versionLabel.Font = new Font("Segoe UI", 8F);
        _versionLabel.Location = new Point(4, 756);
        _versionLabel.Margin = new Padding(18, 0, 8, 10);
        _versionLabel.Name = "_versionLabel";
        _versionLabel.Size = new Size(238, 24);
        _versionLabel.TabIndex = 1;
        _versionLabel.Tag = "muted";
        _versionLabel.Text = "version";
        _versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _contentHost
        //
        _contentHost.ColumnCount = 1;
        _contentHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _contentHost.Controls.Add(_pageHeader, 0, 0);
        _contentHost.Controls.Add(_contentBody, 0, 1);
        _contentHost.Dock = DockStyle.Fill;
        _contentHost.Margin = new Padding(10, 8, 16, 10);
        _contentHost.Name = "_contentHost";
        _contentHost.RowCount = 2;
        _contentHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
        _contentHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _contentHost.Size = new Size(1144, 766);
        _contentHost.TabIndex = 2;
        //
        // _pageHeader — a quiet ramp behind the page title, so every page opens on-brand.
        //
        _pageHeader.Controls.Add(_pageGlyph);
        _pageHeader.Controls.Add(_pageTitle);
        _pageHeader.Controls.Add(_pageSubtitle);
        _pageHeader.Controls.Add(_pageHeaderRule);
        _pageHeader.Dock = DockStyle.Fill;
        _pageHeader.Margin = new Padding(0);
        _pageHeader.Name = "_pageHeader";
        _pageHeader.Size = new Size(1144, 80);
        _pageHeader.TabIndex = 0;
        _pageHeader.Tag = "page";
        //
        // _pageGlyph
        //
        _pageGlyph.BackColor = Color.Transparent;
        _pageGlyph.Font = new Font("Segoe UI Symbol", 22F);
        _pageGlyph.Location = new Point(0, 8);
        _pageGlyph.Margin = new Padding(0);
        _pageGlyph.Name = "_pageGlyph";
        _pageGlyph.Size = new Size(46, 46);
        _pageGlyph.TabIndex = 0;
        _pageGlyph.Tag = "glyph accent";
        _pageGlyph.Text = Glyphs.Home;
        _pageGlyph.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _pageTitle
        //
        _pageTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _pageTitle.AutoEllipsis = true;
        _pageTitle.BackColor = Color.Transparent;
        _pageTitle.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
        _pageTitle.Location = new Point(54, 6);
        _pageTitle.Margin = new Padding(0);
        _pageTitle.Name = "_pageTitle";
        _pageTitle.Size = new Size(1070, 34);
        _pageTitle.TabIndex = 1;
        _pageTitle.Text = "Overview";
        _pageTitle.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _pageSubtitle
        //
        _pageSubtitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _pageSubtitle.AutoEllipsis = true;
        _pageSubtitle.BackColor = Color.Transparent;
        _pageSubtitle.Font = new Font("Segoe UI", 9.5F);
        _pageSubtitle.Location = new Point(56, 44);
        _pageSubtitle.Margin = new Padding(0);
        _pageSubtitle.Name = "_pageSubtitle";
        _pageSubtitle.Size = new Size(1068, 22);
        _pageSubtitle.TabIndex = 2;
        _pageSubtitle.Text = "Device state, headline numbers and the primary transfer actions.";
        _pageSubtitle.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _pageHeaderRule
        //
        _pageHeaderRule.Dock = DockStyle.Bottom;
        _pageHeaderRule.Location = new Point(0, 79);
        _pageHeaderRule.Margin = new Padding(0);
        _pageHeaderRule.Name = "_pageHeaderRule";
        _pageHeaderRule.Radius = 0;
        _pageHeaderRule.Size = new Size(1144, 1);
        _pageHeaderRule.StrokeWidth = 0;
        _pageHeaderRule.TabIndex = 3;
        //
        // _contentBody
        //
        _contentBody.Dock = DockStyle.Fill;
        _contentBody.Location = new Point(0, 80);
        _contentBody.Margin = new Padding(0);
        _contentBody.Name = "_contentBody";
        _contentBody.Size = new Size(1144, 686);
        _contentBody.TabIndex = 1;
        //
        // _statusStrip
        //
        _statusStrip.ColumnCount = 3;
        _statusStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34F));
        _statusStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _statusStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520F));
        _statusStrip.Controls.Add(_statusDot, 0, 0);
        _statusStrip.Controls.Add(_statusLabel, 1, 0);
        _statusStrip.Controls.Add(_backendStatusLabel, 2, 0);
        _statusStrip.Dock = DockStyle.Fill;
        _statusStrip.Margin = new Padding(0);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.RowCount = 1;
        _statusStrip.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _statusStrip.Size = new Size(1420, 32);
        _statusStrip.TabIndex = 3;
        //
        // _statusDot
        //
        _statusDot.Dock = DockStyle.Fill;
        _statusDot.Font = new Font("Segoe UI Symbol", 10F);
        _statusDot.Location = new Point(18, 0);
        _statusDot.Margin = new Padding(18, 0, 0, 0);
        _statusDot.Name = "_statusDot";
        _statusDot.Size = new Size(16, 32);
        _statusDot.TabIndex = 0;
        _statusDot.Text = Glyphs.Info;
        _statusDot.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _statusLabel
        //
        _statusLabel.AutoEllipsis = true;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Location = new Point(34, 0);
        _statusLabel.Margin = new Padding(0);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(866, 32);
        _statusLabel.TabIndex = 1;
        _statusLabel.Tag = "muted";
        _statusLabel.Text = "Ready.";
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _backendStatusLabel
        //
        _backendStatusLabel.AutoEllipsis = true;
        _backendStatusLabel.Dock = DockStyle.Fill;
        _backendStatusLabel.Font = new Font("Segoe UI", 8.5F);
        _backendStatusLabel.Location = new Point(900, 0);
        _backendStatusLabel.Margin = new Padding(0, 0, 18, 0);
        _backendStatusLabel.Name = "_backendStatusLabel";
        _backendStatusLabel.Size = new Size(502, 32);
        _backendStatusLabel.TabIndex = 2;
        _backendStatusLabel.Tag = "muted";
        _backendStatusLabel.Text = "Backend: not selected";
        _backendStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(4, 10, 30);
        ClientSize = new Size(1420, 900);
        Controls.Add(_root);
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(1160, 740);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "I-Total Transfer - verified phone media transfer";
        _root.ResumeLayout(false);
        _header.ResumeLayout(false);
        _header.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_logoImage).EndInit();
        _navHost.ResumeLayout(false);
        _nav.ResumeLayout(false);
        _nav.PerformLayout();
        _contentHost.ResumeLayout(false);
        _pageHeader.ResumeLayout(false);
        _pageHeader.PerformLayout();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        ResumeLayout(false);
    }
}
