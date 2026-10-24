#nullable enable
using System.ComponentModel;
using ITotalTransfer.UI.Controls;

namespace ITotalTransfer.UI.Pages;

partial class DashboardPage
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel _root = null!;

    // ---- device hero ------------------------------------------------------------------
    private CardPanel _deviceCard = null!;
    private Label _deviceGlyph = null!;
    private Label _nameLabel = null!;
    private Label _detailLabel = null!;
    private Label _backendLabel = null!;
    private PillLabel _stateLabel = null!;
    private RoundedButton _checkButton = null!;
    private RoundedButton _scanButton = null!;

    // ---- what the device exposes (the banner's four groups) ---------------------------
    private CardPanel _mediaCard = null!;
    private Label _mediaTitle = null!;
    private Label _mediaHint = null!;
    private TableLayoutPanel _mediaTiles = null!;
    private MetricCard _photosTile = null!;
    private MetricCard _videosTile = null!;
    private MetricCard _musicTile = null!;
    private MetricCard _filesTile = null!;

    // ---- headline metrics -------------------------------------------------------------
    private TableLayoutPanel _metrics = null!;
    private MetricCard _filesCard = null!;
    private MetricCard _sizeCard = null!;
    private MetricCard _verifiedCard = null!;
    private MetricCard _failedCard = null!;

    // ---- current operation: whichever of scan / transfer is running, never both --------
    private CardPanel _operationCard = null!;
    private Label _operationTitle = null!;
    private Label _operationText = null!;
    private FluentProgressBar _operationBar = null!;

    // ---- unfinished work already sitting in the destination (hidden until there is some) ----
    private InfoBar _recoveryBar = null!;

    // ---- destination and the primary actions ------------------------------------------
    private CardPanel _actionsCard = null!;
    private Label _destinationLabel = null!;
    private TextBox _destinationBox = null!;
    private RoundedButton _browseButton = null!;
    private FluentCheckBox _confirmScanCheck = null!;
    private Label _hintLabel = null!;
    private Label _integrationLabel = null!;
    private RoundedButton _startButton = null!;
    private RoundedButton _pauseButton = null!;
    private RoundedButton _stopButton = null!;

    /// <summary>Designer-owned layout: device hero, media-group tiles, metrics, destination and actions.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _root = new TableLayoutPanel();
        _deviceCard = new CardPanel();
        _deviceGlyph = new Label();
        _nameLabel = new Label();
        _detailLabel = new Label();
        _backendLabel = new Label();
        _stateLabel = new PillLabel();
        _checkButton = new RoundedButton();
        _scanButton = new RoundedButton();
        _mediaCard = new CardPanel();
        _mediaTitle = new Label();
        _mediaHint = new Label();
        _mediaTiles = new TableLayoutPanel();
        _photosTile = new MetricCard();
        _videosTile = new MetricCard();
        _musicTile = new MetricCard();
        _filesTile = new MetricCard();
        _metrics = new TableLayoutPanel();
        _filesCard = new MetricCard();
        _sizeCard = new MetricCard();
        _verifiedCard = new MetricCard();
        _failedCard = new MetricCard();
        _operationCard = new CardPanel();
        _operationTitle = new Label();
        _operationText = new Label();
        _operationBar = new FluentProgressBar();
        _recoveryBar = new InfoBar();
        _actionsCard = new CardPanel();
        _destinationLabel = new Label();
        _destinationBox = new TextBox();
        _browseButton = new RoundedButton();
        _confirmScanCheck = new FluentCheckBox();
        _hintLabel = new Label();
        _integrationLabel = new Label();
        _startButton = new RoundedButton();
        _pauseButton = new RoundedButton();
        _stopButton = new RoundedButton();
        _root.SuspendLayout();
        _deviceCard.SuspendLayout();
        _mediaCard.SuspendLayout();
        _mediaTiles.SuspendLayout();
        _metrics.SuspendLayout();
        _operationCard.SuspendLayout();
        _actionsCard.SuspendLayout();
        SuspendLayout();
        //
        // _root — device hero, media groups, metrics, the unfinished-session bar (row 3, collapsed to zero
        // height when there is nothing to continue), current operation, then the action panel.
        //
        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Controls.Add(_deviceCard, 0, 0);
        _root.Controls.Add(_mediaCard, 0, 1);
        _root.Controls.Add(_metrics, 0, 2);
        _root.Controls.Add(_recoveryBar, 0, 3);
        _root.Controls.Add(_operationCard, 0, 4);
        _root.Controls.Add(_actionsCard, 0, 5);
        _root.Dock = DockStyle.Fill;
        _root.Margin = new Padding(0);
        _root.Name = "_root";
        _root.RowCount = 6;
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 196F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122F));
        // The recovery row is collapsed to nothing until a destination actually holds unfinished work,
        // so a finished backup costs no vertical space. DashboardPage.SetRecovery gives it height.
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.Size = new Size(1144, 742);
        _root.TabIndex = 0;
        //
        // _deviceCard
        //
        _deviceCard.Controls.Add(_deviceGlyph);
        _deviceCard.Controls.Add(_nameLabel);
        _deviceCard.Controls.Add(_detailLabel);
        _deviceCard.Controls.Add(_backendLabel);
        _deviceCard.Controls.Add(_stateLabel);
        _deviceCard.Controls.Add(_checkButton);
        _deviceCard.Controls.Add(_scanButton);
        _deviceCard.Dock = DockStyle.Fill;
        _deviceCard.Margin = new Padding(0, 0, 0, 14);
        _deviceCard.Name = "_deviceCard";
        _deviceCard.Size = new Size(1144, 116);
        _deviceCard.StrokeWidth = 1;
        _deviceCard.TabIndex = 0;
        _deviceCard.Tag = "card";
        //
        // _deviceGlyph — the hero mark for the connected device.
        //
        _deviceGlyph.Font = new Font("Segoe UI Symbol", 30F);
        _deviceGlyph.Location = new Point(22, 26);
        _deviceGlyph.Margin = new Padding(0);
        _deviceGlyph.Name = "_deviceGlyph";
        _deviceGlyph.Size = new Size(60, 60);
        _deviceGlyph.TabIndex = 0;
        _deviceGlyph.Tag = "glyph accent";
        _deviceGlyph.Text = Glyphs.Phone;
        _deviceGlyph.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _nameLabel
        //
        _nameLabel.AutoEllipsis = true;
        _nameLabel.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        _nameLabel.Location = new Point(98, 20);
        _nameLabel.Margin = new Padding(0);
        _nameLabel.Name = "_nameLabel";
        _nameLabel.Size = new Size(520, 28);
        _nameLabel.TabIndex = 1;
        _nameLabel.Text = "Phone not connected";
        //
        // _detailLabel
        //
        _detailLabel.AutoEllipsis = true;
        _detailLabel.Font = new Font("Segoe UI", 9F);
        _detailLabel.Location = new Point(100, 50);
        _detailLabel.Margin = new Padding(0);
        _detailLabel.Name = "_detailLabel";
        _detailLabel.Size = new Size(660, 34);
        _detailLabel.TabIndex = 2;
        _detailLabel.Tag = "muted";
        _detailLabel.Text = "Connect your phone by USB and unlock it.";
        //
        // _backendLabel
        //
        _backendLabel.AutoEllipsis = true;
        _backendLabel.Font = new Font("Segoe UI", 8.5F);
        _backendLabel.Location = new Point(100, 86);
        _backendLabel.Margin = new Padding(0);
        _backendLabel.Name = "_backendLabel";
        _backendLabel.Size = new Size(660, 16);
        _backendLabel.TabIndex = 3;
        _backendLabel.Tag = "muted";
        _backendLabel.Text = "Backend: not selected";
        //
        // _stateLabel
        //
        _stateLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _stateLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _stateLabel.Glyph = Glyphs.Phone;
        _stateLabel.Location = new Point(882, 16);
        _stateLabel.Margin = new Padding(0);
        _stateLabel.Name = "_stateLabel";
        _stateLabel.Padding = new Padding(12, 0, 12, 0);
        _stateLabel.Size = new Size(240, 28);
        _stateLabel.TabIndex = 4;
        _stateLabel.Text = "CHECKING DEVICE…";
        _stateLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _scanButton
        //
        _scanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _scanButton.Glyph = Glyphs.Search;
        _scanButton.Location = new Point(966, 54);
        _scanButton.Margin = new Padding(0);
        _scanButton.Name = "_scanButton";
        _scanButton.Size = new Size(156, 36);
        _scanButton.TabIndex = 5;
        _scanButton.Tag = "primary";
        _scanButton.Text = "Scan device";
        //
        // _checkButton
        //
        _checkButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _checkButton.Glyph = Glyphs.Sync;
        _checkButton.Location = new Point(798, 54);
        _checkButton.Margin = new Padding(0);
        _checkButton.Name = "_checkButton";
        _checkButton.Size = new Size(156, 36);
        _checkButton.TabIndex = 6;
        _checkButton.Tag = "subtle";
        _checkButton.Text = "Check device";
        //
        // _mediaCard
        //
        _mediaCard.Controls.Add(_mediaTitle);
        _mediaCard.Controls.Add(_mediaHint);
        _mediaCard.Controls.Add(_mediaTiles);
        _mediaCard.Dock = DockStyle.Fill;
        _mediaCard.Margin = new Padding(0, 0, 0, 14);
        _mediaCard.Name = "_mediaCard";
        _mediaCard.Size = new Size(1144, 182);
        _mediaCard.StrokeWidth = 1;
        _mediaCard.TabIndex = 1;
        _mediaCard.Tag = "card";
        //
        // _mediaTitle
        //
        _mediaTitle.AutoSize = true;
        _mediaTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _mediaTitle.Location = new Point(22, 16);
        _mediaTitle.Margin = new Padding(0);
        _mediaTitle.Name = "_mediaTitle";
        _mediaTitle.Size = new Size(220, 20);
        _mediaTitle.TabIndex = 0;
        _mediaTitle.Text = "What your phone exposes";
        //
        // _mediaHint
        //
        _mediaHint.AutoEllipsis = true;
        _mediaHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _mediaHint.Font = new Font("Segoe UI", 8.5F);
        _mediaHint.Location = new Point(24, 40);
        _mediaHint.Margin = new Padding(0);
        _mediaHint.Name = "_mediaHint";
        _mediaHint.Size = new Size(1096, 16);
        _mediaHint.TabIndex = 1;
        _mediaHint.Tag = "muted";
        _mediaHint.Text = "A group the connected backend cannot read is shown as unavailable — never as an empty folder.";
        //
        // _mediaTiles
        //
        _mediaTiles.ColumnCount = 4;
        _mediaTiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _mediaTiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _mediaTiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _mediaTiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _mediaTiles.Controls.Add(_photosTile, 0, 0);
        _mediaTiles.Controls.Add(_videosTile, 1, 0);
        _mediaTiles.Controls.Add(_musicTile, 2, 0);
        _mediaTiles.Controls.Add(_filesTile, 3, 0);
        _mediaTiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _mediaTiles.Location = new Point(16, 62);
        _mediaTiles.Margin = new Padding(0);
        _mediaTiles.Name = "_mediaTiles";
        _mediaTiles.RowCount = 1;
        _mediaTiles.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mediaTiles.Size = new Size(1112, 108);
        _mediaTiles.TabIndex = 2;
        //
        // _photosTile
        //
        _photosTile.Dock = DockStyle.Fill;
        _photosTile.Glyph = Glyphs.Photo;
        _photosTile.Margin = new Padding(0, 0, 10, 0);
        _photosTile.Name = "_photosTile";
        _photosTile.Size = new Size(268, 108);
        _photosTile.TabIndex = 0;
        _photosTile.Title = "Photos";
        _photosTile.Value = "—";
        //
        // _videosTile
        //
        _videosTile.Dock = DockStyle.Fill;
        _videosTile.Glyph = Glyphs.Video;
        _videosTile.Margin = new Padding(0, 0, 10, 0);
        _videosTile.Name = "_videosTile";
        _videosTile.Size = new Size(268, 108);
        _videosTile.TabIndex = 1;
        _videosTile.Title = "Videos";
        _videosTile.Value = "—";
        //
        // _musicTile
        //
        _musicTile.Dock = DockStyle.Fill;
        _musicTile.Glyph = Glyphs.Audio;
        _musicTile.Margin = new Padding(0, 0, 10, 0);
        _musicTile.Name = "_musicTile";
        _musicTile.Size = new Size(268, 108);
        _musicTile.TabIndex = 2;
        _musicTile.Title = "Music";
        _musicTile.Value = "—";
        //
        // _filesTile
        //
        _filesTile.Dock = DockStyle.Fill;
        _filesTile.Glyph = Glyphs.Document;
        _filesTile.Margin = new Padding(0);
        _filesTile.Name = "_filesTile";
        _filesTile.Size = new Size(268, 108);
        _filesTile.TabIndex = 3;
        _filesTile.Title = "Files";
        _filesTile.Value = "—";
        //
        // _metrics
        //
        _metrics.ColumnCount = 4;
        _metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _metrics.Controls.Add(_filesCard, 0, 0);
        _metrics.Controls.Add(_sizeCard, 1, 0);
        _metrics.Controls.Add(_verifiedCard, 2, 0);
        _metrics.Controls.Add(_failedCard, 3, 0);
        _metrics.Dock = DockStyle.Fill;
        _metrics.Margin = new Padding(0, 0, 0, 14);
        _metrics.Name = "_metrics";
        _metrics.RowCount = 1;
        _metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _metrics.Size = new Size(1144, 108);
        _metrics.TabIndex = 2;
        //
        // _filesCard
        //
        _filesCard.Dock = DockStyle.Fill;
        _filesCard.Glyph = Glyphs.Document;
        _filesCard.Margin = new Padding(0, 0, 10, 0);
        _filesCard.Name = "_filesCard";
        _filesCard.Size = new Size(276, 108);
        _filesCard.TabIndex = 0;
        _filesCard.Title = "Files found";
        _filesCard.Value = "0";
        //
        // _sizeCard
        //
        _sizeCard.Dock = DockStyle.Fill;
        _sizeCard.Glyph = Glyphs.Save;
        _sizeCard.Margin = new Padding(0, 0, 10, 0);
        _sizeCard.Name = "_sizeCard";
        _sizeCard.Size = new Size(276, 108);
        _sizeCard.TabIndex = 1;
        _sizeCard.Title = "Total size";
        _sizeCard.Value = "0 B";
        //
        // _verifiedCard
        //
        _verifiedCard.Dock = DockStyle.Fill;
        _verifiedCard.Glyph = Glyphs.Shield;
        _verifiedCard.Margin = new Padding(0, 0, 10, 0);
        _verifiedCard.Name = "_verifiedCard";
        _verifiedCard.Size = new Size(276, 108);
        _verifiedCard.TabIndex = 2;
        _verifiedCard.Title = "Verified";
        _verifiedCard.Value = "0";
        //
        // _failedCard
        //
        _failedCard.Dock = DockStyle.Fill;
        _failedCard.Glyph = Glyphs.Warning;
        _failedCard.Margin = new Padding(0);
        _failedCard.Name = "_failedCard";
        _failedCard.Size = new Size(276, 108);
        _failedCard.TabIndex = 3;
        _failedCard.Title = "Needs attention";
        _failedCard.Value = "0";
        //
        // _operationCard — the "Current operation" band. Indeterminate while the work is open-ended
        // (scanning a directory whose contents are not known yet, or connecting), determinate once a
        // real total exists. Scan and transfer are separate phases and are labelled as such, so one
        // number is never presented as if it covered both.
        //
        _operationCard.Controls.Add(_operationTitle);
        _operationCard.Controls.Add(_operationText);
        _operationCard.Controls.Add(_operationBar);
        _operationCard.Dock = DockStyle.Fill;
        _operationCard.Margin = new Padding(0, 0, 0, 14);
        _operationCard.Name = "_operationCard";
        _operationCard.Size = new Size(1144, 38);
        _operationCard.StrokeWidth = 1;
        _operationCard.TabIndex = 3;
        _operationCard.Tag = "card";
        //
        // _operationTitle
        //
        _operationTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _operationTitle.Location = new Point(22, 9);
        _operationTitle.Margin = new Padding(0);
        _operationTitle.Name = "_operationTitle";
        _operationTitle.Size = new Size(140, 17);
        _operationTitle.TabIndex = 0;
        _operationTitle.Text = "Current operation";
        _operationTitle.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _operationText — the live line: where the work is, how much it has found, and how fast.
        //
        _operationText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _operationText.AutoEllipsis = true;
        _operationText.Font = new Font("Segoe UI", 9F);
        _operationText.Location = new Point(170, 9);
        _operationText.Margin = new Padding(0);
        _operationText.Name = "_operationText";
        _operationText.Size = new Size(950, 18);
        _operationText.TabIndex = 1;
        _operationText.Tag = "muted";
        _operationText.Text = "Idle — nothing is running.";
        _operationText.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _operationBar
        //
        _operationBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _operationBar.Location = new Point(22, 30);
        _operationBar.Margin = new Padding(0);
        _operationBar.Name = "_operationBar";
        _operationBar.Size = new Size(1098, 6);
        _operationBar.TabIndex = 2;
        _operationBar.Thickness = 6;
        //
        // _recoveryBar
        //
        // A Fluent inline bar, collapsed to zero height until the chosen destination is found to hold
        // unfinished work. It reports what the folder's own session record says and offers to continue;
        // it never invents progress and is never fed from a cache.
        _recoveryBar.Dock = DockStyle.Fill;
        _recoveryBar.Location = new Point(0, 0);
        _recoveryBar.Margin = new Padding(0, 0, 0, 10);
        _recoveryBar.Name = "_recoveryBar";
        _recoveryBar.Size = new Size(1144, 84);
        _recoveryBar.TabIndex = 3;
        _recoveryBar.Visible = false;
        //
        // _actionsCard
        //
        _actionsCard.Controls.Add(_destinationLabel);
        _actionsCard.Controls.Add(_destinationBox);
        _actionsCard.Controls.Add(_browseButton);
        _actionsCard.Controls.Add(_confirmScanCheck);
        _actionsCard.Controls.Add(_hintLabel);
        _actionsCard.Controls.Add(_integrationLabel);
        _actionsCard.Controls.Add(_startButton);
        _actionsCard.Controls.Add(_pauseButton);
        _actionsCard.Controls.Add(_stopButton);
        _actionsCard.Dock = DockStyle.Fill;
        _actionsCard.Margin = new Padding(0);
        _actionsCard.Name = "_actionsCard";
        _actionsCard.Size = new Size(1144, 232);
        _actionsCard.StrokeWidth = 1;
        _actionsCard.TabIndex = 4;
        _actionsCard.Tag = "card";
        //
        // _destinationLabel
        //
        _destinationLabel.AutoSize = true;
        _destinationLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _destinationLabel.Location = new Point(22, 18);
        _destinationLabel.Margin = new Padding(0);
        _destinationLabel.Name = "_destinationLabel";
        _destinationLabel.Size = new Size(130, 17);
        _destinationLabel.TabIndex = 0;
        _destinationLabel.Text = "Destination folder";
        //
        // _destinationBox
        //
        _destinationBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _destinationBox.Location = new Point(22, 44);
        _destinationBox.Margin = new Padding(0);
        _destinationBox.Name = "_destinationBox";
        _destinationBox.PlaceholderText = "Choose where imported media is written…";
        _destinationBox.Size = new Size(740, 25);
        _destinationBox.TabIndex = 1;
        //
        // _browseButton
        //
        _browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _browseButton.Glyph = Glyphs.Folder;
        _browseButton.Location = new Point(972, 42);
        _browseButton.Margin = new Padding(0);
        _browseButton.Name = "_browseButton";
        _browseButton.Size = new Size(150, 34);
        _browseButton.TabIndex = 2;
        _browseButton.Tag = "subtle";
        _browseButton.Text = "Browse…";
        //
        // _confirmScanCheck
        //
        _confirmScanCheck.Location = new Point(24, 84);
        _confirmScanCheck.Margin = new Padding(0);
        _confirmScanCheck.Name = "_confirmScanCheck";
        _confirmScanCheck.Size = new Size(720, 22);
        _confirmScanCheck.TabIndex = 3;
        _confirmScanCheck.Text = "Confirm each scan with a second pass (catches a listing that changes mid-scan)";
        //
        // _hintLabel
        //
        _hintLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _hintLabel.AutoEllipsis = true;
        _hintLabel.Font = new Font("Segoe UI", 8.5F);
        _hintLabel.Location = new Point(24, 114);
        _hintLabel.Margin = new Padding(0);
        _hintLabel.Name = "_hintLabel";
        _hintLabel.Size = new Size(1096, 32);
        _hintLabel.TabIndex = 4;
        _hintLabel.Tag = "muted";
        _hintLabel.Text = "Files are copied, then read back and hashed against the source where the backend can produce one. Nothing is ever deleted from the phone.";
        //
        // _integrationLabel
        //
        _integrationLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _integrationLabel.AutoEllipsis = true;
        _integrationLabel.Font = new Font("Segoe UI", 8.5F);
        _integrationLabel.Location = new Point(24, 168);
        _integrationLabel.Margin = new Padding(0);
        _integrationLabel.Name = "_integrationLabel";
        _integrationLabel.Size = new Size(560, 44);
        _integrationLabel.TabIndex = 5;
        _integrationLabel.Tag = "muted";
        _integrationLabel.Text = "Apple integration not checked yet.";
        //
        // _startButton
        //
        _startButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _startButton.Glyph = Glyphs.Download;
        _startButton.Location = new Point(614, 172);
        _startButton.Margin = new Padding(0);
        _startButton.Name = "_startButton";
        _startButton.Size = new Size(200, 40);
        _startButton.TabIndex = 6;
        _startButton.Tag = "primary";
        _startButton.Text = "Start transfer";
        //
        // _pauseButton
        //
        _pauseButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _pauseButton.Glyph = Glyphs.Pause;
        _pauseButton.Location = new Point(830, 172);
        _pauseButton.Margin = new Padding(0);
        _pauseButton.Name = "_pauseButton";
        _pauseButton.Size = new Size(140, 40);
        _pauseButton.TabIndex = 7;
        _pauseButton.Tag = "subtle";
        _pauseButton.Text = "Pause";
        //
        // _stopButton
        //
        _stopButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _stopButton.Location = new Point(982, 172);
        _stopButton.Margin = new Padding(0);
        _stopButton.Name = "_stopButton";
        _stopButton.Size = new Size(140, 40);
        _stopButton.TabIndex = 8;
        _stopButton.Tag = "subtle";
        _stopButton.Text = "Stop";
        //
        // DashboardPage
        //
        // AutoScrollMinSize is the anti-clipping floor: the stack below needs 690 px (device hero, the
        // four groups, the headline metrics, the current-operation band and the action panel). When the
        // window is shorter than that the page scrolls instead of squeezing the action panel until its
        // integration line is sliced in half, which is what it used to do. On a taller window the action
        // panel still takes the remaining space, so nothing is wasted.
        //
        // 2026-10-22: the metric row lost 10 px and the floor dropped from 742 to 690, so the primary
        // action row stays above the fold at 1440x900 and 150% scaling. Before this, Start transfer sat
        // under the status bar and the operator had to scroll to begin the main task.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        AutoScrollMinSize = new Size(0, 690);
        Controls.Add(_root);
        Margin = new Padding(0);
        Name = "DashboardPage";
        Size = new Size(1144, 742);
        _root.ResumeLayout(false);
        _root.PerformLayout();
        _deviceCard.ResumeLayout(false);
        _deviceCard.PerformLayout();
        _mediaCard.ResumeLayout(false);
        _mediaCard.PerformLayout();
        _mediaTiles.ResumeLayout(false);
        _metrics.ResumeLayout(false);
        _operationCard.ResumeLayout(false);
        _operationCard.PerformLayout();
        _actionsCard.ResumeLayout(false);
        _actionsCard.PerformLayout();
        ResumeLayout(false);
    }
}
