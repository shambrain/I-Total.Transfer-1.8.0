#nullable enable
using ITotalTransfer.UI.Controls;
namespace ITotalTransfer.UI.Pages;

partial class TransferPage
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel _root = null!;

    private CardPanel _progressCard = null!;
    private Label _phaseLabel = null!;
    private FluentProgressBar _progressBar = null!;
    private Label _currentFileLabel = null!;
    private Label _speedLabel = null!;
    private Label _etaLabel = null!;
    private Label _countsLabel = null!;

    private TableLayoutPanel _split = null!;
    private CardPanel _queueCard = null!;
    private Label _queueSummaryLabel = null!;
    private DataGridView _queueGrid = null!;
    private DataGridViewTextBoxColumn _queueFileColumn = null!;
    private DataGridViewTextBoxColumn _queueSizeColumn = null!;
    private DataGridViewTextBoxColumn _queueStateColumn = null!;
    private DataGridViewTextBoxColumn _queueVerifyColumn = null!;
    private DataGridViewTextBoxColumn _queueErrorColumn = null!;

    private CardPanel _logCard = null!;
    private RichTextBox _logBox = null!;
    private FlowLayoutPanel _logToolbar = null!;
    private RoundedButton _openDestinationButton = null!;
    private RoundedButton _openLogButton = null!;
    private RoundedButton _exportReportButton = null!;
    private RoundedButton _clearLogButton = null!;

    /// <summary>Designer-owned transfer page: live progress, persistent queue and the log console.</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _root = new TableLayoutPanel();
        _progressCard = new CardPanel();
        _phaseLabel = new Label();
        _progressBar = new FluentProgressBar();
        _currentFileLabel = new Label();
        _speedLabel = new Label();
        _etaLabel = new Label();
        _countsLabel = new Label();
        _split = new TableLayoutPanel();
        _queueCard = new CardPanel();
        _queueSummaryLabel = new Label();
        _queueGrid = new DataGridView();
        _queueFileColumn = new DataGridViewTextBoxColumn();
        _queueSizeColumn = new DataGridViewTextBoxColumn();
        _queueStateColumn = new DataGridViewTextBoxColumn();
        _queueVerifyColumn = new DataGridViewTextBoxColumn();
        _queueErrorColumn = new DataGridViewTextBoxColumn();
        _logCard = new CardPanel();
        _logBox = new RichTextBox();
        _logToolbar = new FlowLayoutPanel();
        _openDestinationButton = new RoundedButton();
        _openLogButton = new RoundedButton();
        _exportReportButton = new RoundedButton();
        _clearLogButton = new RoundedButton();
        _root.SuspendLayout();
        _progressCard.SuspendLayout();
        _split.SuspendLayout();
        _queueCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_queueGrid).BeginInit();
        _logCard.SuspendLayout();
        _logToolbar.SuspendLayout();
        SuspendLayout();
        //
        // _root
        //
        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Controls.Add(_progressCard, 0, 0);
        _root.Controls.Add(_split, 0, 1);
        _root.Dock = DockStyle.Fill;
        _root.Name = "_root";
        _root.RowCount = 2;
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.Size = new Size(1100, 640);
        _root.TabIndex = 0;
        //
        // _progressCard
        //
        _progressCard.Controls.Add(_phaseLabel);
        _progressCard.Controls.Add(_progressBar);
        _progressCard.Controls.Add(_currentFileLabel);
        _progressCard.Controls.Add(_speedLabel);
        _progressCard.Controls.Add(_etaLabel);
        _progressCard.Controls.Add(_countsLabel);
        _progressCard.Dock = DockStyle.Fill;
        _progressCard.Margin = new Padding(0, 0, 0, 12);
        _progressCard.Name = "_progressCard";
        _progressCard.Size = new Size(1100, 156);
        _progressCard.TabIndex = 0;
        _progressCard.Tag = "card";
        //
        // _phaseLabel
        //
        _phaseLabel.Font = new Font("Segoe UI Semibold", 11F);
        _phaseLabel.Location = new Point(18, 16);
        _phaseLabel.Name = "_phaseLabel";
        _phaseLabel.Size = new Size(1064, 24);
        _phaseLabel.TabIndex = 0;
        _phaseLabel.Text = "Ready. Check the connection and scan the phone.";
        //
        // _progressBar
        //
        // _progressBar — the Fluent progress indicator with the banner ramp and a live percentage.
        //
        _progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _progressBar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _progressBar.Location = new Point(18, 46);
        _progressBar.Name = "_progressBar";
        _progressBar.ShowPercentage = true;
        _progressBar.Size = new Size(1064, 18);
        _progressBar.TabIndex = 1;
        _progressBar.Thickness = 8;
        //
        // _currentFileLabel
        //
        _currentFileLabel.AutoEllipsis = true;
        _currentFileLabel.Location = new Point(18, 78);
        _currentFileLabel.Name = "_currentFileLabel";
        _currentFileLabel.Size = new Size(1064, 20);
        _currentFileLabel.TabIndex = 2;
        _currentFileLabel.Tag = "muted";
        _currentFileLabel.Text = "No active file";
        //
        // _speedLabel
        //
        // _speedLabel — a headline statistic, not a caption.
        //
        _speedLabel.AutoSize = true;
        _speedLabel.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _speedLabel.Location = new Point(18, 92);
        _speedLabel.Name = "_speedLabel";
        _speedLabel.Size = new Size(200, 24);
        _speedLabel.TabIndex = 3;
        _speedLabel.Text = "Speed: —";
        //
        // _etaLabel
        //
        _etaLabel.AutoSize = true;
        _etaLabel.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _etaLabel.Location = new Point(240, 92);
        _etaLabel.Name = "_etaLabel";
        _etaLabel.Size = new Size(200, 24);
        _etaLabel.TabIndex = 4;
        _etaLabel.Text = "ETA: —";
        //
        // _countsLabel
        //
        _countsLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _countsLabel.Location = new Point(18, 128);
        _countsLabel.Name = "_countsLabel";
        _countsLabel.Size = new Size(1064, 20);
        _countsLabel.TabIndex = 5;
        _countsLabel.Tag = "muted";
        _countsLabel.Text = "Copied: 0   ·   Skipped: 0   ·   Failed: 0   ·   Unverified: 0";
        //
        // _split
        //
        _split.ColumnCount = 2;
        _split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        _split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        _split.Controls.Add(_queueCard, 0, 0);
        _split.Controls.Add(_logCard, 1, 0);
        _split.Dock = DockStyle.Fill;
        _split.Name = "_split";
        _split.RowCount = 1;
        _split.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _split.Size = new Size(1100, 460);
        _split.TabIndex = 1;
        //
        // _queueCard
        //
        _queueCard.Controls.Add(_queueGrid);
        _queueCard.Controls.Add(_queueSummaryLabel);
        _queueCard.Dock = DockStyle.Fill;
        _queueCard.Margin = new Padding(0, 0, 12, 0);
        _queueCard.Name = "_queueCard";
        _queueCard.Size = new Size(604, 460);
        _queueCard.TabIndex = 0;
        _queueCard.Tag = "card";
        //
        // _queueSummaryLabel
        //
        _queueSummaryLabel.Dock = DockStyle.Top;
        _queueSummaryLabel.Font = new Font("Segoe UI Semibold", 10F);
        _queueSummaryLabel.Location = new Point(0, 0);
        _queueSummaryLabel.Name = "_queueSummaryLabel";
        _queueSummaryLabel.Padding = new Padding(14, 10, 0, 0);
        _queueSummaryLabel.Size = new Size(604, 34);
        _queueSummaryLabel.TabIndex = 0;
        _queueSummaryLabel.Text = "Persistent queue";
        //
        // _queueGrid
        //
        _queueGrid.AllowUserToAddRows = false;
        _queueGrid.AllowUserToDeleteRows = false;
        _queueGrid.AllowUserToResizeRows = false;
        _queueGrid.BackgroundColor = Color.FromArgb(21, 27, 38);
        _queueGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _queueGrid.Columns.AddRange(_queueFileColumn, _queueSizeColumn, _queueStateColumn, _queueVerifyColumn, _queueErrorColumn);
        _queueGrid.Dock = DockStyle.Fill;
        _queueGrid.Location = new Point(0, 34);
        _queueGrid.MultiSelect = false;
        _queueGrid.Name = "_queueGrid";
        _queueGrid.ReadOnly = true;
        _queueGrid.RowHeadersVisible = false;
        _queueGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _queueGrid.Size = new Size(604, 426);
        _queueGrid.TabIndex = 1;
        //
        // columns
        //
        _queueFileColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _queueFileColumn.HeaderText = "File";
        _queueFileColumn.Name = "_queueFileColumn";
        _queueFileColumn.ReadOnly = true;
        _queueSizeColumn.HeaderText = "Size";
        _queueSizeColumn.Name = "_queueSizeColumn";
        _queueSizeColumn.ReadOnly = true;
        _queueSizeColumn.Width = 90;
        _queueStateColumn.HeaderText = "State";
        _queueStateColumn.Name = "_queueStateColumn";
        _queueStateColumn.ReadOnly = true;
        _queueStateColumn.Width = 110;
        _queueVerifyColumn.HeaderText = "Verification";
        _queueVerifyColumn.Name = "_queueVerifyColumn";
        _queueVerifyColumn.ReadOnly = true;
        _queueVerifyColumn.Width = 110;
        _queueErrorColumn.HeaderText = "Note";
        _queueErrorColumn.Name = "_queueErrorColumn";
        _queueErrorColumn.ReadOnly = true;
        _queueErrorColumn.Width = 160;
        //
        // _logCard
        //
        _logCard.Controls.Add(_logBox);
        _logCard.Controls.Add(_logToolbar);
        _logCard.Dock = DockStyle.Fill;
        _logCard.Margin = new Padding(0);
        _logCard.Name = "_logCard";
        _logCard.Size = new Size(484, 460);
        _logCard.TabIndex = 1;
        _logCard.Tag = "card";
        //
        // _logBox
        //
        _logBox.Dock = DockStyle.Fill;
        _logBox.Location = new Point(0, 34);
        _logBox.Name = "_logBox";
        _logBox.ReadOnly = true;
        _logBox.Size = new Size(484, 426);
        _logBox.TabIndex = 1;
        _logBox.Text = "";
        _logBox.WordWrap = true;
        //
        // _logToolbar
        //
        _logToolbar.Controls.Add(_openDestinationButton);
        _logToolbar.Controls.Add(_openLogButton);
        _logToolbar.Controls.Add(_exportReportButton);
        _logToolbar.Controls.Add(_clearLogButton);
        _logToolbar.Dock = DockStyle.Top;
        _logToolbar.Location = new Point(0, 0);
        _logToolbar.Name = "_logToolbar";
        _logToolbar.Padding = new Padding(8, 4, 0, 0);
        _logToolbar.Size = new Size(484, 34);
        _logToolbar.TabIndex = 0;
        //
        // toolbar buttons
        //
        _openDestinationButton.Margin = new Padding(0, 0, 6, 0);
        _openDestinationButton.Name = "_openDestinationButton";
        _openDestinationButton.Size = new Size(112, 28);
        _openDestinationButton.TabIndex = 0;
        _openDestinationButton.Text = "Destination";
        _openLogButton.Margin = new Padding(0, 0, 6, 0);
        _openLogButton.Name = "_openLogButton";
        _openLogButton.Size = new Size(88, 28);
        _openLogButton.TabIndex = 1;
        _openLogButton.Text = "Log file";
        _exportReportButton.Margin = new Padding(0, 0, 6, 0);
        _exportReportButton.Name = "_exportReportButton";
        _exportReportButton.Size = new Size(118, 28);
        _exportReportButton.TabIndex = 2;
        _exportReportButton.Text = "Export report";
        _clearLogButton.Margin = new Padding(0);
        _clearLogButton.Name = "_clearLogButton";
        _clearLogButton.Size = new Size(92, 28);
        _clearLogButton.TabIndex = 3;
        _clearLogButton.Text = "Clear view";
        _clearLogButton.AccessibleDescription = "Clears the log console on this page; the log file on disk is kept.";
        //
        // TransferPage
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(_root);
        Name = "TransferPage";
        Padding = new Padding(0, 4, 0, 0);
        Size = new Size(1100, 640);
        _root.ResumeLayout(false);
        _progressCard.ResumeLayout(false);
        _progressCard.PerformLayout();
        _split.ResumeLayout(false);
        _queueCard.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_queueGrid).EndInit();
        _logCard.ResumeLayout(false);
        _logToolbar.ResumeLayout(false);
        ResumeLayout(false);
    }
}
