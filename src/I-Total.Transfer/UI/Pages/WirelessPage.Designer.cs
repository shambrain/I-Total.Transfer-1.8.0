#nullable enable
using ITotalTransfer.UI.Controls;
namespace ITotalTransfer.UI.Pages;

partial class WirelessPage
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel _root = null!;

    private CardPanel _settingsCard = null!;
    private FluentCheckBox _enableCheck = null!;
    private Label _addressLabel = null!;
    private ComboBox _addressCombo = null!;
    private Label _portLabel = null!;
    private NumericUpDown _portBox = null!;
    private Label _tokenLabel = null!;
    private NumericUpDown _tokenMinutesBox = null!;
    private FluentCheckBox _tlsCheck = null!;
    private FluentCheckBox _autoApproveCheck = null!;
    private RoundedButton _startButton = null!;
    private RoundedButton _stopButton = null!;
    private RoundedButton _pairButton = null!;
    private Label _statusLabel = null!;
    private Label _pairUrlLabel = null!;
    private TextBox _pairUrlBox = null!;
    private PictureBox _qrBox = null!;
    private Label _ticketStateLabel = null!;
    private Label _noticeLabel = null!;

    private TableLayoutPanel _rightColumn = null!;
    private CardPanel _pendingCard = null!;
    private Label _pendingTitle = null!;
    private DataGridView _pendingGrid = null!;
    private DataGridViewTextBoxColumn _pendingPathColumn = null!;
    private DataGridViewTextBoxColumn _pendingBytesColumn = null!;
    private DataGridViewTextBoxColumn _pendingFromColumn = null!;
    private RoundedButton _approveButton = null!;
    private RoundedButton _refuseButton = null!;
    private RoundedButton _approveAllButton = null!;
    private RoundedButton _refuseAllButton = null!;

    private CardPanel _receivedCard = null!;
    private Label _receivedTitle = null!;
    private DataGridView _receivedGrid = null!;
    private DataGridViewTextBoxColumn _pathColumn = null!;
    private DataGridViewTextBoxColumn _bytesColumn = null!;
    private DataGridViewTextBoxColumn _shaColumn = null!;
    private DataGridViewTextBoxColumn _receivedColumn = null!;
    private RichTextBox _logBox = null!;

    /// <summary>Designer-owned wireless receiver page: listen settings, pairing ticket and receipts.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _root = new TableLayoutPanel();
        _settingsCard = new CardPanel();
        _enableCheck = new FluentCheckBox();
        _addressLabel = new Label();
        _addressCombo = new ComboBox();
        _portLabel = new Label();
        _portBox = new NumericUpDown();
        _tokenLabel = new Label();
        _tokenMinutesBox = new NumericUpDown();
        _tlsCheck = new FluentCheckBox();
        _autoApproveCheck = new FluentCheckBox();
        _startButton = new RoundedButton();
        _stopButton = new RoundedButton();
        _pairButton = new RoundedButton();
        _statusLabel = new Label();
        _pairUrlLabel = new Label();
        _pairUrlBox = new TextBox();
        _qrBox = new PictureBox();
        _ticketStateLabel = new Label();
        _noticeLabel = new Label();
        _rightColumn = new TableLayoutPanel();
        _pendingCard = new CardPanel();
        _pendingTitle = new Label();
        _pendingGrid = new DataGridView();
        _pendingPathColumn = new DataGridViewTextBoxColumn();
        _pendingBytesColumn = new DataGridViewTextBoxColumn();
        _pendingFromColumn = new DataGridViewTextBoxColumn();
        _approveButton = new RoundedButton();
        _refuseButton = new RoundedButton();
        _approveAllButton = new RoundedButton();
        _refuseAllButton = new RoundedButton();
        _receivedCard = new CardPanel();
        _receivedTitle = new Label();
        _receivedGrid = new DataGridView();
        _pathColumn = new DataGridViewTextBoxColumn();
        _bytesColumn = new DataGridViewTextBoxColumn();
        _shaColumn = new DataGridViewTextBoxColumn();
        _receivedColumn = new DataGridViewTextBoxColumn();
        _logBox = new RichTextBox();
        _root.SuspendLayout();
        _settingsCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_portBox).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_tokenMinutesBox).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_qrBox).BeginInit();
        _rightColumn.SuspendLayout();
        _pendingCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_pendingGrid).BeginInit();
        _receivedCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_receivedGrid).BeginInit();
        SuspendLayout();
        //
        // _root
        //
        _root.ColumnCount = 2;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        _root.Controls.Add(_settingsCard, 0, 0);
        _root.Controls.Add(_rightColumn, 1, 0);
        _root.Dock = DockStyle.Fill;
        _root.Name = "_root";
        _root.RowCount = 1;
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.Size = new Size(1100, 640);
        _root.TabIndex = 0;
        //
        // _settingsCard
        //
        _settingsCard.Controls.Add(_enableCheck);
        _settingsCard.Controls.Add(_addressLabel);
        _settingsCard.Controls.Add(_addressCombo);
        _settingsCard.Controls.Add(_portLabel);
        _settingsCard.Controls.Add(_portBox);
        _settingsCard.Controls.Add(_tokenLabel);
        _settingsCard.Controls.Add(_tokenMinutesBox);
        _settingsCard.Controls.Add(_tlsCheck);
        _settingsCard.Controls.Add(_autoApproveCheck);
        _settingsCard.Controls.Add(_startButton);
        _settingsCard.Controls.Add(_stopButton);
        _settingsCard.Controls.Add(_pairButton);
        _settingsCard.Controls.Add(_statusLabel);
        _settingsCard.Controls.Add(_pairUrlLabel);
        _settingsCard.Controls.Add(_pairUrlBox);
        _settingsCard.Controls.Add(_qrBox);
        _settingsCard.Controls.Add(_ticketStateLabel);
        _settingsCard.Controls.Add(_noticeLabel);
        _settingsCard.Dock = DockStyle.Fill;
        _settingsCard.Margin = new Padding(0, 0, 12, 0);
        _settingsCard.Name = "_settingsCard";
        _settingsCard.Size = new Size(450, 640);
        _settingsCard.TabIndex = 0;
        _settingsCard.Tag = "card";
        //
        // _enableCheck
        //
        _enableCheck.AutoSize = true;
        _enableCheck.Font = new Font("Segoe UI Semibold", 10F);
        _enableCheck.Location = new Point(18, 16);
        _enableCheck.Name = "_enableCheck";
        _enableCheck.Size = new Size(220, 23);
        _enableCheck.TabIndex = 0;
        _enableCheck.Text = "Enable wireless receiving";
        //
        // _addressLabel
        //
        _addressLabel.AutoSize = true;
        _addressLabel.Location = new Point(18, 48);
        _addressLabel.Name = "_addressLabel";
        _addressLabel.Size = new Size(90, 15);
        _addressLabel.TabIndex = 1;
        _addressLabel.Tag = "muted";
        _addressLabel.Text = "Bind to address";
        //
        // _addressCombo
        //
        _addressCombo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _addressCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _addressCombo.Location = new Point(18, 68);
        _addressCombo.Name = "_addressCombo";
        _addressCombo.Size = new Size(414, 27);
        _addressCombo.TabIndex = 2;
        //
        // _portLabel
        //
        _portLabel.AutoSize = true;
        _portLabel.Location = new Point(18, 104);
        _portLabel.Name = "_portLabel";
        _portLabel.Size = new Size(30, 15);
        _portLabel.TabIndex = 3;
        _portLabel.Tag = "muted";
        _portLabel.Text = "Port";
        //
        // _portBox
        //
        _portBox.Location = new Point(18, 124);
        _portBox.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
        _portBox.Minimum = new decimal(new int[] { 1024, 0, 0, 0 });
        _portBox.Name = "_portBox";
        _portBox.Size = new Size(120, 27);
        _portBox.TabIndex = 4;
        _portBox.Value = new decimal(new int[] { 47821, 0, 0, 0 });
        //
        // _tokenLabel
        //
        _tokenLabel.AutoSize = true;
        _tokenLabel.Location = new Point(160, 104);
        _tokenLabel.Name = "_tokenLabel";
        _tokenLabel.Size = new Size(150, 15);
        _tokenLabel.TabIndex = 5;
        _tokenLabel.Tag = "muted";
        _tokenLabel.Text = "Pairing ticket lifetime (min)";
        //
        // _tokenMinutesBox
        //
        _tokenMinutesBox.Location = new Point(160, 124);
        _tokenMinutesBox.Maximum = new decimal(new int[] { 120, 0, 0, 0 });
        _tokenMinutesBox.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _tokenMinutesBox.Name = "_tokenMinutesBox";
        _tokenMinutesBox.Size = new Size(120, 27);
        _tokenMinutesBox.TabIndex = 6;
        _tokenMinutesBox.Value = new decimal(new int[] { 10, 0, 0, 0 });
        //
        // _tlsCheck
        //
        _tlsCheck.AutoSize = true;
        _tlsCheck.Location = new Point(18, 162);
        _tlsCheck.Name = "_tlsCheck";
        _tlsCheck.Size = new Size(351, 19);
        _tlsCheck.TabIndex = 7;
        _tlsCheck.Text = "Require TLS (private-network HTTP if no matching certificate)";
        //
        // _autoApproveCheck
        //
        _autoApproveCheck.AutoSize = true;
        _autoApproveCheck.Location = new Point(18, 188);
        _autoApproveCheck.Name = "_autoApproveCheck";
        _autoApproveCheck.Size = new Size(380, 19);
        _autoApproveCheck.TabIndex = 8;
        _autoApproveCheck.Text = "Approve incoming files automatically (skip the approval list)";
        //
        // _startButton
        //
        _startButton.Location = new Point(18, 220);
        _startButton.Name = "_startButton";
        _startButton.Size = new Size(150, 36);
        _startButton.TabIndex = 9;
        _startButton.Tag = "primary";
        _startButton.Text = "Start listening";
        //
        // _stopButton
        //
        _stopButton.Location = new Point(180, 220);
        _stopButton.Name = "_stopButton";
        _stopButton.Size = new Size(120, 36);
        _stopButton.TabIndex = 10;
        _stopButton.Text = "Stop";
        //
        // _pairButton
        //
        _pairButton.Location = new Point(312, 220);
        _pairButton.Name = "_pairButton";
        _pairButton.Size = new Size(120, 36);
        _pairButton.TabIndex = 11;
        _pairButton.Tag = "primary";
        _pairButton.Text = "New ticket";
        //
        // _statusLabel
        //
        _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _statusLabel.Location = new Point(18, 264);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(414, 20);
        _statusLabel.TabIndex = 12;
        _statusLabel.Tag = "muted";
        _statusLabel.Text = "Receiver stopped.";
        //
        // _pairUrlLabel
        //
        _pairUrlLabel.AutoSize = true;
        _pairUrlLabel.Location = new Point(18, 292);
        _pairUrlLabel.Name = "_pairUrlLabel";
        _pairUrlLabel.Size = new Size(200, 15);
        _pairUrlLabel.TabIndex = 13;
        _pairUrlLabel.Tag = "muted";
        _pairUrlLabel.Text = "Pairing URL — single use, shown once";
        //
        // _pairUrlBox
        //
        _pairUrlBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _pairUrlBox.Location = new Point(18, 312);
        _pairUrlBox.Name = "_pairUrlBox";
        _pairUrlBox.ReadOnly = true;
        _pairUrlBox.Size = new Size(414, 27);
        _pairUrlBox.TabIndex = 14;
        //
        // _qrBox
        //
        _qrBox.BackColor = Color.White;
        _qrBox.BorderStyle = BorderStyle.FixedSingle;
        _qrBox.Location = new Point(18, 350);
        _qrBox.Name = "_qrBox";
        _qrBox.Size = new Size(150, 150);
        _qrBox.SizeMode = PictureBoxSizeMode.Zoom;
        _qrBox.TabIndex = 15;
        _qrBox.TabStop = false;
        //
        // _ticketStateLabel
        //
        _ticketStateLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _ticketStateLabel.Location = new Point(184, 352);
        _ticketStateLabel.Name = "_ticketStateLabel";
        _ticketStateLabel.Size = new Size(248, 150);
        _ticketStateLabel.TabIndex = 16;
        _ticketStateLabel.Tag = "muted";
        _ticketStateLabel.Text = "No pairing ticket. Start the receiver, then choose New ticket.";
        //
        // _noticeLabel
        //
        _noticeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _noticeLabel.Location = new Point(18, 508);
        _noticeLabel.Name = "_noticeLabel";
        _noticeLabel.Size = new Size(414, 140);
        _noticeLabel.TabIndex = 17;
        _noticeLabel.Tag = "muted";
        _noticeLabel.Text = "An iPhone cannot be enumerated over the network on its own. A sender - an iOS Shortcut or " +
            "companion app - has to pick the files and POST them to /upload with a session token from this " +
            "ticket. Nothing is written to the folder until you approve it, and every received file is hashed " +
            "on receipt.";
        //
        // _rightColumn
        //
        _rightColumn.ColumnCount = 1;
        _rightColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _rightColumn.Controls.Add(_pendingCard, 0, 0);
        _rightColumn.Controls.Add(_receivedCard, 0, 1);
        _rightColumn.Dock = DockStyle.Fill;
        _rightColumn.Margin = new Padding(0);
        _rightColumn.Name = "_rightColumn";
        _rightColumn.RowCount = 2;
        _rightColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 236F));
        _rightColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _rightColumn.Size = new Size(638, 640);
        _rightColumn.TabIndex = 1;
        //
        // _pendingCard
        //
        _pendingCard.Controls.Add(_pendingGrid);
        _pendingCard.Controls.Add(_pendingTitle);
        _pendingCard.Controls.Add(_approveButton);
        _pendingCard.Controls.Add(_refuseButton);
        _pendingCard.Controls.Add(_approveAllButton);
        _pendingCard.Controls.Add(_refuseAllButton);
        _pendingCard.Dock = DockStyle.Fill;
        _pendingCard.Margin = new Padding(0, 0, 0, 12);
        _pendingCard.Name = "_pendingCard";
        _pendingCard.Size = new Size(638, 224);
        _pendingCard.TabIndex = 0;
        _pendingCard.Tag = "card";
        //
        // _pendingTitle
        //
        _pendingTitle.Dock = DockStyle.Top;
        _pendingTitle.Font = new Font("Segoe UI Semibold", 10F);
        _pendingTitle.Location = new Point(0, 0);
        _pendingTitle.Name = "_pendingTitle";
        _pendingTitle.Padding = new Padding(14, 10, 0, 0);
        _pendingTitle.Size = new Size(638, 34);
        _pendingTitle.TabIndex = 0;
        _pendingTitle.Text = "Waiting for approval (0)";
        //
        // _pendingGrid
        //
        _pendingGrid.AllowUserToAddRows = false;
        _pendingGrid.AllowUserToDeleteRows = false;
        _pendingGrid.AllowUserToResizeRows = false;
        _pendingGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _pendingGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _pendingGrid.Columns.AddRange(_pendingPathColumn, _pendingBytesColumn, _pendingFromColumn);
        _pendingGrid.Location = new Point(14, 36);
        _pendingGrid.MultiSelect = false;
        _pendingGrid.Name = "_pendingGrid";
        _pendingGrid.ReadOnly = true;
        _pendingGrid.RowHeadersVisible = false;
        _pendingGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _pendingGrid.Size = new Size(610, 128);
        _pendingGrid.TabIndex = 1;
        //
        // pending columns
        //
        _pendingPathColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _pendingPathColumn.HeaderText = "Path the sender wants to write";
        _pendingPathColumn.Name = "_pendingPathColumn";
        _pendingPathColumn.ReadOnly = true;
        _pendingBytesColumn.HeaderText = "Bytes";
        _pendingBytesColumn.Name = "_pendingBytesColumn";
        _pendingBytesColumn.ReadOnly = true;
        _pendingBytesColumn.Width = 90;
        _pendingFromColumn.HeaderText = "From";
        _pendingFromColumn.Name = "_pendingFromColumn";
        _pendingFromColumn.ReadOnly = true;
        _pendingFromColumn.Width = 170;
        //
        // _approveButton
        //
        _approveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _approveButton.Location = new Point(14, 176);
        _approveButton.Name = "_approveButton";
        _approveButton.Size = new Size(110, 34);
        _approveButton.TabIndex = 2;
        _approveButton.Tag = "primary";
        _approveButton.Text = "Approve";
        //
        // _refuseButton
        //
        _refuseButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _refuseButton.Location = new Point(132, 176);
        _refuseButton.Name = "_refuseButton";
        _refuseButton.Size = new Size(110, 34);
        _refuseButton.TabIndex = 3;
        _refuseButton.Text = "Refuse";
        //
        // _approveAllButton
        //
        _approveAllButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _approveAllButton.Location = new Point(392, 176);
        _approveAllButton.Name = "_approveAllButton";
        _approveAllButton.Size = new Size(110, 34);
        _approveAllButton.TabIndex = 4;
        _approveAllButton.Text = "Approve all";
        //
        // _refuseAllButton
        //
        _refuseAllButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _refuseAllButton.Location = new Point(510, 176);
        _refuseAllButton.Name = "_refuseAllButton";
        _refuseAllButton.Size = new Size(110, 34);
        _refuseAllButton.TabIndex = 5;
        _refuseAllButton.Text = "Refuse all";
        //
        // _receivedCard
        //
        _receivedCard.Controls.Add(_logBox);
        _receivedCard.Controls.Add(_receivedGrid);
        _receivedCard.Controls.Add(_receivedTitle);
        _receivedCard.Dock = DockStyle.Fill;
        _receivedCard.Margin = new Padding(0);
        _receivedCard.Name = "_receivedCard";
        _receivedCard.Size = new Size(638, 392);
        _receivedCard.TabIndex = 1;
        _receivedCard.Tag = "card";
        //
        // _receivedTitle
        //
        _receivedTitle.Dock = DockStyle.Top;
        _receivedTitle.Font = new Font("Segoe UI Semibold", 10F);
        _receivedTitle.Location = new Point(0, 0);
        _receivedTitle.Name = "_receivedTitle";
        _receivedTitle.Padding = new Padding(14, 10, 0, 0);
        _receivedTitle.Size = new Size(638, 34);
        _receivedTitle.TabIndex = 0;
        _receivedTitle.Text = "Received files (SHA-256 verified on receipt)";
        //
        // _receivedGrid
        //
        _receivedGrid.AllowUserToAddRows = false;
        _receivedGrid.AllowUserToDeleteRows = false;
        _receivedGrid.AllowUserToResizeRows = false;
        _receivedGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _receivedGrid.Columns.AddRange(_pathColumn, _bytesColumn, _shaColumn, _receivedColumn);
        _receivedGrid.Dock = DockStyle.Top;
        _receivedGrid.Location = new Point(0, 34);
        _receivedGrid.MultiSelect = false;
        _receivedGrid.Name = "_receivedGrid";
        _receivedGrid.ReadOnly = true;
        _receivedGrid.RowHeadersVisible = false;
        _receivedGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _receivedGrid.Size = new Size(638, 160);
        _receivedGrid.TabIndex = 1;
        //
        // received columns
        //
        _pathColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _pathColumn.HeaderText = "Path";
        _pathColumn.Name = "_pathColumn";
        _pathColumn.ReadOnly = true;
        _bytesColumn.HeaderText = "Bytes";
        _bytesColumn.Name = "_bytesColumn";
        _bytesColumn.ReadOnly = true;
        _bytesColumn.Width = 80;
        _shaColumn.HeaderText = "SHA-256 (head)";
        _shaColumn.Name = "_shaColumn";
        _shaColumn.ReadOnly = true;
        _shaColumn.Width = 120;
        _receivedColumn.HeaderText = "Received (UTC)";
        _receivedColumn.Name = "_receivedColumn";
        _receivedColumn.ReadOnly = true;
        _receivedColumn.Width = 130;
        //
        // _logBox
        //
        _logBox.Dock = DockStyle.Fill;
        _logBox.Location = new Point(0, 194);
        _logBox.Name = "_logBox";
        _logBox.ReadOnly = true;
        _logBox.Size = new Size(638, 198);
        _logBox.TabIndex = 2;
        _logBox.Text = "";
        _logBox.WordWrap = true;
        //
        // WirelessPage
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(_root);
        Name = "WirelessPage";
        Padding = new Padding(0, 4, 0, 0);
        Size = new Size(1100, 640);
        _root.ResumeLayout(false);
        _settingsCard.ResumeLayout(false);
        _settingsCard.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_portBox).EndInit();
        ((System.ComponentModel.ISupportInitialize)_tokenMinutesBox).EndInit();
        ((System.ComponentModel.ISupportInitialize)_qrBox).EndInit();
        _rightColumn.ResumeLayout(false);
        _pendingCard.ResumeLayout(false);
        _pendingCard.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_pendingGrid).EndInit();
        _receivedCard.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_receivedGrid).EndInit();
        ResumeLayout(false);
    }
}

